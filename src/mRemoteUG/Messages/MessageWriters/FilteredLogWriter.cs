#nullable enable
using System;
using mRemoteUG.App;
using mRemoteUG.Messages.WriterDecorators;

namespace mRemoteUG.Messages.MessageWriters
{
    /// <summary>
    /// The text log with the Options -> Notifications -> Logging filter in front of it: the one
    /// place in the process that writes to the log file.
    /// </summary>
    /// <remarks>
    /// Two things route through here. The first is the message collector's text-log writer, which
    /// is this same instance - having one means there is no second place where the log filter could
    /// be chosen differently. The second is code that cannot use
    /// <see cref="MessageCollector"/> at all because the collector runs its writers on the calling
    /// thread; <see cref="Connection.Protocol.TeardownWatchdog"/> is reporting a stuck UI thread and
    /// so must stay off it. Before this existed, such code called
    /// <c>Logger.Instance.Log</c> directly and ignored the user's settings entirely.
    /// <para>
    /// The filtering options read <see cref="Settings.Default"/> on every message rather than
    /// caching, so a setting changed in the options dialog applies to the very next message without
    /// anything being rebuilt - including while a teardown is hung.
    /// </para>
    /// </remarks>
    internal static class FilteredLogWriter
    {
        internal static readonly IMessageWriter Instance =
            new MessageTypeFilterDecorator(new LogMessageTypeFilteringOptions(),
                                           new TextLogMessageWriter(Logger.Instance));

        /// <summary>
        /// Writes to the log only. Callers of this overload have no notification panel or popup to
        /// reach - they are already outside the collector - so the message is marked
        /// <see cref="IMessage.OnlyLog"/> to say so.
        /// </summary>
        internal static void Write(MessageClass messageClass, string text, Exception? exception = null)
        {
            Instance.Write(new Message(messageClass, text, onlyLog: true, exception));
        }
    }
}
