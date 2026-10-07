using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using mRemoteUG.App;
using mRemoteUG.App.Info;
using mRemoteUG.Config.Connections;
using mRemoteUG.Config.Putty;
using mRemoteUG.Connection.Protocol;
using mRemoteUG.Messages;
using mRemoteUG.Security;
using mRemoteUG.Tools;
using mRemoteUG.Tree;
using mRemoteUG.Tree.Root;
using mRemoteUG.UI;

namespace mRemoteUG.Connection
{
	public class ConnectionsService
    {
        private static readonly object SaveLock = new object();

        private readonly PuttySessionsManager _puttySessionsManager;

        public ConnectionsService()
            : this(PuttySessionsManager.Instance)
        {
        }

        public ConnectionsService(PuttySessionsManager puttySessionsManager)
        {
            _puttySessionsManager = puttySessionsManager.ThrowIfNull(nameof(puttySessionsManager));
        }

        private bool _batchingSaves = false;
        private bool _saveRequested = false;
        private bool _saveAsyncRequested = false;

        public bool IsConnectionsFileLoaded { get; set; }
        public string ConnectionFileName { get; private set; }

        public ConnectionTreeModel ConnectionTreeModel { get; private set; }

        public void NewConnectionsFile(string filename)
        {
            try
            {
                filename.ThrowIfNullOrEmpty(nameof(filename));
                var newConnectionsModel = new ConnectionTreeModel();
                newConnectionsModel.AddRootNode(new RootNodeInfo(RootNodeType.Connection));
                SaveConnections(newConnectionsModel, new SaveFilter(), filename, true);
                LoadConnections(false, filename);
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage(Language.strCouldNotCreateNewConnectionsFile, ex);
            }
        }

        public ConnectionInfo CreateQuickConnect(string connectionString, ProtocolType protocol)
        {
            try
            {
                var uri = new Uri("dummyscheme" + Uri.SchemeDelimiter + connectionString);
                if (string.IsNullOrEmpty(uri.Host)) return null;

                var newConnectionInfo = new ConnectionInfo();
                newConnectionInfo.CopyFrom(DefaultConnectionInfo.Instance);

                newConnectionInfo.Name = Settings.Default.IdentifyQuickConnectTabs ? string.Format(Language.strQuick, uri.Host) : uri.Host;

                newConnectionInfo.Protocol = protocol;
                newConnectionInfo.Hostname = uri.Host;
                if (uri.Port == -1)
                {
                    newConnectionInfo.SetDefaultPort();
                }
                else
                {
                    newConnectionInfo.Port = uri.Port;
                }

                if (string.IsNullOrEmpty(newConnectionInfo.Panel))
                    newConnectionInfo.Panel = Language.strGeneral;

                newConnectionInfo.IsQuickConnect = true;

                return newConnectionInfo;
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage(Language.strQuickConnectFailed, ex);
                return null;
            }
        }

        /// <summary>
        /// Load connections from a file.
        /// </summary>
        /// <param name="import"></param>
        /// <param name="connectionFileName"></param>
        public void LoadConnections(bool import, string connectionFileName)
        {
            var oldConnectionTreeModel = ConnectionTreeModel;

            var connectionLoader = new XmlConnectionsLoader(connectionFileName);

            var newConnectionTreeModel = connectionLoader.Load();

            if (newConnectionTreeModel == null)
            {
                DialogFactory.ShowLoadConnectionsFailedDialog(connectionFileName, "Decrypting connection file failed", IsConnectionsFileLoaded);
                return;
            }

            IsConnectionsFileLoaded = true;
            ConnectionFileName = connectionFileName;

            if (!import)
            {
                _puttySessionsManager.AddSessions();
                newConnectionTreeModel.RootNodes.AddRange(_puttySessionsManager.RootPuttySessionsNodes);
            }

            ConnectionTreeModel = newConnectionTreeModel;
            UpdateCustomConsPathSetting(connectionFileName);
            RaiseConnectionsLoadedEvent(oldConnectionTreeModel, newConnectionTreeModel, connectionFileName);
            Runtime.MessageCollector.AddMessage(MessageClass.DebugMsg, $"Connections loaded using {connectionLoader.GetType().Name}");
        }

        /// <summary>
        /// When turned on, calls to <see cref="SaveConnections()"/> or
        /// <see cref="SaveConnectionsAsync"/> will not immediately execute.
        /// Instead, they will be deferred until <see cref="EndBatchingSaves"/>
        /// is called.
        /// </summary>
        public void BeginBatchingSaves()
        {
            _batchingSaves = true;
        }

        /// <summary>
        /// Immediately executes a single <see cref="SaveConnections()"/> or
        /// <see cref="SaveConnectionsAsync"/> if one has been requested
        /// since calling <see cref="BeginBatchingSaves"/>.
        /// </summary>
        public void EndBatchingSaves()
        {
            _batchingSaves = false;

            if (_saveAsyncRequested)
                SaveConnectionsAsync();
            else if(_saveRequested)
                SaveConnections();
        }

		/// <summary>
		/// All calls to <see cref="SaveConnections()"/> or <see cref="SaveConnectionsAsync"/>
		/// will be deferred until the returned <see cref="DisposableAction"/> is disposed.
		/// Once disposed, this will immediately executes a single <see cref="SaveConnections()"/>
		/// or <see cref="SaveConnectionsAsync"/> if one has been requested.
		/// Place this call in a 'using' block to represent a batched saving context.
		/// </summary>
		/// <returns></returns>
		public DisposableAction BatchedSavingContext()
        {
			return new DisposableAction(BeginBatchingSaves, EndBatchingSaves);
        }

        /// <summary>
        /// Saves the currently loaded <see cref="ConnectionTreeModel"/> with
        /// no <see cref="SaveFilter"/>.
        /// </summary>
        public void SaveConnections()
        {
            SaveConnections(ConnectionTreeModel, new SaveFilter(), ConnectionFileName);
        }

        /// <summary>
        /// Saves the given <see cref="ConnectionTreeModel"/>.
        /// </summary>
        /// <param name="connectionTreeModel"></param>
        /// <param name="saveFilter"></param>
        /// <param name="connectionFileName"></param>
        /// <param name="forceSave">Bypasses safety checks that prevent saving if a connection file isn't loaded.</param>
        /// <param name="propertyNameTrigger">
        /// Optional. The name of the property that triggered
        /// this save.
        /// </param>
        public void SaveConnections(
            ConnectionTreeModel connectionTreeModel,
            SaveFilter saveFilter,
            string connectionFileName,
            bool forceSave = false,
            string propertyNameTrigger = "")
        {
            if (connectionTreeModel == null)
                return;

            if (!forceSave && !IsConnectionsFileLoaded)
                return;

            if (_batchingSaves)
            {
                _saveRequested = true;
                return;
            }

            try
            {
                // Debug: this fires on every save, and the result line below is the one that says
                // anything a reader did not already know.
                Runtime.MessageCollector.AddMessage(MessageClass.DebugMsg, "Saving connections...");
                var stopwatch = Stopwatch.StartNew();

                var saver = new XmlConnectionsSaver(connectionFileName, saveFilter);
                saver.Save(connectionTreeModel, propertyNameTrigger);

                ConnectionFileName = connectionFileName;
                RaiseConnectionsSavedEvent(connectionTreeModel, connectionFileName);
                Runtime.MessageCollector.AddMessage(MessageClass.InformationMsg,
                    $"Successfully saved connections in {stopwatch.ElapsedMilliseconds} ms (trigger: '{propertyNameTrigger}')");
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector?.AddExceptionMessage(string.Format(Language.strConnectionsFileCouldNotSaveAs, connectionFileName), ex, logOnly:false);
            }
        }

        /// <summary>
        /// Save the currently loaded connections asynchronously
        /// </summary>
        /// <param name="propertyNameTrigger">
        /// Optional. The name of the property that triggered
        /// this save.
        /// </param>
        public void SaveConnectionsAsync(string propertyNameTrigger = "")
        {
            if (_batchingSaves)
            {
                _saveAsyncRequested = true;
                return;
            }

            var t = new Thread(() =>
            {
                lock (SaveLock)
                {
                    SaveConnections(
                        ConnectionTreeModel,
                        new SaveFilter(),
                        ConnectionFileName,
                        propertyNameTrigger: propertyNameTrigger);
                }
            });
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
        }

        public string GetStartupConnectionFileName()
        {
            return Settings.Default.LoadConsFromCustomLocation == false
                ? GetDefaultStartupConnectionFileName()
                : Settings.Default.CustomConsPath;
        }

        public string GetDefaultStartupConnectionFileName()
        {
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Application.ProductName,
                ConnectionsFileInfo.DefaultConnectionsFile);
            return File.Exists(appDataPath) ? appDataPath : GetConnectionFileNameNextToExecutable();
        }

        private void UpdateCustomConsPathSetting(string filename)
        {
            if (filename == GetDefaultStartupConnectionFileName())
            {
                Settings.Default.LoadConsFromCustomLocation = false;
            }
            else
            {
                Settings.Default.LoadConsFromCustomLocation = true;
                Settings.Default.CustomConsPath = filename;
            }
        }

        private string GetConnectionFileNameNextToExecutable()
        {
            return Path.Combine(ConnectionsFileInfo.DefaultConnectionsPath, ConnectionsFileInfo.DefaultConnectionsFile);
        }

        #region Events
        public event EventHandler<ConnectionsLoadedEventArgs> ConnectionsLoaded;
        public event EventHandler<ConnectionsSavedEventArgs> ConnectionsSaved;

        private void RaiseConnectionsLoadedEvent(ConnectionTreeModel? previousTreeModel, ConnectionTreeModel newTreeModel,
            string newSourcePath)
        {
            ConnectionsLoaded?.Invoke(this, new ConnectionsLoadedEventArgs(
                previousTreeModel,
                newTreeModel,
                newSourcePath));
        }

        private void RaiseConnectionsSavedEvent(ConnectionTreeModel modelThatWasSaved, string connectionFileName)
        {
            ConnectionsSaved?.Invoke(this, new ConnectionsSavedEventArgs(modelThatWasSaved, connectionFileName));
        }
        #endregion
    }
}
