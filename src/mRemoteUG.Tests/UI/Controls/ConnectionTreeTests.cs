using System.Linq;
using System.Threading;
using mRemoteUG.Connection;
using mRemoteUG.Container;
using mRemoteUG.Tree;
using mRemoteUG.Tree.Root;
using mRemoteUG.UI.Controls;
using NUnit.Framework;

namespace mRemoteUG.Tests.UI.Controls
{
    public class ConnectionTreeTests
	{
		private ConnectionTreeSearchTextFilter _filter;
		private ConnectionTree _connectionTree;

		[SetUp]
		public void Setup()
		{
			_filter = new ConnectionTreeSearchTextFilter();
			_connectionTree = new ConnectionTree
			{
				UseFiltering = true
			};
		}

		[Test]
		[Apartment(ApartmentState.STA)]
		public void FilteringIsRetainedAndUpdatedWhenNodeDeleted()
		{
			// root
			// |- folder1
			// |	|- con1
			// |	|- dontshowme
			// |- folder2
			//		|- con2
			var connectionTreeModel = new ConnectionTreeModel();
			var root = new RootNodeInfo(RootNodeType.Connection);
			var folder1 = new ContainerInfo {Name = "folder1"};
			var folder2 = new ContainerInfo {Name = "folder2"};
			var con1 = new ConnectionInfo {Name = "con1"};
			var con2 = new ConnectionInfo {Name = "con2"};
			var conDontShow = new ConnectionInfo {Name = "dontshowme" };
			root.AddChildRange(new []{folder1, folder2});
			folder1.AddChildRange(new []{con1, conDontShow});
			folder2.AddChild(con2);
			connectionTreeModel.AddRootNode(root);

			_connectionTree.ConnectionTreeModel = connectionTreeModel;
			// ensure all folders expanded
			_connectionTree.ExpandAll();

			// apply filtering on the tree
			_filter.FilterText = "con";
			_connectionTree.ModelFilter = _filter;

			connectionTreeModel.DeleteNode(con1);

			Assert.That(_connectionTree.IsFiltering, Is.True);
			Assert.That(_connectionTree.FilteredObjects, Does.Not.Contain(con1));
			Assert.That(_connectionTree.FilteredObjects, Does.Not.Contain(conDontShow));
			Assert.That(_connectionTree.FilteredObjects, Does.Contain(con2));
		}

	    [Test]
	    [Apartment(ApartmentState.STA)]
	    public void CannotAddConnectionToPuttySessionNode()
	    {
	        var connectionTreeModel = new ConnectionTreeModel();
	        var root = new RootNodeInfo(RootNodeType.Connection);
            var puttyRoot = new RootNodeInfo(RootNodeType.PuttySessions);
	        connectionTreeModel.AddRootNode(root);
	        connectionTreeModel.AddRootNode(puttyRoot);

	        _connectionTree.ConnectionTreeModel = connectionTreeModel;

	        _connectionTree.SelectedObject = puttyRoot;
	        _connectionTree.AddConnection();

	        Assert.That(puttyRoot.Children, Is.Empty);
	    }

	    [Test]
	    [Apartment(ApartmentState.STA)]
	    public void CannotAddFolderToPuttySessionNode()
	    {
	        var connectionTreeModel = new ConnectionTreeModel();
	        var root = new RootNodeInfo(RootNodeType.Connection);
	        var puttyRoot = new RootNodeInfo(RootNodeType.PuttySessions);
	        connectionTreeModel.AddRootNode(root);
	        connectionTreeModel.AddRootNode(puttyRoot);

	        _connectionTree.ConnectionTreeModel = connectionTreeModel;

	        _connectionTree.SelectedObject = puttyRoot;
	        _connectionTree.AddFolder();

	        Assert.That(puttyRoot.Children, Is.Empty);
	    }

	    [Test]
	    [Apartment(ApartmentState.STA)]
	    public void CannotDuplicateRootConnectionNode()
	    {
	        var connectionTreeModel = new ConnectionTreeModel();
	        var root = new RootNodeInfo(RootNodeType.Connection);
	        connectionTreeModel.AddRootNode(root);
	        _connectionTree.ConnectionTreeModel = connectionTreeModel;

	        _connectionTree.SelectedObject = root;
            _connectionTree.DuplicateSelectedNode();

	        Assert.That(connectionTreeModel.RootNodes, Has.One.Items);
	    }

	    [Test]
	    [Apartment(ApartmentState.STA)]
	    public void CannotDuplicateRootPuttyNode()
	    {
	        var connectionTreeModel = new ConnectionTreeModel();
	        var puttyRoot = new RootNodeInfo(RootNodeType.PuttySessions);
	        connectionTreeModel.AddRootNode(puttyRoot);
	        _connectionTree.ConnectionTreeModel = connectionTreeModel;

	        _connectionTree.SelectedObject = puttyRoot;
	        _connectionTree.DuplicateSelectedNode();

	        Assert.That(connectionTreeModel.RootNodes, Has.One.Items);
	    }

	    [Test]
	    [Apartment(ApartmentState.STA)]
	    public void DuplicatingWithNoNodeSelectedDoesNothing()
	    {
	        var connectionTreeModel = new ConnectionTreeModel();
	        var puttyRoot = new RootNodeInfo(RootNodeType.PuttySessions);
	        connectionTreeModel.AddRootNode(puttyRoot);
	        _connectionTree.ConnectionTreeModel = connectionTreeModel;

            _connectionTree.SelectedObject = null;
	        _connectionTree.DuplicateSelectedNode();

	        Assert.That(connectionTreeModel.RootNodes, Has.One.Items);
	    }

	    [Test]
	    [Apartment(ApartmentState.STA)]
	    public void ExpandingAllItemsExpandsEveryContainer()
	    {
            var connectionTreeModel = new ConnectionTreeModel();
	        var root = new RootNodeInfo(RootNodeType.Connection);
            connectionTreeModel.AddRootNode(root);
	        ContainerInfo parent = root;
	        foreach (var i in Enumerable.Repeat("", 8))
	        {
                var newContainer = new ContainerInfo {IsExpanded = false};
                parent.AddChild(newContainer);
	            parent = newContainer;
	        }

	        _connectionTree.ConnectionTreeModel = connectionTreeModel;

	        Assert.That(connectionTreeModel.GetRecursiveChildList().OfType<ContainerInfo>(),
	                    Has.Some.Matches<ContainerInfo>(c => !c.IsExpanded),
	                    "expected at least one collapsed folder before expanding");

	        _connectionTree.ExpandAll();

	        Assert.That(connectionTreeModel.GetRecursiveChildList().OfType<ContainerInfo>(),
	                    Is.All.Matches<ContainerInfo>(c => c.IsExpanded));
	    }

        [Test]
        [Apartment(ApartmentState.STA)]
        public void RenamingNodeWithNothingSelectedDoesNothing()
	    {
	        var connectionTreeModel = new ConnectionTreeModel();
	        var root = new RootNodeInfo(RootNodeType.Connection);
	        connectionTreeModel.AddRootNode(root);

	        _connectionTree.ConnectionTreeModel = connectionTreeModel;
	        _connectionTree.SelectedObject = null;

	        Assert.DoesNotThrow(() => _connectionTree.RenameSelectedNode());
        }
    }
}
