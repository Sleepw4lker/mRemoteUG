using System.Diagnostics.CodeAnalysis;

using Microsoft.Win32;

using mRemoteUG.Tools;

using NUnit.Framework;

namespace mRemoteUG.Tests.Tools;

/// <summary>
/// Exercises the registry watcher against a real key under HKCU.
/// </summary>
/// <remarks>
/// The WMI watcher this replaced could not be tested here at all: it needed the WMI service,
/// a healthy repository and HKEY_USERS, and it failed outright on this development machine.
/// RegNotifyChangeKeyValue needs none of that, so the behaviour is now verifiable locally.
/// </remarks>
[TestFixture]
[SuppressMessage("Design", "CA1001",
    Justification = "[TearDown] disposes _watcher after every test. "
        + "NUnit would dispose an IDisposable fixture once, after its last test.")]
public class RegistryKeyChangeWatcherTests
{
    private const string TestRootPath = @"Software\mRemoteUG.Tests";

    // A fresh key per test, so no test sees another's notifications.
    private string _keyPath = null!;
    private RegistryKeyChangeWatcher? _watcher;

    /// <summary>Generous: registry notifications are delivered asynchronously by the kernel.</summary>
    private static readonly TimeSpan s_signalTimeout = TimeSpan.FromSeconds(10);

    [SetUp]
    public void Setup()
    {
        _keyPath = $@"{TestRootPath}\{Guid.NewGuid():N}";
        Registry.CurrentUser.CreateSubKey(_keyPath)?.Dispose();
    }

    [TearDown]
    public void TearDown()
    {
        _watcher?.Dispose();
        _watcher = null;
        try { Registry.CurrentUser.DeleteSubKeyTree(_keyPath, throwOnMissingSubKey: false); }
        catch (ArgumentException) { }
    }

    [MemberNotNull(nameof(_watcher))]
    private ManualResetEventSlim WatchAndCaptureSignal(string? path = null, bool watchSubtree = true)
    {
        var signalled = new ManualResetEventSlim(false);
        _watcher = new RegistryKeyChangeWatcher(Registry.CurrentUser, path ?? _keyPath, watchSubtree);
        _watcher.Changed += (s, e) => signalled.Set();
        Assert.That(_watcher.Start(), Is.True, "The watcher did not start.");
        return signalled;
    }

    /// <summary>Opens a key the test created itself, so a missing one is the test's own failure.</summary>
    private static RegistryKey OpenTestKey(string path) =>
        Registry.CurrentUser.OpenSubKey(path, writable: true)
        ?? throw new InvalidOperationException($"The test key {path} does not exist.");

    [Test]
    public void StartReturnsFalseForAKeyThatDoesNotExist()
    {
        _watcher = new RegistryKeyChangeWatcher(Registry.CurrentUser, $@"{TestRootPath}\{Guid.NewGuid():N}");
        Assert.That(_watcher.Start(), Is.False);
    }

    [Test]
    public void StartReportsTheKeyItIsWatching()
    {
        WatchAndCaptureSignal();
        Assert.That(_watcher.WatchedKeyName, Does.EndWith(_keyPath.Replace('/', '\\')));
    }

    [Test]
    public void RaisesChangedWhenAValueIsSet()
    {
        ManualResetEventSlim signalled = WatchAndCaptureSignal();

        using (RegistryKey key = OpenTestKey(_keyPath))
        {
            key.SetValue("HostName", "example.test");
        }

        Assert.That(signalled.Wait(s_signalTimeout), Is.True, "No change notification arrived.");
    }

    [Test]
    public void RaisesChangedWhenASubkeyIsCreated()
    {
        // This is the case that matters: PuTTY stores each session as a subkey.
        ManualResetEventSlim signalled = WatchAndCaptureSignal();

        Registry.CurrentUser.CreateSubKey($@"{_keyPath}\a-new-session")?.Dispose();

        Assert.That(signalled.Wait(s_signalTimeout), Is.True, "No change notification arrived.");
    }

    [Test]
    public void RaisesChangedWhenASubkeyIsDeleted()
    {
        Registry.CurrentUser.CreateSubKey($@"{_keyPath}\doomed-session")?.Dispose();
        ManualResetEventSlim signalled = WatchAndCaptureSignal();

        Registry.CurrentUser.DeleteSubKeyTree($@"{_keyPath}\doomed-session");

        Assert.That(signalled.Wait(s_signalTimeout), Is.True, "No change notification arrived.");
    }

    [Test]
    public void RaisesChangedForAChangeInsideASubkeyWhenWatchingTheSubtree()
    {
        Registry.CurrentUser.CreateSubKey($@"{_keyPath}\session")?.Dispose();
        ManualResetEventSlim signalled = WatchAndCaptureSignal();

        using (RegistryKey key = OpenTestKey($@"{_keyPath}\session"))
        {
            key.SetValue("PortNumber", 2222);
        }

        Assert.That(signalled.Wait(s_signalTimeout), Is.True,
                    "A change below the watched key should be reported when watching the subtree.");
    }

    [Test]
    public void KeepsRaisingChangedAfterTheFirstNotification()
    {
        // Registry notifications are one-shot, so this is the behaviour most likely to be
        // wrong: without re-arming, only the first change would ever be reported.
        int changes = 0;
        var third = new ManualResetEventSlim(false);

        _watcher = new RegistryKeyChangeWatcher(Registry.CurrentUser, _keyPath);
        _watcher.Changed += (s, e) =>
        {
            if (Interlocked.Increment(ref changes) >= 3)
            {
                third.Set();
            }
        };
        Assert.That(_watcher.Start(), Is.True);

        for (int i = 0; i < 3; i++)
        {
            using (RegistryKey key = OpenTestKey(_keyPath))
            {
                key.SetValue($"Value{i}", i);
            }

            Thread.Sleep(50);
        }

        Assert.That(third.Wait(s_signalTimeout), Is.True,
                    $"Expected at least 3 notifications, saw {Volatile.Read(ref changes)}.");
    }

    [Test]
    public void NeverRaisesChangedConcurrentlyWithItself()
    {
        // Saving one PuTTY profile writes dozens of values, each of which is a separate
        // one-shot notification. Because the registration is renewed before handlers run,
        // those notifications land on different thread pool threads; a handler that re-reads
        // the whole key is not re-entrant, so the watcher has to serialise them itself.
        int concurrent = 0;
        int maxConcurrent = 0;
        int raises = 0;

        _watcher = new RegistryKeyChangeWatcher(Registry.CurrentUser, _keyPath);
        _watcher.Changed += (s, e) =>
        {
            InterlockedMax(ref maxConcurrent, Interlocked.Increment(ref concurrent));
            Thread.Sleep(20);
            Interlocked.Increment(ref raises);
            Interlocked.Decrement(ref concurrent);
        };
        Assert.That(_watcher.Start(), Is.True);

        using (RegistryKey key = OpenTestKey(_keyPath))
        {
            for (int i = 0; i < 40; i++)
            {
                key.SetValue($"Value{i}", i);
            }
        }

        WaitUntil(() => Volatile.Read(ref raises) >= 1 && Volatile.Read(ref concurrent) == 0);

        Assert.That(Volatile.Read(ref maxConcurrent), Is.EqualTo(1),
                    "Changed must never be raised on two threads at once.");
    }

    [Test]
    public void StillRaisesChangedForAChangeThatArrivesWhileAHandlerIsRunning()
    {
        // Serialising must coalesce rather than drop: a change made while a handler runs has
        // to be reported afterwards, or the tree silently goes stale.
        var firstRaiseStarted = new ManualResetEventSlim(false);
        var releaseFirstRaise = new ManualResetEventSlim(false);
        int raises = 0;

        _watcher = new RegistryKeyChangeWatcher(Registry.CurrentUser, _keyPath);
        _watcher.Changed += (s, e) =>
        {
            if (Interlocked.Increment(ref raises) == 1)
            {
                firstRaiseStarted.Set();
                releaseFirstRaise.Wait(s_signalTimeout);
            }
        };
        Assert.That(_watcher.Start(), Is.True);

        using (RegistryKey key = OpenTestKey(_keyPath))
        {
            key.SetValue("First", 1);
        }

        Assert.That(firstRaiseStarted.Wait(s_signalTimeout), Is.True, "The first notification never arrived.");

        using (RegistryKey key = OpenTestKey(_keyPath))
        {
            key.SetValue("Second", 2);
        }

        Thread.Sleep(100);
        releaseFirstRaise.Set();

        WaitUntil(() => Volatile.Read(ref raises) >= 2);
    }

    /// <summary>
    /// The change folded into the running handler is raised once more afterwards, and then the
    /// watcher goes quiet rather than raising for it again and again.
    /// </summary>
    [Test]
    public void AChangeFoldedIntoARunningHandlerIsRaisedOnceMore()
    {
        var firstRaiseStarted = new ManualResetEventSlim(false);
        var releaseFirstRaise = new ManualResetEventSlim(false);
        int raises = 0;

        _watcher = new RegistryKeyChangeWatcher(Registry.CurrentUser, _keyPath);
        _watcher.Changed += (s, e) =>
        {
            if (Interlocked.Increment(ref raises) == 1)
            {
                firstRaiseStarted.Set();
                releaseFirstRaise.Wait(s_signalTimeout);
            }
        };
        Assert.That(_watcher.Start(), Is.True);

        using (RegistryKey key = OpenTestKey(_keyPath))
        {
            key.SetValue("First", 1);
        }

        Assert.That(firstRaiseStarted.Wait(s_signalTimeout), Is.True, "The first notification never arrived.");

        using (RegistryKey key = OpenTestKey(_keyPath))
        {
            key.SetValue("Second", 2);
        }

        Thread.Sleep(100);
        releaseFirstRaise.Set();
        WaitUntil(() => Volatile.Read(ref raises) >= 2);
        Thread.Sleep(300);

        Assert.That(Volatile.Read(ref raises), Is.EqualTo(2));
    }

    [Test]
    public void StopsRaisingChangedOnceDisposed()
    {
        ManualResetEventSlim signalled = WatchAndCaptureSignal();
        _watcher.Dispose();
        _watcher = null;
        signalled.Reset();

        using (RegistryKey key = OpenTestKey(_keyPath))
        {
            key.SetValue("AfterDispose", "value");
        }

        Assert.That(signalled.Wait(TimeSpan.FromSeconds(1)), Is.False,
                    "A disposed watcher must not keep raising events.");
    }

    [Test]
    public void DisposingTwiceIsHarmless()
    {
        WatchAndCaptureSignal();
        _watcher.Dispose();
        Assert.DoesNotThrow(() => _watcher.Dispose());
        _watcher = null;
    }

    /// <summary>
    /// Each change is reported once, and only once: the watcher re-arms for the next change
    /// rather than for the one it has just handled, and a handler run that saw no further
    /// change stops.
    /// </summary>
    [Test]
    public void EachChangeIsRaisedExactlyOnce()
    {
        int raises = 0;
        _watcher = new RegistryKeyChangeWatcher(Registry.CurrentUser, _keyPath);
        _watcher.Changed += (s, e) => Interlocked.Increment(ref raises);
        Assert.That(_watcher.Start(), Is.True);

        SetValueAndSettle("First");
        int afterFirst = Volatile.Read(ref raises);
        SetValueAndSettle("Second");
        int afterSecond = Volatile.Read(ref raises);

        Assert.Multiple(() =>
        {
            Assert.That(afterFirst, Is.EqualTo(1), "The first change was not raised exactly once.");
            Assert.That(afterSecond, Is.EqualTo(2), "The second change was not raised exactly once.");
        });

        void SetValueAndSettle(string name)
        {
            int before = Volatile.Read(ref raises);
            using (RegistryKey key = OpenTestKey(_keyPath))
            {
                key.SetValue(name, name);
            }

            WaitUntil(() => Volatile.Read(ref raises) > before);
            Thread.Sleep(300);
        }
    }

    [Test]
    public void StartAfterDisposeThrows()
    {
        WatchAndCaptureSignal();
        RegistryKeyChangeWatcher watcher = _watcher;
        _watcher = null;
        watcher.Dispose();

        Assert.Throws<ObjectDisposedException>(() => watcher.Start());
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int seen;
        while ((seen = Volatile.Read(ref target)) < value)
        {
            if (Interlocked.CompareExchange(ref target, value, seen) == seen)
            {
                return;
            }
        }
    }

    private static void WaitUntil(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow + s_signalTimeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            Thread.Sleep(5);
        }

        Assert.Fail("Timed out waiting for the expected state.");
    }
}
