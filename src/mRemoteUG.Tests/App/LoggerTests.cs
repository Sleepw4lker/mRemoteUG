using System.Text.RegularExpressions;

using mRemoteUG.App;

using NUnit.Framework;

namespace mRemoteUG.Tests.App;

/// <summary>
/// Guards the logging setup. The pipeline is built in code rather than from a configuration
/// file, and a mistake there produces a logger that accepts every line and writes none of them.
/// "Silently" is the problem: without these tests the first evidence would be an empty log file
/// on a machine we cannot debug.
/// </summary>
[TestFixture]
public class LoggerTests
{
    // Rebuilt per test in Setup, so no test sees another's files.
    private string _tempDirectory = null!;

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
        string originalPath = Logger.Instance.LogPath;
        string logFile = Path.Combine(_tempDirectory, "format.log");
        try
        {
            Logger.Instance.SetLogPath(logFile);
            string message = "format check " + Guid.NewGuid().ToString("N");

            Logger.Instance.Log.Info(message, DateTime.Now);

            string contents = Contents(logFile);
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
        string originalPath = Logger.Instance.LogPath;
        string logFile = Path.Combine(_tempDirectory, "braces.log");
        try
        {
            Logger.Instance.SetLogPath(logFile);
            string[] messages = new[]
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

            foreach (string message in messages)
            {
                Logger.Instance.Log.Info(message, DateTime.Now);
            }

            string contents = Contents(logFile);
            Assert.Multiple(() =>
            {
                foreach (string message in messages)
                {
                    Assert.That(contents, Does.Contain(message), "Braces were not logged verbatim.");
                }
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
        bool originalUseDefault = Settings.Default.LogToApplicationDirectory;
        string originalPath = Settings.Default.LogFilePath;
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
        bool originalUseDefault = Settings.Default.LogToApplicationDirectory;
        string originalPath = Settings.Default.LogFilePath;
        string custom = Path.Combine(_tempDirectory, "custom.log");
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
        bool originalUseDefault = Settings.Default.LogToApplicationDirectory;
        string originalPath = Settings.Default.LogFilePath;
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
        string originalPath = Logger.Instance.LogPath;
        string logFile = Path.Combine(_tempDirectory, "reported.log");
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
        string originalPath = Logger.Instance.LogPath;
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
        string originalPath = Logger.Instance.LogPath;
        string logFile = Path.Combine(_tempDirectory, "verify.log");
        try
        {
            Logger.Instance.SetLogPath(logFile);

            string message = "log round-trip " + Guid.NewGuid().ToString("N");
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
        string originalPath = Logger.Instance.LogPath;
        string logFile = Path.Combine(_tempDirectory, "thread.log");
        try
        {
            if (string.IsNullOrEmpty(Thread.CurrentThread.Name))
            {
                Thread.CurrentThread.Name = "logging-test-thread";
            }

            string threadName = Thread.CurrentThread.Name;

            Logger.Instance.SetLogPath(logFile);
            string message = "thread check " + Guid.NewGuid().ToString("N");

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
        string originalPath = Logger.Instance.LogPath;
        string logFile = Path.Combine(_tempDirectory, "when.log");
        try
        {
            Logger.Instance.SetLogPath(logFile);
            var when = new DateTime(2019, 3, 4, 5, 6, 7, 890, DateTimeKind.Local);
            string message = "back-dated " + Guid.NewGuid().ToString("N");

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
        string originalPath = Logger.Instance.LogPath;
        string logFile = Path.Combine(_tempDirectory, "exception.log");
        try
        {
            Logger.Instance.SetLogPath(logFile);
            string message = "operation failed " + Guid.NewGuid().ToString("N");

            Logger.Instance.Log.Error(message, DateTime.Now, Caught());

            string contents = Contents(logFile);
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
    /// Pointing the log somewhere else lets go of the old file, which the user can then move,
    /// delete or attach to a report.
    /// </summary>
    [Test]
    public void RetargetingTheLogReleasesTheOldFile()
    {
        string originalPath = Logger.Instance.LogPath;
        string oldFile = Path.Combine(_tempDirectory, "old.log");
        try
        {
            Logger.Instance.SetLogPath(oldFile);
            Logger.Instance.Log.Info("written to the old file", DateTime.Now);

            Logger.Instance.SetLogPath(Path.Combine(_tempDirectory, "new.log"));

            Assert.DoesNotThrow(
                () => new FileStream(oldFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None).Dispose(),
                "The old log file is still held open.");
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
