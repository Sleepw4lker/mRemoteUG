#nullable enable
using mRemoteUG.Messages;
using mRemoteUG.Messages.MessageWriters;
using mRemoteUG.Messages.WriterDecorators;
using NSubstitute;
using NUnit.Framework;

namespace mRemoteUG.Tests.Messages.MessageWriters
{
    public class OnlyLogMessageFilterTests
    {
        // Rebuilt by [SetUp] before each test, so null! rather than an initializer here;
        // see ADR-0022 on when that is the right answer.
        private OnlyLogMessageFilter _sut = null!;
        private IMessageWriter _mockWriter = null!;

        [SetUp]
        public void Setup()
        {
            _mockWriter = Substitute.For<IMessageWriter>();
            _sut = new OnlyLogMessageFilter(_mockWriter);
        }

        [Test]
        public void WillWriteIfTheOnlyLogFlagIsNotSet()
        {
            var msg = Substitute.For<IMessage>();
            msg.OnlyLog.Returns(false);
            _sut.Write(msg);
            _mockWriter.Received().Write(msg);
        }

        [Test]
        public void WillNotWriteIfTheOnlyLogFlagIsSet()
        {
            var msg = Substitute.For<IMessage>();
            msg.OnlyLog.Returns(true);
            _sut.Write(msg);
            _mockWriter.DidNotReceiveWithAnyArgs().Write(msg);
        }
    }
}