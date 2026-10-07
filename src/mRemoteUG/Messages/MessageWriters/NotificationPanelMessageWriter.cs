#nullable enable
using System;
using System.Windows.Forms;
using mRemoteUG.UI.Controls;
using mRemoteUG.UI.Window;

namespace mRemoteUG.Messages.MessageWriters
{
    public class NotificationPanelMessageWriter : IMessageWriter
    {
        private readonly ErrorAndInfoWindow _messageWindow;

        public NotificationPanelMessageWriter(ErrorAndInfoWindow messageWindow)
        {
            if (messageWindow == null)
                throw new ArgumentNullException(nameof(messageWindow));

            _messageWindow = messageWindow;
        }

        /// <summary>
        /// How many rows the notification list keeps. It is a view of the messages, not the
        /// record of them - MessageCollector holds that - so it is capped lower.
        /// </summary>
        private const int MaxRows = 1000;

        public void Write(IMessage message)
        {
            var lvItem = new NotificationMessageListViewItem(message);
            AddToList(lvItem);
        }

        /// <remarks>
        /// Posted, not sent. MessageCollector runs its writers on the calling thread, so this
        /// is reached from protocol callback threads as well as the UI thread, and a blocking
        /// Invoke here waits for the UI thread to pump. While a session teardown has that
        /// thread wedged it never will - which is precisely the condition someone would be
        /// trying to report. See ADR-0005 and ADR-0017.
        /// </remarks>
        private void AddToList(ListViewItem lvItem)
        {
            var list = _messageWindow.lvErrorCollector;

            if (list.InvokeRequired)
            {
                if (list.IsHandleCreated && !list.IsDisposed)
                    list.BeginInvoke((MethodInvoker)(() => AddToList(lvItem)));
                return;
            }

            list.Items.Insert(0, lvItem);
            while (list.Items.Count > MaxRows)
                list.Items.RemoveAt(list.Items.Count - 1);
        }
    }
}