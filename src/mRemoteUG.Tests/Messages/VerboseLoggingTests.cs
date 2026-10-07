#nullable enable
using System;
using System.IO;
using mRemoteUG;
using mRemoteUG.App;
using mRemoteUG.Messages;
using mRemoteUG.Messages.MessageWriters;
using NUnit.Framework;

namespace mRemoteUG.Tests.Messages
{
    /// <summary>
    /// --verbose turns debug logging on for one run without touching the saved setting.
    /// </summary>
    /// <remarks>
    /// This is what makes Debug a level worth using. Debug is off by default, so the only way to
    /// be certain a line reached the log used to be to report it as Information - which is how
    /// routine per-stage and per-resize detail came to sit in every log permanently. Asking
    /// someone to reproduce the problem under --verbose gets that detail back on demand, so it no
    /// longer has to live at Information.
    /// </remarks>
    [TestFixture]
    public class VerboseLoggingTests
    {
        // Rebuilt by [SetUp] before each test, so null! rather than an initializer here;
        // see ADR-0022 on when that is the right answer.
        private string _tempDirectory = null!;
        private string _logFile = null!;
        private string _originalLogPath = null!;
        private bool _originalForceDebug;
        private bool _originalDebugSetting;
        private bool _originalInfoSetting;

        [SetUp]
        public void Setup()
        {
            _originalForceDebug = LogMessageTypeFilteringOptions.ForceDebugMessages;
            _originalDebugSetting = Settings.Default.TextLogMessageWriterWriteDebugMsgs;
            _originalInfoSetting = Settings.Default.TextLogMessageWriterWriteInfoMsgs;
            _originalLogPath = Logger.Instance.LogPath;

            _tempDirectory = Path.Combine(Path.GetTempPath(), "mRemoteUG.Tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDirectory);
            _logFile = Path.Combine(_tempDirectory, "verbose.log");
            Logger.Instance.SetLogPath(_logFile);
        }

        [TearDown]
        public void TearDown()
        {
            LogMessageTypeFilteringOptions.ForceDebugMessages = _originalForceDebug;
            Settings.Default.TextLogMessageWriterWriteDebugMsgs = _originalDebugSetting;
            Settings.Default.TextLogMessageWriterWriteInfoMsgs = _originalInfoSetting;
            Logger.Instance.SetLogPath(_originalLogPath);
            try { Directory.Delete(_tempDirectory, true); } catch (IOException) { }
        }

        [Test]
        public void DebugIsSuppressedWithTheSettingOffAndNoOverride()
        {
            Settings.Default.TextLogMessageWriterWriteDebugMsgs = false;
            LogMessageTypeFilteringOptions.ForceDebugMessages = false;

            var marker = "debug default " + Guid.NewGuid().ToString("N");
            FilteredLogWriter.Write(MessageClass.DebugMsg, marker);

            Assert.That(Contents(), Does.Not.Contain(marker));
        }

        [Test]
        public void DebugReachesTheLogWithTheOverrideOnEvenThoughTheSettingIsOff()
        {
            Settings.Default.TextLogMessageWriterWriteDebugMsgs = false;
            LogMessageTypeFilteringOptions.ForceDebugMessages = true;

            var marker = "debug verbose " + Guid.NewGuid().ToString("N");
            FilteredLogWriter.Write(MessageClass.DebugMsg, marker);

            Assert.That(Contents(), Does.Contain(marker));
        }

        /// <summary>
        /// The switch is for one run; it must not quietly become the user's preference.
        /// </summary>
        [Test]
        public void TheOverrideDoesNotChangeTheSavedSetting()
        {
            Settings.Default.TextLogMessageWriterWriteDebugMsgs = false;
            LogMessageTypeFilteringOptions.ForceDebugMessages = true;

            var unused = new LogMessageTypeFilteringOptions().AllowDebugMessages;

            Assert.That(unused, Is.True);
            Assert.That(Settings.Default.TextLogMessageWriterWriteDebugMsgs, Is.False);
        }

        [Test]
        public void TheOverrideLeavesTheOtherLevelsAlone()
        {
            LogMessageTypeFilteringOptions.ForceDebugMessages = true;
            Settings.Default.TextLogMessageWriterWriteInfoMsgs = false;

            var marker = "info while verbose " + Guid.NewGuid().ToString("N");
            FilteredLogWriter.Write(MessageClass.InformationMsg, marker);

            Assert.That(Contents(), Does.Not.Contain(marker),
                        "--verbose turns on Debug, not everything.");
        }

        [Test]
        public void TheSwitchIsRecognisedOnTheCommandLine()
        {
            Assert.That(SelfTest.HasSwitch(new[] { "--verbose" }, ProgramRoot.VerboseSwitch), Is.True);
            Assert.That(SelfTest.HasSwitch(new[] { "--VERBOSE" }, ProgramRoot.VerboseSwitch), Is.True);
            Assert.That(SelfTest.HasSwitch(new[] { "--selftest" }, ProgramRoot.VerboseSwitch), Is.False);
            Assert.That(SelfTest.HasSwitch(null, ProgramRoot.VerboseSwitch), Is.False);
        }

        private string Contents()
        {
            if (!File.Exists(_logFile)) return string.Empty;

            // The sink holds the file open, so read it sharing write access.
            using (var stream = new FileStream(_logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream))
                return reader.ReadToEnd();
        }
    }
}
