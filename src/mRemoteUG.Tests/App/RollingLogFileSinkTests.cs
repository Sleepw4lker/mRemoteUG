using mRemoteUG.App.Logging;

using NUnit.Framework;

using Serilog.Events;
using Serilog.Formatting;
using Serilog.Parsing;

namespace mRemoteUG.Tests.App;

/// <summary>
/// Guards the file behaviour the application depends on but never states out loud: which file
/// is the live one, how many backups survive, and that a log which cannot be written does not
/// take the process with it.
/// </summary>
/// <remarks>
/// The size limit is a constructor parameter precisely so these tests can drive a roll with a
/// few hundred bytes instead of the 10MB the application uses.
/// </remarks>
[TestFixture]
public class RollingLogFileSinkTests
{
    // Rebuilt per test in Setup, so no test sees another's backups.
    private string _tempDirectory = null!;
    private string _logFile = null!;

    [SetUp]
    public void Setup()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "mRemoteUG.Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
        _logFile = Path.Combine(_tempDirectory, "mRemoteUG.log");
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(_tempDirectory, true); } catch (IOException) { }
    }

    /// <summary>
    /// The whole reason this sink is hand-written rather than Serilog.Sinks.File. The file the
    /// user is told to attach has to be the file being written: Serilog's own rolling moves the
    /// live content off to mRemoteUG_001.log and leaves the oldest content sitting under the
    /// base name, which would quietly invalidate LogPath, the Open File button, the marker
    /// SelfTest reads back, and the instruction in README.
    /// </summary>
    [Test]
    public void TheLiveFileKeepsItsNameWhenItRolls()
    {
        using (RollingLogFileSink sink = NewSink(maximumFileSizeBytes: 200, maximumBackups: 3))
        {
            sink.Emit(Event("oldest line"));
            EmitUntilRolled(sink);
            sink.Emit(Event("newest line"));
        }

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(_logFile), Is.True, "The live log file is gone after a roll.");
            Assert.That(Contents(_logFile), Does.Contain("newest line"),
                        "The newest line is not in the file the user is told to attach.");
            Assert.That(File.Exists(_logFile + ".1"), Is.True, "The roll made no backup.");
            Assert.That(Contents(_logFile + ".1"), Does.Contain("oldest line"),
                        "The rolled-off content is not in .1.");
        });
    }

    [Test]
    public void BackupsDoNotOutnumberTheLimit()
    {
        const int backups = 2;

        using (RollingLogFileSink sink = NewSink(maximumFileSizeBytes: 120, maximumBackups: backups))
        {
            for (int i = 0; i < 200; i++)
            {
                sink.Emit(Event("line " + i));
            }
        }

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(_logFile + "." + backups), Is.True,
                        "The backups never filled up, so the limit was not exercised.");
            Assert.That(File.Exists(_logFile + "." + (backups + 1)), Is.False,
                        "A backup beyond the limit was kept.");
        });
    }

    /// <summary>
    /// --selftest writes a marker and reads it back within the same run, and five fixtures do
    /// the same. That only works because the sink shares the file for reading and flushes every
    /// event rather than buffering.
    /// </summary>
    [Test]
    public void TheLogCanBeReadWhileTheSinkHoldsItOpen()
    {
        using (RollingLogFileSink sink = NewSink())
        {
            sink.Emit(Event("held open"));

            Assert.That(Contents(_logFile), Does.Contain("held open"));
        }
    }

    /// <summary>
    /// TextLogMessageWriter has no try/catch of its own and log4net used to swallow appender
    /// failures internally, so a log path that cannot be opened has to cost lines rather than
    /// the process.
    /// </summary>
    [Test]
    public void APathThatCannotBeOpenedCostsLinesRatherThanThrowing()
    {
        // A directory standing where the file should be: it can never be opened for writing.
        string blocked = Path.Combine(_tempDirectory, "blocked.log");
        Directory.CreateDirectory(blocked);

        Assert.DoesNotThrow(() =>
        {
            using (var sink = new RollingLogFileSink(blocked, 1024, 3, new Log4NetStyleFormatter()))
            {
                sink.Emit(Event("swallowed"));
            }
        });
    }

    /// <summary>
    /// log4net wrote "%-6level- ", so the level is its own spelling - INFO, not Information -
    /// left-aligned in six columns with the hyphen immediately after. Anyone reading a log, or
    /// grepping one, sees the same columns either side of the framework change (ADR-0013).
    /// </summary>
    [Test]
    public void EachLevelIsSpelledAndPaddedTheWayLog4NetWroteIt()
    {
        using (RollingLogFileSink sink = NewSink())
        {
            sink.Emit(Event("d", LogEventLevel.Debug));
            sink.Emit(Event("i", LogEventLevel.Information));
            sink.Emit(Event("w", LogEventLevel.Warning));
            sink.Emit(Event("e", LogEventLevel.Error));
        }

        string contents = Contents(_logFile);
        Assert.Multiple(() =>
        {
            Assert.That(contents, Does.Contain("DEBUG - d"));
            Assert.That(contents, Does.Contain("INFO  - i"));
            Assert.That(contents, Does.Contain("WARN  - w"));
            Assert.That(contents, Does.Contain("ERROR - e"));
        });
    }

    /// <summary>
    /// Logger.SetLogPath disposes the Serilog pipeline and then the sink, and Serilog disposes
    /// the sinks it owns as well. One of those two is always a second call.
    /// </summary>
    [Test]
    public void DisposingTwiceIsHarmless()
    {
        RollingLogFileSink sink = NewSink();
        sink.Dispose();

        Assert.DoesNotThrow(() => sink.Dispose());
    }

    /// <summary>
    /// Disposing closes the handle, so whatever comes next - the next log path, a backup move,
    /// the user deleting the file - finds it free.
    /// </summary>
    [Test]
    public void DisposingReleasesTheFile()
    {
        RollingLogFileSink sink = NewSink();
        sink.Emit(Event("before dispose"));

        sink.Dispose();

        Assert.DoesNotThrow(() => OpenExclusively(_logFile).Dispose(), "The sink still holds the file.");
    }

    /// <summary>
    /// The limit is the most a file holds: one that has reached it exactly rolls.
    /// </summary>
    [Test]
    public void AFileThatReachesTheLimitExactlyRolls()
    {
        using (var sink = new RollingLogFileSink(_logFile, 10, 3, new FixedWidthFormatter(10)))
        {
            sink.Emit(Event("ten bytes"));
        }

        Assert.That(File.Exists(_logFile + ".1"), Is.True, "A file at the limit did not roll.");
    }

    /// <summary>
    /// A custom log path under a folder nobody has created yet still gets its log.
    /// </summary>
    [Test]
    public void AMissingLogDirectoryIsCreated()
    {
        string logFile = Path.Combine(_tempDirectory, "not", "yet", "mRemoteUG.log");

        using (var sink = new RollingLogFileSink(logFile, 1024, 3, new Log4NetStyleFormatter()))
        {
            sink.Emit(Event("first line"));
        }

        Assert.That(File.Exists(logFile), Is.True, "No log was written under the missing directory.");
        Assert.That(Contents(logFile), Does.Contain("first line"));
    }

    private static FileStream OpenExclusively(string path)
    {
        return new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    /// <summary>Writes exactly <c>width</c> ASCII bytes per event, whatever the event says.</summary>
    private sealed class FixedWidthFormatter : ITextFormatter
    {
        private readonly int _width;

        public FixedWidthFormatter(int width)
        {
            _width = width;
        }

        public void Format(LogEvent logEvent, TextWriter output)
        {
            output.Write(new string('x', _width));
        }
    }

    private RollingLogFileSink NewSink(long maximumFileSizeBytes = 1024 * 1024, int maximumBackups = 5)
    {
        return new RollingLogFileSink(_logFile, maximumFileSizeBytes, maximumBackups,
                                      new Log4NetStyleFormatter());
    }

    private void EmitUntilRolled(RollingLogFileSink sink)
    {
        for (int i = 0; i < 100 && !File.Exists(_logFile + ".1"); i++)
        {
            sink.Emit(Event("filler " + i));
        }
    }

    private static LogEvent Event(string text, LogEventLevel level = LogEventLevel.Information)
    {
        var property = new LogEventProperty(SerilogLogSink.MessageProperty, new ScalarValue(text));
        MessageTemplate template = new MessageTemplateParser().Parse("{" + SerilogLogSink.MessageProperty + "}");

        return new LogEvent(DateTimeOffset.Now, level, null, template, new[] { property });
    }

    private static string Contents(string path)
    {
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (var reader = new StreamReader(stream))
        {
            return reader.ReadToEnd();
        }
    }
}
