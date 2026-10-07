namespace mRemoteUG.Tree
{
    /// <summary>
    /// Where a drag is hovering relative to the node under the cursor. Replaces
    /// ObjectListView's enum of the same name; only the three values the connection
    /// tree ever used are kept.
    /// </summary>
    public enum DropTargetLocation
    {
        None,
        Item,
        AboveItem,
        BelowItem
    }
}
