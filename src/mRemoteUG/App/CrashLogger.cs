using System;
using System.Threading;
using System.Windows.Forms;
using mRemoteUG.Messages;
using mRemoteUG.Messages.MessageWriters;
using mRemoteUG.UI.TaskDialog;

namespace mRemoteUG.App
{
    /// <summary>
    /// Puts exceptions that escape every <c>catch</c> into the log before the process goes down.
    /// </summary>
    /// <remarks>
    /// Until this existed, a crash was the one failure the log could not tell you about: the
    /// application installed no handler, so the runtime printed a stack trace to a console nobody
    /// was reading and exited. On a machine that is only deployed to - the case ADR-0013 is built
    /// around - that left no evidence at all. The case that proved it: <c>--selftest</c> died at
    /// <c>0xC000041D</c> in 12 runs out of 30 with nothing in the log, because the exception that
    /// started it was handed to WinForms' own dialog rather than to anything that writes things
    /// down. See platform-findings.
    /// <para>
    /// Reports go through <see cref="FilteredLogWriter"/> like everything else (ADR-0017), not to
    /// the sink directly: the message collector cannot be used here because its writers run on the
    /// calling thread and may not be attached yet, but the user's Logging settings still decide.
    /// Error messages are on by default, so a crash is logged unless someone has turned errors off.
    /// </para>
    /// </remarks>
    internal static class CrashLogger
    {
        private static bool _installed;
        private static bool _interactive;
        private static int _uiThreadExceptions;

        /// <summary>
        /// How many exceptions have reached <see cref="Application.ThreadException"/> on a
        /// non-interactive run. Non-zero makes <c>--selftest</c> report FAIL.
        /// </summary>
        internal static int UiThreadExceptionCount => Volatile.Read(ref _uiThreadExceptions);

        /// <summary>
        /// Whether the UI thread's handler is on. Reported by <c>--selftest</c>, because the
        /// consequence of it being off is a crash carrying no record of what caused it.
        /// </summary>
        internal static bool ThreadExceptionInstalled { get; private set; }

        /// <summary>
        /// Installs the handlers. Call once, as early in <c>Main</c> as possible.
        /// </summary>
        /// <param name="interactive">
        /// False under <c>--selftest</c>. A modal dialog raised on a non-interactive run never
        /// gets answered, so that path logs and carries on; it does not show anything and it does
        /// not end the process. The handler itself is installed either way - see the remarks.
        /// </param>
        /// <remarks>
        /// The two handlers are not equivalent. <see cref="AppDomain.UnhandledException"/> is a
        /// notification - the process still dies exactly as it did before - so it is installed on
        /// every path and changes nothing but the log. <see cref="Application.ThreadException"/>
        /// is different: registering it <em>suppresses</em> the WinForms error dialog. That cuts
        /// both ways, and is why it is registered on every path - unsubscribed, the dialog is not
        /// avoided, it is WinForms'. Having taken it on, an interactive run has to see the user off
        /// itself rather than carry on in whatever state the failure left it; a non-interactive one
        /// logs and lets <c>--selftest</c> score it.
        /// </remarks>
        internal static void Install(bool interactive)
        {
            if (_installed) return;
            _installed = true;

            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            // Installed on every path. Which of the two things it then does - log only, or log and
            // see the user off - is decided by this flag, not by whether the handler exists.
            _interactive = interactive;
            Application.ThreadException += OnThreadException;
            ThreadExceptionInstalled = true;
        }

        internal static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            // Terminating is all but always true; say which, because a log line that turns out to
            // be the last one is worth being able to recognise as such.
            var fate = e.IsTerminating ? "the process is going down" : "the process is continuing";
            Report($"Unhandled exception - {fate}.", e.ExceptionObject as Exception);
        }

        private static void OnThreadException(object sender, ThreadExceptionEventArgs e)
        {
            if (HandleThreadException(e.Exception, _interactive))
                ShowAndExit(e.Exception);
        }

        /// <summary>
        /// What to do about an exception that reached the UI thread's handler. Returns whether the
        /// caller should show the user a dialog and end the process.
        /// </summary>
        /// <remarks>
        /// Split out from <see cref="OnThreadException"/> so the policy can be tested without a
        /// message loop and without <see cref="Environment.Exit"/> taking the test runner with it.
        /// </remarks>
        internal static bool HandleThreadException(Exception exception, bool interactive)
        {
            if (interactive)
            {
                Report("Unhandled exception on the UI thread.", exception);
                return true;
            }

            // A modal dialog on a non-interactive run never gets answered, which is why this
            // handler used not to be installed at all on that path. That was the wrong conclusion:
            // leaving the handler off does not avoid a dialog, it hands the dialog to WinForms,
            // which shows its own ThreadExceptionDialog. During teardown that dialog cannot always
            // create its window handle, and the Win32Exception (1406) that follows kills the
            // process - so the record of what actually failed is replaced by a complaint about a
            // dialog nobody asked for. Measured at 12 crashes in 30 runs; see platform-findings.
            //
            // So: log it, count it, and let the run carry on. The count makes --selftest report
            // FAIL, which is the outcome an exception on the UI thread deserves, and the report
            // still gets written rather than being lost with the process.
            Interlocked.Increment(ref _uiThreadExceptions);
            Report("Unhandled exception on the UI thread - recorded, the run continues.", exception);
            return false;
        }


        /// <remarks>
        /// Nothing in here may throw. This runs while the process is already failing, and an
        /// exception raised from a crash handler replaces a diagnosable crash with a mystifying
        /// one. The sink swallows IO errors of its own accord, so this is belt and braces.
        /// </remarks>
        private static void Report(string message, Exception exception)
        {
            try
            {
                FilteredLogWriter.Write(MessageClass.ErrorMsg, message, exception);
            }
            catch (Exception)
            {
            }
        }

        private static void ShowAndExit(Exception exception)
        {
            try
            {
                CTaskDialog.ShowTaskDialogBox(
                    Application.ProductName,
                    "The application has to close",
                    "Something failed that " + Application.ProductName + " cannot recover from. " +
                    "The details have been written to the log.",
                    exception?.ToString() ?? "",
                    Logger.Instance.LogPath,
                    "",
                    "",
                    "",
                    ETaskDialogButtons.Ok,
                    ESysIcons.Error,
                    ESysIcons.Information);
            }
            catch (Exception)
            {
                // A dialog is a courtesy; the log is the point.
            }

            // Environment.Exit rather than Application.Exit: the UI thread has just failed, and
            // asking it to run a graceful shutdown is asking the broken thing to tidy up. The log
            // is already on disk either way - the sink flushes every event as it writes it.
            Environment.Exit(1);
        }
    }
}
