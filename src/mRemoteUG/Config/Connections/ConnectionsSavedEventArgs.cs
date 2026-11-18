using mRemoteUG.Tree;

namespace mRemoteUG.Config.Connections;

public class ConnectionsSavedEventArgs : EventArgs
{
    public ConnectionTreeModel ModelThatWasSaved { get; }
    public string ConnectionFileName { get; }

    public ConnectionsSavedEventArgs(ConnectionTreeModel modelThatWasSaved, string connectionFileName)
    {
        ArgumentNullException.ThrowIfNull(modelThatWasSaved);

        ModelThatWasSaved = modelThatWasSaved;
        ConnectionFileName = connectionFileName;
    }
}
