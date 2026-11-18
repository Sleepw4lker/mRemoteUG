using mRemoteUG.Connection;


namespace mRemoteUG.Tree;

public class TreeNodeCompositeClickHandler : ITreeNodeClickHandler<ConnectionInfo>
{
    public IEnumerable<ITreeNodeClickHandler<ConnectionInfo>> ClickHandlers { get; set; } =
        Array.Empty<ITreeNodeClickHandler<ConnectionInfo>>();

    public void Execute(ConnectionInfo clickedNode)
    {
        ArgumentNullException.ThrowIfNull(clickedNode);

        foreach (ITreeNodeClickHandler<ConnectionInfo> handler in ClickHandlers)
        {
            handler.Execute(clickedNode);
        }
    }
}
