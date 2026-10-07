namespace mRemoteUG.Tree
{
    /// <summary>
    /// Decides whether a single tree node matches the active filter. Deliberately free
    /// of any UI dependency so filtering can be unit tested without a control.
    /// </summary>
    public interface IConnectionTreeNodeFilter
    {
        bool Filter(object modelObject);
    }
}
