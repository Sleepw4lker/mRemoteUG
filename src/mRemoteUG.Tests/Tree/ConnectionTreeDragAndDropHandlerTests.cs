using mRemoteUG.Connection;
using mRemoteUG.Container;
using mRemoteUG.Tree;
using mRemoteUG.Tree.Root;

using NUnit.Framework;


namespace mRemoteUG.Tests.Tree;

public class ConnectionTreeDragAndDropHandlerTests
{
    // Rebuilt per test in Setup: the tests move nodes around, and the handler keeps feedback state.
    private ConnectionTreeDragAndDropHandler _dragAndDropHandler = null!;
    private RootNodeInfo _rootNode = null!;
    private ContainerInfo _container1 = null!;
    private ContainerInfo _container2 = null!;
    private ContainerInfo _container3 = null!;
    private ConnectionInfo _connection1 = null!;
    private ConnectionInfo _connection2 = null!;
    private ConnectionInfo _connection3 = null!;
    private ConnectionInfo _connection4 = null!;
    private ConnectionInfo _connection5 = null!;

    [SetUp]
    public void Setup()
    {
        _dragAndDropHandler = new ConnectionTreeDragAndDropHandler();
        InitializeNodes();
        CreateSimpleTreeModel();
    }

    [TestCase(DropTargetLocation.AboveItem)]
    [TestCase(DropTargetLocation.BelowItem)]
    public void DroppingBesideATargetWithoutAParentLeavesTheSourceWhereItWas(DropTargetLocation location)
    {
        ContainerInfo? formerParent = _connection1.Parent;
        var parentless = new ContainerInfo();

        ConnectionTreeDragAndDropHandler.DropModel(_connection1, parentless, location);

        Assert.That(_connection1.Parent, Is.SameAs(formerParent));
    }

    [Test]
    public void CanDragConnectionOntoContainer()
    {
        ConnectionInfo source = _connection3;
        ContainerInfo target = _container1;
        DragDropEffects dragDropEffects = _dragAndDropHandler.CanModelDrop(source, target, DropTargetLocation.Item);
        Assert.That(dragDropEffects, Is.EqualTo(DragDropEffects.Move));
    }

    [Test]
    public void CanDragContainerOntoContainer()
    {
        ContainerInfo source = _container1;
        ContainerInfo target = _container2;
        DragDropEffects dragDropEffects = _dragAndDropHandler.CanModelDrop(source, target, DropTargetLocation.Item);
        Assert.That(dragDropEffects, Is.EqualTo(DragDropEffects.Move));
    }

    [Test]
    public void CanDragContainerBetweenContainers()
    {
        ContainerInfo source = _container3;
        ContainerInfo target = _container2;
        DragDropEffects dragDropEffects =
            _dragAndDropHandler.CanModelDrop(source, target, DropTargetLocation.AboveItem);
        Assert.That(dragDropEffects, Is.EqualTo(DragDropEffects.Move));
    }

    [Test]
    public void CanDragConnectionBetweenConnections()
    {
        ConnectionInfo source = _connection1;
        ConnectionInfo target = _connection4;
        DragDropEffects dragDropEffects =
            _dragAndDropHandler.CanModelDrop(source, target, DropTargetLocation.AboveItem);
        Assert.That(dragDropEffects, Is.EqualTo(DragDropEffects.Move));
    }

    [Test]
    public void CantDragConnectionOntoItself()
    {
        ConnectionInfo source = _connection1;
        ConnectionInfo target = _connection1;
        DragDropEffects dragDropEffects = _dragAndDropHandler.CanModelDrop(source, target, DropTargetLocation.Item);
        Assert.That(dragDropEffects, Is.EqualTo(DragDropEffects.None));
    }

    [Test]
    public void CantDragContainerOntoItself()
    {
        ContainerInfo source = _container1;
        ContainerInfo target = _container1;
        DragDropEffects dragDropEffects = _dragAndDropHandler.CanModelDrop(source, target, DropTargetLocation.Item);
        Assert.That(dragDropEffects, Is.EqualTo(DragDropEffects.None));
    }

    [Test]
    public void CantDragContainerOntoItsChild()
    {
        ContainerInfo source = _container2;
        ContainerInfo target = _container3;
        DragDropEffects dragDropEffects = _dragAndDropHandler.CanModelDrop(source, target, DropTargetLocation.Item);
        Assert.That(dragDropEffects, Is.EqualTo(DragDropEffects.None));
    }

    [Test]
    public void CantDragContainerBetweenItsChildren()
    {
        ContainerInfo source = _container3;
        ConnectionInfo target = _connection4;
        DragDropEffects dragDropEffects =
            _dragAndDropHandler.CanModelDrop(source, target, DropTargetLocation.BelowItem);
        Assert.That(dragDropEffects, Is.EqualTo(DragDropEffects.None));
    }

    [Test]
    public void CantDragNodeOntoItsCurrentParent()
    {
        ContainerInfo source = _container3;
        ContainerInfo target = _container2;
        DragDropEffects dragDropEffects = _dragAndDropHandler.CanModelDrop(source, target, DropTargetLocation.Item);
        Assert.That(dragDropEffects, Is.EqualTo(DragDropEffects.None));
    }

    [Test]
    public void CantDragRootNodeInfo()
    {
        RootNodeInfo source = _rootNode;
        var target = new ContainerInfo();
        DragDropEffects dragDropEffects = _dragAndDropHandler.CanModelDrop(source, target, DropTargetLocation.Item);
        Assert.That(dragDropEffects, Is.EqualTo(DragDropEffects.None));
    }

    [Test]
    public void CantDragNodeAboveRootNodeInfo()
    {
        ConnectionInfo source = _connection1;
        RootNodeInfo target = _rootNode;
        DragDropEffects dragDropEffects =
            _dragAndDropHandler.CanModelDrop(source, target, DropTargetLocation.AboveItem);
        Assert.That(dragDropEffects, Is.EqualTo(DragDropEffects.None));
    }

    [Test]
    public void CantDragNodeBelowRootNodeInfo()
    {
        ConnectionInfo source = _connection1;
        RootNodeInfo target = _rootNode;
        DragDropEffects dragDropEffects =
            _dragAndDropHandler.CanModelDrop(source, target, DropTargetLocation.BelowItem);
        Assert.That(dragDropEffects, Is.EqualTo(DragDropEffects.None));
    }

    [Test]
    public void DraggingNodeBelowSiblingRearrangesTheUnderlyingModelCorrectly()
    {
        ConnectionInfo source = _connection3;
        ConnectionInfo target = _connection5;
        DropTargetLocation location = DropTargetLocation.BelowItem;
        ConnectionTreeDragAndDropHandler.DropModel(source, target, location);
        int actualIndex = _container3.Children.IndexOf(source);
        int expectedIndex = _container3.Children.IndexOf(target) + 1;
        Assert.That(actualIndex, Is.EqualTo(expectedIndex));
    }

    [Test]
    public void DraggingNodeAboveSiblingRearrangesTheUnderlyingModelCorrectly()
    {
        ConnectionInfo source = _connection3;
        ConnectionInfo target = _connection5;
        DropTargetLocation location = DropTargetLocation.AboveItem;
        ConnectionTreeDragAndDropHandler.DropModel(source, target, location);
        int actualIndex = _container3.Children.IndexOf(source);
        int expectedIndex = _container3.Children.IndexOf(target) - 1;
        Assert.That(actualIndex, Is.EqualTo(expectedIndex));
    }

    [Test]
    public void DraggingNodeToNewContainerAddsNodeToTheNewContainer()
    {
        ConnectionInfo source = _connection3;
        ContainerInfo target = _container1;
        DropTargetLocation location = DropTargetLocation.Item;
        ConnectionTreeDragAndDropHandler.DropModel(source, target, location);
        Assert.That(target.Children.Contains(source));
    }

    [Test]
    public void DraggingNodeToNewContainerRemovesNodeFromOldContainer()
    {
        ConnectionInfo source = _connection3;
        ContainerInfo target = _container1;
        DropTargetLocation location = DropTargetLocation.Item;
        ConnectionTreeDragAndDropHandler.DropModel(source, target, location);
        Assert.That(!_container3.Children.Contains(source));
    }


    [Test]
    public void CanDragConnectionBelowAConnection()
    {
        DragDropEffects dragDropEffects =
            _dragAndDropHandler.CanModelDrop(_connection1, _connection4, DropTargetLocation.BelowItem);
        Assert.That(dragDropEffects, Is.EqualTo(DragDropEffects.Move));
    }

    [Test]
    public void CantDropWithoutATargetLocation()
    {
        DragDropEffects dragDropEffects =
            _dragAndDropHandler.CanModelDrop(_connection1, _connection4, DropTargetLocation.None);
        Assert.That(dragDropEffects, Is.EqualTo(DragDropEffects.None));
    }

    [Test]
    public void CantDragConnectionOntoAnotherConnection()
    {
        DragDropEffects dragDropEffects =
            _dragAndDropHandler.CanModelDrop(_connection1, _connection4, DropTargetLocation.Item);
        Assert.That(dragDropEffects, Is.EqualTo(DragDropEffects.None));
    }

    [Test]
    public void CantDragConnectionOntoThePuttySessionsRoot()
    {
        DragDropEffects dragDropEffects =
            _dragAndDropHandler.CanModelDrop(_connection1, new RootPuttySessionsNodeInfo(), DropTargetLocation.Item);
        Assert.That(dragDropEffects, Is.EqualTo(DragDropEffects.None));
    }

    /// <summary>
    /// The drop sink draws no insertion feedback where a drop can never land, and green feedback
    /// where it can. Only <see cref="ConnectionTreeDragAndDropHandler.HandleModelCanDrop"/>
    /// shows it, so the cases go through the event.
    /// </summary>
    [TestCase("root", "container1", DropTargetLocation.Item, false, TestName = "a root node is not draggable")]
    [TestCase("connection1", "connection4", DropTargetLocation.Item, false,
              TestName = "nothing drops onto a connection")]
    [TestCase("connection1", "root", DropTargetLocation.AboveItem, false,
              TestName = "nothing drops beside the root")]
    [TestCase("connection3", "container1", DropTargetLocation.Item, true, TestName = "a folder takes a connection")]
    [TestCase("connection1", "connection4", DropTargetLocation.AboveItem, true,
              TestName = "a connection goes between connections")]
    public void CanDropFeedbackIsShownOnlyWhereADropCanLand(
        string source,
        string target,
        DropTargetLocation location,
        bool feedbackExpected)
    {
        var feedback = new RecordingDropFeedback();
        var args = new ModelDropEventArgs
        {
            SourceModels = new[] { Node(source) },
            TargetModel = Node(target),
            DropTargetLocation = location,
            DropSink = feedback
        };

        _dragAndDropHandler.HandleModelCanDrop(null, args);

        Assert.That(feedback.EnableFeedback, Is.EqualTo(feedbackExpected));
        Assert.That(args.Effect, Is.EqualTo(feedbackExpected ? DragDropEffects.Move : DragDropEffects.None));
        Assert.That(args.Handled, Is.True);
    }

    /// <summary>A refused drop says why, next to the target row.</summary>
    [TestCase("container3", "container3", DropTargetLocation.Item, nameof(Language.strNodeCannotDragOnSelf))]
    [TestCase("container2", "container3", DropTargetLocation.Item, nameof(Language.strNodeCannotDragParentOnChild))]
    [TestCase("container3", "container2", DropTargetLocation.Item, nameof(Language.strNodeAlreadyInFolder))]
    [TestCase("container3", "connection4", DropTargetLocation.BelowItem,
              nameof(Language.strNodeCannotDragParentOnChild))]
    [TestCase("root", "container1", DropTargetLocation.Item, nameof(Language.strNodeNotDraggable))]
    public void ARefusedDropExplainsWhy(string source, string target, DropTargetLocation location, string message)
    {
        var args = new ModelDropEventArgs
        {
            SourceModels = new[] { Node(source) },
            TargetModel = Node(target),
            DropTargetLocation = location,
            DropSink = new RecordingDropFeedback()
        };

        _dragAndDropHandler.HandleModelCanDrop(null, args);

        Assert.That(args.Effect, Is.EqualTo(DragDropEffects.None));
        Assert.That(args.InfoMessage, Is.EqualTo(Language.ResourceManager.GetString(message, Language.Culture)));
    }

    private ConnectionInfo Node(string name)
    {
        return name switch
        {
            "root" => _rootNode,
            "container1" => _container1,
            "container2" => _container2,
            "container3" => _container3,
            "connection1" => _connection1,
            "connection3" => _connection3,
            "connection4" => _connection4,
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, null)
        };
    }

    private sealed class RecordingDropFeedback : IDropFeedback
    {
        public bool EnableFeedback { get; set; } = true;
        public Color FeedbackColor { get; set; }
    }

    private void InitializeNodes()
    {
        _rootNode = new RootNodeInfo(RootNodeType.Connection);
        _container1 = new ContainerInfo();
        _container2 = new ContainerInfo();
        _container3 = new ContainerInfo();
        _connection1 = new ConnectionInfo();
        _connection2 = new ConnectionInfo();
        _connection3 = new ConnectionInfo();
        _connection4 = new ConnectionInfo();
        _connection5 = new ConnectionInfo();
    }

    private void CreateSimpleTreeModel()
    {
        /*
         * Root
         * |-- container1
         * |   L-- connection1
         * L-- container2
         *     |-- container3
         *     |   |-- connection3
         *     |   |-- connection4
         *     |   L-- connection5
         *     L-- connection2
         */
        _rootNode.AddChild(_container1);
        _rootNode.AddChild(_container2);
        _container1.AddChild(_connection1);
        _container2.AddChild(_container3);
        _container2.AddChild(_connection2);
        _container3.AddChild(_connection3);
        _container3.AddChild(_connection4);
        _container3.AddChild(_connection5);
    }
}
