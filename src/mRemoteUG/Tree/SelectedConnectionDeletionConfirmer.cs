using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using mRemoteUG.Connection;
using mRemoteUG.Container;


namespace mRemoteUG.Tree;

public class SelectedConnectionDeletionConfirmer : IConfirm<ConnectionInfo>
{
    private readonly Func<string, string, MessageBoxButtons, MessageBoxIcon, DialogResult> _confirmationFunc;

    public SelectedConnectionDeletionConfirmer(
        Func<string, string, MessageBoxButtons, MessageBoxIcon, DialogResult> confirmationFunc)
    {
        _confirmationFunc = confirmationFunc;
    }

    public bool Confirm(ConnectionInfo? deletionTarget)
    {
        if (deletionTarget == null)
        {
            return false;
        }

        var deletionTargetAsContainer = deletionTarget as ContainerInfo;
        if (deletionTargetAsContainer != null)
        {
            return deletionTargetAsContainer.HasChildren()
                ? UserConfirmsNonEmptyFolderDeletion(deletionTargetAsContainer)
                : UserConfirmsEmptyFolderDeletion(deletionTargetAsContainer);
        }

        return UserConfirmsConnectionDeletion(deletionTarget);
    }

    [SuppressMessage("Performance", "CA1863",
                     Justification = "The format is a localized resource read at run time; "
                                     + "a cached CompositeFormat would freeze the UI language.")]
    private bool UserConfirmsEmptyFolderDeletion(AbstractConnectionRecord deletionTarget)
    {
        string messagePrompt = string.Format(
            CultureInfo.CurrentCulture,
            Language.strConfirmDeleteNodeFolder,
            deletionTarget.Name);
        return PromptUser(messagePrompt);
    }

    [SuppressMessage("Performance", "CA1863",
                     Justification = "The format is a localized resource read at run time; "
                                     + "a cached CompositeFormat would freeze the UI language.")]
    private bool UserConfirmsNonEmptyFolderDeletion(AbstractConnectionRecord deletionTarget)
    {
        string messagePrompt = string.Format(
            CultureInfo.CurrentCulture,
            Language.strConfirmDeleteNodeFolderNotEmpty,
            deletionTarget.Name);
        return PromptUser(messagePrompt);
    }

    [SuppressMessage("Performance", "CA1863",
                     Justification = "The format is a localized resource read at run time; "
                                     + "a cached CompositeFormat would freeze the UI language.")]
    private bool UserConfirmsConnectionDeletion(AbstractConnectionRecord deletionTarget)
    {
        string messagePrompt = string.Format(
            CultureInfo.CurrentCulture,
            Language.strConfirmDeleteNodeConnection,
            deletionTarget.Name);
        return PromptUser(messagePrompt);
    }

    private bool PromptUser(string promptMessage)
    {
        DialogResult msgBoxResponse = _confirmationFunc.Invoke(
            promptMessage,
            // Not null: ProductName reads [AssemblyProduct] first, and Properties/AssemblyInfo.cs sets it.
            Application.ProductName!,
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        return msgBoxResponse == DialogResult.Yes;
    }
}
