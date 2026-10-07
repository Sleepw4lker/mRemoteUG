using System;
using Serilog;
using Serilog.Events;
using Serilog.Parsing;

namespace mRemoteUG.App.Logging
{
    /// <summary>
    /// Adapts Serilog to <see cref="ILogSink"/>.
    /// </summary>
    internal sealed class SerilogLogSink : ILogSink
    {
        /// <summary>
        /// The property the message text travels under, from here to
        /// <see cref="Log4NetStyleFormatter"/>.
        /// </summary>
        internal const string MessageProperty = "LogLine";

        /// <remarks>
        /// Constant, and never the caller's text. Serilog parses its first argument as a message
        /// template. Most stray braces survive that - an unbound {token} renders as itself - but
        /// two cases corrupt the line: {{ and }} are escapes and collapse to single braces, and a
        /// {Name} matching a property already on the event is substituted, so a message mentioning
        /// {ThreadId} would come out carrying the thread id instead of the words. log4net had no
        /// such behaviour, so this is a hazard the framework swap introduced rather than one the
        /// application had. Passing the text as a property instead means it is never parsed at all.
        /// </remarks>
        private static readonly MessageTemplate Template =
            new MessageTemplateParser().Parse("{" + MessageProperty + "}");

        private readonly ILogger _logger;

        internal SerilogLogSink(ILogger logger)
        {
            _logger = logger;
        }

        public void Debug(string message, DateTime timestamp, Exception? exception = null)
        {
            Write(LogEventLevel.Debug, message, timestamp, exception);
        }

        public void Info(string message, DateTime timestamp, Exception? exception = null)
        {
            Write(LogEventLevel.Information, message, timestamp, exception);
        }

        public void Warn(string message, DateTime timestamp, Exception? exception = null)
        {
            Write(LogEventLevel.Warning, message, timestamp, exception);
        }

        public void Error(string message, DateTime timestamp, Exception? exception = null)
        {
            Write(LogEventLevel.Error, message, timestamp, exception);
        }

        /// <remarks>
        /// The event is built here and handed to <see cref="ILogger.Write(LogEvent)"/> rather than
        /// going through Information()/Warning(), because those stamp the event with the time of
        /// the call. That is the wrong time for anything replayed out of the message collector's
        /// backlog, which is the whole of startup.
        /// <para>
        /// Enrichers still run on this path - Serilog applies them when it dispatches the event -
        /// so the thread column is filled in by <see cref="ThreadIdEnricher"/> on the calling
        /// thread, which is the thread that logged. LoggerTests pins that.
        /// </para>
        /// </remarks>
        private void Write(LogEventLevel level, string message, DateTime timestamp, Exception exception)
        {
            var property = new LogEventProperty(MessageProperty, new ScalarValue(message));
            var logEvent = new LogEvent(new DateTimeOffset(timestamp), level, exception, Template,
                                        new[] { property });

            _logger.Write(logEvent);
        }
    }
}
