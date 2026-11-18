using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

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

namespace mRemoteUG.Connection;

public class ConnectionsService
{
    private static readonly object s_saveLock = new object();

    private readonly PuttySessionsManager _puttySessionsManager;

    public ConnectionsService()
        : this(PuttySessionsManager.Instance)
    {
    }

    public ConnectionsService(PuttySessionsManager puttySessionsManager)
    {
        _puttySessionsManager = puttySessionsManager.ThrowIfNull(nameof(puttySessionsManager));
    }

    /// <summary>
    /// 1 while the most recent save failed, 0 otherwise. An int for Interlocked: saves run on
    /// autosave threads under s_saveLock, but SaveConnections() is also called directly on the
    /// UI thread, outside that lock.
    /// </summary>
    private int _lastSaveFailed;

    /// <summary>
    /// 1 once a save skipped because no file is loaded has been logged, 0 again when a file is
    /// loaded. An int for Interlocked, for the same reason as <see cref="_lastSaveFailed"/>.
    /// </summary>
    private int _skippedSaveLogged;

    private bool _batchingSaves;
    private bool _saveRequested;
    private bool _saveAsyncRequested;

    /// <summary>
    /// The model in use came from a file and may be saved. Only <see cref="LoadConnections"/>
    /// sets it, when a load succeeds; a load or import that fails leaves it as it was, because
    /// the model in use does not change.
    /// </summary>
    public bool IsConnectionsFileLoaded { get; set; }
    // The setter is internal only so a test that saves through Runtime.ConnectionsService can
    // put the process-wide value back afterwards.
    public string? ConnectionFileName { get; internal set; }

    public ConnectionTreeModel? ConnectionTreeModel { get; private set; }

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

    [SuppressMessage("Performance", "CA1863",
                     Justification = "The format is a localized resource read at run time; "
                                     + "a cached CompositeFormat would freeze the UI language.")]
    public static ConnectionInfo? CreateQuickConnect(string connectionString, ProtocolType protocol)
    {
        try
        {
            var uri = new Uri("dummyscheme" + Uri.SchemeDelimiter + connectionString);
            if (string.IsNullOrEmpty(uri.Host))
            {
                return null;
            }

            var newConnectionInfo = new ConnectionInfo();
            newConnectionInfo.CopyFrom(DefaultConnectionInfo.Instance);

            newConnectionInfo.Name = Settings.Default.IdentifyQuickConnectTabs
                ? string.Format(CultureInfo.CurrentCulture, Language.strQuick, uri.Host)
                : uri.Host;

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
            {
                newConnectionInfo.Panel = Language.strGeneral;
            }

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
        ConnectionTreeModel? oldConnectionTreeModel = ConnectionTreeModel;

        var connectionLoader = new XmlConnectionsLoader(connectionFileName);

        ConnectionTreeModel? newConnectionTreeModel = connectionLoader.Load();

        if (newConnectionTreeModel == null)
        {
            DialogFactory.ShowLoadConnectionsFailedDialog(
                connectionFileName,
                "Decrypting connection file failed",
                IsConnectionsFileLoaded);
            return;
        }

        IsConnectionsFileLoaded = true;
        Interlocked.Exchange(ref _skippedSaveLogged, 0);
        ConnectionFileName = connectionFileName;

        if (!import)
        {
            _puttySessionsManager.AddSessions();
            newConnectionTreeModel.RootNodes.AddRange(_puttySessionsManager.RootPuttySessionsNodes);
        }

        ConnectionTreeModel = newConnectionTreeModel;
        UpdateCustomConsPathSetting(connectionFileName);
        RaiseConnectionsLoadedEvent(oldConnectionTreeModel, newConnectionTreeModel, connectionFileName);
        Runtime.MessageCollector.AddMessage(
            MessageClass.DebugMsg,
            $"Connections loaded using {connectionLoader.GetType().Name}");
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
        {
            SaveConnectionsAsync();
        }
        else if (_saveRequested)
        {
            SaveConnections();
        }
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
        // Both are set together by the first load, so there is nothing to save before it.
        if (ConnectionFileName == null)
        {
            return;
        }

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
    [SuppressMessage("Performance", "CA1863",
                     Justification = "The format is a localized resource read at run time; "
                                     + "a cached CompositeFormat would freeze the UI language.")]
    public void SaveConnections(
        ConnectionTreeModel? connectionTreeModel,
        SaveFilter saveFilter,
        string connectionFileName,
        bool forceSave = false,
        string propertyNameTrigger = "")
    {
        if (connectionTreeModel == null)
        {
            return;
        }

        if (!forceSave && !IsConnectionsFileLoaded)
        {
            // Logged, not shown: autosave runs after every edit, so this says it once until a
            // file is loaded. Without it, a cleared mark lost every later edit without a trace.
            if (Interlocked.Exchange(ref _skippedSaveLogged, 1) == 0)
            {
                Runtime.MessageCollector.AddMessage(
                    MessageClass.WarningMsg,
                    $"Save skipped: no connections file is loaded (target: '{connectionFileName}', "
                    + $"trigger: '{propertyNameTrigger}')",
                    onlyLog: true);
            }

            return;
        }

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

            Interlocked.Exchange(ref _lastSaveFailed, 0);
            ConnectionFileName = connectionFileName;
            RaiseConnectionsSavedEvent(connectionTreeModel, connectionFileName);
            Runtime.MessageCollector.AddMessage(MessageClass.InformationMsg,
                $"Successfully saved connections in {stopwatch.ElapsedMilliseconds} ms (trigger: '"
                + $"{propertyNameTrigger}')");
        }
        catch (Exception ex)
        {
            // Autosave runs after every edit, so a failure that persists - a read-only or locked
            // file, a full disk - would otherwise put the same box up after every edit. The first
            // failure in a row is shown; the rest are logged only, until a save succeeds again.
            bool alreadyFailing = Interlocked.Exchange(ref _lastSaveFailed, 1) == 1;
            Runtime.MessageCollector?.AddExceptionMessage(
                string.Format(
                    CultureInfo.CurrentCulture,
                    Language.strConnectionsFileCouldNotSaveAs,
                    connectionFileName),
                ex,
                logOnly: alreadyFailing);
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
            lock (s_saveLock)
            {
                if (ConnectionFileName == null)
                {
                    return;
                }

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

    public static string GetStartupConnectionFileName()
    {
        return Settings.Default.LoadConsFromCustomLocation == false
            ? GetDefaultStartupConnectionFileName()
            : Settings.Default.CustomConsPath;
    }

    public static string GetDefaultStartupConnectionFileName()
    {
        string appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            // Not null: ProductName reads [AssemblyProduct] first, and Properties/AssemblyInfo.cs sets it.
            Application.ProductName!,
            ConnectionsFileInfo.DefaultConnectionsFile);
        return File.Exists(appDataPath) ? appDataPath : GetConnectionFileNameNextToExecutable();
    }

    private static void UpdateCustomConsPathSetting(string filename)
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

    private static string GetConnectionFileNameNextToExecutable()
    {
        return Path.Combine(ConnectionsFileInfo.DefaultConnectionsPath, ConnectionsFileInfo.DefaultConnectionsFile);
    }

    #region Events
    public event EventHandler<ConnectionsLoadedEventArgs>? ConnectionsLoaded;
    public event EventHandler<ConnectionsSavedEventArgs>? ConnectionsSaved;

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
