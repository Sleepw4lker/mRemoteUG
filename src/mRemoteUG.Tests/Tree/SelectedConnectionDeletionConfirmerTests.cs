using mRemoteUG.Connection;
using mRemoteUG.Tree;

using NUnit.Framework;


namespace mRemoteUG.Tests.Tree;

public class SelectedConnectionDeletionConfirmerTests
{
    private readonly ConnectionInfo _testConnectionInfo = new();

    [Test]
    public void ClickingYesReturnsTrue()
    {
        var deletionConfirmer = new SelectedConnectionDeletionConfirmer(MockClickYes);
        Assert.That(deletionConfirmer.Confirm(_testConnectionInfo), Is.True);
    }

    [Test]
    public void ClickingNoReturnsFalse()
    {
        var deletionConfirmer = new SelectedConnectionDeletionConfirmer(MockClickNo);
        Assert.That(deletionConfirmer.Confirm(_testConnectionInfo), Is.False);
    }

    private DialogResult MockClickYes(string promptMessage, string title, MessageBoxButtons buttons,
                                      MessageBoxIcon icon)
    {
        return DialogResult.Yes;
    }

    private DialogResult MockClickNo(string promptMessage, string title, MessageBoxButtons buttons, MessageBoxIcon icon)
    {
        return DialogResult.No;
    }
}
