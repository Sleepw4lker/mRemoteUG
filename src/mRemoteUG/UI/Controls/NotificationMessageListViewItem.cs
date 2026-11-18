using System.Globalization;

using mRemoteUG.Messages;

namespace mRemoteUG.UI.Controls;

public class NotificationMessageListViewItem : ListViewItem
{
    public NotificationMessageListViewItem(IMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        ImageIndex = Convert.ToInt32(message.Class, CultureInfo.InvariantCulture);
        Text = message.Text.Replace(Environment.NewLine, "  ");
        Tag = message;
    }
}
