using System;
using System.Globalization;
using System.Threading;
using Serilog.Core;
using Serilog.Events;

namespace mRemoteUG.App.Logging
{
    /// <summary>
    /// Records which thread logged a line, reproducing log4net's %thread.
    /// </summary>
    /// <remarks>
    /// An enricher rather than something the formatter works out for itself: enrichers run on the
    /// calling thread while the event is being built, so the value is the thread that logged.
    /// A formatter runs at the sink, which is only the same thread by coincidence of the sink
    /// being synchronous. ADR-0013 makes the thread column a documented guarantee - it is how a
    /// teardown reported from the watchdog's timer thread is told apart from the UI thread's own
    /// lines - so it is worth not leaving to coincidence.
    /// </remarks>
    internal sealed class ThreadIdEnricher : ILogEventEnricher
    {
        internal const string ThreadProperty = "ThreadId";

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            // log4net prints the thread's name when it has one and its managed id otherwise.
            var name = Thread.CurrentThread.Name;
            var thread = string.IsNullOrEmpty(name)
                ? Environment.CurrentManagedThreadId.ToString(CultureInfo.InvariantCulture)
                : name;

            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(ThreadProperty, thread));
        }
    }
}
