using System;
using System.IO;
using System.Windows.Forms;
using mRemoteUG.App.Logging;
using Serilog;
// ReSharper disable ArrangeAccessorOwnerBody

namespace mRemoteUG.App
{
	public class Logger
    {
        private const long MaximumFileSizeBytes = 10 * 1024 * 1024;
        private const int MaxSizeRollBackups = 5;

        public static readonly Logger Instance = new Logger();

        private readonly object _gate = new object();

        // Fully qualified: Serilog.Core.Logger would otherwise bind to this class.
        private Serilog.Core.Logger _serilog;
        private RollingLogFileSink _sink;
        private ILogSink _log;

        /// <summary>
        /// The log sink, building the log file the first time it is asked for.
        /// </summary>
        /// <remarks>
        /// Reading this property creates the log file, so nothing may touch it speculatively -
        /// see <see cref="EnsureConfigured"/>. It is read from the teardown watchdog's timer
        /// thread as well as the UI thread, hence the lock.
        /// </remarks>
        internal ILogSink Log
        {
            get
            {
                lock (_gate)
                {
                    EnsureConfigured();
                    return _log;
                }
            }
        }

        /// <summary>
        /// Where the log is actually being written, whether or not anything has been written yet.
        /// </summary>
        public string LogPath { get; private set; }

        /// <summary>
        /// Whether the log file has been opened yet - which is to say, whether a log file
        /// exists. False until something is actually logged.
        /// </summary>
        internal bool IsConfigured
        {
            get { lock (_gate) { return _log != null; } }
        }

        public static string DefaultLogPath => BuildLogFilePath();

        /// <summary>
        /// Where the log belongs according to Options -> Notifications -> Logging.
        /// </summary>
        /// <remarks>
        /// The one place this decision is made. Startup and the options page both go through it,
        /// which is what stops the live log path from drifting away from the one the next start
        /// would choose. An empty custom path falls back to the default rather than failing: the
        /// options text box can be cleared.
        /// </remarks>
        public static string EffectiveLogPath =>
            Settings.Default.LogToApplicationDirectory || string.IsNullOrWhiteSpace(Settings.Default.LogFilePath)
                ? DefaultLogPath
                : Settings.Default.LogFilePath;

        private Logger()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (string.IsNullOrEmpty(Settings.Default.LogFilePath))
                Settings.Default.LogFilePath = BuildLogFilePath();

            LogPath = EffectiveLogPath;
        }

        /// <summary>
        /// Builds the logging pipeline in code rather than from a configuration file.
        /// </summary>
        /// <remarks>
        /// There is no configuration file to get wrong, and nothing to deploy alongside the
        /// executable: the log is often the only evidence available from a machine the application
        /// is merely deployed to, so it has to work without anyone having configured it. That is
        /// also what app.config used to be for, and it was deleted along with the last of the
        /// log4net configuration (ADR-0015).
        /// <para>
        /// This runs on first use rather than at construction because the sink opens the file.
        /// Building it eagerly would create -- and hold open -- a log for a user who has turned
        /// every message type off under Options -> Notifications -> Logging, and would create one
        /// at the default path before <see cref="LogPath"/> had been consulted. Deferring it means
        /// the filter in front of TextLogMessageWriter decides whether a log file exists at all.
        /// </para>
        /// <para>
        /// The minimum level is Debug because which levels reach the file is not Serilog's decision
        /// to make -- MessageTypeFilterDecorator has already made it, from the user's settings,
        /// before anything arrives here (ADR-0017). Serilog is a file writer, not a filter.
        /// </para>
        /// </remarks>
        private void EnsureConfigured()
        {
            if (_log != null) return;

            _sink = new RollingLogFileSink(LogPath, MaximumFileSizeBytes, MaxSizeRollBackups,
                                           new Log4NetStyleFormatter());

            _serilog = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.With(new ThreadIdEnricher())
                .WriteTo.Sink(_sink)
                .CreateLogger();

            _log = new SerilogLogSink(_serilog);
        }

        /// <summary>
        /// Points the log at <paramref name="path"/> - immediately if the log is already open,
        /// and otherwise when it is first needed.
        /// </summary>
        /// <remarks>
        /// Deliberately does not open the log. The options page calls this on every OK, and
        /// opening here would create a log file for a user who had just turned every message type
        /// off.
        /// <para>
        /// A Serilog pipeline is immutable once built, so retargeting means disposing this one and
        /// building the next. That is the tidier half of the bargain: disposing closes the handle
        /// on the old file, which reassigning the appender path in place never clearly did.
        /// </para>
        /// </remarks>
        public void SetLogPath(string path)
        {
            lock (_gate)
            {
                LogPath = string.IsNullOrWhiteSpace(path) ? DefaultLogPath : path;
                if (_log == null) return;

                _serilog.Dispose();
                _sink.Dispose();
                _serilog = null;
                _sink = null;
                _log = null;

                EnsureConfigured();
            }
        }

        private static string BuildLogFilePath()
        {
            var logFilePath = GetLogDirectory();
            var logFileName = Path.ChangeExtension(Application.ProductName, ".log");
            if (logFileName == null) return "mRemoteUG.log";
            var logFile = Path.Combine(logFilePath, logFileName);
            return logFile;
        }

        private static string GetLogDirectory()
        {
            return Info.SettingsFileInfo.LogsPath;
        }

    }
}
