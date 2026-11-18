using mRemoteUG.Connection;
using mRemoteUG.Container;
using mRemoteUG.Tree.Root;


namespace mRemoteUG.Tree;

public class ConnectionTreeDragAndDropHandler
{
    private readonly Color _dropAllowedFeedbackColor = Color.Green;
    private readonly Color _dropDeniedFeedbackColor = Color.Red;
    private string? _infoMessage;
    private Color _currentFeedbackColor;
    private bool _enableFeedback;


    public void HandleModelDropped(object? sender, ModelDropEventArgs e)
    {
        var dropTarget = e.TargetModel as ConnectionInfo;
        if (dropTarget == null)
        {
            return;
        }

        // The tree only ever drags connections; anything else has nothing to drop.
        if (e.SourceModels[0] is not ConnectionInfo dropSource)
        {
            return;
        }

        DropModel(dropSource, dropTarget, e.DropTargetLocation);
        e.Handled = true;
    }

    public static void DropModel(ConnectionInfo dropSource, ConnectionInfo dropTarget,
                                 DropTargetLocation dropTargetLocation)
    {
        if (dropTargetLocation == DropTargetLocation.Item)
        {
            DropModelOntoTarget(dropSource, dropTarget);
        }
        else if (dropTargetLocation == DropTargetLocation.AboveItem)
        {
            DropModelAboveTarget(dropSource, dropTarget);
        }
        else if (dropTargetLocation == DropTargetLocation.BelowItem)
        {
            DropModelBelowTarget(dropSource, dropTarget);
        }
    }

    private static void DropModelOntoTarget(ConnectionInfo dropSource, ConnectionInfo dropTarget)
    {
        var dropTargetAsContainer = dropTarget as ContainerInfo;
        if (dropTargetAsContainer == null)
        {
            return;
        }

        dropSource.SetParent(dropTargetAsContainer);
    }

    // A target without a parent is a root, and there is no "beside a root" to drop into.
    // CanModelDrop already refuses that drop, so the guard only keeps a direct call from
    // dereferencing the missing parent.
    private static void DropModelAboveTarget(ConnectionInfo dropSource, ConnectionInfo dropTarget)
    {
        ContainerInfo? targetParent = dropTarget.Parent;
        if (targetParent == null)
        {
            return;
        }

        if (dropSource.Parent != targetParent)
        {
            targetParent.AddChildAbove(dropSource, dropTarget);
        }
        else
        {
            targetParent.SetChildAbove(dropSource, dropTarget);
        }
    }

    private static void DropModelBelowTarget(ConnectionInfo dropSource, ConnectionInfo dropTarget)
    {
        ContainerInfo? targetParent = dropTarget.Parent;
        if (targetParent == null)
        {
            return;
        }

        if (dropSource.Parent != targetParent)
        {
            targetParent.AddChildBelow(dropSource, dropTarget);
        }
        else
        {
            targetParent.SetChildBelow(dropSource, dropTarget);
        }
    }

    public void HandleModelCanDrop(object? sender, ModelDropEventArgs e)
    {
        _enableFeedback = true;
        _currentFeedbackColor = _dropDeniedFeedbackColor;
        _infoMessage = null;
        ConnectionInfo dropSource = e.SourceModels.Cast<ConnectionInfo>().First();
        var dropTarget = e.TargetModel as ConnectionInfo;

        e.Effect = CanModelDrop(dropSource, dropTarget, e.DropTargetLocation);
        e.InfoMessage = _infoMessage;
        e.DropSink.EnableFeedback = _enableFeedback;
        e.DropSink.FeedbackColor = _currentFeedbackColor;
        e.Handled = true;
    }

    public DragDropEffects CanModelDrop(
        ConnectionInfo dropSource,
        ConnectionInfo? dropTarget,
        DropTargetLocation dropTargetLocation)
    {
        DragDropEffects dragDropEffect = DragDropEffects.None;
        if (!NodeIsDraggable(dropSource))
        {
            _infoMessage = Language.strNodeNotDraggable;
            _enableFeedback = false;
        }
        else if (dropTargetLocation == DropTargetLocation.Item)
        {
            dragDropEffect = HandleCanDropOnItem(dropSource, dropTarget);
        }
        else if (dropTargetLocation == DropTargetLocation.AboveItem
                 || dropTargetLocation == DropTargetLocation.BelowItem)
        {
            dragDropEffect = HandleCanDropBetweenItems(dropSource, dropTarget);
        }

        return dragDropEffect;
    }

    private DragDropEffects HandleCanDropOnItem(ConnectionInfo dropSource, ConnectionInfo? dropTarget)
    {
        DragDropEffects dragDropEffect = DragDropEffects.None;
        if (dropTarget is ContainerInfo && !(dropTarget is RootPuttySessionsNodeInfo))
        {
            if (!IsValidDrag(dropSource, dropTarget))
            {
                return dragDropEffect;
            }

            dragDropEffect = DragDropEffects.Move;
            _currentFeedbackColor = _dropAllowedFeedbackColor;
        }
        else
        {
            _enableFeedback = false;
        }
        return dragDropEffect;
    }

    private DragDropEffects HandleCanDropBetweenItems(ConnectionInfo dropSource, ConnectionInfo? dropTarget)
    {
        DragDropEffects dragDropEffect = DragDropEffects.None;
        if (AncestorDraggingOntoChild(dropSource, dropTarget))
        {
            _infoMessage = Language.strNodeCannotDragParentOnChild;
        }
        else if (dropTarget is PuttySessionInfo || dropTarget is RootNodeInfo)
        {
            _enableFeedback = false;
        }
        else
        {
            dragDropEffect = DragDropEffects.Move;
            _currentFeedbackColor = _dropAllowedFeedbackColor;
        }
        return dragDropEffect;
    }

    private bool IsValidDrag(ConnectionInfo dropSource, ConnectionInfo dropTarget)
    {
        bool validDrag = false;
        if (NodeDraggingOntoSelf(dropSource, dropTarget))
        {
            _infoMessage = Language.strNodeCannotDragOnSelf;
        }
        else if (AncestorDraggingOntoChild(dropSource, dropTarget))
        {
            _infoMessage = Language.strNodeCannotDragParentOnChild;
        }
        else if (DraggingOntoCurrentParent(dropSource, dropTarget))
        {
            _infoMessage = Language.strNodeAlreadyInFolder;
        }
        else
        {
            validDrag = true;
        }

        return validDrag;
    }

    private static bool NodeIsDraggable(ConnectionInfo? node)
    {
        return node != null && !(node is RootNodeInfo) && !(node is PuttySessionInfo);
    }

    private static bool NodeDraggingOntoSelf(ConnectionInfo source, ConnectionInfo target)
    {
        return source.Equals(target);
    }

    private static bool AncestorDraggingOntoChild(ConnectionInfo source, ConnectionInfo? target)
    {
        var sourceAsContainer = source as ContainerInfo;
        return target != null
               && sourceAsContainer != null
               && sourceAsContainer.GetRecursiveChildList().Contains(target);
    }

    private static bool DraggingOntoCurrentParent(ConnectionInfo source, ConnectionInfo target)
    {
        var targetAsContainer = target as ContainerInfo;
        return targetAsContainer != null && targetAsContainer.Children.Contains(source);
    }
}
