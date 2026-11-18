using System.Diagnostics;

using mRemoteUG.Messages;
using mRemoteUG.Messages.MessageWriters;

namespace mRemoteUG.Connection.Protocol;

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
    private static readonly object s_gate = new object();

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

    private static System.Threading.Timer? s_timer;
    private static string? s_stage;
    private static Stopwatch? s_clock;
    private static TimeSpan s_nextReport;

    /// <summary>Records the stage now starting, replacing any previous one.</summary>
    internal static void Enter(string stage)
    {
        lock (s_gate)
        {
            s_stage = stage;
            s_clock = Stopwatch.StartNew();
            s_nextReport = FirstReportAfter;
            if (s_timer == null)
            {
                s_timer = new System.Threading.Timer(Tick, null, PollEvery, PollEvery);
            }
        }
    }

    /// <summary>Records that no teardown stage is outstanding.</summary>
    internal static void Leave()
    {
        lock (s_gate)
        {
            s_stage = null;
            s_clock = null;
        }
    }

    /// <summary>Drops the timer and any outstanding stage. Tests only.</summary>
    internal static void ResetForTests()
    {
        lock (s_gate)
        {
            s_timer?.Dispose();
            s_timer = null;
            s_stage = null;
            s_clock = null;
        }
    }

    private static void Tick(object? state)
    {
        string? message = null;
        lock (s_gate)
        {
            if (s_stage != null && s_clock != null && s_clock.Elapsed >= s_nextReport)
            {
                message = $"Close: still {s_stage} after {(int)s_clock.Elapsed.TotalSeconds} s - " +
                          "the UI thread has not come back from this stage.";
                s_nextReport = s_clock.Elapsed + ReportEvery;
            }
        }

        if (message != null)
        {
            Report(message);
        }
    }
}
