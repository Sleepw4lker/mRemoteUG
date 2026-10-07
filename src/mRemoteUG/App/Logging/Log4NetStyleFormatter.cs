using System;
using System.Globalization;
using System.IO;
using Serilog.Events;
using Serilog.Formatting;

namespace mRemoteUG.App.Logging
{
    /// <summary>
    /// Writes a log line in the exact shape log4net's "%date [%thread] %-6level- %message%newline"
    /// produced.
    /// </summary>
    /// <remarks>
    /// The format is a contract, not a preference. ADR-0013 makes reading a log off another machine
    /// the fork's verification mechanism and says a tester needs no configuration because the
    /// pattern already carries the date and the thread; README tells anyone reporting a problem to
    /// attach that file. Keeping the output byte-identical means logs written before and after the
    /// framework swap concatenate, and nothing that reads a log has to learn a second shape.
    /// <para>
    /// Hence the level names are log4net's - INFO and WARN, not Serilog's Information and Warning -
    /// and the width-6 left alignment of %-6level is reproduced by hand.
    /// </para>
    /// </remarks>
    internal sealed class Log4NetStyleFormatter : ITextFormatter
    {
        // log4net's bare %date is ISO8601: local time, comma before the milliseconds.
        private const string TimestampFormat = "yyyy-MM-dd HH:mm:ss,fff";

        private const int LevelWidth = 6;

        public void Format(LogEvent logEvent, TextWriter output)
        {
            if (logEvent == null || output == null)
                return;

            output.Write(logEvent.Timestamp.LocalDateTime.ToString(TimestampFormat, CultureInfo.InvariantCulture));
            output.Write(" [");
            output.Write(Thread(logEvent));
            output.Write("] ");
            output.Write(LevelName(logEvent.Level).PadRight(LevelWidth));
            output.Write("- ");
            output.Write(Message(logEvent));
            output.Write(Environment.NewLine);

            // After the line, not inside it: log4net appended the exception below the rendered
            // message too (PatternLayout.IgnoresException), so the one-line-per-event shape that
            // ADR-0013 pins is unchanged. ToString() is what carries the type, the stack and
            // every inner exception - the three things the old flattening threw away.
            if (logEvent.Exception != null)
            {
                output.Write(logEvent.Exception.ToString());
                output.Write(Environment.NewLine);
            }
        }

        private static string LevelName(LogEventLevel level)
        {
            switch (level)
            {
                case LogEventLevel.Verbose:
                case LogEventLevel.Debug:
                    return "DEBUG";
                case LogEventLevel.Information:
                    return "INFO";
                case LogEventLevel.Warning:
                    return "WARN";
                case LogEventLevel.Error:
                    return "ERROR";
                case LogEventLevel.Fatal:
                    return "FATAL";
                default:
                    return level.ToString().ToUpperInvariant();
            }
        }

        /// <remarks>
        /// Read straight off the property rather than through RenderMessage, so that braces in the
        /// text are never treated as a message template - see <see cref="SerilogLogSink"/>. The
        /// fallback covers an event that did not come through that sink; nothing writes one today,
        /// but losing a line entirely would be the worse failure.
        /// </remarks>
        private static string Message(LogEvent logEvent)
        {
            if (logEvent.Properties.TryGetValue(SerilogLogSink.MessageProperty, out var value) &&
                value is ScalarValue scalar && scalar.Value is string text)
                return text;

            return logEvent.RenderMessage(CultureInfo.InvariantCulture);
        }

        private static string Thread(LogEvent logEvent)
        {
            if (logEvent.Properties.TryGetValue(ThreadIdEnricher.ThreadProperty, out var value) &&
                value is ScalarValue scalar && scalar.Value is string text)
                return text;

            return Environment.CurrentManagedThreadId.ToString(CultureInfo.InvariantCulture);
        }
    }
}
