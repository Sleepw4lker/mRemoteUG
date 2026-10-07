using System;

namespace mRemoteUG.App.Logging
{
    /// <summary>
    /// The four levels the application logs at, and the whole of what it wants from a logging
    /// framework.
    /// </summary>
    /// <remarks>
    /// Serilog sits behind this interface and nowhere else, so the next framework change costs one
    /// file instead of a sweep (ADR-0019). The method names are log4net's spelling rather than
    /// Serilog's on purpose: keeping Info/Warn meant the two sanctioned call sites -
    /// TextLogMessageWriter and SelfTest - did not have to change when log4net went, which kept the
    /// swap off the code paths that ADR-0017 guards.
    /// <para>
    /// The timestamp is a parameter rather than something the sink reads off the clock: messages
    /// are not always written when they happen. MessageCollector.SubscribeAndReplay hands the
    /// startup backlog to the writers once they exist, and stamping those at write time bunched
    /// the whole of startup onto one instant.
    /// </para>
    /// </remarks>
    internal interface ILogSink
    {
        void Debug(string message, DateTime timestamp, Exception? exception = null);

        void Info(string message, DateTime timestamp, Exception? exception = null);

        void Warn(string message, DateTime timestamp, Exception? exception = null);

        void Error(string message, DateTime timestamp, Exception? exception = null);
    }
}
