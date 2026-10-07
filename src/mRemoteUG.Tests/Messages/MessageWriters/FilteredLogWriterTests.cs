#nullable enable
using System;
using System.IO;
using mRemoteUG;
using mRemoteUG.App;
using mRemoteUG.Messages;
using mRemoteUG.Messages.MessageWriters;
using NUnit.Framework;

namespace mRemoteUG.Tests.Messages.MessageWriters
{
    /// <summary>
    /// The shared log sink, and the thing that makes "all logging follows Options -> Notifications"
    /// true rather than aspirational.
    /// </summary>
    [TestFixture]
    public class FilteredLogWriterTests
    {
        // Rebuilt by [SetUp] before each test, so null! rather than an initializer here;
        // see ADR-0022 on when that is the right answer.
        private string _tempDirectory = null!;
        private string _logFile = null!;
        private string _originalLogPath = null!;
        private bool _debug, _info, _warning, _error;

        [SetUp]
        public void Setup()
        {
            _originalLogPath = Logger.Instance.LogPath;
            _debug = Settings.Default.TextLogMessageWriterWriteDebugMsgs;
            _info = Settings.Default.TextLogMessageWriterWriteInfoMsgs;
            _warning = Settings.Default.TextLogMessageWriterWriteWarningMsgs;
            _error = Settings.Default.TextLogMessageWriterWriteErrorMsgs;

            _tempDirectory = Path.Combine(Path.GetTempPath(), "mRemoteUG.Tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDirectory);
            _logFile = Path.Combine(_tempDirectory, "filtered.log");
            Logger.Instance.SetLogPath(_logFile);
        }

        [TearDown]
        public void TearDown()
        {
            Settings.Default.TextLogMessageWriterWriteDebugMsgs = _debug;
            Settings.Default.TextLogMessageWriterWriteInfoMsgs = _info;
            Settings.Default.TextLogMessageWriterWriteWarningMsgs = _warning;
            Settings.Default.TextLogMessageWriterWriteErrorMsgs = _error;
            Logger.Instance.SetLogPath(_originalLogPath);
            try { Directory.Delete(_tempDirectory, true); } catch (IOException) { }
        }

        [Test]
        public void AWarningIsWrittenWhenWarningLoggingIsOn()
        {
            Settings.Default.TextLogMessageWriterWriteWarningMsgs = true;

            var marker = "warning on " + Guid.NewGuid().ToString("N");
            FilteredLogWriter.Write(MessageClass.WarningMsg, marker);

            Assert.That(LogContents(), Does.Contain(marker));
        }

        [Test]
        public void AWarningIsSuppressedWhenWarningLoggingIsOff()
        {
            Settings.Default.TextLogMessageWriterWriteWarningMsgs = false;

            var marker = "warning off " + Guid.NewGuid().ToString("N");
            FilteredLogWriter.Write(MessageClass.WarningMsg, marker);

            Assert.That(LogContents(), Does.Not.Contain(marker));
        }

        [Test]
        public void EveryMessageClassObeysItsOwnSetting()
        {
            foreach (var (messageClass, allow) in new[]
            {
                (MessageClass.DebugMsg, false), (MessageClass.InformationMsg, false),
                (MessageClass.WarningMsg, false), (MessageClass.ErrorMsg, false),
                (MessageClass.DebugMsg, true), (MessageClass.InformationMsg, true),
                (MessageClass.WarningMsg, true), (MessageClass.ErrorMsg, true)
            })
            {
                Allow(messageClass, allow);
                var marker = $"{messageClass} {allow} {Guid.NewGuid():N}";
                FilteredLogWriter.Write(messageClass, marker);

                Assert.That(LogContents().Contains(marker), Is.EqualTo(allow),
                            $"{messageClass} with its setting {(allow ? "on" : "off")} behaved the wrong way.");
            }
        }

        /// <summary>
        /// The point of the exercise: with everything switched off, nothing at all reaches the log.
        /// </summary>
        /// <remarks>
        /// In a fresh process the file is not created either, because the sink is not built
        /// until something is written - but the logger is a process-wide singleton and another
        /// fixture may already have configured it, so what is asserted here is that the file stays
        /// empty. The stronger property is checked by mRemoteUG.exe --selftest, which starts clean.
        /// </remarks>
        [Test]
        public void NothingReachesTheLogWhenEveryMessageTypeIsOff()
        {
            Settings.Default.TextLogMessageWriterWriteDebugMsgs = false;
            Settings.Default.TextLogMessageWriterWriteInfoMsgs = false;
            Settings.Default.TextLogMessageWriterWriteWarningMsgs = false;
            Settings.Default.TextLogMessageWriterWriteErrorMsgs = false;

            foreach (MessageClass messageClass in Enum.GetValues(typeof(MessageClass)))
                FilteredLogWriter.Write(messageClass, "should never appear " + messageClass);

            Assert.That(LogContents(), Is.Empty);
        }

        private static void Allow(MessageClass messageClass, bool allow)
        {
            switch (messageClass)
            {
                case MessageClass.DebugMsg:
                    Settings.Default.TextLogMessageWriterWriteDebugMsgs = allow; break;
                case MessageClass.InformationMsg:
                    Settings.Default.TextLogMessageWriterWriteInfoMsgs = allow; break;
                case MessageClass.WarningMsg:
                    Settings.Default.TextLogMessageWriterWriteWarningMsgs = allow; break;
                case MessageClass.ErrorMsg:
                    Settings.Default.TextLogMessageWriterWriteErrorMsgs = allow; break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(messageClass));
            }
        }

        private string LogContents()
        {
            if (!File.Exists(_logFile)) return string.Empty;

            // The sink holds the file open, so read it sharing write access.
            using (var stream = new FileStream(_logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream))
                return reader.ReadToEnd();
        }
    }
}
