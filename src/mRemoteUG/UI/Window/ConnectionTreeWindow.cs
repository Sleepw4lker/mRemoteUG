using mRemoteUG.App;
using mRemoteUG.Config.Connections;
using mRemoteUG.Connection;
using mRemoteUG.Tree;
using mRemoteUG.Tree.Root;
using mRemoteUG.UI.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
// ReSharper disable ArrangeAccessorOwnerBody

namespace mRemoteUG.UI.Window
{
    public partial class ConnectionTreeWindow
	{
        private readonly IConnectionInitiator _connectionInitiator = new ConnectionInitiator();

		public ConnectionInfo SelectedConnection => olvConnections.SelectedConnection;

	    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	    public ConnectionTree ConnectionTree
	    {
	        get { return olvConnections; }
            set { olvConnections = value; }
	    }

		public ConnectionTreeWindow()
		{
			InitializeComponent();

			// Before anything can be shown: the search row is a Panel, and a Panel that is never
			// given a height keeps the 100 it is born with.
			ApplySearchRowMetrics(DeviceDpi);
			SetMenuEventHandlers();
		    SetConnectionTreeEventHandlers();
		    Settings.Default.PropertyChanged += OnAppSettingsChanged;
		    _searchDebounce.Tick += (o, args) =>
		    {
		        _searchDebounce.Stop();
		        ApplyFiltering();
		    };
        }

	    /// <summary>
	    /// Settles the search box before filtering the tree.
	    /// </summary>
	    /// <remarks>
	    /// Filtering calls RebuildAll, which clears every node and rebuilds the visible subtree
	    /// from the model. Driven straight from TextChanged that happened once per keystroke,
	    /// so typing a six-character search rebuilt the whole tree six times and threw five of
	    /// them away. BeginUpdate/EndUpdate was already correct, so the cost was the node
	    /// allocation itself, not flicker.
	    /// </remarks>
	    private readonly System.Windows.Forms.Timer _searchDebounce =
	        new System.Windows.Forms.Timer { Interval = 250 };

	    private void OnAppSettingsChanged(object? o, PropertyChangedEventArgs propertyChangedEventArgs)
	    {
	        if (propertyChangedEventArgs.PropertyName == nameof(Settings.UseFilterSearch))
	        {
	            ConnectionTree.UseFiltering = Settings.Default.UseFilterSearch;
	            ApplyFiltering();
            }
	    }


	    #region Form Stuff
        private void Tree_Load(object sender, EventArgs e)
        {
            ApplyLanguage();
            //work on the theme change

            // Called rather than posted: by Load the auto-scale pass has run, so the font this
            // measures against is already the final one.
            ApplySearchRowMetrics(DeviceDpi);
        }

        /// <summary>The search glyph at 96 DPI. Every glyph this application ships is this size.</summary>
        private const int LogicalSearchGlyphSize = 16;

        /// <summary>
        /// Sizes the search row for a DPI.
        /// </summary>
        /// <remarks>
        /// The row is docked, so its height is the only thing that has to be given to it - the box
        /// fills what is left of the width beside the glyph. The height is re-derived here on every
        /// pass rather than scaled from its own previous value: a number computed from itself
        /// compounds every time the framework rescales the control in between, which is the trap
        /// TheQuickConnectEntryFieldIsRecomputedNotScaled pins for the quick connect field.
        /// <para>
        /// PreferredHeight is the floor because font metrics do not grow linearly with DPI - Segoe
        /// UI 8.25pt measures 13px tall at 96 DPI but 19px at 120 - so a purely DPI-scaled glyph
        /// size would clip the text beside it. The Multiline flip this replaces is no longer
        /// needed: a single-line TextBox pins its own height to PreferredHeight and, docked to
        /// Fill, takes the full width without being told.
        /// </para>
        /// </remarks>
        private void ApplySearchRowMetrics(int dpi)
        {
            var rowHeight = Math.Max(DpiScaling.Scale(LogicalSearchGlyphSize, dpi), txtSearch.PreferredHeight);
            pnlSearch.Height = rowHeight;
            PictureBox1.Width = rowHeight;
        }

        /// <summary>
        /// Re-measures the search row once a DPI change has settled.
        /// </summary>
        /// <remarks>
        /// RescaleConstantsForDpi below runs part-way through the change and measures against the
        /// font the window has at that moment, which is still the old one. FrmMain calls this
        /// afterwards, when the font is the new one - the same two-step the connection tabs use.
        /// </remarks>
        /// <summary>The docked row holding the search box and its glyph.</summary>
        /// <remarks>Exposed so a test can measure it; nothing else reads it.</remarks>
        internal Panel SearchRow => pnlSearch;

        internal void RefreshSearchRowMetrics(int dpi)
        {
            if (IsDisposed)
                return;

            ApplySearchRowMetrics(dpi);
            PerformLayout();
        }

        /// <remarks>
        /// This window is embedded with TopLevel false, so it is a child window and is sent
        /// WM_DPICHANGED_BEFOREPARENT rather than WM_DPICHANGED. RescaleConstantsForDpi is
        /// therefore the hook that fires here - the inverse of FrmMain, where overriding only it
        /// was dead code.
        /// </remarks>
        protected override void RescaleConstantsForDpi(int deviceDpiOld, int deviceDpiNew)
        {
            base.RescaleConstantsForDpi(deviceDpiOld, deviceDpiNew);
            ApplySearchRowMetrics(deviceDpiNew);
        }

        private void ApplyLanguage()
        {
            Text = Language.strConnections;
            TabText = Language.strConnections;

            mMenAddConnection.ToolTipText = Language.strAddConnection;
            mMenAddFolder.ToolTipText = Language.strAddFolder;
            mMenView.ToolTipText = Language.strMenuView.Replace("&", "");
            mMenViewExpandAllFolders.Text = Language.strExpandAllFolders;
            mMenViewCollapseAllFolders.Text = Language.strCollapseAllFolders;
            mMenSortAscending.ToolTipText = Language.strSortAsc;

            txtSearch.Text = Language.strSearchPrompt;
        }

        #endregion

        #region ConnectionTree
	    private void SetConnectionTreeEventHandlers()
	    {
	        olvConnections.NodeDeletionConfirmer = new SelectedConnectionDeletionConfirmer(MessageBox.Show);
            olvConnections.KeyDown += tvConnections_KeyDown;
            olvConnections.KeyPress += tvConnections_KeyPress;
            SetTreePostSetupActions();
            SetConnectionTreeDoubleClickHandlers();
	        SetConnectionTreeSingleClickHandlers();
	        Runtime.ConnectionsService.ConnectionsLoaded += ConnectionsServiceOnConnectionsLoaded;
        }

	    private void SetTreePostSetupActions()
	    {
	        var actions = new List<IConnectionTreeDelegate>
	        {
	            new PreviouslyOpenedFolderExpander(),
	            new RootNodeExpander()
	        };

	        if (Settings.Default.OpenConsFromLastSession && !Settings.Default.NoReconnect)
                actions.Add(new PreviousSessionOpener(_connectionInitiator));

	        olvConnections.PostSetupActions = actions;
	    }

	    private void SetConnectionTreeDoubleClickHandlers()
	    {
	        var doubleClickHandler = new TreeNodeCompositeClickHandler
	        {
	            ClickHandlers = new ITreeNodeClickHandler<ConnectionInfo>[]
	            {
	                new ExpandNodeClickHandler(olvConnections),
	                new OpenConnectionClickHandler(_connectionInitiator)
	            }
	        };
	        olvConnections.DoubleClickHandler = doubleClickHandler;
	    }

        private void SetConnectionTreeSingleClickHandlers()
        {
            var handlers = new List<ITreeNodeClickHandler<ConnectionInfo>>();
            if (Settings.Default.SingleClickOnConnectionOpensIt)
                handlers.Add(new OpenConnectionClickHandler(_connectionInitiator));
            if (Settings.Default.SingleClickSwitchesToOpenConnection)
                handlers.Add(new SwitchToConnectionClickHandler(_connectionInitiator));
            var singleClickHandler = new TreeNodeCompositeClickHandler {ClickHandlers = handlers};
            olvConnections.SingleClickHandler = singleClickHandler;
        }

	    private void ConnectionsServiceOnConnectionsLoaded(object? o, ConnectionsLoadedEventArgs connectionsLoadedEventArgs)
	    {
	        if (olvConnections.InvokeRequired)
	        {
	            olvConnections.Invoke(() => ConnectionsServiceOnConnectionsLoaded(o, connectionsLoadedEventArgs));
                return;
	        }

	        olvConnections.ConnectionTreeModel = connectionsLoadedEventArgs.NewConnectionTreeModel;
	        olvConnections.SelectedObject = connectionsLoadedEventArgs.NewConnectionTreeModel.RootNodes
	            .OfType<RootNodeInfo>().FirstOrDefault();
	    }
        #endregion

        #region Top Menu
        private void SetMenuEventHandlers()
        {
            mMenViewExpandAllFolders.Click += (sender, args) => olvConnections.ExpandAll();
            mMenViewCollapseAllFolders.Click += (sender, args) =>
            {
                olvConnections.CollapseAll();
                olvConnections.Expand(olvConnections.GetRootConnectionNode());
            };
            mMenSortAscending.Click += (sender, args) => olvConnections.SortRecursive(olvConnections.GetRootConnectionNode(), ListSortDirection.Ascending);
        }
        #endregion

        #region Tree Context Menu
        private void cMenTreeAddConnection_Click(object sender, EventArgs e)
		{
			olvConnections.AddConnection();
		}

        private void cMenTreeAddFolder_Click(object sender, EventArgs e)
		{
            olvConnections.AddFolder();
		}
        #endregion

        #region Search
        private void txtSearch_GotFocus(object sender, EventArgs e)
		{
			if (txtSearch.Text == Language.strSearchPrompt)
				txtSearch.Text = "";
		}

        private void txtSearch_LostFocus(object sender, EventArgs e)
		{
            if (txtSearch.Text != "") return;
            txtSearch.Text = Language.strSearchPrompt;
		}

        private void txtSearch_KeyDown(object sender, KeyEventArgs e)
		{
			try
			{
				if (e.KeyCode == Keys.Escape)
				{
					e.Handled = true;
				    olvConnections.Focus();
				}
				else if (e.KeyCode == Keys.Up)
				{
                    var match = olvConnections.NodeSearcher.PreviousMatch();
                    JumpToNode(match);
                    e.Handled = true;
				}
				else if (e.KeyCode == Keys.Down)
				{
				    var match = olvConnections.NodeSearcher.NextMatch();
				    JumpToNode(match);
                    e.Handled = true;
				}
				else
				{
					tvConnections_KeyDown(sender, e);
				}
			}
			catch (Exception ex)
			{
				Runtime.MessageCollector.AddExceptionMessage("txtSearch_KeyDown (UI.Window.ConnectionTreeWindow) failed", ex);
			}
		}

        private void txtSearch_TextChanged(object sender, EventArgs e)
        {
            _searchDebounce.Stop();
            _searchDebounce.Start();
        }

	    private void ApplyFiltering()
	    {
	        if (Settings.Default.UseFilterSearch)
	        {
	            if (txtSearch.Text == "" || txtSearch.Text == Language.strSearchPrompt)
	            {
	                olvConnections.RemoveFilter();
	                return;
	            }
	            olvConnections.ApplyFilter(txtSearch.Text);
	        }
	        else
	        {
	            if (txtSearch.Text == "") return;
	            olvConnections.NodeSearcher?.SearchByName(txtSearch.Text);
	            JumpToNode(olvConnections.NodeSearcher?.CurrentMatch);
	        }
        }

	    private void JumpToNode(ConnectionInfo connectionInfo)
	    {
	        if (connectionInfo == null)
	        {
	            olvConnections.SelectedObject = null;
                return;
	        }
	        ExpandParentsRecursive(connectionInfo);
            olvConnections.SelectObject(connectionInfo);
            olvConnections.EnsureModelVisible(connectionInfo);
        }

	    private void ExpandParentsRecursive(ConnectionInfo connectionInfo)
	    {
	        while (true)
	        {
	            if (connectionInfo?.Parent == null) return;
	            olvConnections.Expand(connectionInfo.Parent);
	            connectionInfo = connectionInfo.Parent;
	        }
	    }

	    private void tvConnections_KeyPress(object? sender, KeyPressEventArgs e)
		{
			try
			{
			    if (!char.IsLetterOrDigit(e.KeyChar)) return;
			    // swallow the key so TreeView type-ahead does not also move the selection
			    e.Handled = true;
			    txtSearch.Text = e.KeyChar.ToString();
			    txtSearch.Focus();
			    txtSearch.SelectionStart = txtSearch.TextLength;
			}
			catch (Exception ex)
			{
				Runtime.MessageCollector.AddExceptionMessage("tvConnections_KeyPress (UI.Window.ConnectionTreeWindow) failed", ex);
			}
		}

        private void tvConnections_KeyDown(object? sender, KeyEventArgs e)
		{
			try
			{
				if (e.KeyCode == Keys.Enter)
				{
					e.Handled = true;
                    _connectionInitiator.OpenConnection(SelectedConnection);
				}
				else if (e.Control && e.KeyCode == Keys.F)
				{
					txtSearch.Focus();
                    txtSearch.SelectAll();
				    e.Handled = true;
				}
			}
			catch (Exception ex)
			{
				Runtime.MessageCollector.AddExceptionMessage("tvConnections_KeyDown (UI.Window.ConnectionTreeWindow) failed", ex);
			}
		}
        #endregion
	}
}
