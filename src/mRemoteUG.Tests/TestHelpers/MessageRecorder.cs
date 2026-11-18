using System.Collections.Specialized;

using mRemoteUG.App;
using mRemoteUG.Messages;

namespace mRemoteUG.Tests.TestHelpers;

/// <summary>
/// Records what is reported to <see cref="Runtime.MessageCollector"/> while it is alive.
/// </summary>
/// <remarks>
/// The collector is process-wide and keeps everything earlier tests reported, so a test that
/// read its <c>Messages</c> would see those too. Subscribing only for the test's own duration
/// leaves exactly the messages the code under test produced.
/// </remarks>
public sealed class MessageRecorder : IDisposable
{
    private readonly List<IMessage> _messages = new List<IMessage>();

    public MessageRecorder()
    {
        Runtime.MessageCollector.CollectionChanged += OnCollectionChanged;
    }

    public IReadOnlyList<IMessage> Messages => _messages;

    /// <summary>The recorded messages of one class whose text contains <paramref name="text"/>.</summary>
    public IReadOnlyList<IMessage> Matching(MessageClass messageClass, string text)
    {
        return _messages
               .Where(message => message.Class == messageClass
                                 && message.Text.Contains(text, StringComparison.Ordinal))
               .ToList();
    }

    public void Dispose()
    {
        Runtime.MessageCollector.CollectionChanged -= OnCollectionChanged;
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        if (args.NewItems != null)
        {
            _messages.AddRange(args.NewItems.Cast<IMessage>());
        }
    }
}
