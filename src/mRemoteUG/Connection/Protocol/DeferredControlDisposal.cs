using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using mRemoteUG.App;
using mRemoteUG.Messages;

namespace mRemoteUG.Connection.Protocol
{
    /// <summary>
    /// Holds a hosted control that is not safe to dispose yet, and disposes it once it is.
    /// </summary>
    /// <remarks>
    /// Releasing an RDP control whose session has not finished disconnecting blocks inside the
    /// release, on the UI thread, for as long as the network side takes - 47.5 seconds in the log
    /// that found this. A wedged UI thread takes the whole desktop with it, because a
    /// redirected-keys session owns a low-level keyboard hook Windows has to call on that thread.
    /// So the close must not wait for such a control.
    /// <para>
    /// The obvious alternative - give up on the control and leave it alive, which is upstream
    /// mRemoteNG's behaviour - is worse than it looks. A live RDP control that is still alive when
    /// the process exits crashes it outright, and so does one that has merely been unparented. Both
    /// were isolated down to a single variable each: leave one alone and let the process end,
    /// crash; dispose it with its parent chain first, clean exit every time.
    /// </para>
    /// <para>
    /// So neither block nor abandon: park it. The control moves straight from the dying tab into an
    /// off-screen holding form - never parentless, never orphaned - the tab closes immediately, and
    /// every idle turn afterwards asks whether the session has gone down yet. When it has, the
    /// control is disposed for real. The UI never blocks, and nothing is left alive at exit.
    /// </para>
    /// </remarks>
    internal static class DeferredControlDisposal
    {
        /// <summary>Stop retrying a control that is never going to come back. Shortened by tests.</summary>
        internal static TimeSpan GiveUpAfter = TimeSpan.FromMinutes(2);

        private sealed class HeldControl
        {
            public Control Control;
            public Func<bool> IsSafeToDispose;
            public string Label;
            public Stopwatch Held;
            public Action BeforeDispose;
        }

        private static readonly List<HeldControl> Held = new List<HeldControl>();
        private static Form _holdingArea;
        private static bool _idleHandlerAttached;

        internal static int Count
        {
            get { lock (Held) return Held.Count; }
        }

        /// <summary>
        /// Takes a control the caller must not dispose, and disposes it later on its behalf.
        /// </summary>
        /// <param name="control">The hosted control.</param>
        /// <param name="isSafeToDispose">
        /// Asked on each idle turn. Until it answers true the control is left strictly alone.
        /// </param>
        /// <param name="label">Session label, for the log.</param>
        /// <param name="beforeDispose">
        /// Run once, immediately before the control is really disposed, on whichever of the three
        /// release paths reaches it first. Parking is what separates a close from the release, so
        /// this is a protocol's only chance to drop what it must not still be holding when the
        /// native control goes - its OCX event sinks, and the sub-interface RCWs that keep the
        /// native session's refcount off zero. Best-effort: a hook that throws does not stop the
        /// dispose, because a live control reaching process exit crashes the process.
        /// </param>
        internal static void Hold(Control control, Func<bool> isSafeToDispose, string label,
                                  Action beforeDispose = null)
        {
            if (control == null || control.IsDisposed)
                return;

            try
            {
                // Straight from one parent to the next. An unparented live RDP control is as fatal
                // at exit as an undisposed one, so it must never be without a parent in between.
                var holdingArea = GetHoldingArea();
                var stopwatch = Stopwatch.StartNew();

                // Three stages rather than one. TearDown already reports this whole call as
                // "disposing the hosted control" if it stalls, but that cannot say which of these
                // three is the one not returning - and a park was assumed cheap because only
                // Dispose() had ever been measured blocking on a live session (see ADR-0005).
                // TeardownWatchdog.Enter replaces whatever stage is current, so whichever of these
                // is still running when it reports is the one it names.
                TeardownWatchdog.Enter($"[{label}] park: detaching from the dying tab");
                control.Parent?.Controls.Remove(control);
                Log($"Close [{label}] park: detached at {stopwatch.ElapsedMilliseconds} ms");

                TeardownWatchdog.Enter($"[{label}] park: attaching to the holding area");
                holdingArea.Controls.Add(control);
                Log($"Close [{label}] park: attached to the holding area at {stopwatch.ElapsedMilliseconds} ms");

                TeardownWatchdog.Enter($"[{label}] park: hiding");
                control.Visible = false;
                Log($"Close [{label}] park: hidden at {stopwatch.ElapsedMilliseconds} ms");

                lock (Held)
                {
                    Held.Add(new HeldControl
                    {
                        Control = control,
                        IsSafeToDispose = isSafeToDispose,
                        Label = label,
                        Held = Stopwatch.StartNew(),
                        BeforeDispose = beforeDispose
                    });
                }

                Log($"Close [{label}] hosted control parked rather than disposed - its session has "
                    + $"not gone down, and disposing one that has not is what blocks the UI thread "
                    + $"({Count} parked)");

                AttachIdleHandler();
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector?.AddExceptionMessage(
                    "Couldn't park the hosted control (Connection.Protocol.DeferredControlDisposal)", ex);
            }
        }

        /// <summary>
        /// Disposes whatever is now safe to dispose. Runs on idle turns, one control at a time.
        /// </summary>
        /// <remarks>
        /// One per turn deliberately. Even a dispose that is safe still takes tens of milliseconds,
        /// and the point of all this is that the UI thread stays available between them.
        /// </remarks>
        private static void OnIdle(object? sender, EventArgs e)
        {
            HeldControl next = null;
            var giveUp = false;

            lock (Held)
            {
                foreach (var candidate in Held)
                {
                    var expired = candidate.Held.Elapsed > GiveUpAfter;
                    var safe = false;

                    if (!expired)
                    {
                        safe = IsSafeToDispose(candidate);
                    }

                    if (!safe && !expired)
                        continue;

                    next = candidate;
                    giveUp = expired && !safe;
                    break;
                }

                if (next != null)
                    Held.Remove(next);

                if (Held.Count == 0)
                    DetachIdleHandler();
            }

            if (next == null)
                return;

            DisposeHeldControl(next, giveUp
                                        ? $"after {Seconds(next.Held.Elapsed, "F0")} s its session still "
                                          + "had not gone down; disposing anyway rather than hold it forever"
                                        : $"its session went down after {Seconds(next.Held.Elapsed, "F1")} s");
        }

        private static void DisposeHeldControl(HeldControl held, string why)
        {
            Log($"Close [{held.Label}] disposing the parked hosted control - {why}");

            // Before the release rather than after it: releasing the control is the thing that can
            // block, and what is still advised on it or still referencing it is what decides how
            // long that takes.
            try { held.BeforeDispose?.Invoke(); }
            catch (Exception ex)
            {
                Runtime.MessageCollector?.AddExceptionMessage(
                    "A pre-dispose hook threw; disposing the parked control anyway "
                    + "(Connection.Protocol.DeferredControlDisposal)", ex);
            }

            var stopwatch = Stopwatch.StartNew();
            try { held.Control.Dispose(); }
            catch (Exception ex)
            {
                Runtime.MessageCollector?.AddExceptionMessage(
                    "Couldn't dispose a parked hosted control (Connection.Protocol.DeferredControlDisposal)", ex);
            }

            Log($"Close [{held.Label}] parked hosted control disposed at {stopwatch.ElapsedMilliseconds} ms");
        }

        /// <summary>
        /// Disposes everything still parked, then the holding area itself.
        /// </summary>
        /// <remarks>
        /// Called on the way out. Leaving a live RDP control alive at process exit crashes the
        /// process, so however long this takes it is not an option to skip it - and by this point
        /// the sessions have had the whole application close to go down in, so in practice there is
        /// rarely anything here at all.
        /// </remarks>
        internal static void DisposeEverythingNow()
        {
            List<HeldControl> remaining;
            lock (Held)
            {
                remaining = new List<HeldControl>(Held);
                Held.Clear();
                DetachIdleHandler();
            }

            // Quitting while sessions are still parked mid-logon would otherwise put the very block
            // this class exists to avoid back on the way out, since the dispose below is
            // unconditional. Give them a bounded chance to settle first - measured at 0.6-1.9 s in
            // practice - which also drives each probe to ask for its disconnect.
            WaitForParkedSessionsToSettle(remaining);

            foreach (var held in remaining)
                DisposeHeldControl(held, "the application is closing and nothing may be left alive");

            try
            {
                _holdingArea?.Dispose();
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector?.AddExceptionMessage(
                    "Couldn't dispose the holding area (Connection.Protocol.DeferredControlDisposal)", ex);
            }
            finally
            {
                _holdingArea = null;
            }
        }

        /// <summary>
        /// How long the way out may spend waiting for parked sessions to settle. Shortened by tests.
        /// </summary>
        internal static TimeSpan SettleWaitOnShutdown = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Pumps until every parked session reports itself safe to dispose, or the budget runs out.
        /// </summary>
        /// <remarks>
        /// <see cref="Application.Idle"/> is not raised here - the message loop has already
        /// finished - so the probes are called directly. That is also what makes it safe to do so:
        /// with no idle turns arriving, nothing else is mutating the list underneath us.
        /// </remarks>
        private static void WaitForParkedSessionsToSettle(List<HeldControl> parked)
        {
            if (parked.Count == 0)
                return;

            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < SettleWaitOnShutdown)
            {
                if (parked.TrueForAll(IsSafeToDispose))
                {
                    Log($"Close: all {parked.Count} parked session(s) settled in "
                        + $"{stopwatch.ElapsedMilliseconds} ms; disposing them now");
                    return;
                }

                Application.DoEvents();
                Thread.Sleep(10);
            }

            var unsettled = parked.FindAll(held => !IsSafeToDispose(held)).Count;
            Log($"Close: {unsettled} of {parked.Count} parked session(s) had still not settled after "
                + $"{stopwatch.ElapsedMilliseconds} ms; disposing them anyway, because a live RDP "
                + "control reaching process exit crashes the process",
                MessageClass.WarningMsg);
        }

        private static bool IsSafeToDispose(HeldControl held)
        {
            try { return held.IsSafeToDispose == null || held.IsSafeToDispose(); }
            catch (Exception) { return true; /* nothing left to ask - let it go */ }
        }

        /// <summary>
        /// An off-screen form that owns parked controls. Shown once so its handle exists - a
        /// control cannot be reparented into a form that has none - then hidden immediately, and
        /// positioned off-screen so the showing is never visible.
        /// </summary>
        private static Form GetHoldingArea()
        {
            if (_holdingArea != null && !_holdingArea.IsDisposed)
                return _holdingArea;

            _holdingArea = new Form
            {
                Text = "mRemoteUG parked sessions",
                ShowInTaskbar = false,
                FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.Manual,
                Location = new Point(-32000, -32000),
                Size = new Size(1, 1)
            };

            _holdingArea.Show();
            _holdingArea.Visible = false;
            return _holdingArea;
        }

        private static void AttachIdleHandler()
        {
            if (_idleHandlerAttached)
                return;

            _idleHandlerAttached = true;
            Application.Idle += OnIdle;
        }

        private static void DetachIdleHandler()
        {
            if (!_idleHandlerAttached)
                return;

            _idleHandlerAttached = false;
            Application.Idle -= OnIdle;
        }

        /// <summary>
        /// Invariant, so the log reads the same everywhere. On a de-DE machine the default gave
        /// "0,6 s" in an otherwise English log, which is a poor thing to have to parse later.
        /// </summary>
        private static string Seconds(TimeSpan elapsed, string format) =>
            elapsed.TotalSeconds.ToString(format, System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>
        /// Parking detail, at Debug unless the caller says otherwise.
        /// </summary>
        /// <remarks>
        /// Each of these fires per parked control on the way out, so they were per-session noise
        /// in a default log. The one that is not - sessions that never settled - asks for Warning
        /// explicitly, because that is the case a log has to show without anyone having thought to
        /// turn something on first.
        /// </remarks>
        private static void Log(string message, MessageClass messageClass = MessageClass.DebugMsg)
        {
            Runtime.MessageCollector?.AddMessage(messageClass, message, true);
        }

        /// <summary>Tests only: drop everything without touching a message loop.</summary>
        internal static void ResetForTests()
        {
            lock (Held)
            {
                Held.Clear();
                DetachIdleHandler();
            }

            try { _holdingArea?.Dispose(); }
            catch (Exception) { /* nothing to do about it here */ }
            _holdingArea = null;
        }

        /// <summary>Tests only: stand in for an idle turn.</summary>
        internal static void RunOneIdleTurnForTests() => OnIdle(null, EventArgs.Empty);
    }
}
