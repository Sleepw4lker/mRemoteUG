using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;

using mRemoteUG.Config.Putty;
using mRemoteUG.Connection;
using mRemoteUG.Tests.TestHelpers;

using NUnit.Framework;

namespace mRemoteUG.Tests.Config.Putty;

/// <summary>
/// The PuTTY Profile root in the tree, and how a registry change gets to it (ADR-0033: the
/// refresh runs on the thread that started the watcher, never on the watcher's own).
/// </summary>
/// <remarks>
/// Each test builds its own manager and points its registry provider at a scratch key
/// (<see cref="PuttyRegistryScratch"/>); none of them reads the real PuTTY key. The UI thread is
/// stood in for by <see cref="RecordingContext"/>, which only queues what is posted to it, so
/// the tests decide when the refresh runs instead of waiting for it.
/// </remarks>
[TestFixture]
[SuppressMessage("Design", "CA1001",
    Justification = "[TearDown] disposes _scratch after every test. "
        + "NUnit would dispose an IDisposable fixture once, after its last test.")]
public class PuttySessionsManagerTests
{
    private static readonly string[] s_alphaZetaArguments = { "alpha", "zeta" };
    private static readonly string[] s_alphaArguments = { "alpha" };
    private static readonly string[] s_fromPuttyArguments = { "from putty" };
    private static readonly string[] s_defaultSettingsMySessionArguments = { "Default Settings", "My Session" };

    private static readonly TimeSpan s_signalTimeout = TimeSpan.FromSeconds(10);

    // Rebuilt for every test in Setup: each test gets a scratch key and a manager of its own.
    private PuttyRegistryScratch _scratch = null!;
    private PuttySessionsManager _manager = null!;
    private PuttySessionsRegistryProvider _provider = null!;
    private SynchronizationContext? _originalContext;

    [SetUp]
    public void Setup()
    {
        _originalContext = SynchronizationContext.Current;
        _scratch = new PuttyRegistryScratch();
        _manager = new PuttySessionsManager();
        _provider = _manager.Providers.OfType<PuttySessionsRegistryProvider>().Single();
        _provider.SessionsKeyPath = _scratch.SessionsKeyPath;
    }

    [TearDown]
    public void TearDown()
    {
        _manager.StopWatcher();
        SynchronizationContext.SetSynchronizationContext(_originalContext);
        _scratch.Dispose();
    }

    [Test]
    public void ANewManagerReadsTheRegistry()
    {
        Assert.That(_manager.Providers, Is.EqualTo(new[] { _provider }));
    }

    [Test]
    public void AddingTheSameProviderTwiceKeepsOne()
    {
        _manager.AddProvider(_provider);

        Assert.That(_manager.Providers, Has.Exactly(1).Items);
    }

    [Test]
    public void AnAddedProvidersCollectionChangesAreForwardedWithTheProviderAsSender()
    {
        var second = new PuttyRegistryScratch();
        try
        {
            PuttySessionsRegistryProvider other = second.CreateProvider();
            second.AddSession("alpha", ("HostName", "h"));
            var seen = new List<(object? Sender, NotifyCollectionChangedEventArgs Args)>();
            _manager.PuttySessionsCollectionChanged += (sender, args) => seen.Add((sender, args));

            _manager.AddProvider(other);
            _manager.AddSessions();

            Assert.Multiple(() =>
            {
                Assert.That(_manager.Providers, Is.EqualTo(new AbstractPuttySessionsProvider[] { _provider, other }));
                Assert.That(seen, Has.Count.EqualTo(1));
                Assert.That(seen[0].Sender, Is.SameAs(other));
                Assert.That(seen[0].Args.Action, Is.EqualTo(NotifyCollectionChangedAction.Add));
            });
        }
        finally
        {
            second.Dispose();
        }
    }

    [Test]
    public void WithoutSessionsThereIsNoPuttyProfileRoot()
    {
        _scratch.CreateSessionsKey();

        _manager.AddSessions();

        Assert.That(_manager.RootPuttySessionsNodes, Is.Empty);
    }

    [Test]
    public void SessionsGetOnePuttyProfileRootHoldingThemSorted()
    {
        _scratch.AddSession("zeta", ("HostName", "z"));
        _scratch.AddSession("alpha", ("HostName", "a"));

        _manager.AddSessions();
        _manager.AddSessions();

        Assert.Multiple(() =>
        {
            Assert.That(_manager.RootPuttySessionsNodes, Is.EqualTo(new[] { _provider.RootInfo }));
            Assert.That(_provider.RootInfo.Children.Select(c => c.Name), Is.EqualTo(s_alphaZetaArguments));
        });
    }

    [Test]
    public void TheRootAppearsOnceTheFirstSessionIsSaved()
    {
        _scratch.CreateSessionsKey();
        _manager.AddSessions();

        _scratch.AddSession("alpha", ("HostName", "a"));
        _manager.AddSessions();

        Assert.That(_manager.RootPuttySessionsNodes, Is.EqualTo(new[] { _provider.RootInfo }));
    }

    [Test]
    public void AChangeBeforeTheWatcherStartsRefreshesNothing()
    {
        _scratch.AddSession("alpha", ("HostName", "a"));

        _manager.PuttySessionChanged(_provider, new PuttySessionChangedEventArgs());

        Assert.That(_provider.RootInfo.Children, Is.Empty);
    }

    [Test]
    public void AChangeIsRefreshedOnTheContextTheWatcherWasStartedOn()
    {
        RecordingContext ui = StartWatcherOnRecordingContext();
        _scratch.AddSession("alpha", ("HostName", "a"));

        _manager.PuttySessionChanged(_provider, new PuttySessionChangedEventArgs());

        Assert.Multiple(() =>
        {
            Assert.That(ui.Pending, Is.EqualTo(1), "The refresh should have been posted.");
            Assert.That(_provider.RootInfo.Children, Is.Empty, "The refresh ran on the calling thread.");
        });

        ui.RunPending();

        Assert.That(_provider.RootInfo.Children.Select(c => c.Name), Is.EqualTo(s_alphaArguments));
    }

    [Test]
    public void ChangesArrivingBeforeTheRefreshRunsAreFoldedIntoIt()
    {
        RecordingContext ui = StartWatcherOnRecordingContext();

        for (int i = 0; i < 5; i++)
        {
            _manager.PuttySessionChanged(_provider, new PuttySessionChangedEventArgs());
        }

        Assert.That(ui.Pending, Is.EqualTo(1));
    }

    [Test]
    public void AChangeAfterTheWatcherStopsRefreshesNothing()
    {
        RecordingContext ui = StartWatcherOnRecordingContext();
        _manager.StopWatcher();

        _manager.PuttySessionChanged(_provider, new PuttySessionChangedEventArgs());

        Assert.That(ui.Pending, Is.Zero);
    }

    [Test]
    public void ASessionSavedInTheRegistryReachesTheTreeThroughTheContext()
    {
        _scratch.CreateSessionsKey();
        RecordingContext ui = StartWatcherOnRecordingContext();
        var added = new List<NotifyCollectionChangedEventArgs>();
        _manager.PuttySessionsCollectionChanged += (sender, args) => added.Add(args);

        _scratch.AddSession("from%20putty", ("HostName", "h"));

        Assert.That(ui.WaitForPost(s_signalTimeout), Is.True, "The registry change was never posted.");
        ui.RunPending();

        Assert.Multiple(() =>
        {
            Assert.That(_provider.RootInfo.Children.Select(c => c.Name), Is.EqualTo(s_fromPuttyArguments));
            Assert.That(_manager.RootPuttySessionsNodes, Is.EqualTo(new[] { _provider.RootInfo }));
            Assert.That(added.Select(a => ((PuttySessionInfo?)a.NewItems?[0])?.Name), Is.EqualTo(s_fromPuttyArguments));
        });
    }

    [Test]
    public void StopWatcherStopsTheProvidersWatcher()
    {
        // Listens on the provider itself: the manager drops a late change anyway once it has
        // stopped, so only the provider's own event shows whether the watcher was left running.
        // The watcher is static but its handler belongs to the provider that started it, so a
        // watcher leaked by an earlier test makes this provider deaf, and "nothing after the
        // stop" would pass without stopping anything; hence the event before the stop.
        _scratch.CreateSessionsKey();
        using var raised = new ManualResetEventSlim(false);
        _provider.PuttySessionChanged += (sender, e) => raised.Set();
        StartWatcherOnRecordingContext();
        _scratch.AddSession("before-stop", ("HostName", "h"));
        Assert.That(raised.Wait(s_signalTimeout), Is.True, "The provider's watcher never raised a change.");

        _manager.StopWatcher();
        raised.Reset();
        _scratch.AddSession("after-stop", ("HostName", "h"));

        Assert.That(raised.Wait(TimeSpan.FromSeconds(1)), Is.False, "The provider's watcher is still running.");
    }

    [Test]
    public void ARestartedWatcherRefreshesOncePerChange()
    {
        var counting = new CountingProvider();
        _manager.AddProvider(counting);
        StartWatcherOnRecordingContext();
        _manager.StopWatcher();
        RecordingContext ui = StartWatcherOnRecordingContext();

        counting.RaiseChanged();
        ui.RunPending();

        Assert.That(counting.Reads, Is.EqualTo(1), "Stopping should have unsubscribed from the provider.");
    }

    [Test]
    public void StoppingDropsTheRefreshFoldedIntoAPendingOne()
    {
        var counting = new CountingProvider();
        _manager.AddProvider(counting);
        RecordingContext ui = StartWatcherOnRecordingContext();
        counting.RaiseChanged();
        counting.RaiseChanged();

        _manager.StopWatcher();
        ui.RunPending();

        // The refresh already posted still runs once; the second request, folded into it,
        // is dropped because the manager stopped in between.
        Assert.That(counting.Reads, Is.EqualTo(1));
    }

    [Test]
    public void StartingTheWatcherTwiceKeepsTheFirstContext()
    {
        RecordingContext first = StartWatcherOnRecordingContext();
        RecordingContext second = StartWatcherOnRecordingContext();

        _manager.PuttySessionChanged(_provider, new PuttySessionChangedEventArgs());

        Assert.Multiple(() =>
        {
            Assert.That(first.Pending, Is.EqualTo(1));
            Assert.That(second.Pending, Is.Zero);
        });
    }

    [Test]
    public void TheSessionListOffersExactlyTheSessionNames()
    {
        // SessionList is what the property grid's PuTTY Session drop-down uses, and it always
        // reads the application's manager, so that one's provider is redirected for the test.
        PuttySessionsRegistryProvider instanceProvider =
            PuttySessionsManager.Instance.Providers.OfType<PuttySessionsRegistryProvider>().Single();
        string original = instanceProvider.SessionsKeyPath;
        instanceProvider.SessionsKeyPath = _scratch.SessionsKeyPath;
        try
        {
            _scratch.AddSession("My%20Session", ("HostName", "h"));
            var list = new PuttySessionsManager.SessionList();

            Assert.Multiple(() =>
            {
                Assert.That(PuttySessionsManager.SessionList.Names,
                            Is.EqualTo(s_defaultSettingsMySessionArguments));
                Assert.That(list.GetStandardValues(null).Cast<string>(),
                            Is.EqualTo(s_defaultSettingsMySessionArguments));
                Assert.That(list.GetStandardValuesExclusive(null), Is.True);
                Assert.That(list.GetStandardValuesSupported(null), Is.True);
            });
        }
        finally
        {
            instanceProvider.SessionsKeyPath = original;
        }
    }

    private RecordingContext StartWatcherOnRecordingContext()
    {
        var context = new RecordingContext();
        SynchronizationContext.SetSynchronizationContext(context);
        _manager.StartWatcher();
        return context;
    }

    /// <summary>A provider with no sessions that counts how often the manager refreshes it.</summary>
    private sealed class CountingProvider : AbstractPuttySessionsProvider
    {
        public int Reads { get; private set; }

        public override string[] GetSessionNames(bool raw = false)
        {
            if (raw)
            {
                Reads++;
            }

            return [];
        }

        public override PuttySessionInfo? GetSession(string sessionName)
        {
            return null;
        }

        public void RaiseChanged()
        {
            RaiseSessionChangedEvent(new PuttySessionChangedEventArgs());
        }
    }

    /// <summary>Stands in for the UI thread: queues what is posted until the test runs it.</summary>
    [SuppressMessage("Design", "CA1001",
        Justification = "A SemaphoreSlim holds a kernel handle only once AvailableWaitHandle is read, which "
            + "this never does; disposing it would make a late Post from the still-running watcher throw.")]
    private sealed class RecordingContext : SynchronizationContext
    {
        private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _posted = new();
        private readonly SemaphoreSlim _postSignal = new(0);

        public int Pending => _posted.Count;

        public override void Post(SendOrPostCallback d, object? state)
        {
            _posted.Enqueue((d, state));
            _postSignal.Release();
        }

        public bool WaitForPost(TimeSpan timeout)
        {
            return _postSignal.Wait(timeout);
        }

        public void RunPending()
        {
            while (_posted.TryDequeue(out (SendOrPostCallback Callback, object? State) item))
            {
                item.Callback(item.State);
            }
        }
    }
}
