using System;
using System.Globalization;
using System.IO;
using System.Text;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;

namespace mRemoteUG.App.Logging
{
    /// <summary>
    /// Writes log events to a file, rolling it by size and keeping a fixed number of backups.
    /// </summary>
    /// <remarks>
    /// Hand-written rather than Serilog.Sinks.File because the two disagree about which file is the
    /// current one. log4net's StaticLogFileName layout - which this reproduces - keeps
    /// mRemoteUG.log as the live file and trails the backups behind it as .1 ... .5, oldest
    /// highest. Serilog.Sinks.File does the reverse: the live file becomes mRemoteUG_001.log and
    /// the base name ends up holding the oldest content. That would quietly invalidate
    /// <see cref="Logger.LogPath"/>, the Open File button on the options page, the marker SelfTest
    /// reads back, and README's instruction to attach %LOCALAPPDATA%\mRemoteUG\mRemoteUG.log when
    /// reporting a problem. The file the user is told to send has to be the file being written.
    /// <para>
    /// Writing the sink here also keeps the runtime dependency set at one package (ADR-0015):
    /// ILogEventSink and ITextFormatter are both in Serilog itself.
    /// </para>
    /// </remarks>
    internal sealed class RollingLogFileSink : ILogEventSink, IDisposable
    {
        // No BOM, and UTF-8 rather than log4net's system ANSI codepage, so that a connection name
        // with non-ASCII characters in it survives into the log.
        private static readonly Encoding LogEncoding = new UTF8Encoding(false);

        private readonly object _gate = new object();
        private readonly string _path;
        private readonly long _maximumFileSizeBytes;
        private readonly int _maximumBackups;
        private readonly ITextFormatter _formatter;

        private FileStream _stream;
        private StreamWriter _writer;
        private bool _disposed;

        /// <remarks>
        /// Opens the file. Constructing this is therefore the moment a log file comes into
        /// existence, which is why <see cref="Logger"/> defers constructing it until something is
        /// actually logged.
        /// </remarks>
        internal RollingLogFileSink(string path, long maximumFileSizeBytes, int maximumBackups,
                                    ITextFormatter formatter)
        {
            _path = path;
            _maximumFileSizeBytes = maximumFileSizeBytes;
            _maximumBackups = maximumBackups;
            _formatter = formatter;

            Open();
        }

        public void Emit(LogEvent logEvent)
        {
            if (logEvent == null)
                return;

            lock (_gate)
            {
                if (_disposed || _writer == null)
                    return;

                try
                {
                    _formatter.Format(logEvent, _writer);

                    // Flushed on every event, not buffered: --selftest writes a marker and then
                    // reads the file back within the same run, and five test fixtures do the same.
                    _writer.Flush();

                    RollIfNeeded();
                }
                catch (Exception)
                {
                    // Logging must not be the thing that brings the application down. log4net
                    // swallowed appender failures internally and TextLogMessageWriter has no
                    // try/catch of its own, so a log path that has gone read-only or been locked
                    // by something else has to cost lines, not the process.
                }
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed)
                    return;

                _disposed = true;
                Close();
            }
        }

        private void RollIfNeeded()
        {
            if (_stream == null || _stream.Length < _maximumFileSizeBytes)
                return;

            Close();
            ShiftBackups();
            Open();
        }

        /// <remarks>
        /// Oldest first, so nothing is overwritten while it is still wanted: drop the last backup,
        /// walk the rest down one, then the live file becomes .1.
        /// </remarks>
        private void ShiftBackups()
        {
            Delete(BackupPath(_maximumBackups));

            for (var index = _maximumBackups - 1; index >= 1; index--)
                Move(BackupPath(index), BackupPath(index + 1));

            Move(_path, BackupPath(1));
        }

        private string BackupPath(int index)
        {
            return _path + "." + index.ToString(CultureInfo.InvariantCulture);
        }

        private void Open()
        {
            try
            {
                var directory = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                // FileShare.ReadWrite: the file stays open for writing for the life of the
                // process, and --selftest and the test fixtures read it while it is held.
                _stream = new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                _writer = new StreamWriter(_stream, LogEncoding);
            }
            catch (Exception)
            {
                // Same reasoning as Emit. A log that cannot be opened is a log that stays empty,
                // not a start-up crash.
                _stream = null;
                _writer = null;
            }
        }

        private void Close()
        {
            try
            {
                _writer?.Flush();
                _writer?.Dispose();
            }
            catch (Exception)
            {
            }

            _writer = null;
            _stream = null;
        }

        private static void Delete(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception)
            {
            }
        }

        private static void Move(string from, string to)
        {
            try
            {
                if (!File.Exists(from))
                    return;

                if (File.Exists(to))
                    File.Delete(to);

                File.Move(from, to);
            }
            catch (Exception)
            {
            }
        }
    }
}
