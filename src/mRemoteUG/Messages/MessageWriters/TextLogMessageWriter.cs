#nullable enable
using System;
using mRemoteUG.App;

namespace mRemoteUG.Messages.MessageWriters
{
    public class TextLogMessageWriter : IMessageWriter
    {
        private readonly Logger _logger;

        public TextLogMessageWriter(Logger logger)
        {
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));

            _logger = logger;
        }

        /// <remarks>
        /// The message carries its own date and its own exception, and both are passed on rather
        /// than left to the sink. The date matters because the backlog the collector replays at
        /// startup is written long after it happened; the exception because the log is the only
        /// writer that wants the whole of it.
        /// </remarks>
        public void Write(IMessage message)
        {
            switch (message.Class)
            {
                case MessageClass.InformationMsg:
                    _logger.Log.Info(message.Text, message.Date, message.Exception);
                    break;
                case MessageClass.DebugMsg:
                    _logger.Log.Debug(message.Text, message.Date, message.Exception);
                    break;
                case MessageClass.WarningMsg:
                    _logger.Log.Warn(message.Text, message.Date, message.Exception);
                    break;
                case MessageClass.ErrorMsg:
                    _logger.Log.Error(message.Text, message.Date, message.Exception);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}