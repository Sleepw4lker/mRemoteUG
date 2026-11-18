using mRemoteUG.Connection;


namespace mRemoteUG.Tree;

public class OpenConnectionClickHandler : ITreeNodeClickHandler<ConnectionInfo>
{
    private readonly IConnectionInitiator _connectionInitiator;

    public OpenConnectionClickHandler(IConnectionInitiator connectionInitiator)
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

        _connectionInitiator.OpenConnection(clickedNode);
    }
}
