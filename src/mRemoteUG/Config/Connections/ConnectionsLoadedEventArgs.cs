using System;
using mRemoteUG.Tree;

namespace mRemoteUG.Config.Connections
{
    public class ConnectionsLoadedEventArgs : EventArgs
    {
        /// <summary>
        /// The previous <see cref="ConnectionTreeModel"/> that is being
        /// unloaded, or null on the first load, when there was none.
        /// </summary>
        public ConnectionTreeModel? PreviousConnectionTreeModel { get; }

        /// <summary>
        /// The new <see cref="ConnectionTreeModel"/> that is being loaded.
        /// </summary>
        public ConnectionTreeModel NewConnectionTreeModel { get; }

        /// <summary>
        /// The path to the new connections source (a file path to the connection file).
        /// </summary>
        public string NewSourcePath { get; }

        public ConnectionsLoadedEventArgs(
            ConnectionTreeModel? previousTreeModelModel, ConnectionTreeModel newTreeModelModel,
            string newSourcePath)
        {
            if (newTreeModelModel == null)
                throw new ArgumentNullException(nameof(newTreeModelModel));
            if (newSourcePath == null)
                throw new ArgumentNullException(nameof(newSourcePath));

            PreviousConnectionTreeModel = previousTreeModelModel;
            NewConnectionTreeModel = newTreeModelModel;
            NewSourcePath = newSourcePath;
        }
    }
}
