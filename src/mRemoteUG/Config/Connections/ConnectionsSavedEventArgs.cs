using System;
using mRemoteUG.Tree;

namespace mRemoteUG.Config.Connections
{
    public class ConnectionsSavedEventArgs
    {
        public ConnectionTreeModel ModelThatWasSaved { get; }
        public string ConnectionFileName { get; }

        public ConnectionsSavedEventArgs(ConnectionTreeModel modelThatWasSaved, string connectionFileName)
        {
            if (modelThatWasSaved == null)
                throw new ArgumentNullException(nameof(modelThatWasSaved));

            ModelThatWasSaved = modelThatWasSaved;
            ConnectionFileName = connectionFileName;
        }
    }
}
