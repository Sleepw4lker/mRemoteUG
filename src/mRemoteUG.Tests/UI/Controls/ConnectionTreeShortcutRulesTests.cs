using System.Diagnostics.CodeAnalysis;

using mRemoteUG.Connection;
using mRemoteUG.Tree;
using mRemoteUG.Tree.Root;
using mRemoteUG.UI.Controls;

using NUnit.Framework;

namespace mRemoteUG.Tests.UI.Controls;

/// <summary>
/// The connection tree's shortcut keys belong to its context menu's items, and WinForms
/// skips a disabled item's shortcut, so the items' enabled state is the only rule a
/// shortcut obeys. Each test here changes only the tree's selection and never opens the
/// menu: a shortcut never opens it either (#169).
/// </summary>
[SuppressMessage("Design", "CA1001",
    Justification = "[TearDown] disposes _connectionTree after every test. "
        + "NUnit would dispose an IDisposable fixture once, after its last test.")]
[TestFixture]
[Apartment(ApartmentState.STA)]
public class ConnectionTreeShortcutRulesTests
{
    private ConnectionTree _connectionTree = null!;
    private RootNodeInfo _root = null!;
    private ConnectionInfo _connection = null!;
    private PuttySessionInfo _puttySession = null!;

    private ConnectionContextMenu Menu => (ConnectionContextMenu)_connectionTree.ContextMenuStrip!;

    [SetUp]
    public void Setup()
    {
        // root
        // |- connection
        // PuTTY root
        // |- PuTTY session
        var model = new ConnectionTreeModel();
        _root = new RootNodeInfo(RootNodeType.Connection);
        _connection = new ConnectionInfo { Name = "connection" };
        _root.AddChild(_connection);
        var puttyRoot = new RootPuttySessionsNodeInfo();
        _puttySession = new PuttySessionInfo { Name = "session" };
        puttyRoot.AddChild(_puttySession);
        model.AddRootNode(_root);
        model.AddRootNode(puttyRoot);

        _connectionTree = new ConnectionTree { ConnectionTreeModel = model };
    }

    [TearDown]
    public void TearDown()
    {
        _connectionTree.Dispose();
    }

    [Test]
    public void SelectingTheRootDisablesConnectDuplicateDeleteAndMove()
    {
        _connectionTree.SelectedObject = _root;

        Assert.Multiple(() =>
        {
            Assert.That(Menu.ConnectItem.Enabled, Is.False, "Connect");
            Assert.That(Menu.DuplicateItem.Enabled, Is.False, "Duplicate");
            Assert.That(Menu.DeleteItem.Enabled, Is.False, "Delete");
            Assert.That(Menu.MoveUpItem.Enabled, Is.False, "Move Up");
            Assert.That(Menu.MoveDownItem.Enabled, Is.False, "Move Down");
        });
    }

    [Test]
    public void SelectingAPuttySessionDisablesMoveDuplicateAndDelete()
    {
        _connectionTree.SelectedObject = _puttySession;

        Assert.Multiple(() =>
        {
            Assert.That(Menu.MoveUpItem.Enabled, Is.False, "Move Up");
            Assert.That(Menu.MoveDownItem.Enabled, Is.False, "Move Down");
            Assert.That(Menu.DuplicateItem.Enabled, Is.False, "Duplicate");
            Assert.That(Menu.DeleteItem.Enabled, Is.False, "Delete");
        });
    }

    [Test]
    public void SelectingNothingDisablesEveryShortcutItem()
    {
        _connectionTree.SelectedObject = _connection;
        _connectionTree.SelectedObject = null;

        AssertShortcutItemsEnabled(false);
    }

    [Test]
    public void SelectingAConnectionAfterTheRootEnablesTheShortcutItemsAgain()
    {
        _connectionTree.SelectedObject = _root;
        _connectionTree.SelectedObject = _connection;

        AssertShortcutItemsEnabled(true);
    }

    private void AssertShortcutItemsEnabled(bool enabled)
    {
        Assert.Multiple(() =>
        {
            Assert.That(Menu.ConnectItem.Enabled, Is.EqualTo(enabled), "Connect");
            Assert.That(Menu.DuplicateItem.Enabled, Is.EqualTo(enabled), "Duplicate");
            Assert.That(Menu.RenameItem.Enabled, Is.EqualTo(enabled), "Rename");
            Assert.That(Menu.DeleteItem.Enabled, Is.EqualTo(enabled), "Delete");
            Assert.That(Menu.MoveUpItem.Enabled, Is.EqualTo(enabled), "Move Up");
            Assert.That(Menu.MoveDownItem.Enabled, Is.EqualTo(enabled), "Move Down");
        });
    }
}
