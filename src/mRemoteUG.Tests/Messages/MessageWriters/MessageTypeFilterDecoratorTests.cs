#nullable enable
using mRemoteUG.Messages;
using mRemoteUG.Messages.MessageWriters;
using mRemoteUG.Messages.WriterDecorators;
using NSubstitute;
using NUnit.Framework;

namespace mRemoteUG.Tests.Messages.MessageWriters
{
    public class MessageTypeFilterDecoratorTests
    {
        // Rebuilt by [SetUp] before each test, so null! rather than an initializer here;
        // see ADR-0022 on when that is the right answer.
        private MessageTypeFilterDecorator _sut = null!;
        private IMessageWriter _mockWriter = null!;
        private IMessageTypeFilteringOptions _filter = null!;
        private IMessage _message = null!;

        [SetUp]
        public void Setup()
        {
            _mockWriter = Substitute.For<IMessageWriter>();
            _filter = Substitute.For<IMessageTypeFilteringOptions>();
            _sut = new MessageTypeFilterDecorator(_filter, _mockWriter);
            _message = Substitute.For<IMessage>();
        }

        [Test]
        public void DebugMessageWrittenIfAllowed()
        {
            _message.Class.Returns(MessageClass.DebugMsg);
            _filter.AllowDebugMessages.Returns(true);
            _sut.Write(_message);
            _mockWriter.Received().Write(_message);
        }

        [Test]
        public void DebugMessageNotWrittenIfNotAllowed()
        {
            _message.Class.Returns(MessageClass.DebugMsg);
            _filter.AllowDebugMessages.Returns(false);
            _sut.Write(_message);
            _mockWriter.DidNotReceive().Write(_message);
        }

        [Test]
        public void InfoMessageWrittenIfAllowed()
        {
            _message.Class.Returns(MessageClass.InformationMsg);
            _filter.AllowInfoMessages.Returns(true);
            _sut.Write(_message);
            _mockWriter.Received().Write(_message);
        }

        [Test]
        public void InfoMessageNotWrittenIfNotAllowed()
        {
            _message.Class.Returns(MessageClass.InformationMsg);
            _filter.AllowInfoMessages.Returns(false);
            _sut.Write(_message);
            _mockWriter.DidNotReceive().Write(_message);
        }

        [Test]
        public void WarningMessageWrittenIfAllowed()
        {
            _message.Class.Returns(MessageClass.WarningMsg);
            _filter.AllowWarningMessages.Returns(true);
            _sut.Write(_message);
            _mockWriter.Received().Write(_message);
        }

        [Test]
        public void WarningMessageNotWrittenIfNotAllowed()
        {
            _message.Class.Returns(MessageClass.WarningMsg);
            _filter.AllowWarningMessages.Returns(false);
            _sut.Write(_message);
            _mockWriter.DidNotReceive().Write(_message);
        }

        [Test]
        public void ErrorMessageWrittenIfAllowed()
        {
            _message.Class.Returns(MessageClass.ErrorMsg);
            _filter.AllowErrorMessages.Returns(true);
            _sut.Write(_message);
            _mockWriter.Received().Write(_message);
        }

        [Test]
        public void ErrorMessageNotWrittenIfNotAllowed()
        {
            _message.Class.Returns(MessageClass.ErrorMsg);
            _filter.AllowErrorMessages.Returns(false);
            _sut.Write(_message);
            _mockWriter.DidNotReceive().Write(_message);
        }
    }
}
