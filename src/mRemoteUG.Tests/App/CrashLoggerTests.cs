using System;
using System.IO;
using mRemoteUG;
using mRemoteUG.App;
using NUnit.Framework;

namespace mRemoteUG.Tests.App
{
    /// <summary>
    /// The last thing the log gets to say.
    /// </summary>
    /// <remarks>
    /// A crash used to be the one failure the log could not report: the application installed no
    /// handler, so the runtime printed to a console nobody was reading and exited. On a machine
    /// that is only deployed to - the case ADR-0013 is built around - that left nothing behind.
    /// <para>
    /// This covers the reporting and the policy. The wiring is covered by <c>--selftest</c>,
    /// whose <c>UI thread</c> check reports whether the handler is installed and fails the run if
    /// anything reached it.
    /// </para>
    /// </remarks>
    [TestFixture]
    public class CrashLoggerTests
    {
        private string _tempDirectory;
        private string _logFile;
        private string _originalLogPath;
        private bool _error;

        [SetUp]
        public void Setup()
        {
            _originalLogPath = Logger.Instance.LogPath;
            _error = Settings.Default.TextLogMessageWriterWriteErrorMsgs;
            Settings.Default.TextLogMessageWriterWriteErrorMsgs = true;

            _tempDirectory = Path.Combine(Path.GetTempPath(), "mRemoteUG.Tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDirectory);
            _logFile = Path.Combine(_tempDirectory, "crash.log");
            Logger.Instance.SetLogPath(_logFile);
        }

        [TearDown]
        public void TearDown()
        {
            Settings.Default.TextLogMessageWriterWriteErrorMsgs = _error;
            Logger.Instance.SetLogPath(_originalLogPath);
            try { Directory.Delete(_tempDirectory, true); } catch (IOException) { }
        }

        [Test]
        public void AnUnhandledExceptionIsLoggedWhole()
        {
            CrashLogger.OnUnhandledException(null, new UnhandledExceptionEventArgs(Wrapped(), true));

            var contents = LogContents();
            Assert.Multiple(() =>
            {
                Assert.That(contents, Does.Contain("Unhandled exception"));
                Assert.That(contents, Does.Contain("the process is going down"),
                            "Whether it was fatal is worth recording: this is the last line.");
                Assert.That(contents, Does.Contain("InvalidOperationException"), "No type.");
                Assert.That(contents, Does.Contain("inner boom"), "No inner exception.");
                Assert.That(contents, Does.Contain("   at "), "No stack frames.");
            });
        }

        /// <summary>
        /// The crash report goes through the same filter as everything else (ADR-0017).
        /// </summary>
        /// <remarks>
        /// Deliberate rather than incidental: a setting that says error messages are not logged has
        /// to mean it, or it silently does not mean what it says. Errors are on by default, so this
        /// costs the report only for someone who has turned them off on purpose.
        /// </remarks>
        [Test]
        public void ACrashReportHonoursTheErrorLoggingSetting()
        {
            Settings.Default.TextLogMessageWriterWriteErrorMsgs = false;

            CrashLogger.OnUnhandledException(null, new UnhandledExceptionEventArgs(Wrapped(), true));

            Assert.That(LogContents(), Does.Not.Contain("Unhandled exception"));
        }

        /// <summary>
        /// A handler that throws turns a diagnosable crash into a mystifying one.
        /// </summary>
        [Test]
        public void ReportingSurvivesAnExceptionObjectItCannotUse()
        {
            // AppDomain.UnhandledException carries an object, not an Exception - it is not
            // guaranteed to be one, and the cast gives null when it is not.
            Assert.DoesNotThrow(() =>
                CrashLogger.OnUnhandledException(null, new UnhandledExceptionEventArgs("not an exception", true)));
        }


        /// <summary>
        /// A non-interactive run logs the exception and asks for no dialog.
        /// </summary>
        /// <remarks>
        /// This is the defect that hid the shutdown crash. <c>--selftest</c> used to install no
        /// <see cref="System.Windows.Forms.Application.ThreadException"/> handler at all, on the
        /// reasoning that a modal box on a non-interactive run never gets answered. But leaving the
        /// handler off does not avoid a dialog - it hands the dialog to WinForms, which shows its
        /// own <c>ThreadExceptionDialog</c>. During teardown that dialog cannot always create its
        /// window handle, and the <c>Win32Exception (1406)</c> that results goes on to kill the
        /// process, replacing the exception that actually failed with one about a dialog. Measured
        /// at 12 crashes in 30 runs on 2026-09-28; see platform-findings.
        /// </remarks>
        [Test]
        public void AUiThreadExceptionOnANonInteractiveRunIsLoggedAndWantsNoDialog()
        {
            var wantsDialog = CrashLogger.HandleThreadException(Wrapped(), interactive: false);

            var contents = LogContents();
            Assert.Multiple(() =>
            {
                Assert.That(wantsDialog, Is.False,
                            "A dialog on a non-interactive run never gets answered, and WinForms' own is worse than none.");
                Assert.That(contents, Does.Contain("UI thread"));
                Assert.That(contents, Does.Contain("InvalidOperationException"), "No type.");
                Assert.That(contents, Does.Contain("inner boom"), "No inner exception.");
                Assert.That(contents, Does.Contain("   at "), "No stack frames.");
            });
        }

        /// <summary>
        /// An interactive run still gets told it should see the user off.
        /// </summary>
        [Test]
        public void AUiThreadExceptionOnAnInteractiveRunStillWantsTheDialog()
        {
            var wantsDialog = CrashLogger.HandleThreadException(Wrapped(), interactive: true);

            Assert.Multiple(() =>
            {
                Assert.That(wantsDialog, Is.True);
                Assert.That(LogContents(), Does.Contain("UI thread"));
            });
        }

        /// <summary>
        /// The count is what turns a UI-thread exception into a self-test failure, so a run that
        /// raised one cannot report RESULT: PASS.
        /// </summary>
        [Test]
        public void UiThreadExceptionsAreCounted()
        {
            var before = CrashLogger.UiThreadExceptionCount;

            CrashLogger.HandleThreadException(Wrapped(), interactive: false);
            CrashLogger.HandleThreadException(Wrapped(), interactive: false);

            Assert.That(CrashLogger.UiThreadExceptionCount, Is.EqualTo(before + 2));
        }

        /// <summary>
        /// An interactive run's exception is not counted against the self-test: nothing is running
        /// a self-test on that path, and the process is about to go down anyway.
        /// </summary>
        [Test]
        public void AnInteractiveRunDoesNotCountTowardsTheSelfTest()
        {
            var before = CrashLogger.UiThreadExceptionCount;

            CrashLogger.HandleThreadException(Wrapped(), interactive: true);

            Assert.That(CrashLogger.UiThreadExceptionCount, Is.EqualTo(before));
        }


        /// <summary>
        /// The handler goes on even when there is nobody to show a dialog to.
        /// </summary>
        /// <remarks>
        /// This is the regression guard for the shutdown crash: with
        /// <see cref="System.Windows.Forms.Application.ThreadException"/> unsubscribed, WinForms
        /// shows its own dialog instead, and during teardown that dialog fails to create its window
        /// handle and takes the process down at 0xC000041D with nothing in the log about the
        /// exception that started it.
        /// </remarks>
        [Test]
        public void TheThreadExceptionHandlerIsInstalledEvenWhenNotInteractive()
        {
            CrashLogger.Install(interactive: false);

            Assert.That(CrashLogger.ThreadExceptionInstalled, Is.True,
                        "Leaving it off does not avoid a dialog - it hands the dialog to WinForms.");
        }

        private static Exception Wrapped()
        {
            try
            {
                try
                {
                    throw new FileNotFoundException("inner boom");
                }
                catch (Exception inner)
                {
                    throw new InvalidOperationException("outer boom", inner);
                }
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        private string LogContents()
        {
            if (!File.Exists(_logFile)) return "";

            // The sink holds the file open, so read it sharing write access.
            using (var stream = new FileStream(_logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }
    }
}
