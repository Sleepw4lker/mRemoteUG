using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using mRemoteUG.UI;

namespace mRemoteUG.Messages.MessageWriters;

public class PopupMessageWriter : IMessageWriter
{
    private readonly IUiThreadInvoker _uiThreadInvoker;
    private readonly Func<IWin32Window?> _ownerProvider;

    public PopupMessageWriter(IUiThreadInvoker uiThreadInvoker, Func<IWin32Window?> ownerProvider)
    {
        ArgumentNullException.ThrowIfNull(uiThreadInvoker);
        ArgumentNullException.ThrowIfNull(ownerProvider);

        _uiThreadInvoker = uiThreadInvoker;
        _ownerProvider = ownerProvider;
    }

    /// <summary>
    /// Shows the box: owner, text, caption, icon. Tests replace it to see the call without a window.
    /// </summary>
    internal Action<IWin32Window?, string, string, MessageBoxIcon> ShowMessageBox { get; set; } =
        (owner, text, caption, icon) => MessageBox.Show(owner, text, caption, MessageBoxButtons.OK, icon);

    /// <remarks>
    /// Posted, not shown here and not sent. MessageCollector runs its writers on the reporting
    /// thread: an autosave thread, an RDP callback, the PuTTY watcher. A box shown there has no
    /// owner, so it can open behind the main window, and it blocks that thread until clicked -
    /// the autosave thread while it holds the save lock. Sending would block it just the same,
    /// and while a teardown has the UI thread wedged it would never return (ADR-0005). Posted
    /// from the UI thread too, which also gets a box raised inside a COM callback out of it.
    /// </remarks>
    public void Write(IMessage message)
    {
        _uiThreadInvoker.Post(() => Show(message));
    }

    [SuppressMessage("Performance", "CA1863",
                     Justification = "The format is a localized resource read at run time; "
                                     + "a cached CompositeFormat would freeze the UI language.")]
    private void Show(IMessage message)
    {
        // ControlUiThreadInvoker runs a post inline when it has no live control left to marshal
        // through - during shutdown, once the main form is gone - which would be the reporting
        // thread again. Show nothing then: the box would block that thread and could not be
        // owned, and the log writer has had the message already.
        if (!_uiThreadInvoker.OnUiThread)
        {
            return;
        }

        // Debug and Information said the same thing in two identical arms, and MessageClass
        // has exactly these four members - so the ArgumentOutOfRangeException that sat here
        // as the default could never be reached.
        (string titleFormat, MessageBoxIcon icon) = message.Class switch
        {
            MessageClass.WarningMsg => (Language.strTitleWarning, MessageBoxIcon.Warning),
            MessageClass.ErrorMsg => (Language.strTitleError, MessageBoxIcon.Error),
            _ => (Language.strTitleInformation, MessageBoxIcon.Information),
        };

        ShowMessageBox(
            _ownerProvider(),
            message.Text,
            string.Format(CultureInfo.CurrentCulture, titleFormat, message.Date),
            icon);
    }
}
