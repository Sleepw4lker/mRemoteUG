#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using mRemoteUG.App;
using mRemoteUG.Messages.MessageWriters;
using mRemoteUG.UI.Forms;
using mRemoteUG.UI.Window;

namespace mRemoteUG.Messages.WriterDecorators
{
    public class MessageFocusDecorator : IMessageWriter
    {
        private readonly IMessageTypeFilteringOptions _filter;
        private readonly IMessageWriter _decoratedWriter;
        private readonly ErrorAndInfoWindow _messageWindow;
        private readonly FrmMain _frmMain = FrmMain.Default;
        private int _switchPending;

        public MessageFocusDecorator(ErrorAndInfoWindow messageWindow, IMessageTypeFilteringOptions filter, IMessageWriter decoratedWriter)
        {
            _filter = filter ?? throw new ArgumentNullException(nameof(filter));
            _messageWindow = messageWindow ?? throw new ArgumentNullException(nameof(messageWindow));
            _decoratedWriter = decoratedWriter ?? throw new ArgumentNullException(nameof(decoratedWriter));
        }

        public async void Write(IMessage message)
        {
            _decoratedWriter.Write(message);

            if (!WeShouldFocusNotificationPanel(message))
                return;

            // A burst of messages (e.g. a session disconnecting) used to queue one dock-panel
            // relayout per message on the UI thread. Collapse the burst into a single switch.
            if (Interlocked.CompareExchange(ref _switchPending, 1, 0) != 0)
                return;

            await SwitchToMessageAsync();
        }

        private bool WeShouldFocusNotificationPanel(IMessage message)
        {
            // ReSharper disable once SwitchStatementMissingSomeCases
            switch (message.Class)
            {
                case MessageClass.InformationMsg:
                    if (_filter.AllowInfoMessages)
                        return true;
                    break;
                case MessageClass.WarningMsg:
                    if (_filter.AllowWarningMessages) return true;
                    break;
                case MessageClass.ErrorMsg:
                    if (_filter.AllowErrorMessages) return true;
                    break;
            }
            return false;
        }

        private async Task SwitchToMessageAsync()
        {
            await Task
                .Delay(TimeSpan.FromMilliseconds(300))
                .ContinueWith(task => SwitchToMessage());
        }

        private void SwitchToMessage()
        {
            // This runs on a thread-pool continuation of an async void method, so an escaping
            // exception would take the process down. Swallow and log instead, and always clear
            // the pending flag so a later message can still raise the panel.
            try
            {
                if (_messageWindow.InvokeRequired)
                    _frmMain.Invoke((MethodInvoker)SwitchToMessageCore);
                else
                    SwitchToMessageCore();
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector?.AddExceptionMessage("Couldn't switch to the notification panel", ex);
            }
            finally
            {
                Interlocked.Exchange(ref _switchPending, 0);
            }
        }

        private void SwitchToMessageCore()
        {
            if (_messageWindow.IsDisposed)
                return;

            _messageWindow.PreviousActiveForm = _frmMain.Layout.ActiveDocument;
            _frmMain.Layout.ShowTool(_messageWindow);

            if (_messageWindow.lvErrorCollector.Items.Count == 0)
                return;

            _messageWindow.lvErrorCollector.Focus();
            _messageWindow.lvErrorCollector.SelectedItems.Clear();
            _messageWindow.lvErrorCollector.Items[0].Selected = true;
            _messageWindow.lvErrorCollector.FocusedItem = _messageWindow.lvErrorCollector.Items[0];
        }
    }
}