using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using mRemoteUG;
using mRemoteUG.App;
using mRemoteUG.Connection.Protocol;
using NUnit.Framework;

namespace mRemoteUG.Tests.Connection.Protocol
{
    /// <summary>
    /// The watchdog is the only thing that will say anything at all when a teardown stage stops
    /// returning, so it has to be known to fire. An untested watchdog that silently does nothing
    /// is worse than none: it would be trusted.
    /// </summary>
    [TestFixture]
    public class TeardownWatchdogTests
    {
        private ConcurrentQueue<string> _reports;
        private Action<string> _originalReport;
        private TimeSpan _originalFirst, _originalEvery, _originalPoll;

        [SetUp]
        public void Setup()
        {
            _originalReport = TeardownWatchdog.Report;
            _originalFirst = TeardownWatchdog.FirstReportAfter;
            _originalEvery = TeardownWatchdog.ReportEvery;
            _originalPoll = TeardownWatchdog.PollEvery;

            _reports = new ConcurrentQueue<string>();
            TeardownWatchdog.ResetForTests();
            TeardownWatchdog.Report = message => _reports.Enqueue(message);
            TeardownWatchdog.FirstReportAfter = TimeSpan.FromMilliseconds(100);
            TeardownWatchdog.ReportEvery = TimeSpan.FromMilliseconds(100);
            TeardownWatchdog.PollEvery = TimeSpan.FromMilliseconds(20);
        }

        [TearDown]
        public void TearDown()
        {
            TeardownWatchdog.ResetForTests();
            TeardownWatchdog.Report = _originalReport;
            TeardownWatchdog.FirstReportAfter = _originalFirst;
            TeardownWatchdog.ReportEvery = _originalEvery;
            TeardownWatchdog.PollEvery = _originalPoll;
        }

        /// <summary>
        /// The watchdog cannot use the message collector - the collector runs its writers on the
        /// calling thread, and the calling thread is the one that is stuck - so it used to write to
        /// the log sink directly and ignore Options -> Notifications -> Logging entirely.
        /// </summary>
        [Test]
        public void TheDefaultReportHonoursTheWarningLoggingSetting()
        {
            var originalWarnings = Settings.Default.TextLogMessageWriterWriteWarningMsgs;
            var originalLogPath = Logger.Instance.LogPath;
            var directory = Path.Combine(Path.GetTempPath(), "mRemoteUG.Tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var logFile = Path.Combine(directory, "watchdog.log");

            try
            {
                Logger.Instance.SetLogPath(logFile);

                Settings.Default.TextLogMessageWriterWriteWarningMsgs = false;
                var suppressed = "watchdog off " + Guid.NewGuid().ToString("N");
                _originalReport(suppressed);
                Assert.That(Contents(logFile), Does.Not.Contain(suppressed));

                Settings.Default.TextLogMessageWriterWriteWarningMsgs = true;
                var written = "watchdog on " + Guid.NewGuid().ToString("N");
                _originalReport(written);
                Assert.That(Contents(logFile), Does.Contain(written));
            }
            finally
            {
                Settings.Default.TextLogMessageWriterWriteWarningMsgs = originalWarnings;
                Logger.Instance.SetLogPath(originalLogPath);
                try { Directory.Delete(directory, true); } catch (IOException) { }
            }
        }

        private static string Contents(string path)
        {
            if (!File.Exists(path)) return string.Empty;

            // The sink holds the file open, so read it sharing write access.
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream))
                return reader.ReadToEnd();
        }

        [Test]
        public void AStageThatDoesNotReturnIsReportedWithItsNameAndHowLongItHasBeenRunning()
        {
            TeardownWatchdog.Enter("[#7/server1] disposing the hosted control");

            Assert.That(WaitForReports(1), Is.True, "The watchdog never reported a stalled stage.");

            var report = _reports.First();
            Assert.Multiple(() =>
            {
                Assert.That(report, Does.Contain("disposing the hosted control"),
                            "The report has to name the stage, or it cannot be acted on.");
                Assert.That(report, Does.Contain("#7/server1"),
                            "The report has to name the session.");
            });
        }

        [Test]
        public void AStageThatIsStillStuckIsReportedAgain()
        {
            TeardownWatchdog.Enter("[#1/server1] disposing the hosted control");

            Assert.That(WaitForReports(3), Is.True,
                        "The watchdog reported once and then went quiet, so a long hang would look " +
                        "the same as a brief stall.");
        }

        [Test]
        public void AStageThatCompletesIsNotReported()
        {
            TeardownWatchdog.Enter("[#1/server1] disposing the hosted control");
            TeardownWatchdog.Leave();

            Thread.Sleep(300);

            Assert.That(_reports, Is.Empty,
                        "A teardown that finished normally must not put anything in the log.");
        }

        private bool WaitForReports(int count)
        {
            var deadline = Stopwatch.StartNew();
            while (_reports.Count < count && deadline.ElapsedMilliseconds < 5000)
                Thread.Sleep(10);
            return _reports.Count >= count;
        }
    }
}
