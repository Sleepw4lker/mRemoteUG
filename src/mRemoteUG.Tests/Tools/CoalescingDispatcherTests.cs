using mRemoteUG.Tools;

using NUnit.Framework;

namespace mRemoteUG.Tests.Tools;

/// <summary>
/// Exercises the dispatcher that moves the PuTTY Profile refresh onto the UI thread.
/// </summary>
[TestFixture]
public class CoalescingDispatcherTests
{
    private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Stands in for the WinForms context: queues callbacks and runs them on one dedicated
    /// thread, so "did this run on the right thread" is answerable without a message loop.
    /// </summary>
    private sealed class PumpContext : SynchronizationContext, IDisposable
    {
        private readonly Queue<Action> _queue = new Queue<Action>();
        private readonly Thread _thread;
        private bool _running = true;

        public int ThreadId => _thread.ManagedThreadId;

        public PumpContext()
        {
            _thread = new Thread(Pump) { IsBackground = true, Name = nameof(PumpContext) };
            _thread.Start();
        }

        public override void Post(SendOrPostCallback d, object? state)
        {
            lock (_queue)
            {
                _queue.Enqueue(() => d(state));
                Monitor.Pulse(_queue);
            }
        }

        private void Pump()
        {
            while (true)
            {
                Action work;
                lock (_queue)
                {
                    while (_running && _queue.Count == 0)
                    {
                        Monitor.Wait(_queue);
                    }

                    if (!_running && _queue.Count == 0)
                    {
                        return;
                    }

                    work = _queue.Dequeue();
                }

                // A wedged pump would hide the real assertion failure behind a timeout.
                try { work(); } catch { }
            }
        }

        public void Dispose()
        {
            lock (_queue)
            {
                _running = false;
                Monitor.Pulse(_queue);
            }
            _thread.Join(s_timeout);
        }
    }

    [Test]
    public void RunsTheActionOnTheCapturedContextNotTheRequestingThread()
    {
        using var context = new PumpContext();
        int ranOn = 0;
        var ran = new ManualResetEventSlim(false);
        var dispatcher = new CoalescingDispatcher(() =>
        {
            ranOn = Environment.CurrentManagedThreadId;
            ran.Set();
        }, context);

        dispatcher.Request();

        Assert.That(ran.Wait(s_timeout), Is.True, "The action never ran.");
        Assert.That(ranOn, Is.EqualTo(context.ThreadId),
                    "The action must run on the captured context's thread, not the caller's.");
    }

    [Test]
    public void RunsInlineWhenThereIsNoContext()
    {
        // Headless callers, and anything running before the UI exists, have no context.
        int ranOn = 0;
        var dispatcher = new CoalescingDispatcher(() => ranOn = Environment.CurrentManagedThreadId, null);

        dispatcher.Request();

        Assert.That(ranOn, Is.EqualTo(Environment.CurrentManagedThreadId));
    }

    [Test]
    public void NeverRunsTheActionConcurrentlyWithItself()
    {
        using var context = new PumpContext();
        int concurrent = 0;
        int maxConcurrent = 0;
        int runs = 0;
        var dispatcher = new CoalescingDispatcher(() =>
        {
            InterlockedMax(ref maxConcurrent, Interlocked.Increment(ref concurrent));
            Thread.Sleep(5);
            Interlocked.Increment(ref runs);
            Interlocked.Decrement(ref concurrent);
        }, context);

        Parallel.Invoke(Enumerable.Repeat<Action>(() => dispatcher.Request(), 50).ToArray());
        WaitFor(() => Volatile.Read(ref runs) >= 1 && Volatile.Read(ref concurrent) == 0);

        Assert.That(Volatile.Read(ref maxConcurrent), Is.EqualTo(1));
    }

    [Test]
    public void CollapsesABurstOfRequestsIntoFarFewerRuns()
    {
        using var context = new PumpContext();
        int runs = 0;
        var dispatcher = new CoalescingDispatcher(() =>
        {
            Interlocked.Increment(ref runs);
            Thread.Sleep(20);
        }, context);

        for (int i = 0; i < 50; i++)
        {
            dispatcher.Request();
        }

        WaitFor(() => Volatile.Read(ref runs) >= 1);
        Thread.Sleep(300);

        // The exact count is timing dependent; the point is that 50 notifications must not
        // mean 50 registry reads and 50 tree rebuilds.
        Assert.That(Volatile.Read(ref runs), Is.LessThan(10),
                    "A burst of requests should collapse, not queue one run each.");
    }

    [Test]
    public void StillRunsAgainForARequestMadeWhileTheActionWasRunning()
    {
        using var context = new PumpContext();
        var firstRunStarted = new ManualResetEventSlim(false);
        var releaseFirstRun = new ManualResetEventSlim(false);
        int runs = 0;

        var dispatcher = new CoalescingDispatcher(() =>
        {
            if (Interlocked.Increment(ref runs) == 1)
            {
                firstRunStarted.Set();
                releaseFirstRun.Wait(s_timeout);
            }
        }, context);

        dispatcher.Request();
        Assert.That(firstRunStarted.Wait(s_timeout), Is.True, "The first run never started.");

        // Arrives while the first run holds the context's thread. It must not be dropped.
        dispatcher.Request();
        releaseFirstRun.Set();

        WaitFor(() => Volatile.Read(ref runs) >= 2);
    }

    [Test]
    public void KeepsWorkingAfterTheActionThrows()
    {
        // A throwing refresh must not leave the dispatcher believing a run is still
        // outstanding, which would silently stop every later refresh.
        using var context = new PumpContext();
        int runs = 0;
        var dispatcher = new CoalescingDispatcher(() =>
        {
            if (Interlocked.Increment(ref runs) == 1)
            {
                throw new InvalidOperationException("boom");
            }
        }, context);

        dispatcher.Request();
        WaitFor(() => Volatile.Read(ref runs) >= 1);

        dispatcher.Request();
        WaitFor(() => Volatile.Read(ref runs) >= 2);
    }

    [Test]
    public void StopIgnoresLaterRequests()
    {
        using var context = new PumpContext();
        int runs = 0;
        var dispatcher = new CoalescingDispatcher(() => Interlocked.Increment(ref runs), context);

        dispatcher.Stop();
        dispatcher.Request();
        Thread.Sleep(100);

        Assert.That(Volatile.Read(ref runs), Is.Zero,
                    "Requests arriving after shutdown must not be dispatched.");
    }

    /// <summary>
    /// The exception reaches the caller to be reported, and it leaves no state behind: the next
    /// request runs the action exactly once.
    /// </summary>
    [Test]
    public void AThrowingActionSurfacesItsExceptionAndTheNextRequestRunsOnce()
    {
        int runs = 0;
        var dispatcher = new CoalescingDispatcher(() =>
        {
            if (++runs == 1)
            {
                throw new InvalidOperationException("boom");
            }
        }, null);

        Assert.Throws<InvalidOperationException>(() => dispatcher.Request());
        dispatcher.Request();

        Assert.That(runs, Is.EqualTo(2));
    }

    /// <summary>
    /// A run that saw no request arrive while it ran leaves nothing behind: the next request
    /// runs the action again, and only once.
    /// </summary>
    [Test]
    public void EachRequestWithoutAContextRunsTheActionOnce()
    {
        int runs = 0;
        var dispatcher = new CoalescingDispatcher(() => runs++, null);

        dispatcher.Request();
        dispatcher.Request();

        Assert.That(runs, Is.EqualTo(2));
    }

    /// <summary>
    /// During shutdown the UI thread can be gone before the last registry notification arrives.
    /// That is not an error to report, and there is nothing left to post to again.
    /// </summary>
    [Test]
    public void AContextWhoseThreadIsGoneStopsTheDispatcher()
    {
        var context = new GoneContext();
        int runs = 0;
        var dispatcher = new CoalescingDispatcher(() => runs++, context);

        Assert.DoesNotThrow(() => dispatcher.Request());
        dispatcher.Request();

        Assert.Multiple(() =>
        {
            Assert.That(context.Posts, Is.EqualTo(1), "The dispatcher kept posting to a context that is gone.");
            Assert.That(runs, Is.Zero);
        });
    }

    /// <summary>Behaves like a WindowsFormsSynchronizationContext whose thread has exited.</summary>
    private sealed class GoneContext : SynchronizationContext
    {
        public int Posts { get; private set; }

        public override void Post(SendOrPostCallback d, object? state)
        {
            Posts++;
            throw new ObjectDisposedException("the context's thread");
        }
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

    private static void WaitFor(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow + s_timeout;
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
