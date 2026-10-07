using System.Threading;
using mRemoteUG.Container;
using mRemoteUG.Tree;
using mRemoteUG.Tree.Root;
using mRemoteUG.UI.Controls;
using NUnit.Framework;


namespace mRemoteUG.Tests.Tree
{
    public class ConnectionTreeTests
    {
        private ConnectionTree _connectionTree;
        private ConnectionTreeModel _connectionTreeModel;

        [SetUp]
        public void Setup()
        {
            _connectionTreeModel = CreateConnectionTreeModel();
            _connectionTree = new ConnectionTree
            {
                PostSetupActions = new IConnectionTreeDelegate[] {new RootNodeExpander()}
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

        private ConnectionTreeModel CreateConnectionTreeModel()
        {
            var connectionTreeModel = new ConnectionTreeModel();
            connectionTreeModel.AddRootNode(new RootNodeInfo(RootNodeType.Connection));
            return connectionTreeModel;
        }
    }
}