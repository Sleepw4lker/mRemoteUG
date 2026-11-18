using System.Collections.Specialized;

using mRemoteUG.App.Initialization;
using mRemoteUG.Messages;
using mRemoteUG.Messages.MessageWriters;
#if DEBUG
using mRemoteUG.Messages.WriterDecorators;
#endif
using NSubstitute;

using NUnit.Framework;

using Message = mRemoteUG.Messages.Message;

namespace mRemoteUG.Tests.App.Initialization;

/// <summary>
/// How the writers are assembled decides whether the Options -> Notifications settings are
/// honoured, so the assembly itself is worth pinning down.
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
public class MessageCollectorSetupTests
{
    [Test]
    public void TheTextLogWriterIsTheOneSharedFilteredInstance()
    {
        var writers = new List<IMessageWriter>();
        MessageCollectorSetup.BuildMessageWritersFromSettings(writers);

        Assert.That(writers.Any(w => ReferenceEquals(w, FilteredLogWriter.Instance)), Is.True,
                    "The collector must write to the same filtered log sink as everything else, " +
                    "or the log filter could be chosen in two places and drift apart.");
    }

#if DEBUG
    /// <summary>
    /// The Output window is a log, so it follows the Logging group. It used to be registered
    /// bare, which made a DEBUG build behave differently from the build a user runs.
    /// </summary>
    [Test]
    public void TheDebugConsoleWriterIsFiltered()
    {
        var writers = new List<IMessageWriter>();
        MessageCollectorSetup.BuildMessageWritersFromSettings(writers);

        Assert.That(writers.Any(w => w is MessageTypeFilterDecorator), Is.True);
        Assert.That(writers.Any(w => w is DebugConsoleMessageWriter), Is.False,
                    "The debug console writer is registered without a filter in front of it.");
    }
#endif

    [Test]
    public void EveryMessageOfAnAddNotificationReachesEveryWriter()
    {
        IMessageWriter first = Substitute.For<IMessageWriter>();
        IMessageWriter second = Substitute.For<IMessageWriter>();
        var message = new Message(MessageClass.InformationMsg, "hello");

        MessageCollectorSetup.FanOut(new[] { first, second },
                                     new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add,
                                                                          new[] { message }));

        first.Received(1).Write(message);
        second.Received(1).Write(message);
    }

    /// <summary>
    /// The collector only ever adds, but the handler used to dereference NewItems regardless,
    /// so anything else would have thrown inside whichever thread reported the message.
    /// </summary>
    [Test]
    public void ANotificationThatIsNotAnAddIsIgnored()
    {
        IMessageWriter writer = Substitute.For<IMessageWriter>();

        Assert.That(() => MessageCollectorSetup.FanOut(
                        new[] { writer },
                        new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset)),
                    Throws.Nothing);

        writer.DidNotReceiveWithAnyArgs().Write(Arg.Any<IMessage>());
    }
}
