using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;

using mRemoteUG.Connection.Protocol;

using NUnit.Framework;

namespace mRemoteUG.Tests.Connection.Protocol;

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
[NonParallelizable]
[Apartment(ApartmentState.STA)]
[SuppressMessage("Design", "CA1001",
    Justification = "[TearDown] disposes _originalParent after every test. "
        + "NUnit would dispose an IDisposable fixture once, after its last test.")]
public class DeferredControlDisposalTests
{
    // Rebuilt by [SetUp] before each test, so null! rather than an initializer here;
    // see ADR-0022 on when that is the right answer.
    private Form _originalParent = null!;
    /// <summary>The shipped value, captured at type load before any test can change it.</summary>
    private static readonly TimeSpan s_productionSettleWait = DeferredControlDisposal.SettleWaitOnShutdown;
    private static readonly TimeSpan s_productionGiveUpAfter = DeferredControlDisposal.GiveUpAfter;

    [SetUp]
    public void Setup()
    {
        DeferredControlDisposal.ResetForTests();
        DeferredControlDisposal.SettleWaitOnShutdown = s_productionSettleWait;
        DeferredControlDisposal.GiveUpAfter = s_productionGiveUpAfter;
        _originalParent = new Form();
        _originalParent.Show();
    }

    [TearDown]
    public void TearDown()
    {
        DeferredControlDisposal.ResetForTests();
        DeferredControlDisposal.SettleWaitOnShutdown = s_productionSettleWait;
        DeferredControlDisposal.GiveUpAfter = s_productionGiveUpAfter;
        // Hold() now drives TeardownWatchdog too - nothing here resets it otherwise, and it
        // would leave its background timer running for every fixture that runs afterwards.
        TeardownWatchdog.ResetForTests();
        _originalParent?.Dispose();
    }

    private Panel ParkedControl(System.Func<bool> isSafeToDispose)
    {
        var control = new Panel();
        _originalParent.Controls.Add(control);
        DeferredControlDisposal.Hold(control, isSafeToDispose, "#1/server1");
        return control;
    }

    [Test]
    public void AParkedControlIsLeftAloneWhileItIsNotSafeToDispose()
    {
        Control control = ParkedControl(() => false);

        for (int i = 0; i < 5; i++)
        {
            DeferredControlDisposal.RunOneIdleTurnForTests();
        }

        Assert.Multiple(() =>
        {
            Assert.That(control.IsDisposed, Is.False);
            Assert.That(DeferredControlDisposal.Count, Is.EqualTo(1), "It was dropped, not held.");
        });
    }

    [Test]
    public void AParkedControlIsDisposedOnTheFirstIdleTurnThatSaysItIsSafe()
    {
        bool safe = false;
        Control control = ParkedControl(() => safe);

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
        Control control = ParkedControl(() => false);

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
        Control first = ParkedControl(() => true);
        Control second = ParkedControl(() => true);

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
        Control stillBusy = ParkedControl(() => false);

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
        bool settled = false;
        int askedWhileUnsettled = 0;
        var control = new Panel();
        _originalParent.Controls.Add(control);
        DeferredControlDisposal.Hold(control, () =>
        {
            if (settled)
            {
                return true;
            }

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
        bool disposedOnIdle = false;
        System.Exception? failure = null;

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

                DateTime deadline = System.DateTime.UtcNow.AddSeconds(5);
                using var poller = new System.Windows.Forms.Timer { Interval = 25 };
                poller.Tick += (sender, args) =>
                {
                    if (!control.IsDisposed && System.DateTime.UtcNow <= deadline)
                    {
                        return;
                    }

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
        {
            throw failure;
        }

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
        int hookRan = 0;
        Control control = ParkedControlWithHook(() => true, () => hookRan++);

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
        int hookRan = 0;
        Control control = ParkedControlWithHook(() => false, () => hookRan++);

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
        int hookRan = 0;
        Control control = ParkedControlWithHook(() => false, () => hookRan++);

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
        Control control = ParkedControlWithHook(() => true, () => throw new InvalidOperationException("hook"));

        DeferredControlDisposal.RunOneIdleTurnForTests();

        Assert.Multiple(() =>
        {
            Assert.That(control.IsDisposed, Is.True,
                        "A throwing hook left the control alive.");
            Assert.That(DeferredControlDisposal.Count, Is.Zero, "It was left parked as well.");
        });
    }

    private Panel ParkedControlWithHook(System.Func<bool> isSafeToDispose, System.Action beforeDispose)
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
                if (SleepOnDetach > TimeSpan.Zero)
                {
                    Thread.Sleep(SleepOnDetach);
                }
            }
            else if (SleepOnAttach > TimeSpan.Zero)
            {
                Thread.Sleep(SleepOnAttach);
            }
        }
    }

    private static string? WatchdogStageUnderSimulatedSlowness(Action<SlowReparentControl> configure)
    {
        Action<string> originalReport = TeardownWatchdog.Report;
        TimeSpan originalFirst = TeardownWatchdog.FirstReportAfter;
        TimeSpan originalPoll = TeardownWatchdog.PollEvery;
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
            {
                Thread.Sleep(10);
            }

            return reports.TryPeek(out string? report) ? report : null;
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
        string? report = WatchdogStageUnderSimulatedSlowness(
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
        string? report = WatchdogStageUnderSimulatedSlowness(
            c => c.SleepOnAttach = TimeSpan.FromMilliseconds(300));

        Assert.That(report, Is.Not.Null, "The watchdog never reported the stalled attach.");
        Assert.Multiple(() =>
        {
            Assert.That(report, Does.Contain("attaching to the holding area"));
            Assert.That(report, Does.Not.Contain("detaching"));
        });
    }

    /// <summary>A panel that records the last visibility it was asked for.</summary>
    /// <remarks>
    /// <see cref="Control.Visible"/> reads false for any control whose parent is hidden, and
    /// the holding area always is, so reading it back cannot tell whether the park hid the
    /// control itself.
    /// </remarks>
    private sealed class VisibilityRecordingPanel : Panel
    {
        public bool? LastVisibleSet { get; private set; }

        protected override void SetVisibleCore(bool value)
        {
            LastVisibleSet = value;
            base.SetVisibleCore(value);
        }
    }

    [Test]
    public void HoldingAnAlreadyDisposedControlParksNothing()
    {
        var control = new Panel();
        _originalParent.Controls.Add(control);
        control.Dispose();

        DeferredControlDisposal.Hold(control, () => true, "#1/server1");

        Assert.Multiple(() =>
        {
            Assert.That(DeferredControlDisposal.Count, Is.Zero,
                        "A disposed control was parked, to be disposed a second time.");
            Assert.That(DeferredControlDisposal.IdleHandlerAttachedForTests, Is.False);
        });
    }

    [Test]
    public void AParkedControlIsHidden()
    {
        var control = new VisibilityRecordingPanel();
        _originalParent.Controls.Add(control);

        DeferredControlDisposal.Hold(control, () => false, "#1/server1");

        Assert.That(control.LastVisibleSet, Is.False,
                    "The parked control was not hidden itself, so showing the holding area shows it.");
    }

    [Test]
    public void TheHoldingAreaIsAnInvisibleOffScreenFormOutsideTheTaskbar()
    {
        // A window flashing up on screen, or a stray taskbar button, every time a session is
        // parked would be a visible regression; the form only exists to own the controls.
        Control control = ParkedControl(() => false);

        Form holdingArea = control.FindForm()
                           ?? throw new AssertionException("The parked control is not on any form.");
        Assert.Multiple(() =>
        {
            Assert.That(holdingArea, Is.SameAs(DeferredControlDisposal.HoldingAreaForTests));
            Assert.That(holdingArea, Is.Not.SameAs(_originalParent));
            Assert.That(holdingArea.Visible, Is.False, "The holding area is visible.");
            Assert.That(holdingArea.ShowInTaskbar, Is.False, "The holding area has a taskbar button.");
            Assert.That(holdingArea.FormBorderStyle, Is.EqualTo(FormBorderStyle.None));
            Assert.That(holdingArea.StartPosition, Is.EqualTo(FormStartPosition.Manual));
            Assert.That(holdingArea.Location, Is.EqualTo(new Point(-32000, -32000)),
                        "The holding area is not off-screen.");
            Assert.That(holdingArea.Text, Is.EqualTo("mRemoteUG parked sessions"));
            Assert.That(holdingArea.IsHandleCreated, Is.True,
                        "The holding area was never shown, so it has no handle to reparent into.");
        });
    }

    [Test]
    public void EveryParkedControlSharesOneHoldingArea()
    {
        Control first = ParkedControl(() => false);
        Control second = ParkedControl(() => false);

        Assert.That(second.FindForm(), Is.SameAs(first.FindForm()),
                    "A second park built a second holding form.");
    }

    [Test]
    public void OfTwoControlsThatAreBothSafeTheFirstParkedIsDisposedFirst()
    {
        Control first = ParkedControl(() => true);
        Control second = ParkedControl(() => true);

        DeferredControlDisposal.RunOneIdleTurnForTests();

        Assert.Multiple(() =>
        {
            Assert.That(first.IsDisposed, Is.True, "The first parked control was passed over.");
            Assert.That(second.IsDisposed, Is.False);
        });
    }

    [Test]
    public void TheIdleHandlerStaysAttachedWhileAnythingIsStillParked()
    {
        ParkedControl(() => true);
        ParkedControl(() => true);
        Assume.That(DeferredControlDisposal.IdleHandlerAttachedForTests, Is.True);

        DeferredControlDisposal.RunOneIdleTurnForTests();

        Assert.Multiple(() =>
        {
            Assert.That(DeferredControlDisposal.Count, Is.EqualTo(1));
            Assert.That(DeferredControlDisposal.IdleHandlerAttachedForTests, Is.True,
                        "The handler was detached with a control still parked, so nothing would "
                        + "ever come back for it.");
        });
    }

    [Test]
    public void TheIdleHandlerIsDetachedOnceTheLastParkedControlIsGone()
    {
        // Left attached, Application.Idle keeps calling into this class on every idle turn of
        // the application's life for nothing.
        ParkedControl(() => true);

        DeferredControlDisposal.RunOneIdleTurnForTests();

        Assert.Multiple(() =>
        {
            Assert.That(DeferredControlDisposal.Count, Is.Zero);
            Assert.That(DeferredControlDisposal.IdleHandlerAttachedForTests, Is.False);
        });
    }

    [Test]
    public void ParkingTwiceHooksTheIdleTurnOnce()
    {
        // Application.RaiseIdle raises the event on this thread without a message loop, so the
        // number of handlers it reaches is what is counted here. Hooked twice, one idle turn
        // would dispose two controls, which is what one-per-turn exists to prevent.
        Control first = ParkedControl(() => true);
        Control second = ParkedControl(() => true);

        Application.RaiseIdle(EventArgs.Empty);

        Assert.That(new[] { first.IsDisposed, second.IsDisposed }, Has.Exactly(1).True,
                    "One idle turn disposed more than one control: the handler is hooked twice.");
    }

    [Test]
    public void ControlsParkedAfterTheHandlerWasDetachedAreStillDisposedOnePerIdleTurn()
    {
        // The second round has to hook the idle turn again, and only once.
        Control first = ParkedControl(() => true);
        Application.RaiseIdle(EventArgs.Empty);
        Assume.That(first.IsDisposed, Is.True);
        Assume.That(DeferredControlDisposal.IdleHandlerAttachedForTests, Is.False);

        Control second = ParkedControl(() => true);
        Control third = ParkedControl(() => true);
        Application.RaiseIdle(EventArgs.Empty);
        bool[] afterOneTurn = { second.IsDisposed, third.IsDisposed };
        Application.RaiseIdle(EventArgs.Empty);

        Assert.Multiple(() =>
        {
            Assert.That(afterOneTurn, Has.Exactly(1).True,
                        "The second round was never picked up, or ran more than one dispose on "
                        + "one idle turn.");
            Assert.That(second.IsDisposed && third.IsDisposed, Is.True);
            Assert.That(DeferredControlDisposal.IdleHandlerAttachedForTests, Is.False);
        });
    }

    [Test]
    public void ShutdownUnhooksTheIdleTurnAndDisposesTheHoldingArea()
    {
        ParkedControl(() => true);
        Form holdingArea = DeferredControlDisposal.HoldingAreaForTests
                           ?? throw new InconclusiveException("Parking made no holding area.");

        DeferredControlDisposal.DisposeEverythingNow();

        Assert.Multiple(() =>
        {
            Assert.That(DeferredControlDisposal.IdleHandlerAttachedForTests, Is.False,
                        "Application.Idle still holds the class after shutdown.");
            Assert.That(holdingArea.IsDisposed, Is.True, "The holding form was left alive.");
            Assert.That(DeferredControlDisposal.HoldingAreaForTests, Is.Null,
                        "The disposed holding form is still referenced, so a later park would "
                        + "reuse a dead form.");
        });
    }
}
