#nullable enable
using System;
using System.IO;
using System.Linq;
using mRemoteUG;
using mRemoteUG.App;
using mRemoteUG.Messages;
using mRemoteUG.Messages.MessageWriters;
using NUnit.Framework;

namespace mRemoteUG.Tests.Messages
{
    /// <summary>
    /// What survives of an exception between the catch block and the log file.
    /// </summary>
    /// <remarks>
    /// The two collector helpers used to flatten the exception into text, and each kept a different
    /// half: AddExceptionMessage rendered Message + StackTrace and dropped the whole
    /// InnerException chain, AddExceptionMessage walked the chain and dropped the stack. Between
    /// them they cover about 115 call sites, so whichever a caller happened to pick, half the
    /// evidence was gone before it reached the one artifact a user is asked to send.
    /// </remarks>
    [TestFixture]
    public class ExceptionLoggingTests
    {
        // Rebuilt by [SetUp] before each test, so null! rather than an initializer here;
        // see ADR-0022 on when that is the right answer.
        private string _tempDirectory = null!;
        private string _logFile = null!;
        private string _originalLogPath = null!;
        private bool _error;

        [SetUp]
        public void Setup()
        {
            _originalLogPath = Logger.Instance.LogPath;
            _error = Settings.Default.TextLogMessageWriterWriteErrorMsgs;
            Settings.Default.TextLogMessageWriterWriteErrorMsgs = true;

            _tempDirectory = Path.Combine(Path.GetTempPath(), "mRemoteUG.Tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDirectory);
            _logFile = Path.Combine(_tempDirectory, "exceptions.log");
            Logger.Instance.SetLogPath(_logFile);
        }

        [TearDown]
        public void TearDown()
        {
            Settings.Default.TextLogMessageWriterWriteErrorMsgs = _error;
            Logger.Instance.SetLogPath(_originalLogPath);
            try { Directory.Delete(_tempDirectory, true); } catch (IOException) { }
        }

        /// <summary>
        /// The headline case. A TargetInvocationException-shaped wrap is what the reflection, XML
        /// and COM paths actually throw, and its own message says nothing at all.
        /// </summary>
        [Test]
        public void AWrappedExceptionKeepsBothItsInnerChainAndItsStack()
        {
            var collector = new MessageCollector();

            collector.AddExceptionMessage("Error retrieving inherited property 'Hostname'", Wrapped());
            WriteLastToLog(collector);

            var contents = LogContents();
            Assert.Multiple(() =>
            {
                Assert.That(contents, Does.Contain("Error retrieving inherited property 'Hostname'"));
                Assert.That(contents, Does.Contain("inner boom"), "The inner exception's message was dropped.");
                Assert.That(contents, Does.Contain("FileNotFoundException"), "The inner exception's type was dropped.");
                Assert.That(contents, Does.Contain("   at "), "The stack trace was dropped.");
            });
        }

        [Test]
        public void AnExceptionMessageAlsoCarriesTheStack()
        {
            var collector = new MessageCollector();

            collector.AddExceptionMessage("Unable to import file.", Wrapped());
            WriteLastToLog(collector);

            Assert.That(LogContents(), Does.Contain("   at "), "The stack trace was dropped.");
        }

        [Test]
        public void TheExceptionTypeIsRecorded()
        {
            var collector = new MessageCollector();

            collector.AddExceptionMessage("Loading settings failed", Wrapped());
            WriteLastToLog(collector);

            // The type matters because Exception.Message is localized - without it, a log from a
            // German machine names the failure in German and nothing else identifies it.
            Assert.That(LogContents(), Does.Contain("InvalidOperationException"));
        }

        /// <summary>
        /// The text the notification list and the pop-up show stays a sentence.
        /// </summary>
        /// <remarks>
        /// NotificationMessageListViewItem flattens newlines into one row, so a stack trace in
        /// Text is a single unreadable line several thousand characters wide.
        /// </remarks>
        [Test]
        public void TheUserVisibleTextStaysShort()
        {
            var collector = new MessageCollector();

            collector.AddExceptionMessage("Could not save the connections file", Wrapped());

            var text = collector.Messages.Last().Text;
            Assert.Multiple(() =>
            {
                Assert.That(text, Does.Contain("Could not save the connections file"));
                Assert.That(text, Does.Contain("outer boom"), "The reason should still be readable.");
                Assert.That(text, Does.Not.Contain("   at "), "A stack trace reached the user-visible text.");
            });
        }

        [Test]
        public void TheExceptionTravelsOnTheMessage()
        {
            var collector = new MessageCollector();

            collector.AddExceptionMessage("something failed", Wrapped());

            Assert.That(collector.Messages.Last().Exception, Is.InstanceOf<InvalidOperationException>());
        }

        /// <summary>
        /// Messages replayed out of the backlog keep the time they happened.
        /// </summary>
        /// <remarks>
        /// SubscribeAndReplay hands everything reported before the writers existed - the whole of
        /// startup - to them at once. Stamped at write time, those lines all landed on the same
        /// instant, which is exactly the part of the log ADR-0018 is read through.
        /// </remarks>
        [Test]
        public void AReplayedBacklogKeepsTheTimeEachMessageHappened()
        {
            var collector = new MessageCollector();
            var marker = "backlog " + Guid.NewGuid().ToString("N");
            var happened = new DateTime(2021, 7, 8, 9, 10, 11, 120, DateTimeKind.Local);

            collector.AddMessage(new Message(MessageClass.ErrorMsg, marker, onlyLog: true) { Date = happened });

            // Attach the writer afterwards, the way MessageCollectorSetup does at frmMain_Load.
            collector.SubscribeAndReplay((sender, args) =>
            {
                if (args.NewItems == null) return;
                foreach (IMessage message in args.NewItems)
                    FilteredLogWriter.Instance.Write(message);
            });

            Assert.That(LogContents(), Does.Contain($"2021-07-08 09:10:11,120 ["),
                        "The replayed message was stamped when it was written, not when it happened.");
        }

        private static void WriteLastToLog(MessageCollector collector)
        {
            FilteredLogWriter.Instance.Write(collector.Messages.Last());
        }

        /// <summary>
        /// A genuinely thrown, genuinely wrapped exception, so that StackTrace is populated - it is
        /// null on an exception that was only constructed.
        /// </summary>
        private static Exception Wrapped()
        {
            try
            {
                try
                {
                    throw new FileNotFoundException("inner boom");
                }
                catch (Exception inner)
                {
                    throw new InvalidOperationException("outer boom", inner);
                }
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        private string LogContents()
        {
            if (!File.Exists(_logFile)) return "";

            // The sink holds the file open, so read it sharing write access.
            using (var stream = new FileStream(_logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }
    }
}
