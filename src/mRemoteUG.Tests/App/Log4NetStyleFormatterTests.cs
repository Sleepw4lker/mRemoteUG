using mRemoteUG.App.Logging;

using NUnit.Framework;

using Serilog.Events;
using Serilog.Parsing;

namespace mRemoteUG.Tests.App;

/// <summary>
/// The formatter on its own, for what the file round trip in LoggerTests cannot tell apart.
/// </summary>
[TestFixture]
public class Log4NetStyleFormatterTests
{
    /// <summary>
    /// The exception goes below the message line, and the next event has to start on a line of
    /// its own rather than run on from the last line of the stack trace.
    /// </summary>
    [Test]
    public void AnEventWithAnExceptionEndsWithANewLine()
    {
        var formatter = new Log4NetStyleFormatter();
        using var output = new StringWriter();

        formatter.Format(Event("failed", LoggerTests.Caught()), output);

        Assert.That(output.ToString(), Does.EndWith(Environment.NewLine));
    }

    [Test]
    public void NothingToWriteToIsIgnored()
    {
        var formatter = new Log4NetStyleFormatter();

        // Serilog declares both parameters non-null; this test and the next hold the guard
        // against a caller that ignores that.
        Assert.DoesNotThrow(() => formatter.Format(Event("nowhere", null), null!));
    }

    [Test]
    public void NoEventWritesNothing()
    {
        var formatter = new Log4NetStyleFormatter();
        using var output = new StringWriter();

        formatter.Format(null!, output);

        Assert.That(output.ToString(), Is.Empty);
    }

    private static LogEvent Event(string text, Exception? exception)
    {
        var property = new LogEventProperty(SerilogLogSink.MessageProperty, new ScalarValue(text));
        MessageTemplate template =
            new MessageTemplateParser().Parse("{" + SerilogLogSink.MessageProperty + "}");

        return new LogEvent(DateTimeOffset.Now, LogEventLevel.Error, exception, template, new[] { property });
    }
}
