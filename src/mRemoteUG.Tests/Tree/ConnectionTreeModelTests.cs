using mRemoteUG.Connection;
using mRemoteUG.Container;
using mRemoteUG.Tree;

using NUnit.Framework;


namespace mRemoteUG.Tests.Tree;

public class ConnectionTreeModelTests
{
    private readonly ConnectionTreeModel _connectionTreeModel = new();

    [Test]
    public void GetChildListProvidesAllChildren()
    {
        var root = new ContainerInfo();
        var folder1 = new ContainerInfo();
        var folder2 = new ContainerInfo();
        var con1 = new ConnectionInfo();
        root.AddChild(folder1);
        folder1.AddChild(folder2);
        root.AddChild(con1);
        _connectionTreeModel.AddRootNode(root);
        IEnumerable<ConnectionInfo> connectionList = ConnectionTreeModel.GetRecursiveChildList(root);
        Assert.That(connectionList, Is.EquivalentTo(new[] { folder1, folder2, con1 }));
    }
}
