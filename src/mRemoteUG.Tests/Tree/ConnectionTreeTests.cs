using System.Diagnostics.CodeAnalysis;

using mRemoteUG.Container;
using mRemoteUG.Tree;
using mRemoteUG.Tree.Root;
using mRemoteUG.UI.Controls;

using NUnit.Framework;


namespace mRemoteUG.Tests.Tree;

[SuppressMessage("Design", "CA1001",
    Justification = "[TearDown] disposes _connectionTree after every test. "
        + "NUnit would dispose an IDisposable fixture once, after its last test.")]
public class ConnectionTreeTests
{
    // Rebuilt per test in Setup: the tree is disposed after each test, and the tests change the model.
    private ConnectionTree _connectionTree = null!;
    private ConnectionTreeModel _connectionTreeModel = null!;

    [SetUp]
    public void Setup()
    {
        _connectionTreeModel = CreateConnectionTreeModel();
        _connectionTree = new ConnectionTree
        {
            PostSetupActions = new IConnectionTreePostSetupAction[] { new RootNodeExpander() }
        };
    }

    [TearDown]
    public void Teardown()
    {
        _connectionTree.Dispose();
    }


    [Test, Apartment(ApartmentState.STA)]
    public void CanDeleteLastFolderInTheTree()
    {
        var lastFolder = new ContainerInfo();
        _connectionTreeModel.RootNodes[0].AddChild(lastFolder);
        _connectionTree.ConnectionTreeModel = _connectionTreeModel;
        _connectionTree.SelectObject(lastFolder);
        _connectionTree.DeleteSelectedNode();
        Assert.That(_connectionTree.GetRootConnectionNode().HasChildren, Is.False);
    }

    private static ConnectionTreeModel CreateConnectionTreeModel()
    {
        var connectionTreeModel = new ConnectionTreeModel();
        connectionTreeModel.AddRootNode(new RootNodeInfo(RootNodeType.Connection));
        return connectionTreeModel;
    }
}
