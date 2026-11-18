using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

using mRemoteUG.App;
using mRemoteUG.Connection;
using mRemoteUG.Connection.Protocol;
using mRemoteUG.Container;
using mRemoteUG.Tree;
using mRemoteUG.Tree.Root;
// ReSharper disable UnusedParameter.Local


namespace mRemoteUG.UI.Controls;

public sealed class ConnectionContextMenu : ContextMenuStrip
{
    private ToolStripMenuItem _cMenTreeAddConnection;
    private ToolStripMenuItem _cMenTreeAddFolder;
    private ToolStripSeparator _cMenTreeSep1;
    private ToolStripMenuItem _cMenTreeConnect;
    private ToolStripMenuItem _cMenTreeConnectWithOptions;
    private ToolStripMenuItem _cMenTreeConnectWithOptionsConnectToConsoleSession;
    private ToolStripMenuItem _cMenTreeConnectWithOptionsNoCredentials;
    private ToolStripMenuItem _cMenTreeConnectWithOptionsConnectInFullscreen;
    private ToolStripMenuItem _cMenTreeDisconnect;
    private ToolStripSeparator _cMenTreeSep2;
    private ToolStripMenuItem _cMenTreeToolsSort;
    private ToolStripMenuItem _cMenTreeToolsSortAscending;
    private ToolStripMenuItem _cMenTreeToolsSortDescending;
    private ToolStripSeparator _cMenTreeSep3;
    private ToolStripMenuItem _cMenTreeRename;
    private ToolStripMenuItem _cMenTreeDelete;
    private ToolStripSeparator _cMenTreeSep4;
    private ToolStripMenuItem _cMenTreeMoveUp;
    private ToolStripMenuItem _cMenTreeMoveDown;
    private ToolStripMenuItem _cMenTreeDuplicate;
    private ToolStripMenuItem _cMenTreeConnectWithOptionsChoosePanelBeforeConnecting;
    private ToolStripMenuItem _cMenTreeConnectWithOptionsDontConnectToConsoleSession;
    private ToolStripMenuItem _cMenTreeImport;
    private ToolStripMenuItem _cMenTreeExportFile;
    private ToolStripSeparator _toolStripSeparator1;
    private ToolStripMenuItem _cMenTreeImportFile;
    private readonly ConnectionTree _connectionTree;
    private readonly ConnectionInitiator _connectionInitiator;

    // The six items with shortcut keys (EnableShortcutKeys), for the tests.
    internal ToolStripMenuItem ConnectItem => _cMenTreeConnect;
    internal ToolStripMenuItem DuplicateItem => _cMenTreeDuplicate;
    internal ToolStripMenuItem RenameItem => _cMenTreeRename;
    internal ToolStripMenuItem DeleteItem => _cMenTreeDelete;
    internal ToolStripMenuItem MoveUpItem => _cMenTreeMoveUp;
    internal ToolStripMenuItem MoveDownItem => _cMenTreeMoveDown;


    public ConnectionContextMenu(ConnectionTree connectionTree)
    {
        _connectionTree = connectionTree;
        _connectionInitiator = new ConnectionInitiator();
        InitializeComponent();
        ApplyLanguage();
        EnableShortcutKeys();
        // Nothing is selected yet; the tree keeps this up to date as its selection changes.
        ShowHideMenuItems();
        Opening += (sender, args) =>
        {
            if (_connectionTree.SelectedConnection == null)
            {
                args.Cancel = true;
                return;
            }
            ShowHideMenuItems();
        };
    }

    [MemberNotNull(
        nameof(_cMenTreeAddConnection), nameof(_cMenTreeAddFolder), nameof(_cMenTreeSep1),
        nameof(_cMenTreeConnect), nameof(_cMenTreeConnectWithOptions),
        nameof(_cMenTreeConnectWithOptionsConnectToConsoleSession),
        nameof(_cMenTreeConnectWithOptionsNoCredentials),
        nameof(_cMenTreeConnectWithOptionsConnectInFullscreen), nameof(_cMenTreeDisconnect),
        nameof(_cMenTreeSep2), nameof(_cMenTreeToolsSort), nameof(_cMenTreeToolsSortAscending),
        nameof(_cMenTreeToolsSortDescending), nameof(_cMenTreeSep3), nameof(_cMenTreeRename),
        nameof(_cMenTreeDelete), nameof(_cMenTreeSep4), nameof(_cMenTreeMoveUp), nameof(_cMenTreeMoveDown),
        nameof(_cMenTreeDuplicate), nameof(_cMenTreeConnectWithOptionsChoosePanelBeforeConnecting),
        nameof(_cMenTreeConnectWithOptionsDontConnectToConsoleSession), nameof(_cMenTreeImport),
        nameof(_cMenTreeExportFile), nameof(_toolStripSeparator1), nameof(_cMenTreeImportFile))]
    private void InitializeComponent()
    {
        _cMenTreeConnect = new ToolStripMenuItem();
        _cMenTreeConnectWithOptions = new ToolStripMenuItem();
        _cMenTreeConnectWithOptionsConnectToConsoleSession = new ToolStripMenuItem();
        _cMenTreeConnectWithOptionsDontConnectToConsoleSession = new ToolStripMenuItem();
        _cMenTreeConnectWithOptionsConnectInFullscreen = new ToolStripMenuItem();
        _cMenTreeConnectWithOptionsNoCredentials = new ToolStripMenuItem();
        _cMenTreeConnectWithOptionsChoosePanelBeforeConnecting = new ToolStripMenuItem();
        _cMenTreeDisconnect = new ToolStripMenuItem();
        _cMenTreeSep1 = new ToolStripSeparator();
        _cMenTreeSep2 = new ToolStripSeparator();
        _cMenTreeDuplicate = new ToolStripMenuItem();
        _cMenTreeRename = new ToolStripMenuItem();
        _cMenTreeDelete = new ToolStripMenuItem();
        _cMenTreeSep3 = new ToolStripSeparator();
        _cMenTreeImport = new ToolStripMenuItem();
        _cMenTreeImportFile = new ToolStripMenuItem();
        _cMenTreeExportFile = new ToolStripMenuItem();
        _cMenTreeSep4 = new ToolStripSeparator();
        _cMenTreeAddConnection = new ToolStripMenuItem();
        _cMenTreeAddFolder = new ToolStripMenuItem();
        _toolStripSeparator1 = new ToolStripSeparator();
        _cMenTreeToolsSort = new ToolStripMenuItem();
        _cMenTreeToolsSortAscending = new ToolStripMenuItem();
        _cMenTreeToolsSortDescending = new ToolStripMenuItem();
        _cMenTreeMoveUp = new ToolStripMenuItem();
        _cMenTreeMoveDown = new ToolStripMenuItem();


        // 
        // cMenTree
        // 
        Items.AddRange(new ToolStripItem[] {
            _cMenTreeConnect,
            _cMenTreeConnectWithOptions,
            _cMenTreeDisconnect,
            _cMenTreeSep1,
            _cMenTreeSep2,
            _cMenTreeDuplicate,
            _cMenTreeRename,
            _cMenTreeDelete,
            _cMenTreeSep3,
            _cMenTreeImport,
            _cMenTreeExportFile,
            _cMenTreeSep4,
            _cMenTreeAddConnection,
            _cMenTreeAddFolder,
            _toolStripSeparator1,
            _cMenTreeToolsSort,
            _cMenTreeMoveUp,
            _cMenTreeMoveDown
        });
        Name = "cMenTree";
        Size = new System.Drawing.Size(200, 364);
        // 
        // cMenTreeConnect
        // 
        _cMenTreeConnect.Image = Resources.Play;
        _cMenTreeConnect.Name = "_cMenTreeConnect";
        _cMenTreeConnect.Size = new System.Drawing.Size(199, 22);
        _cMenTreeConnect.Text = "Connect";
        _cMenTreeConnect.Click += OnConnectClicked;
        // 
        // cMenTreeConnectWithOptions
        // 
        _cMenTreeConnectWithOptions.DropDownItems.AddRange(new ToolStripItem[] {
            _cMenTreeConnectWithOptionsConnectToConsoleSession,
            _cMenTreeConnectWithOptionsDontConnectToConsoleSession,
            _cMenTreeConnectWithOptionsConnectInFullscreen,
            _cMenTreeConnectWithOptionsNoCredentials,
            _cMenTreeConnectWithOptionsChoosePanelBeforeConnecting
        });
        _cMenTreeConnectWithOptions.Name = "_cMenTreeConnectWithOptions";
        _cMenTreeConnectWithOptions.Size = new System.Drawing.Size(199, 22);
        _cMenTreeConnectWithOptions.Text = "Connect (with options)";
        // 
        // cMenTreeConnectWithOptionsConnectToConsoleSession
        // 
        _cMenTreeConnectWithOptionsConnectToConsoleSession.Image = Resources.monitor_go;
        _cMenTreeConnectWithOptionsConnectToConsoleSession.Name = "_cMenTreeConnectWithOptionsConnectToConsoleSession";
        _cMenTreeConnectWithOptionsConnectToConsoleSession.Size = new System.Drawing.Size(245, 22);
        _cMenTreeConnectWithOptionsConnectToConsoleSession.Text = "Connect to console session";
        _cMenTreeConnectWithOptionsConnectToConsoleSession.Click += OnConnectToConsoleSessionClicked;
        // 
        // cMenTreeConnectWithOptionsDontConnectToConsoleSession
        // 
        _cMenTreeConnectWithOptionsDontConnectToConsoleSession.Image = Resources.monitor_delete;
        _cMenTreeConnectWithOptionsDontConnectToConsoleSession.Name =
            "_cMenTreeConnectWithOptionsDontConnectToConsoleSession";
        _cMenTreeConnectWithOptionsDontConnectToConsoleSession.Size = new System.Drawing.Size(245, 22);
        _cMenTreeConnectWithOptionsDontConnectToConsoleSession.Text = "Don\'t connect to console session";
        _cMenTreeConnectWithOptionsDontConnectToConsoleSession.Visible = false;
        _cMenTreeConnectWithOptionsDontConnectToConsoleSession.Click += OnDontConnectToConsoleSessionClicked;
        // 
        // cMenTreeConnectWithOptionsConnectInFullscreen
        // 
        _cMenTreeConnectWithOptionsConnectInFullscreen.Image = Resources.arrow_out;
        _cMenTreeConnectWithOptionsConnectInFullscreen.Name = "_cMenTreeConnectWithOptionsConnectInFullscreen";
        _cMenTreeConnectWithOptionsConnectInFullscreen.Size = new System.Drawing.Size(245, 22);
        _cMenTreeConnectWithOptionsConnectInFullscreen.Text = "Connect in fullscreen";
        _cMenTreeConnectWithOptionsConnectInFullscreen.Click += OnConnectInFullscreenClicked;
        // 
        // cMenTreeConnectWithOptionsNoCredentials
        // 
        _cMenTreeConnectWithOptionsNoCredentials.Image = Resources.key_delete;
        _cMenTreeConnectWithOptionsNoCredentials.Name = "_cMenTreeConnectWithOptionsNoCredentials";
        _cMenTreeConnectWithOptionsNoCredentials.Size = new System.Drawing.Size(245, 22);
        _cMenTreeConnectWithOptionsNoCredentials.Text = "Connect without credentials";
        _cMenTreeConnectWithOptionsNoCredentials.Click += OnConnectWithNoCredentialsClick;
        // 
        // cMenTreeConnectWithOptionsChoosePanelBeforeConnecting
        // 
        _cMenTreeConnectWithOptionsChoosePanelBeforeConnecting.Image = Resources.Panels;
        _cMenTreeConnectWithOptionsChoosePanelBeforeConnecting.Name =
            "_cMenTreeConnectWithOptionsChoosePanelBeforeConnecting";
        _cMenTreeConnectWithOptionsChoosePanelBeforeConnecting.Size = new System.Drawing.Size(245, 22);
        _cMenTreeConnectWithOptionsChoosePanelBeforeConnecting.Text = "Choose panel before connecting";
        _cMenTreeConnectWithOptionsChoosePanelBeforeConnecting.Click += OnChoosePanelBeforeConnectingClicked;
        // 
        // cMenTreeDisconnect
        // 
        _cMenTreeDisconnect.Image = Resources.Pause;
        _cMenTreeDisconnect.Name = "_cMenTreeDisconnect";
        _cMenTreeDisconnect.Size = new System.Drawing.Size(199, 22);
        _cMenTreeDisconnect.Text = "Disconnect";
        _cMenTreeDisconnect.Click += OnDisconnectClicked;
        // 
        // cMenTreeSep1
        // 
        _cMenTreeSep1.Name = "_cMenTreeSep1";
        _cMenTreeSep1.Size = new System.Drawing.Size(196, 6);
        //
        // cMenTreeSep2
        // 
        _cMenTreeSep2.Name = "_cMenTreeSep2";
        _cMenTreeSep2.Size = new System.Drawing.Size(196, 6);
        // 
        // cMenTreeDuplicate
        // 
        _cMenTreeDuplicate.Image = Resources.page_copy;
        _cMenTreeDuplicate.Name = "_cMenTreeDuplicate";
        _cMenTreeDuplicate.Size = new System.Drawing.Size(199, 22);
        _cMenTreeDuplicate.Text = "Duplicate";
        _cMenTreeDuplicate.Click += OnDuplicateClicked;
        // 
        // cMenTreeRename
        // 
        _cMenTreeRename.Image = Resources.Rename;
        _cMenTreeRename.Name = "_cMenTreeRename";
        _cMenTreeRename.Size = new System.Drawing.Size(199, 22);
        _cMenTreeRename.Text = "Rename";
        _cMenTreeRename.Click += OnRenameClicked;
        // 
        // cMenTreeDelete
        // 
        _cMenTreeDelete.Image = Resources.Delete;
        _cMenTreeDelete.Name = "_cMenTreeDelete";
        _cMenTreeDelete.Size = new System.Drawing.Size(199, 22);
        _cMenTreeDelete.Text = "Delete";
        _cMenTreeDelete.Click += OnDeleteClicked;
        // 
        // cMenTreeSep3
        // 
        _cMenTreeSep3.Name = "_cMenTreeSep3";
        _cMenTreeSep3.Size = new System.Drawing.Size(196, 6);
        // 
        // cMenTreeImport
        // 
        _cMenTreeImport.DropDownItems.AddRange(new ToolStripItem[] {
            _cMenTreeImportFile
        });
        _cMenTreeImport.Name = "_cMenTreeImport";
        _cMenTreeImport.Size = new System.Drawing.Size(199, 22);
        _cMenTreeImport.Text = "&Import";
        //
        // cMenTreeImportFile
        //
        _cMenTreeImportFile.Name = "_cMenTreeImportFile";
        _cMenTreeImportFile.Size = new System.Drawing.Size(226, 22);
        _cMenTreeImportFile.Text = "Import from &File...";
        _cMenTreeImportFile.Click += OnImportFileClicked;
        //
        // cMenTreeExportFile
        // 
        _cMenTreeExportFile.Name = "_cMenTreeExportFile";
        _cMenTreeExportFile.Size = new System.Drawing.Size(199, 22);
        _cMenTreeExportFile.Text = "&Export to File...";
        _cMenTreeExportFile.Click += OnExportFileClicked;
        // 
        // cMenTreeSep4
        // 
        _cMenTreeSep4.Name = "_cMenTreeSep4";
        _cMenTreeSep4.Size = new System.Drawing.Size(196, 6);
        // 
        // cMenTreeAddConnection
        // 
        _cMenTreeAddConnection.Image = Resources.Connection_Add;
        _cMenTreeAddConnection.Name = "_cMenTreeAddConnection";
        _cMenTreeAddConnection.Size = new System.Drawing.Size(199, 22);
        _cMenTreeAddConnection.Text = "New Connection";
        _cMenTreeAddConnection.Click += OnAddConnectionClicked;
        // 
        // cMenTreeAddFolder
        // 
        _cMenTreeAddFolder.Image = Resources.Folder_Add;
        _cMenTreeAddFolder.Name = "_cMenTreeAddFolder";
        _cMenTreeAddFolder.Size = new System.Drawing.Size(199, 22);
        _cMenTreeAddFolder.Text = "New Folder";
        _cMenTreeAddFolder.Click += OnAddFolderClicked;
        // 
        // ToolStripSeparator1
        // 
        _toolStripSeparator1.Name = "_toolStripSeparator1";
        _toolStripSeparator1.Size = new System.Drawing.Size(196, 6);
        // 
        // cMenTreeToolsSort
        // 
        _cMenTreeToolsSort.DropDownItems.AddRange(new ToolStripItem[] {
            _cMenTreeToolsSortAscending,
            _cMenTreeToolsSortDescending
        });
        _cMenTreeToolsSort.Name = "_cMenTreeToolsSort";
        _cMenTreeToolsSort.Size = new System.Drawing.Size(199, 22);
        _cMenTreeToolsSort.Text = "Sort";
        // 
        // cMenTreeToolsSortAscending
        // 
        _cMenTreeToolsSortAscending.Image = Resources.Sort_AZ;
        _cMenTreeToolsSortAscending.Name = "_cMenTreeToolsSortAscending";
        _cMenTreeToolsSortAscending.Size = new System.Drawing.Size(161, 22);
        _cMenTreeToolsSortAscending.Text = "Ascending (A-Z)";
        _cMenTreeToolsSortAscending.Click += OnSortAscendingClicked;
        // 
        // cMenTreeToolsSortDescending
        // 
        _cMenTreeToolsSortDescending.Image = Resources.Sort_ZA;
        _cMenTreeToolsSortDescending.Name = "_cMenTreeToolsSortDescending";
        _cMenTreeToolsSortDescending.Size = new System.Drawing.Size(161, 22);
        _cMenTreeToolsSortDescending.Text = "Descending (Z-A)";
        _cMenTreeToolsSortDescending.Click += OnSortDescendingClicked;
        // 
        // cMenTreeMoveUp
        // 
        _cMenTreeMoveUp.Image = Resources.Arrow_Up;
        _cMenTreeMoveUp.Name = "_cMenTreeMoveUp";
        _cMenTreeMoveUp.Size = new System.Drawing.Size(199, 22);
        _cMenTreeMoveUp.Text = "Move up";
        _cMenTreeMoveUp.Click += OnMoveUpClicked;
        // 
        // cMenTreeMoveDown
        // 
        _cMenTreeMoveDown.Image = Resources.Arrow_Down;
        _cMenTreeMoveDown.Name = "_cMenTreeMoveDown";
        _cMenTreeMoveDown.Size = new System.Drawing.Size(199, 22);
        _cMenTreeMoveDown.Text = "Move down";
        _cMenTreeMoveDown.Click += OnMoveDownClicked;
    }

    private void ApplyLanguage()
    {
        _cMenTreeConnect.Text = Language.strConnect;
        _cMenTreeConnectWithOptions.Text = Language.strConnectWithOptions;
        _cMenTreeConnectWithOptionsConnectToConsoleSession.Text = Language.strConnectToConsoleSession;
        _cMenTreeConnectWithOptionsDontConnectToConsoleSession.Text = Language.strDontConnectToConsoleSessionMenuItem;
        _cMenTreeConnectWithOptionsConnectInFullscreen.Text = Language.strConnectInFullscreen;
        _cMenTreeConnectWithOptionsNoCredentials.Text = Language.strConnectNoCredentials;
        _cMenTreeConnectWithOptionsChoosePanelBeforeConnecting.Text = Language.strChoosePanelBeforeConnecting;
        _cMenTreeDisconnect.Text = Language.strMenuDisconnect;

        _cMenTreeDuplicate.Text = Language.strDuplicate;
        _cMenTreeRename.Text = Language.strRename;
        _cMenTreeDelete.Text = Language.strMenuDelete;

        _cMenTreeImport.Text = Language.strImportMenuItem;
        _cMenTreeImportFile.Text = Language.strImportFromFileMenuItem;
        _cMenTreeExportFile.Text = Language.strExportToFileMenuItem;

        _cMenTreeAddConnection.Text = Language.strAddConnection;
        _cMenTreeAddFolder.Text = Language.strAddFolder;

        _cMenTreeToolsSort.Text = Language.strSort;
        _cMenTreeToolsSortAscending.Text = Language.strSortAsc;
        _cMenTreeToolsSortDescending.Text = Language.strSortDesc;
        _cMenTreeMoveUp.Text = Language.strMoveUp;
        _cMenTreeMoveDown.Text = Language.strMoveDown;
    }

    internal void ShowHideMenuItems()
    {
        try
        {
            Enabled = true;
            EnableMenuItemsRecursive(Items);
            if (_connectionTree.SelectedConnection == null)
            {
                ShowHideMenuItemsForNoSelection();
            }
            else if (_connectionTree.SelectedConnection is RootPuttySessionsNodeInfo)
            {
                ShowHideMenuItemsForRootPuttyNode();
            }
            else if (_connectionTree.SelectedConnection is PuttySessionInfo)
            {
                ShowHideMenuItemsForPuttyNode(_connectionTree.SelectedConnection);
            }
            else if (_connectionTree.SelectedConnection is RootNodeInfo)
            {
                ShowHideMenuItemsForRootConnectionNode();
            }
            else if (_connectionTree.SelectedConnection is ContainerInfo)
            {
                ShowHideMenuItemsForContainer(_connectionTree.SelectedConnection);
            }
            else if (_connectionTree.SelectedConnection is { } connection)
            {
                ShowHideMenuItemsForConnectionNode(connection);
            }
        }
        catch (Exception ex)
        {
            Runtime.MessageCollector.AddExceptionMessage(
                "ShowHideMenuItems (UI.Controls.ConnectionContextMenu) failed",
                ex);
        }
    }

    /// <summary>
    /// Disables the items with shortcut keys. The menu itself never opens with nothing selected
    /// (see the Opening handler), but their shortcuts would still fire.
    /// </summary>
    internal void ShowHideMenuItemsForNoSelection()
    {
        _cMenTreeConnect.Enabled = false;
        _cMenTreeDuplicate.Enabled = false;
        _cMenTreeRename.Enabled = false;
        _cMenTreeDelete.Enabled = false;
        _cMenTreeMoveUp.Enabled = false;
        _cMenTreeMoveDown.Enabled = false;
    }

    internal void ShowHideMenuItemsForRootPuttyNode()
    {
        _cMenTreeAddConnection.Enabled = false;
        _cMenTreeAddFolder.Enabled = false;
        _cMenTreeConnect.Enabled = false;
        _cMenTreeConnectWithOptions.Enabled = false;
        _cMenTreeDisconnect.Enabled = false;
        _cMenTreeToolsSort.Enabled = false;
        _cMenTreeDuplicate.Enabled = false;
        _cMenTreeImport.Enabled = false;
        _cMenTreeExportFile.Enabled = false;
        _cMenTreeRename.Enabled = false;
        _cMenTreeDelete.Enabled = false;
        _cMenTreeMoveUp.Enabled = false;
        _cMenTreeMoveDown.Enabled = false;
    }

    internal void ShowHideMenuItemsForPuttyNode(ConnectionInfo connectionInfo)
    {
        _cMenTreeAddConnection.Enabled = false;
        _cMenTreeAddFolder.Enabled = false;

        if (connectionInfo.OpenConnections.Count == 0)
        {
            _cMenTreeDisconnect.Enabled = false;
        }

        _cMenTreeConnectWithOptionsConnectInFullscreen.Enabled = false;
        _cMenTreeConnectWithOptionsConnectToConsoleSession.Enabled = false;
        _cMenTreeToolsSort.Enabled = false;
        _cMenTreeDuplicate.Enabled = false;
        _cMenTreeRename.Enabled = false;
        _cMenTreeDelete.Enabled = false;
        _cMenTreeMoveUp.Enabled = false;
        _cMenTreeMoveDown.Enabled = false;
        _cMenTreeImport.Enabled = false;
        _cMenTreeExportFile.Enabled = false;
    }

    internal void ShowHideMenuItemsForRootConnectionNode()
    {
        _cMenTreeConnect.Enabled = false;
        _cMenTreeConnectWithOptions.Enabled = false;
        _cMenTreeConnectWithOptionsConnectInFullscreen.Enabled = false;
        _cMenTreeConnectWithOptionsConnectToConsoleSession.Enabled = false;
        _cMenTreeConnectWithOptionsChoosePanelBeforeConnecting.Enabled = false;
        _cMenTreeDisconnect.Enabled = false;
        _cMenTreeDuplicate.Enabled = false;
        _cMenTreeDelete.Enabled = false;
        _cMenTreeMoveUp.Enabled = false;
        _cMenTreeMoveDown.Enabled = false;
    }

    internal void ShowHideMenuItemsForContainer(ConnectionInfo connectionInfo)
    {
        _cMenTreeConnectWithOptionsConnectInFullscreen.Enabled = false;
        _cMenTreeConnectWithOptionsConnectToConsoleSession.Enabled = false;
        _cMenTreeDisconnect.Enabled = false;

        if (OpenSessionsUnder(connectionInfo).Count > 0)
        {
            _cMenTreeDisconnect.Enabled = true;
        }
    }

    /// <summary>
    /// The sessions open on a connection, or on every connection anywhere under a folder: the
    /// same nodes that opening the folder connects, sub-folders included.
    /// </summary>
    internal static List<ProtocolBase> OpenSessionsUnder(ConnectionInfo node)
    {
        var sessions = new List<ProtocolBase>();
        CollectOpenSessions(node, sessions);
        return sessions;
    }

    private static void CollectOpenSessions(ConnectionInfo node, List<ProtocolBase> sessions)
    {
        if (node is ContainerInfo container)
        {
            foreach (ConnectionInfo child in container.Children)
            {
                CollectOpenSessions(child, sessions);
            }

            return;
        }

        sessions.AddRange(node.OpenConnections);
    }

    internal void ShowHideMenuItemsForConnectionNode(ConnectionInfo connectionInfo)
    {
        if (connectionInfo.OpenConnections.Count == 0)
        {
            _cMenTreeDisconnect.Enabled = false;
        }

        if (connectionInfo.Protocol != ProtocolType.RDP)
        {
            _cMenTreeConnectWithOptionsConnectInFullscreen.Enabled = false;
            _cMenTreeConnectWithOptionsConnectToConsoleSession.Enabled = false;
        }
    }

    internal void DisableShortcutKeys()
    {
        _cMenTreeConnect.ShortcutKeys = Keys.None;
        _cMenTreeDuplicate.ShortcutKeys = Keys.None;
        _cMenTreeRename.ShortcutKeys = Keys.None;
        _cMenTreeDelete.ShortcutKeys = Keys.None;
        _cMenTreeMoveUp.ShortcutKeys = Keys.None;
        _cMenTreeMoveDown.ShortcutKeys = Keys.None;
    }

    internal void EnableShortcutKeys()
    {
        _cMenTreeConnect.ShortcutKeys = ((Keys.Control | Keys.Shift) | Keys.C);
        _cMenTreeDuplicate.ShortcutKeys = Keys.Control | Keys.D;
        _cMenTreeRename.ShortcutKeys = Keys.F2;
        _cMenTreeDelete.ShortcutKeys = Keys.Delete;
        _cMenTreeMoveUp.ShortcutKeys = Keys.Control | Keys.Up;
        _cMenTreeMoveDown.ShortcutKeys = Keys.Control | Keys.Down;
    }

    private static void EnableMenuItemsRecursive(ToolStripItemCollection items, bool enable = true)
    {
        foreach (ToolStripItem item in items)
        {
            var menuItem = item as ToolStripMenuItem;
            if (menuItem == null)
            {
                continue;
            }
            menuItem.Enabled = enable;
            if (menuItem.HasDropDownItems)
            {
                EnableMenuItemsRecursive(menuItem.DropDownItems, enable);
            }
        }
    }

    #region Click handlers
    private void OnConnectClicked(object? sender, EventArgs e)
    {
        var selectedNodeAsContainer = _connectionTree.SelectedConnection as ContainerInfo;
        if (selectedNodeAsContainer != null)
        {
            _connectionInitiator.OpenConnection(selectedNodeAsContainer, ConnectionInfo.Force.DoNotJump);
        }
        else if (_connectionTree.SelectedConnection is { } selected)
        {
            _connectionInitiator.OpenConnection(selected, ConnectionInfo.Force.DoNotJump);
        }
    }

    private void OnConnectToConsoleSessionClicked(object? sender, EventArgs e)
    {
        var selectedNodeAsContainer = _connectionTree.SelectedConnection as ContainerInfo;
        if (selectedNodeAsContainer != null)
        {
            _connectionInitiator.OpenConnection(
                selectedNodeAsContainer,
                ConnectionInfo.Force.UseConsoleSession | ConnectionInfo.Force.DoNotJump);
        }
        else if (_connectionTree.SelectedConnection is { } selected)
        {
            _connectionInitiator.OpenConnection(
                selected,
                ConnectionInfo.Force.UseConsoleSession | ConnectionInfo.Force.DoNotJump);
        }
    }

    private void OnDontConnectToConsoleSessionClicked(object? sender, EventArgs e)
    {
        var selectedNodeAsContainer = _connectionTree.SelectedConnection as ContainerInfo;
        if (selectedNodeAsContainer != null)
        {
            _connectionInitiator.OpenConnection(
                selectedNodeAsContainer,
                ConnectionInfo.Force.DontUseConsoleSession | ConnectionInfo.Force.DoNotJump);
        }
        else if (_connectionTree.SelectedConnection is { } selected)
        {
            _connectionInitiator.OpenConnection(
                selected,
                ConnectionInfo.Force.DontUseConsoleSession | ConnectionInfo.Force.DoNotJump);
        }
    }

    private void OnConnectInFullscreenClicked(object? sender, EventArgs e)
    {
        var selectedNodeAsContainer = _connectionTree.SelectedConnection as ContainerInfo;
        if (selectedNodeAsContainer != null)
        {
            _connectionInitiator.OpenConnection(
                selectedNodeAsContainer,
                ConnectionInfo.Force.Fullscreen | ConnectionInfo.Force.DoNotJump);
        }
        else if (_connectionTree.SelectedConnection is { } selected)
        {
            _connectionInitiator.OpenConnection(
                selected,
                ConnectionInfo.Force.Fullscreen | ConnectionInfo.Force.DoNotJump);
        }
    }

    private void OnConnectWithNoCredentialsClick(object? sender, EventArgs e)
    {
        var selectedNodeAsContainer = _connectionTree.SelectedConnection as ContainerInfo;
        if (selectedNodeAsContainer != null)
        {
            _connectionInitiator.OpenConnection(selectedNodeAsContainer, ConnectionInfo.Force.NoCredentials);
        }
        else if (_connectionTree.SelectedConnection is { } selected)
        {
            _connectionInitiator.OpenConnection(selected, ConnectionInfo.Force.NoCredentials);
        }
    }

    private void OnChoosePanelBeforeConnectingClicked(object? sender, EventArgs e)
    {
        var selectedNodeAsContainer = _connectionTree.SelectedConnection as ContainerInfo;
        if (selectedNodeAsContainer != null)
        {
            _connectionInitiator.OpenConnection(
                selectedNodeAsContainer,
                ConnectionInfo.Force.OverridePanel | ConnectionInfo.Force.DoNotJump);
        }
        else if (_connectionTree.SelectedConnection is { } selected)
        {
            _connectionInitiator.OpenConnection(
                selected,
                ConnectionInfo.Force.OverridePanel | ConnectionInfo.Force.DoNotJump);
        }
    }

    private void OnDisconnectClicked(object? sender, EventArgs e)
    {
        DisconnectConnection(_connectionTree.SelectedConnection);
    }

    public static void DisconnectConnection(ConnectionInfo? connectionInfo)
    {
        try
        {
            if (connectionInfo == null)
            {
                return;
            }

            // A snapshot, not the live lists: a PuTTY session closes inline, and its Closed
            // handler takes it out of OpenConnections while the loop is still running.
            foreach (ProtocolBase session in OpenSessionsUnder(connectionInfo))
            {
                session.Disconnect();
            }
        }
        catch (Exception ex)
        {
            Runtime.MessageCollector.AddExceptionMessage(
                "DisconnectConnection (UI.Window.ConnectionTreeWindow) failed",
                ex);
        }
    }

    private void OnDuplicateClicked(object? sender, EventArgs e)
    {
        _connectionTree.DuplicateSelectedNode();
    }

    private void OnRenameClicked(object? sender, EventArgs e)
    {
        _connectionTree.RenameSelectedNode();
    }

    private void OnDeleteClicked(object? sender, EventArgs e)
    {
        _connectionTree.DeleteSelectedNode();
    }

    private void OnImportFileClicked(object? sender, EventArgs e)
    {
        // The menu opens only on a selected node, and a connection in the tree always has a
        // folder or root above it; with no connections loaded there is nothing to import into.
        ContainerInfo? selectedNodeAsContainer;
        if (_connectionTree.SelectedConnection == null)
        {
            selectedNodeAsContainer = Runtime.ConnectionsService.ConnectionTreeModel?.RootNodes.First();
        }
        else
        {
            selectedNodeAsContainer = _connectionTree.SelectedConnection as ContainerInfo
                ?? _connectionTree.SelectedConnection.Parent;
        }

        if (selectedNodeAsContainer == null)
        {
            return;
        }

        Import.ImportFromFile(selectedNodeAsContainer);
    }

    private void OnExportFileClicked(object? sender, EventArgs e)
    {
        ConnectionTreeModel? connectionTreeModel = Runtime.ConnectionsService.ConnectionTreeModel;
        if (connectionTreeModel == null)
        {
            return;
        }

        Export.ExportToFile(_connectionTree.SelectedConnection, connectionTreeModel);
    }

    private void OnAddConnectionClicked(object? sender, EventArgs e)
    {
        _connectionTree.AddConnection();
    }

    private void OnAddFolderClicked(object? sender, EventArgs e)
    {
        _connectionTree.AddFolder();
    }

    private void OnSortAscendingClicked(object? sender, EventArgs e)
    {
        _connectionTree.SortRecursive(_connectionTree.SelectedConnection, ListSortDirection.Ascending);
    }

    private void OnSortDescendingClicked(object? sender, EventArgs e)
    {
        _connectionTree.SortRecursive(_connectionTree.SelectedConnection, ListSortDirection.Descending);
    }

    private void OnMoveUpClicked(object? sender, EventArgs e)
    {
        ConnectionInfo selected = SelectedWithParent(out ContainerInfo parent);
        parent.PromoteChild(selected);
    }

    private void OnMoveDownClicked(object? sender, EventArgs e)
    {
        ConnectionInfo selected = SelectedWithParent(out ContainerInfo parent);
        parent.DemoteChild(selected);
    }

    /// <summary>
    /// The selected node and the folder it sits in, for moving it.
    /// </summary>
    /// <remarks>
    /// Throws where this used to dereference null: with nothing selected, or with the root
    /// selected, which has no parent. A backstop only: Move Up and Move Down are disabled in
    /// both cases (<see cref="ShowHideMenuItems"/>), and their shortcut keys follow that.
    /// </remarks>
    private ConnectionInfo SelectedWithParent(out ContainerInfo parent)
    {
        ConnectionInfo selected = _connectionTree.SelectedConnection
            ?? throw new InvalidOperationException("No connection is selected.");
        parent = selected.Parent
            ?? throw new InvalidOperationException("The selected node has no folder to move within.");
        return selected;
    }

    #endregion
}
