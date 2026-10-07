#nullable enable
using System;

namespace mRemoteUG.Messages
{
    public interface IMessage
    {
        MessageClass Class { get; set; }

        string Text { get; set; }

        /// <summary>
        /// The exception this message is about, if it is about one. Null for most messages.
        /// </summary>
        /// <remarks>
        /// Carried as the object rather than flattened into <see cref="Text"/>, so that each
        /// writer can decide how much of it to show. The log renders it in full; the pop-up and
        /// the notification list keep the short sentence in <see cref="Text"/>.
        /// </remarks>
        /// <summary>The exception this message reports, or null if it reports none.</summary>
        Exception? Exception { get; set; }

        DateTime Date { get; set; }

        bool OnlyLog { get; set; }
    }
}