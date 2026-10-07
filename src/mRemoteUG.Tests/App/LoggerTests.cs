using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using mRemoteUG;
using mRemoteUG.App;
using NUnit.Framework;

namespace mRemoteUG.Tests.App
{
    /// <summary>
    /// Guards the logging setup. The pipeline is built in code rather than from a configuration
    /// file, and a mistake there produces a logger that accepts every line and writes none of them.
    /// "Silently" is the problem: without these tests the first evidence would be an empty log file
    /// on a machine we cannot debug.
    /// </summary>
    [TestFixture]
    public class LoggerTests
    {
        private string _tempDirectory;

        [SetUp]
        public void Setup()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "mRemoteUG.Tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_tempDirectory, true); } catch (IOException) { }
        }

        [Test]
        public void LoggerExposesALogSink()
        {
            Assert.That(Logger.Instance.Log, Is.Not.Null);
        }

        /// <summary>
        /// The line format is a contract rather than a preference: ADR-0013 has a tester read this
        /// file off another machine with nothing configured, and README tells anyone reporting a
        /// problem to attach it. It came through the move off log4net byte for byte, and this is
        /// what says so - date, thread, level padded to six, then "- " and the message.
        /// </summary>
        [Test]
        public void ALoggedLineCarriesTheDateThreadAndLevel()
        {
            var originalPath = Logger.Instance.LogPath;
            var logFile = Path.Combine(_tempDirectory, "format.log");
            try
            {
                Logger.Instance.SetLogPath(logFile);
                var message = "format check " + Guid.NewGuid().ToString("N");

                Logger.Instance.Log.Info(message, DateTime.Now);

                var contents = Contents(logFile);
                var expected = new Regex(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2},\d{3} \[[^\]]+\] INFO  - " +
                                         Regex.Escape(message) + @"\r?$", RegexOptions.Multiline);
                Assert.That(expected.IsMatch(contents), Is.True,
                            "The log line did not match the documented pattern. The file contained: " + contents);
            }
            finally
            {
                Logger.Instance.SetLogPath(originalPath);
            }
        }

        /// <summary>
        /// Serilog reads its first argument as a message template. An unbound {token} survives that
        /// unchanged, which is why the obvious cases below look harmless, but {{ and }} are escapes
        /// and a name matching a property already on the event is substituted - so a message
        /// mentioning {ThreadId} would come out carrying the thread id. log4net had no such
        /// behaviour, so this guards a hazard the framework swap introduced rather than one the
        /// application ever had.
        /// </summary>
        [Test]
        public void BracesInAMessageAreLoggedVerbatim()
        {
            var originalPath = Logger.Instance.LogPath;
            var logFile = Path.Combine(_tempDirectory, "braces.log");
            try
            {
                Logger.Instance.SetLogPath(logFile);
                var messages = new[]
                {
                    "connection {6B29FC40-CA47-1067-B31D-00DD010662DA} opened",
                    "format-like {0} and {1:x} placeholders",
                    "an unmatched { brace",
                    // Doubled braces are a template escape: rendered as a template these collapse
                    // to one brace each.
                    "a registry value of {{Default}} was read",
                    // The killer: rendered as a template this binds to the enricher's property and
                    // the text is replaced by the actual thread id.
                    "the literal text {ThreadId} is not a placeholder"
                };

                foreach (var message in messages)
                    Logger.Instance.Log.Info(message, DateTime.Now);

                var contents = Contents(logFile);
                Assert.Multiple(() =>
                {
                    foreach (var message in messages)
                        Assert.That(contents, Does.Contain(message), "Braces were not logged verbatim.");
                });
            }
            finally
            {
                Logger.Instance.SetLogPath(originalPath);
            }
        }

        [Test]
        public void EffectiveLogPathIsTheDefaultWhenTheDefaultLocationIsInUse()
        {
            var originalUseDefault = Settings.Default.LogToApplicationDirectory;
            var originalPath = Settings.Default.LogFilePath;
            try
            {
                Settings.Default.LogToApplicationDirectory = true;
                Settings.Default.LogFilePath = Path.Combine(_tempDirectory, "ignored.log");

                Assert.That(Logger.EffectiveLogPath, Is.EqualTo(Logger.DefaultLogPath));
            }
            finally
            {
                Settings.Default.LogToApplicationDirectory = originalUseDefault;
                Settings.Default.LogFilePath = originalPath;
            }
        }

        [Test]
        public void EffectiveLogPathIsTheCustomPathWhenTheDefaultLocationIsNot()
        {
            var originalUseDefault = Settings.Default.LogToApplicationDirectory;
            var originalPath = Settings.Default.LogFilePath;
            var custom = Path.Combine(_tempDirectory, "custom.log");
            try
            {
                Settings.Default.LogToApplicationDirectory = false;
                Settings.Default.LogFilePath = custom;

                Assert.That(Logger.EffectiveLogPath, Is.EqualTo(custom));
            }
            finally
            {
                Settings.Default.LogToApplicationDirectory = originalUseDefault;
                Settings.Default.LogFilePath = originalPath;
            }
        }

        /// <summary>
        /// The options text box can be cleared, and an empty path would otherwise reach the sink.
        /// </summary>
        [Test]
        public void EffectiveLogPathFallsBackToTheDefaultWhenTheCustomPathIsEmpty()
        {
            var originalUseDefault = Settings.Default.LogToApplicationDirectory;
            var originalPath = Settings.Default.LogFilePath;
            try
            {
                Settings.Default.LogToApplicationDirectory = false;
                Settings.Default.LogFilePath = "   ";

                Assert.That(Logger.EffectiveLogPath, Is.EqualTo(Logger.DefaultLogPath));
            }
            finally
            {
                Settings.Default.LogToApplicationDirectory = originalUseDefault;
                Settings.Default.LogFilePath = originalPath;
            }
        }

        [Test]
        public void LogPathReportsWhereTheLogIsActuallyBeingWritten()
        {
            var originalPath = Logger.Instance.LogPath;
            var logFile = Path.Combine(_tempDirectory, "reported.log");
            try
            {
                Logger.Instance.SetLogPath(logFile);

                Assert.That(Logger.Instance.LogPath, Is.EqualTo(logFile));
            }
            finally
            {
                Logger.Instance.SetLogPath(originalPath);
            }
        }

        [Test]
        public void SettingAnEmptyLogPathFallsBackToTheDefault()
        {
            var originalPath = Logger.Instance.LogPath;
            try
            {
                Logger.Instance.SetLogPath("  ");

                Assert.That(Logger.Instance.LogPath, Is.EqualTo(Logger.DefaultLogPath));
            }
            finally
            {
                Logger.Instance.SetLogPath(originalPath);
            }
        }

        [Test]
        public void WritingALogMessageProducesAFileAtTheConfiguredPath()
        {
            var originalPath = Logger.Instance.LogPath;
            var logFile = Path.Combine(_tempDirectory, "verify.log");
            try
            {
                Logger.Instance.SetLogPath(logFile);

                var message = "log round-trip " + Guid.NewGuid().ToString("N");
                Logger.Instance.Log.Info(message, DateTime.Now);

                Assert.That(File.Exists(logFile), Is.True, $"No log file was created at {logFile}.");
                Assert.That(Contents(logFile), Does.Contain(message));
            }
            finally
            {
                Logger.Instance.SetLogPath(originalPath);
            }
        }

        /// <summary>
        /// The thread column has to name the thread that logged, not the one that wrote.
        /// </summary>
        /// <remarks>
        /// This is the test that says enrichers still run now that SerilogLogSink builds the event
        /// itself and calls Write(LogEvent) rather than Information(). A thread *name* is the
        /// discriminator on purpose: the formatter's fallback uses the managed thread id, which is
        /// a number, so a name in that column can only have come from ThreadIdEnricher having run.
        /// </remarks>
        [Test]
        public void TheThreadColumnNamesTheThreadThatLogged()
        {
            var originalPath = Logger.Instance.LogPath;
            var logFile = Path.Combine(_tempDirectory, "thread.log");
            try
            {
                if (string.IsNullOrEmpty(Thread.CurrentThread.Name))
                    Thread.CurrentThread.Name = "logging-test-thread";
                var threadName = Thread.CurrentThread.Name;

                Logger.Instance.SetLogPath(logFile);
                var message = "thread check " + Guid.NewGuid().ToString("N");

                Logger.Instance.Log.Info(message, DateTime.Now);

                Assert.That(Contents(logFile), Does.Contain($"[{threadName}] INFO  - {message}"));
            }
            finally
            {
                Logger.Instance.SetLogPath(originalPath);
            }
        }

        /// <summary>
        /// The line carries the time the event happened, not the time it reached the file.
        /// </summary>
        /// <remarks>
        /// Those are the same instant for a live message and minutes apart for anything the
        /// message collector replays out of its startup backlog - which is the whole of startup,
        /// and the part of the log ADR-0018 is read through.
        /// </remarks>
        [Test]
        public void ALoggedLineCarriesTheTimeTheEventHappened()
        {
            var originalPath = Logger.Instance.LogPath;
            var logFile = Path.Combine(_tempDirectory, "when.log");
            try
            {
                Logger.Instance.SetLogPath(logFile);
                var when = new DateTime(2019, 3, 4, 5, 6, 7, 890, DateTimeKind.Local);
                var message = "back-dated " + Guid.NewGuid().ToString("N");

                Logger.Instance.Log.Info(message, when);

                Assert.That(Contents(logFile), Does.Contain($"2019-03-04 05:06:07,890 ["));
            }
            finally
            {
                Logger.Instance.SetLogPath(originalPath);
            }
        }

        /// <summary>
        /// An exception reaches the log whole: type, message, stack, and every inner exception.
        /// </summary>
        /// <remarks>
        /// It goes on the lines *after* the message line, which is what log4net did and what keeps
        /// the one-line-per-event shape ADR-0013 pins intact.
        /// </remarks>
        [Test]
        public void AnExceptionIsLoggedWholeAndBelowTheMessageLine()
        {
            var originalPath = Logger.Instance.LogPath;
            var logFile = Path.Combine(_tempDirectory, "exception.log");
            try
            {
                Logger.Instance.SetLogPath(logFile);
                var message = "operation failed " + Guid.NewGuid().ToString("N");

                Logger.Instance.Log.Error(message, DateTime.Now, Caught());

                var contents = Contents(logFile);
                Assert.Multiple(() =>
                {
                    var firstLine = new Regex(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2},\d{3} \[[^\]]+\] ERROR - " +
                                              Regex.Escape(message) + @"\r?$", RegexOptions.Multiline);
                    Assert.That(firstLine.IsMatch(contents), Is.True,
                                "The message line changed shape. File contained: " + contents);
                    Assert.That(contents, Does.Contain("InvalidOperationException"), "No outer type.");
                    Assert.That(contents, Does.Contain("FileNotFoundException"), "No inner type.");
                    Assert.That(contents, Does.Contain("inner boom"), "No inner message.");
                    Assert.That(contents, Does.Contain("   at "), "No stack frames.");
                });
            }
            finally
            {
                Logger.Instance.SetLogPath(originalPath);
            }
        }

        /// <summary>
        /// A genuinely thrown, genuinely wrapped exception - so that StackTrace is populated, which
        /// it is not on one that was only constructed.
        /// </summary>
        internal static Exception Caught()
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

        /// <remarks>
        /// The sink holds the file open, so read it sharing write access.
        /// </remarks>
        private static string Contents(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }
    }
}
