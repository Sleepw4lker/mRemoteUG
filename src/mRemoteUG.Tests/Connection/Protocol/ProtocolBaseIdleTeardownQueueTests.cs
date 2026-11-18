using mRemoteUG.Connection.Protocol;

using NUnit.Framework;

namespace mRemoteUG.Tests.Connection.Protocol;

/// <summary>
/// Pins the queue that keeps one session's teardown from running while others are still
/// unwinding.
/// </summary>
/// <remarks>
/// Simply posting the teardown is not enough: COM pumps messages for any outgoing call from an
/// STA, so a posted teardown is dispatched inside whatever mstscax call happens to be running.
/// That is how a teardown ends up releasing one RDP control while several others are still
/// unwinding their own disconnects, which hangs the UI thread outright - where a session torn
/// down on its own completes in well under 100 ms.
///
/// <see cref="Application.Idle"/> is raised only by WinForms' own message loop and never by
/// COM's, which is the whole point of using it. That is a property of the framework rather
/// than of this code, so it is worth pinning: if it stopped holding, the fix would quietly
/// stop working and the teardowns would never run at all.
/// </remarks>
[TestFixture]
[NonParallelizable]
[Apartment(ApartmentState.STA)]
public class ProtocolBaseIdleTeardownQueueTests
{
    private static readonly int[] s_orderOneTwoThree = { 1, 2, 3 };
    private static readonly int[] s_orderOne = { 1 };
    private static readonly int[] s_orderOneTwo = { 1, 2 };

    /// <summary>
    /// Left with no <see cref="ProtocolBase.UiThreadInvoker"/>, so posting runs inline and the
    /// queue can be exercised without building a window to marshal through.
    /// </summary>
    private sealed class QueueingProtocol : ProtocolBase
    {
        public QueueingProtocol() : base("queueing")
        {
        }

        public void QueueWhenIdle(Action action) => PostToUiThreadWhenIdle(action);

        protected override void CleanupProtocolResources()
        {
        }
    }

    [SetUp]
    public void Setup() => ProtocolBase.ResetIdleQueueForTests();

    [TearDown]
    public void TearDown() => ProtocolBase.ResetIdleQueueForTests();

    [Test]
    public void QueuedWorkDoesNotRunInline()
    {
        bool ran = false;

        new QueueingProtocol().QueueWhenIdle(() => ran = true);

        Assert.That(ran, Is.False,
                    "The work ran on the spot. Running a teardown inline is exactly what puts it " +
                    "inside another session's mstscax call.");
    }

    [Test]
    public void DrainRunsEverythingImmediatelyForWhenNoIdleTurnIsComing()
    {
        var order = new List<int>();
        var protocol = new QueueingProtocol();
        protocol.QueueWhenIdle(() => order.Add(1));
        protocol.QueueWhenIdle(() => order.Add(2));

        ProtocolBase.DrainQueuedTeardownsNow();

        Assert.That(order, Is.EqualTo(s_orderOneTwo),
                    "Work queued for idle was lost. During shutdown the message loop stops, so this " +
                    "is the only thing that still runs those teardowns.");
    }

    [Test]
    public void QueuedWorkRunsOnTheMessageLoopsIdleTurnInOrder()
    {
        var order = new List<int>();
        Exception? failure = null;

        // Its own STA thread with its own message loop. Application.Run/ExitThread act on the
        // thread that calls them, and running them on the shared test thread tears the message
        // loop out from under every fixture that follows.
        var thread = new Thread(() =>
        {
            try
            {
                var protocol = new QueueingProtocol();
                protocol.QueueWhenIdle(() => order.Add(1));
                protocol.QueueWhenIdle(() => order.Add(2));
                protocol.QueueWhenIdle(() => order.Add(3));

                DateTime deadline = DateTime.UtcNow.AddSeconds(5);
                using var poller = new System.Windows.Forms.Timer { Interval = 25 };
                poller.Tick += (sender, args) =>
                {
                    if (order.Count < 3 && DateTime.UtcNow <= deadline)
                    {
                        return;
                    }

                    poller.Stop();
                    Application.ExitThread();
                };
                poller.Start();
                Application.Run();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.That(thread.Join(TimeSpan.FromSeconds(20)), Is.True, "The message loop thread never finished.");

        if (failure != null)
        {
            throw failure;
        }

        Assert.That(order, Is.EqualTo(s_orderOneTwoThree),
                    "Queued teardowns did not all run on idle turns, in order. If Application.Idle " +
                    "no longer fires here, teardowns would never run at all.");
    }

    // Application.RaiseIdle raises Application.Idle on this thread without a message loop, so
    // each call below is exactly one idle turn, and the number of handlers it reaches is what
    // these count.

    [Test]
    public void QueueingTwiceHooksTheIdleTurnOnce()
    {
        // Hooked twice, one idle turn would run two teardowns back to back, which is the pile-up
        // the queue exists to prevent.
        var ran = new List<int>();
        var protocol = new QueueingProtocol();
        protocol.QueueWhenIdle(() => ran.Add(1));
        protocol.QueueWhenIdle(() => ran.Add(2));

        Application.RaiseIdle(EventArgs.Empty);

        Assert.That(ran, Is.EqualTo(s_orderOne), "One idle turn ran more than one queued item.");
    }

    [Test]
    public void TheIdleHandlerIsAttachedWhileWorkIsQueuedAndDetachedOnceItIsDone()
    {
        var protocol = new QueueingProtocol();
        protocol.QueueWhenIdle(() => { });
        bool attachedWhileQueued = ProtocolBase.IdleHandlerAttachedForTests;

        Application.RaiseIdle(EventArgs.Empty);
        bool attachedAfterTheLastItem = ProtocolBase.IdleHandlerAttachedForTests;

        // The turn that finds the queue empty is the one that lets go.
        Application.RaiseIdle(EventArgs.Empty);

        Assert.Multiple(() =>
        {
            Assert.That(attachedWhileQueued, Is.True);
            Assert.That(attachedAfterTheLastItem, Is.True);
            Assert.That(ProtocolBase.IdleHandlerAttachedForTests, Is.False,
                        "Application.Idle still holds the queue with nothing left in it.");
        });
    }

    [Test]
    public void AnIdleTurnWithNothingQueuedDoesNotThrow()
    {
        var protocol = new QueueingProtocol();
        protocol.QueueWhenIdle(() => { });
        Application.RaiseIdle(EventArgs.Empty);

        Assert.DoesNotThrow(() => Application.RaiseIdle(EventArgs.Empty),
                            "The idle turn after the last item tried to dequeue from an empty queue.");
    }

    [Test]
    public void WorkQueuedAfterTheHandlerLetGoStillRunsOnceTurnByTurn()
    {
        // The second round has to hook the loop again, and only once.
        var ran = new List<int>();
        var protocol = new QueueingProtocol();
        protocol.QueueWhenIdle(() => ran.Add(1));
        Application.RaiseIdle(EventArgs.Empty);
        Application.RaiseIdle(EventArgs.Empty);
        Assume.That(ProtocolBase.IdleHandlerAttachedForTests, Is.False);

        protocol.QueueWhenIdle(() => ran.Add(2));
        protocol.QueueWhenIdle(() => ran.Add(3));
        Application.RaiseIdle(EventArgs.Empty);

        Assert.That(ran, Is.EqualTo(s_orderOneTwo),
                    "The second round was never run, or ran more than one item on one idle turn.");
    }
}
