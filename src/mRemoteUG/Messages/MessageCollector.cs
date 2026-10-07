#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
// ReSharper disable ArrangeAccessorOwnerBody

namespace mRemoteUG.Messages
{
    public class MessageCollector : INotifyCollectionChanged
    {
        private readonly IList<IMessage> _messageList;

        /// <summary>
        /// Membership of <see cref="_messageList"/>, so the uniqueness check is not a scan.
        /// </summary>
        /// <remarks>
        /// IMessage does not override Equals, so this compares by reference exactly as the
        /// List.Contains it replaces did. That mattered: the same instance added twice is
        /// collected once and raises one notification, which MessageCollectorTests pins.
        /// </remarks>
        private readonly HashSet<IMessage> _collected = new HashSet<IMessage>();
        private readonly object _gate = new object();

        /// <summary>
        /// How many messages are kept. Older ones are dropped as newer ones arrive.
        /// </summary>
        /// <remarks>
        /// The list was never trimmed and ClearMessages had no caller outside its own test, so
        /// every message text stayed reachable for the life of the process - and under
        /// --verbose, with several sessions opening and closing, that is tens of thousands of
        /// strings.
        /// <para>
        /// The cap is large on purpose. SubscribeAndReplay hands the backlog to writers that
        /// attach later than the first messages are reported, so it has to stay well above
        /// everything a startup can produce.
        /// </para>
        /// </remarks>
        private const int MaxRetainedMessages = 5000;

        /// <summary>
        /// A snapshot of everything collected so far. Messages arrive from protocol callback
        /// threads as well as the UI thread, so this cannot hand out the live list.
        /// </summary>
        public IEnumerable<IMessage> Messages
        {
            get { lock (_gate) { return _messageList.ToArray(); } }
        }

        public MessageCollector()
        {
            _messageList = new List<IMessage>();
        }

        public void AddMessage(MessageClass messageClass, string messageText, bool onlyLog = false)
        {
            var message = new Message(messageClass, messageText, onlyLog);
            AddMessage(message);
        }

        public void AddMessage(IMessage message)
        {
            AddMessages(new [] {message});
        }

        public void AddMessages(IEnumerable<IMessage> messages)
        {
            var newMessages = new List<IMessage>();
            lock (_gate)
            {
                foreach (var message in messages)
                {
                    if (!_collected.Add(message)) continue;
                    _messageList.Add(message);
                    newMessages.Add(message);
                }

                // Oldest first, so what is dropped is what nobody is going to scroll back to.
                while (_messageList.Count > MaxRetainedMessages)
                {
                    _collected.Remove(_messageList[0]);
                    _messageList.RemoveAt(0);
                }
            }
            // Raised outside the lock: one of the writers shows a modal message box, and holding
            // the lock across that would stop every other thread from reporting anything.
            if (newMessages.Any())
                RaiseCollectionChangedEvent(NotifyCollectionChangedAction.Add, newMessages);
        }

        /// <summary>
        /// Reports <paramref name="ex"/>, keeping the exception itself rather than a rendering of it.
        /// </summary>
        /// <remarks>
        /// The text is the short, readable form - the message and the reason, down the
        /// InnerException chain - because that is what the notification list and the pop-up show,
        /// and a stack trace in a message box is no use to anyone. The exception travels alongside
        /// it so the log can render the whole thing: type, message, stack and every inner
        /// exception. Flattening it here used to mean choosing between the inner chain and the
        /// stack, and whichever this method picked, the other was gone for good.
        /// </remarks>
        public void AddExceptionMessage(string message, Exception ex, MessageClass msgClass = MessageClass.ErrorMsg, bool logOnly = true)
        {
            var text = message + Environment.NewLine + Tools.MiscTools.GetExceptionMessageRecursive(ex);
            AddMessage(new Message(msgClass, text, logOnly, ex));
        }

        public void ClearMessages()
        {
            lock (_gate)
            {
                _messageList.Clear();
                _collected.Clear();
            }
        }

        /// <summary>
        /// Attaches <paramref name="handler"/> and hands it everything collected before it existed,
        /// exactly once.
        /// </summary>
        /// <remarks>
        /// The message writers are not built until frmMain_Load, so anything reported before that -
        /// the startup banner, command line parsing, a compatibility problem, a connection file that
        /// would not load - reached no writer at all. The messages were kept, but nothing ever
        /// replayed them, so the log simply began part-way through startup however the user had
        /// configured it.
        /// <para>
        /// Snapshotting and subscribing happen together under the lock, so a message cannot fall
        /// between the two and be lost. The replay itself runs outside the lock, for the same reason
        /// the change event does.
        /// </para>
        /// </remarks>
        public void SubscribeAndReplay(NotifyCollectionChangedEventHandler handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            IList backlog;
            lock (_gate)
            {
                backlog = _messageList.ToArray();
                CollectionChanged += handler;
            }

            if (backlog.Count > 0)
                handler(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, backlog));
        }

        public event NotifyCollectionChangedEventHandler? CollectionChanged;

        private void RaiseCollectionChangedEvent(NotifyCollectionChangedAction action, IList items)
        {
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(action, items));
        }
    }
}