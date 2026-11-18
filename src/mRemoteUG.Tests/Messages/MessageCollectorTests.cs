using mRemoteUG.Messages;

using NSubstitute;

using NUnit.Framework;


namespace mRemoteUG.Tests.Messages;

public class MessageCollectorTests
{
    // Rebuilt by [SetUp] before each test, so null! rather than an initializer here;
    // see ADR-0022 on when that is the right answer.
    private MessageCollector _messageCollector = null!;

    [SetUp]
    public void Setup()
    {
        _messageCollector = new MessageCollector();
    }

    [Test]
    public void InitializesWithNoMessages()
    {
        Assert.That(_messageCollector.Messages, Is.Empty);
    }

    [Test]
    public void MessageAddedToList()
    {
        IMessage message = Substitute.For<IMessage>();
        _messageCollector.AddMessage(message);
        Assert.That(_messageCollector.Messages, Does.Contain(message));
    }

    [Test]
    public void ListCanBeCleared()
    {
        IMessage message = Substitute.For<IMessage>();
        _messageCollector.AddMessage(message);
        _messageCollector.ClearMessages();
        Assert.That(_messageCollector.Messages, Is.Empty);
    }

    [Test]
    public void MessagesAreUnique()
    {
        IMessage message = Substitute.For<IMessage>();
        _messageCollector.AddMessage(message);
        _messageCollector.AddMessage(message);
        Assert.That(_messageCollector.Messages, Is.Unique);
    }

    [Test]
    public void NotifiedWhenMessageAdded()
    {
        bool wasCalled = false;
        _messageCollector.CollectionChanged += (sender, args) => wasCalled = true;
        IMessage message = Substitute.For<IMessage>();
        _messageCollector.AddMessage(message);
        Assert.That(wasCalled, Is.True);
    }

    [Test]
    public void BatchAddAddsAllItems()
    {
        IMessage msg1 = Substitute.For<IMessage>();
        IMessage msg2 = Substitute.For<IMessage>();
        IMessage[] msgCollection = new[] { msg1, msg2 };
        _messageCollector.AddMessages(msgCollection);
        Assert.That(_messageCollector.Messages, Is.EquivalentTo(msgCollection));
    }

    [Test]
    public void OneNotificationRaisedForBatchAdd()
    {
        int notificationCount = 0;
        _messageCollector.CollectionChanged += (sender, args) => notificationCount++;
        IMessage msg1 = Substitute.For<IMessage>();
        IMessage msg2 = Substitute.For<IMessage>();
        _messageCollector.AddMessages(new[] { msg1, msg2 });
        Assert.That(notificationCount, Is.EqualTo(1));
    }

    [Test]
    public void EventNotRaisedIfMsgIsntUnique()
    {
        int notificationCount = 0;
        IMessage msg1 = Substitute.For<IMessage>();
        _messageCollector.AddMessage(msg1);
        _messageCollector.CollectionChanged += (sender, args) => notificationCount++;
        _messageCollector.AddMessage(msg1);
        Assert.That(notificationCount, Is.EqualTo(0));
    }
    [Test]
    public void OlderMessagesAreDroppedOnceTheCapIsReached()
    {
        // The list was never trimmed and ClearMessages has no production caller, so every
        // message stayed reachable for the life of the process.
        const int cap = 5000;
        for (int i = 0; i < cap + 100; i++)
        {
            _messageCollector.AddMessage(Substitute.For<IMessage>());
        }

        Assert.That(_messageCollector.Messages.Count(), Is.EqualTo(cap));
    }

    [Test]
    public void TheMessagesKeptAfterTrimmingAreTheNewestOnes()
    {
        const int cap = 5000;
        for (int i = 0; i < cap; i++)
        {
            _messageCollector.AddMessage(Substitute.For<IMessage>());
        }

        IMessage newest = Substitute.For<IMessage>();
        _messageCollector.AddMessage(newest);

        Assert.That(_messageCollector.Messages, Has.Member(newest));
    }

    [Test]
    public void AMessageCanBeCollectedAgainOnceTheListIsCleared()
    {
        IMessage message = Substitute.For<IMessage>();
        _messageCollector.AddMessage(message);
        _messageCollector.ClearMessages();
        int notificationCount = 0;
        _messageCollector.CollectionChanged += (sender, args) => notificationCount++;

        _messageCollector.AddMessage(message);

        Assert.Multiple(() =>
        {
            Assert.That(_messageCollector.Messages, Has.Member(message));
            Assert.That(notificationCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void ADroppedMessageCanBeCollectedAgain()
    {
        const int cap = 5000;
        IMessage oldest = Substitute.For<IMessage>();
        _messageCollector.AddMessage(oldest);
        for (int i = 0; i < cap; i++)
        {
            _messageCollector.AddMessage(MessageClass.InformationMsg, "filler " + i);
        }

        Assert.That(_messageCollector.Messages, Has.No.Member(oldest), "The oldest message was not dropped.");

        _messageCollector.AddMessage(oldest);

        Assert.That(_messageCollector.Messages, Has.Member(oldest));
    }
}
