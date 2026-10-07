using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;
using mRemoteUG.Connection.Protocol;
using NUnit.Framework;

namespace mRemoteUG.Tests.Connection.Protocol
{
    /// <summary>
    /// Pins that a parked control is eventually disposed, rather than merely not disposed.
    /// </summary>
    /// <remarks>
    /// This is the half that separates parking from upstream mRemoteNG's leak. Upstream never
    /// disposes a hosted RDP control at all; this fork declines to dispose one only while doing so
    /// would block the UI thread, and comes back for it afterwards. If that second half does not
    /// happen the fork has upstream's leak plus a holding form, and - worse - a live RDP control
    /// reaching process exit, which crashes the process.
    /// </remarks>
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class DeferredControlDisposalTests
    {
        private Form _originalParent;
        /// <summary>The shipped value, captured at type load before any test can change it.</summary>
        private static readonly TimeSpan ProductionSettleWait = DeferredControlDisposal.SettleWaitOnShutdown;
        private static readonly TimeSpan ProductionGiveUpAfter = DeferredControlDisposal.GiveUpAfter;

        [SetUp]
        public void Setup()
        {
            DeferredControlDisposal.ResetForTests();
            DeferredControlDisposal.SettleWaitOnShutdown = ProductionSettleWait;
            DeferredControlDisposal.GiveUpAfter = ProductionGiveUpAfter;
            _originalParent = new Form();
            _originalParent.Show();
        }

        [TearDown]
        public void TearDown()
        {
            DeferredControlDisposal.ResetForTests();
            DeferredControlDisposal.SettleWaitOnShutdown = ProductionSettleWait;
            DeferredControlDisposal.GiveUpAfter = ProductionGiveUpAfter;
            // Hold() now drives TeardownWatchdog too - nothing here resets it otherwise, and it
            // would leave its background timer running for every fixture that runs afterwards.
            TeardownWatchdog.ResetForTests();
            _originalParent?.Dispose();
            _originalParent = null;
        }

        private Control ParkedControl(System.Func<bool> isSafeToDispose)
        {
            var control = new Panel();
            _originalParent.Controls.Add(control);
            DeferredControlDisposal.Hold(control, isSafeToDispose, "#1/server1");
            return control;
        }

        [Test]
        public void AParkedControlIsLeftAloneWhileItIsNotSafeToDispose()
        {
            var control = ParkedControl(() => false);

            for (var i = 0; i < 5; i++)
                DeferredControlDisposal.RunOneIdleTurnForTests();

            Assert.Multiple(() =>
            {
                Assert.That(control.IsDisposed, Is.False);
                Assert.That(DeferredControlDisposal.Count, Is.EqualTo(1), "It was dropped, not held.");
            });
        }

        [Test]
        public void AParkedControlIsDisposedOnTheFirstIdleTurnThatSaysItIsSafe()
        {
            var safe = false;
            var control = ParkedControl(() => safe);

            DeferredControlDisposal.RunOneIdleTurnForTests();
            Assert.That(control.IsDisposed, Is.False, "Disposed while the session was still up.");

            safe = true;
            DeferredControlDisposal.RunOneIdleTurnForTests();

            Assert.Multiple(() =>
            {
                Assert.That(control.IsDisposed, Is.True,
                            "The parked control was never disposed, which is upstream's leak with extra steps.");
                Assert.That(DeferredControlDisposal.Count, Is.Zero);
            });
        }

        [Test]
        public void ParkingTakesTheControlOutOfItsOldParentWithoutLeavingItParentless()
        {
            // Both halves matter. Left in place, the old parent's dispose cascades into the control
            // and blocks after all; left parentless, it crashes the process at exit.
            var control = ParkedControl(() => false);

            Assert.Multiple(() =>
            {
                Assert.That(control.Parent, Is.Not.Null, "The control was left parentless.");
                Assert.That(control.Parent, Is.Not.EqualTo(_originalParent),
                            "The control was left in the parent that is about to be disposed.");
            });
        }

        [Test]
        public void OnlyOneControlIsDisposedPerIdleTurn()
        {
            // Even a safe dispose takes tens of milliseconds. The whole point is that the UI thread
            // stays available between them, so they must not all run on one turn.
            var first = ParkedControl(() => true);
            var second = ParkedControl(() => true);

            DeferredControlDisposal.RunOneIdleTurnForTests();

            Assert.That(new[] { first.IsDisposed, second.IsDisposed },
                        Has.Exactly(1).True,
                        "Either none or both were disposed on a single idle turn.");
        }

        [Test]
        public void ShutdownDisposesEverythingStillParked()
        {
            // Whatever is left when the message loop ends has to go: a live RDP control reaching
            // process exit crashes the process.
            var stillBusy = ParkedControl(() => false);

            DeferredControlDisposal.SettleWaitOnShutdown = TimeSpan.FromMilliseconds(100);
            DeferredControlDisposal.DisposeEverythingNow();

            Assert.Multiple(() =>
            {
                Assert.That(stillBusy.IsDisposed, Is.True,
                            "A control was still alive after shutdown disposal.");
                Assert.That(DeferredControlDisposal.Count, Is.Zero);
            });
        }

        [Test]
        public void ShutdownWaitsForAParkedSessionToSettleBeforeDisposingIt()
        {
            // Quitting while a session is still parked mid-logon would otherwise put the very block
            // this class exists to avoid back on the way out, since the shutdown dispose is
            // unconditional. Sessions were measured settling in 0.6-1.9 s, so the way out waits.
            var settled = false;
            var askedWhileUnsettled = 0;
            var control = new Panel();
            _originalParent.Controls.Add(control);
            DeferredControlDisposal.Hold(control, () =>
            {
                if (settled) return true;
                askedWhileUnsettled++;
                return false;
            }, "#1/server1");

            // Settles shortly after the wait begins, as a real session does.
            using var settleAfterAMoment = new System.Threading.Timer(_ => settled = true, null, 150, -1);

            DeferredControlDisposal.SettleWaitOnShutdown = TimeSpan.FromSeconds(5);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            DeferredControlDisposal.DisposeEverythingNow();

            Assert.Multiple(() =>
            {
                Assert.That(askedWhileUnsettled, Is.GreaterThan(0),
                            "Shutdown never asked whether the session had settled; it just disposed.");
                Assert.That(stopwatch.Elapsed, Is.GreaterThan(TimeSpan.FromMilliseconds(100)),
                            "Shutdown did not wait for the session to settle.");
                Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(5)),
                            "Shutdown waited out the whole budget instead of stopping once it settled.");
                Assert.That(control.IsDisposed, Is.True, "The control was left alive at exit.");
            });
        }

        [Test]
        public void ParkingIsDrivenByRealIdleTurns()
        {
            // The retry hangs off Application.Idle, which only a running message loop raises.
            // Application.DoEvents() does not raise it, so this needs a real loop - on its own STA
            // thread, because Application.Run/ExitThread act on the thread that calls them and
            // running them on the shared test thread tears the loop out from under every fixture
            // that follows.
            var disposedOnIdle = false;
            System.Exception failure = null;

            var thread = new Thread(() =>
            {
                try
                {
                    DeferredControlDisposal.ResetForTests();

                    using var parent = new Form();
                    parent.Show();
                    var control = new Panel();
                    parent.Controls.Add(control);
                    DeferredControlDisposal.Hold(control, () => true, "#1/server1");

                    var deadline = System.DateTime.UtcNow.AddSeconds(5);
                    using var poller = new System.Windows.Forms.Timer { Interval = 25 };
                    poller.Tick += (sender, args) =>
                    {
                        if (!control.IsDisposed && System.DateTime.UtcNow <= deadline) return;
                        disposedOnIdle = control.IsDisposed;
                        poller.Stop();
                        Application.ExitThread();
                    };
                    poller.Start();
                    Application.Run();
                }
                catch (System.Exception ex)
                {
                    failure = ex;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            Assert.That(thread.Join(System.TimeSpan.FromSeconds(20)), Is.True,
                        "The message loop thread never finished.");

            if (failure != null)
                throw failure;

            Assert.That(disposedOnIdle, Is.True,
                        "A real idle turn never disposed the parked control, so in the running "
                        + "application nothing ever would.");
        }
        [Test]
        public void ThePreDisposeHookRunsOnTheIdleTurnThatDisposesTheControl()
        {
            // The hook is where a protocol drops what it must not be holding when the native
            // control is released - its OCX event sinks, and the sub-interface RCWs that keep the
            // native session's refcount off zero. Parking is the only path that separates the
            // close from the release, so without this the release happens with both still in
            // place.
            var hookRan = 0;
            var control = ParkedControlWithHook(() => true, () => hookRan++);

            DeferredControlDisposal.RunOneIdleTurnForTests();

            Assert.Multiple(() =>
            {
                Assert.That(hookRan, Is.EqualTo(1), "The pre-dispose hook did not run.");
                Assert.That(control.IsDisposed, Is.True);
            });
        }

        [Test]
        public void ThePreDisposeHookRunsWhenTheWaitIsGivenUpOn()
        {
            // The give-up path disposes a control whose session never went down. That is the case
            // where the sinks are most certainly still advised, so it is the one that most needs
            // the hook - and the one an implementation that only hooks the happy path misses.
            var hookRan = 0;
            var control = ParkedControlWithHook(() => false, () => hookRan++);

            DeferredControlDisposal.GiveUpAfter = TimeSpan.Zero;
            DeferredControlDisposal.RunOneIdleTurnForTests();

            Assert.Multiple(() =>
            {
                Assert.That(hookRan, Is.EqualTo(1),
                            "The control was disposed after the give-up without running the hook.");
                Assert.That(control.IsDisposed, Is.True);
            });
        }

        [Test]
        public void ThePreDisposeHookRunsOnTheWayOut()
        {
            var hookRan = 0;
            var control = ParkedControlWithHook(() => false, () => hookRan++);

            DeferredControlDisposal.SettleWaitOnShutdown = TimeSpan.FromMilliseconds(50);
            DeferredControlDisposal.DisposeEverythingNow();

            Assert.Multiple(() =>
            {
                Assert.That(hookRan, Is.EqualTo(1),
                            "Shutdown disposed the control without running the hook.");
                Assert.That(control.IsDisposed, Is.True);
            });
        }

        [Test]
        public void AThrowingPreDisposeHookDoesNotStopTheControlBeingDisposed()
        {
            // Everything the hook does is best-effort cleanup. A control left alive because some
            // of it failed is the one outcome worse than the leak: at process exit it crashes.
            var control = ParkedControlWithHook(() => true, () => throw new InvalidOperationException("hook"));

            DeferredControlDisposal.RunOneIdleTurnForTests();

            Assert.Multiple(() =>
            {
                Assert.That(control.IsDisposed, Is.True,
                            "A throwing hook left the control alive.");
                Assert.That(DeferredControlDisposal.Count, Is.Zero, "It was left parked as well.");
            });
        }

        private Control ParkedControlWithHook(System.Func<bool> isSafeToDispose, System.Action beforeDispose)
        {
            var control = new Panel();
            _originalParent.Controls.Add(control);
            DeferredControlDisposal.Hold(control, isSafeToDispose, "#1/server1", beforeDispose);
            return control;
        }

        /// <summary>
        /// A control that can be made to sit in one of Hold()'s three transitions for as long as
        /// a test wants, to prove the watchdog stage reported for each is the right one and not
        /// just "disposing the hosted control" - the one label that cannot say which of the three
        /// is actually stuck.
        /// </summary>
        private sealed class SlowReparentControl : Control
        {
            public TimeSpan SleepOnDetach = TimeSpan.Zero;
            public TimeSpan SleepOnAttach = TimeSpan.Zero;

            protected override void OnParentChanged(EventArgs e)
            {
                base.OnParentChanged(e);
                if (Parent == null)
                {
                    if (SleepOnDetach > TimeSpan.Zero) Thread.Sleep(SleepOnDetach);
                }
                else if (SleepOnAttach > TimeSpan.Zero)
                {
                    Thread.Sleep(SleepOnAttach);
                }
            }
        }

        private static string WatchdogStageUnderSimulatedSlowness(Action<SlowReparentControl> configure)
        {
            var originalReport = TeardownWatchdog.Report;
            var originalFirst = TeardownWatchdog.FirstReportAfter;
            var originalPoll = TeardownWatchdog.PollEvery;
            var reports = new ConcurrentQueue<string>();

            try
            {
                TeardownWatchdog.ResetForTests();
                TeardownWatchdog.Report = reports.Enqueue;
                TeardownWatchdog.FirstReportAfter = TimeSpan.FromMilliseconds(50);
                TeardownWatchdog.PollEvery = TimeSpan.FromMilliseconds(20);

                var control = new SlowReparentControl();
                configure(control);
                // Not _originalParent: Hold() reads Control.Parent itself, so the control needs a
                // real parent of its own, independent of what the other tests in this fixture use.
                using var parent = new Form();
                parent.Show();
                parent.Controls.Add(control);

                DeferredControlDisposal.Hold(control, () => true, "#9/server9");

                var deadline = Stopwatch.StartNew();
                while (reports.IsEmpty && deadline.ElapsedMilliseconds < 2000)
                    Thread.Sleep(10);

                return reports.TryPeek(out var report) ? report : null;
            }
            finally
            {
                TeardownWatchdog.ResetForTests();
                TeardownWatchdog.Report = originalReport;
                TeardownWatchdog.FirstReportAfter = originalFirst;
                TeardownWatchdog.PollEvery = originalPoll;
            }
        }

        [Test]
        public void AStalledDetachIsReportedAsDetachingNotAsTheWholeParkingStage()
        {
            var report = WatchdogStageUnderSimulatedSlowness(
                c => c.SleepOnDetach = TimeSpan.FromMilliseconds(300));

            Assert.That(report, Is.Not.Null, "The watchdog never reported the stalled detach.");
            Assert.Multiple(() =>
            {
                Assert.That(report, Does.Contain("detaching from the dying tab"));
                Assert.That(report, Does.Not.Contain("attaching"));
            });
        }

        [Test]
        public void AStalledAttachIsReportedAsAttachingNotAsTheWholeParkingStage()
        {
            // The one the production log that prompted this cannot rule out: moving an ActiveX
            // control to a different top-level Form, which the holding area is, can force a
            // handle recreation that a same-Form reparent would not.
            var report = WatchdogStageUnderSimulatedSlowness(
                c => c.SleepOnAttach = TimeSpan.FromMilliseconds(300));

            Assert.That(report, Is.Not.Null, "The watchdog never reported the stalled attach.");
            Assert.Multiple(() =>
            {
                Assert.That(report, Does.Contain("attaching to the holding area"));
                Assert.That(report, Does.Not.Contain("detaching"));
            });
        }
    }
}
