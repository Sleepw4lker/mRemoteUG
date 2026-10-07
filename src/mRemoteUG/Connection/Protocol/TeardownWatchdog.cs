using System;
using System.Diagnostics;
using mRemoteUG.Messages;
using mRemoteUG.Messages.MessageWriters;

namespace mRemoteUG.Connection.Protocol
{
    /// <summary>
    /// Reports, from a background thread, a session teardown stage that has stopped returning.
    /// </summary>
    /// <remarks>
    /// Every stage of <see cref="ProtocolBase"/>'s teardown is logged only once it has completed,
    /// so a stage that never completes leaves nothing behind at all - the log simply stops
    /// mid-teardown. That is what "closing several tabs at once freezes the application" looks like
    /// from the outside, and it gives no way to tell a hang from a crash, or to know which stage is
    /// responsible.
    /// <para>
    /// Reports go to <see cref="Messages.MessageWriters.FilteredLogWriter"/> rather than through
    /// <see cref="Messages.MessageCollector"/>: the collector invokes its writers on the calling
    /// thread and one of them updates the notification panel, and the UI thread is precisely the
    /// one that is stuck. Going to the log writer rather than to the sink directly keeps the report
    /// subject to Options -> Notifications -> Logging, which reads the setting per message, so a
    /// user who has switched warnings off stays switched off even mid-hang.
    /// </para>
    /// </remarks>
    internal static class TeardownWatchdog
    {
        private static readonly object Gate = new object();

        /// <summary>How long a stage may run before the first report. Shortened by tests.</summary>
        internal static TimeSpan FirstReportAfter = TimeSpan.FromSeconds(5);

        /// <summary>How often to repeat the report while the stage is still outstanding.</summary>
        internal static TimeSpan ReportEvery = TimeSpan.FromSeconds(10);

        /// <summary>How often the watchdog looks. Shortened by tests.</summary>
        internal static TimeSpan PollEvery = TimeSpan.FromSeconds(1);

        /// <summary>Where reports go. Replaced by tests.</summary>
        internal static Action<string> Report = message =>
        {
            try { FilteredLogWriter.Write(MessageClass.WarningMsg, message); }
            catch { /* the watchdog must never be the thing that throws */ }
        };

        private static System.Threading.Timer _timer;
        private static string _stage;
        private static Stopwatch _clock;
        private static TimeSpan _nextReport;

        /// <summary>Records the stage now starting, replacing any previous one.</summary>
        internal static void Enter(string stage)
        {
            lock (Gate)
            {
                _stage = stage;
                _clock = Stopwatch.StartNew();
                _nextReport = FirstReportAfter;
                if (_timer == null)
                    _timer = new System.Threading.Timer(Tick, null, PollEvery, PollEvery);
            }
        }

        /// <summary>Records that no teardown stage is outstanding.</summary>
        internal static void Leave()
        {
            lock (Gate)
            {
                _stage = null;
                _clock = null;
            }
        }

        /// <summary>Drops the timer and any outstanding stage. Tests only.</summary>
        internal static void ResetForTests()
        {
            lock (Gate)
            {
                _timer?.Dispose();
                _timer = null;
                _stage = null;
                _clock = null;
            }
        }

        private static void Tick(object? state)
        {
            string message = null;
            lock (Gate)
            {
                if (_stage != null && _clock != null && _clock.Elapsed >= _nextReport)
                {
                    message = $"Close: still {_stage} after {(int)_clock.Elapsed.TotalSeconds} s - " +
                              "the UI thread has not come back from this stage.";
                    _nextReport = _clock.Elapsed + ReportEvery;
                }
            }

            if (message != null)
                Report(message);
        }
    }
}
