using mRemoteUG.Connection;
using mRemoteUG.Container;
using mRemoteUG.UI.Controls;


namespace mRemoteUG.Tree;

public class PreviousSessionOpener : IConnectionTreePostSetupAction
{
    private readonly IConnectionInitiator _connectionInitiator;

    public PreviousSessionOpener(IConnectionInitiator connectionInitiator)
    {
        ArgumentNullException.ThrowIfNull(connectionInitiator);

        _connectionInitiator = connectionInitiator;
    }

    public void Execute(IConnectionTree connectionTree)
    {
        IEnumerable<ConnectionInfo> connectionInfoList = connectionTree
            .GetRootConnectionNode()
            .GetRecursiveChildList()
            .Where(node => !(node is ContainerInfo));
        IEnumerable<ConnectionInfo> previouslyOpenedConnections = connectionInfoList
            .Where(item =>
                item.PleaseConnect &&
                // ignore items that have already connected
                !_connectionInitiator.ActiveConnections.Contains(item.ConstantID));

        foreach (ConnectionInfo connectionInfo in previouslyOpenedConnections)
        {
            _connectionInitiator.OpenConnection(connectionInfo);
        }
    }
}
