using System.Drawing;

namespace mRemoteUG.Tree
{
    /// <summary>
    /// Decides whether a drag hovering at a given height means "above this node",
    /// "onto this node" or "below this node". Split out as a pure function because it
    /// is the only piece of the drop geometry worth testing, and it needs no control.
    /// </summary>
    public static class TreeNodeDropLocationCalculator
    {
        public static DropTargetLocation Locate(Rectangle rowBounds, int pointY)
        {
            if (rowBounds.Height <= 0)
                return DropTargetLocation.Item;

            // A third at each edge inserts between nodes; the middle third drops onto the
            // node. Matches the feel of ObjectListView's CanDropBetween sink.
            var third = rowBounds.Height / 3;
            if (third == 0)
                third = 1;

            if (pointY < rowBounds.Top + third)
                return DropTargetLocation.AboveItem;

            if (pointY >= rowBounds.Bottom - third)
                return DropTargetLocation.BelowItem;

            return DropTargetLocation.Item;
        }
    }
}
