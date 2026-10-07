#nullable enable
using System.Windows.Forms;

namespace mRemoteUG.Messages.MessageWriters
{
    public class PopupMessageWriter : IMessageWriter
    {
        public void Write(IMessage message)
        {
            // Debug and Information said the same thing in two identical arms, and MessageClass
            // has exactly these four members - so the ArgumentOutOfRangeException that sat here
            // as the default could never be reached.
            switch (message.Class)
            {
                case MessageClass.WarningMsg:
                    MessageBox.Show(message.Text, string.Format(Language.strTitleWarning, message.Date), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    break;
                case MessageClass.ErrorMsg:
                    MessageBox.Show(message.Text, string.Format(Language.strTitleError, message.Date), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    break;
                default:
                    MessageBox.Show(message.Text, string.Format(Language.strTitleInformation, message.Date), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    break;
            }
        }
    }
}
