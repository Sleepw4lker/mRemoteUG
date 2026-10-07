using System;
using System.Collections.Specialized;
using System.ComponentModel;
using mRemoteUG.Connection;
using mRemoteUG.UI.Forms;

namespace mRemoteUG.Config.Connections
{
    public class SaveConnectionsOnEdit
    {
        private readonly ConnectionsService _connectionsService;

        public SaveConnectionsOnEdit(ConnectionsService connectionsService)
        {
            if (connectionsService == null)
                throw new ArgumentNullException(nameof(connectionsService));

            _connectionsService = connectionsService;
            connectionsService.ConnectionsLoaded += ConnectionsServiceOnConnectionsLoaded;
        }

        private void ConnectionsServiceOnConnectionsLoaded(object? sender, ConnectionsLoadedEventArgs connectionsLoadedEventArgs)
        {
            connectionsLoadedEventArgs.NewConnectionTreeModel.CollectionChanged += ConnectionTreeModelOnCollectionChanged;
            connectionsLoadedEventArgs.NewConnectionTreeModel.PropertyChanged += ConnectionTreeModelOnPropertyChanged;

            var oldTree = connectionsLoadedEventArgs.PreviousConnectionTreeModel;
            if (oldTree != null)
            {
                oldTree.CollectionChanged -= ConnectionTreeModelOnCollectionChanged;
                oldTree.PropertyChanged -= ConnectionTreeModelOnPropertyChanged;
            }
        }

        private void ConnectionTreeModelOnPropertyChanged(object? sender, PropertyChangedEventArgs propertyChangedEventArgs)
        {
            // Opening or closing a session is transient UI state, not an edit. Saving here made
            // every connect/disconnect re-serialize and re-encrypt the whole connections file
            // (PBKDF2 per secret) while the session was still tearing down. The "Connected" flag
            // is still persisted on exit: frmMain_FormClosing runs Shutdown.Cleanup ->
            // SaveConnections() before the connection windows are closed, so PreviousSessionOpener
            // still reopens last session's connections.
            if (propertyChangedEventArgs.PropertyName == nameof(ConnectionInfo.OpenConnections))
                return;

            SaveConnectionOnEdit(propertyChangedEventArgs.PropertyName);
        }

        private void ConnectionTreeModelOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs notifyCollectionChangedEventArgs)
        {
            SaveConnectionOnEdit();
        }

        private void SaveConnectionOnEdit(string propertyName = "")
        {
            if (!mRemoteUG.Settings.Default.SaveConnectionsAfterEveryEdit)
                return;
            if (FrmMain.Default.IsClosing)
                return;

            _connectionsService.SaveConnectionsAsync(propertyName);
        }
    }
}
