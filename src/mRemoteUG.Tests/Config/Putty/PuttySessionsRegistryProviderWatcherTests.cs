using System.Diagnostics.CodeAnalysis;

using Microsoft.Win32;

using mRemoteUG.App;
using mRemoteUG.Config.Putty;
using mRemoteUG.Messages;
using mRemoteUG.Tests.TestHelpers;

using NUnit.Framework;

namespace mRemoteUG.Tests.Config.Putty;

/// <summary>
/// Which registry key <see cref="PuttySessionsRegistryProvider.StartWatcher"/> watches, and that
/// a change there reaches <see cref="AbstractPuttySessionsProvider.PuttySessionChanged"/>.
/// </summary>
/// <remarks>
/// Watches a scratch key (<see cref="PuttyRegistryScratch"/>), never the real PuTTY key. The
/// notifications themselves come from <c>RegNotifyChangeKeyValue</c> and arrive on a thread
/// pool thread, so the positive cases wait on a signal, as <c>RegistryKeyChangeWatcherTests</c>
/// does; only the cases asserting that nothing arrives wait a fixed second.
/// </remarks>
[TestFixture]
[SuppressMessage("Design", "CA1001",
    Justification = "[TearDown] disposes _scratch and _changed after every test. "
        + "NUnit would dispose an IDisposable fixture once, after its last test.")]
public class PuttySessionsRegistryProviderWatcherTests
{
    /// <summary>Generous: registry notifications are delivered asynchronously by the kernel.</summary>
    private static readonly TimeSpan s_signalTimeout = TimeSpan.FromSeconds(10);

    /// <summary>How long a case asserting that no notification arrives listens for one.</summary>
    private static readonly TimeSpan s_quietPeriod = TimeSpan.FromSeconds(1);

    // Rebuilt for every test in Setup: each test gets a scratch key of its own.
    private PuttyRegistryScratch _scratch = null!;
    private PuttySessionsRegistryProvider _provider = null!;
    private ManualResetEventSlim _changed = null!;
    private PuttySessionChangedEventArgs? _lastArgs;
    private object? _lastSender;

    [SetUp]
    public void Setup()
    {
        _scratch = new PuttyRegistryScratch();
        _provider = _scratch.CreateProvider();
        _changed = new ManualResetEventSlim(false);
        _provider.PuttySessionChanged += (sender, e) =>
        {
            _lastSender = sender;
            _lastArgs = e;
            _changed.Set();
        };
    }

    [TearDown]
    public void TearDown()
    {
        // The watcher is static, shared by every provider instance: stop it before the key goes.
        _provider.StopWatcher();
        _scratch.Dispose();
        _changed.Dispose();
    }

    [Test]
    public void ASessionSavedUnderAWatchedSessionsKeyRaisesPuttySessionChanged()
    {
        _scratch.CreateSessionsKey();
        _provider.StartWatcher();

        _scratch.AddSession("new%20session", ("HostName", "h"));

        Assert.That(_changed.Wait(s_signalTimeout), Is.True, "No change notification arrived.");
        Assert.Multiple(() =>
        {
            Assert.That(_lastSender, Is.SameAs(_provider));
            Assert.That(_lastArgs, Is.Not.Null);
            Assert.That(_lastArgs?.Session, Is.Null, "The watcher cannot tell which session changed.");
        });
    }

    [Test]
    public void AnEditInsideASessionRaisesPuttySessionChanged()
    {
        _scratch.AddSession("alpha", ("HostName", "h"));
        _provider.StartWatcher();

        _scratch.AddSession("alpha", ("PortNumber", 2222));

        Assert.That(_changed.Wait(s_signalTimeout), Is.True, "No change notification arrived.");
    }

    [Test]
    public void WithoutASessionsKeyItWatchesPuttysKeySoTheFirstSessionIsNoticed()
    {
        PuttyRegistryScratch.CreateKey(_scratch.PuttyKeyPath);
        _provider.StartWatcher();

        _scratch.AddSession("first", ("HostName", "h"));

        Assert.That(_changed.Wait(s_signalTimeout), Is.True, "No change notification arrived.");
    }

    [Test]
    public void WithoutPuttysKeyItWatchesItsParent()
    {
        PuttyRegistryScratch.CreateKey(_scratch.SimonTathamKeyPath);
        _provider.StartWatcher();

        _scratch.AddSession("first", ("HostName", "h"));

        Assert.That(_changed.Wait(s_signalTimeout), Is.True, "No change notification arrived.");
    }

    [Test]
    public void ItWatchesTheDeepestKeyThatExists()
    {
        // With the Sessions key present, a change beside it under PuTTY's key is none of the
        // watcher's business; watching a parent instead would report every PuTTY setting.
        _scratch.CreateSessionsKey();
        _provider.StartWatcher();

        using (RegistryKey putty = Registry.CurrentUser.OpenSubKey(_scratch.PuttyKeyPath, writable: true)
                                ?? throw new AssertionException("The scratch PuTTY key is missing."))
        {
            putty.SetValue("RandSeedFile", "x");
        }

        Assert.That(_changed.Wait(s_quietPeriod), Is.False, "A change outside the Sessions key was reported.");
    }

    [Test]
    public void WithNoPuttyKeyAtAllItLogsThatItIsNotWatching()
    {
        _provider.StartWatcher();

        Assert.That(MessagesMentioning(_scratch.SessionsKeyPath),
                    Has.Some.Matches<IMessage>(m => m.Class == MessageClass.InformationMsg
                                                    && m.OnlyLog
                                                    && m.Text.Contains("not watching")),
                    "Expected an informational, log-only message: no PuTTY is normal, not worth a pop-up.");
    }

    [Test]
    public void AKeyThatCannotBeOpenedIsLoggedAsAWarningAndNotRetriedHigherUp()
    {
        // A key name segment longer than 255 characters makes OpenSubKey throw before it
        // reaches the registry. The parents exist, but the watcher gives up on the first error
        // rather than falling back to them.
        string unopenable = $@"{_scratch.PuttyKeyPath}\{new string('x', 256)}";
        PuttyRegistryScratch.CreateKey(_scratch.PuttyKeyPath);
        _provider.SessionsKeyPath = unopenable;
        _provider.StartWatcher();

        using (RegistryKey putty = Registry.CurrentUser.OpenSubKey(_scratch.PuttyKeyPath, writable: true)
                                ?? throw new AssertionException("The scratch PuTTY key is missing."))
        {
            putty.SetValue("AnyValue", "x");
        }

        Assert.Multiple(() =>
        {
            Assert.That(MessagesMentioning(unopenable),
                        Has.Some.Matches<IMessage>(m => m.Class == MessageClass.WarningMsg));
            Assert.That(_changed.Wait(s_quietPeriod), Is.False, "It fell back to watching a parent key.");
        });
    }

    [Test]
    public void StopWatcherStopsTheNotifications()
    {
        _scratch.CreateSessionsKey();
        _provider.StartWatcher();
        _provider.StopWatcher();

        _scratch.AddSession("after-stop", ("HostName", "h"));

        Assert.That(_changed.Wait(s_quietPeriod), Is.False, "A stopped watcher still reported a change.");
    }

    [Test]
    public void StartingTwiceLeavesOneWatcherForStopWatcherToStop()
    {
        // A second watcher would replace the first in the static field, and StopWatcher would
        // then only stop the second one.
        _scratch.CreateSessionsKey();
        _provider.StartWatcher();
        _provider.StartWatcher();
        _provider.StopWatcher();

        _scratch.AddSession("after-stop", ("HostName", "h"));

        Assert.That(_changed.Wait(s_quietPeriod), Is.False, "A watcher outlived StopWatcher.");
    }

    [Test]
    public void TheWatcherCanBeStartedAgainAfterStopping()
    {
        _scratch.CreateSessionsKey();
        _provider.StartWatcher();
        _provider.StopWatcher();
        _provider.StartWatcher();

        _scratch.AddSession("after-restart", ("HostName", "h"));

        Assert.That(_changed.Wait(s_signalTimeout), Is.True, "The restarted watcher reported nothing.");
    }

    [Test]
    public void StopWatcherWithoutAWatcherIsHarmless()
    {
        Assert.DoesNotThrow(() => _provider.StopWatcher());
    }

    private static List<IMessage> MessagesMentioning(string text)
    {
        return Runtime.MessageCollector.Messages.Where(m => m.Text != null && m.Text.Contains(text)).ToList();
    }
}
