using mRemoteUG.Connection;


namespace mRemoteUG.Tree;

public class SwitchToConnectionClickHandler : ITreeNodeClickHandler<ConnectionInfo>
{
    private readonly IConnectionInitiator _connectionInitiator;

    public SwitchToConnectionClickHandler(IConnectionInitiator connectionInitiator)
    {
        ArgumentNullException.ThrowIfNull(connectionInitiator);

        _connectionInitiator = connectionInitiator;
    }

    public void Execute(ConnectionInfo clickedNode)
    {
        ArgumentNullException.ThrowIfNull(clickedNode);

        if (clickedNode.GetTreeNodeType() != TreeNodeType.Connection
            && clickedNode.GetTreeNodeType() != TreeNodeType.PuttySession)
        {
            return;
        }

        _connectionInitiator.SwitchToOpenConnection(clickedNode);
    }
}
