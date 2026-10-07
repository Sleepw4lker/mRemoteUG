using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;
using mRemoteUG.App;
using mRemoteUG.Config.Putty;
using mRemoteUG.App.Initialization;
using mRemoteUG.Config;
using mRemoteUG.Config.Connections;
using mRemoteUG.Config.DataProviders;
using mRemoteUG.Config.Settings;
using mRemoteUG.Connection;
using mRemoteUG.Messages;
using mRemoteUG.Messages.MessageWriters;
using mRemoteUG.Tools;
using mRemoteUG.UI.Controls;
using mRemoteUG.UI.Menu;
using mRemoteUG.UI.Panels;
using mRemoteUG.UI.TaskDialog;
using mRemoteUG.UI.Window;

// ReSharper disable MemberCanBePrivate.Global

namespace mRemoteUG.UI.Forms
{
    public partial class FrmMain
    {
        public static FrmMain Default { get; } = new FrmMain();

        private bool _inSizeMove;
        private bool _inMouseActivate;
        private string _connectionsFileName;
        private bool _showFullPathInTitle;
        private readonly ScreenSelectionSystemMenu _screenSystemMenu;
        private ConnectionInfo _selectedConnection;
        private readonly IList<IMessageWriter> _messageWriters = new List<IMessageWriter>();
        private readonly FileBackupPruner _backupPruner = new FileBackupPruner();

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        internal FullscreenHandler Fullscreen { get; set; }

        /// <summary>
        /// The main window static layout: the left tool column, the document area and
        /// the notifications strip.
        /// </summary>
        internal MainLayout Layout { get; }

        private FrmMain()
		{
			_showFullPathInTitle = Settings.Default.ShowCompleteConsPathInTitle;
			InitializeComponent();
            Fullscreen = new FullscreenHandler(this);

            Layout = new MainLayout(splitMain, splitLeft, splitDocuments, tabDocuments);
            Layout.ActiveDocumentChanged += Layout_ActiveDocumentChanged;

            _screenSystemMenu = new ScreenSelectionSystemMenu(this);
        }

        #region Properties
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public FormWindowState PreviousWindowState { get; set; }

	    public bool IsClosing { get; private set; }


        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string ConnectionsFileName
		{
			get => _connectionsFileName;
            set
			{
				if (_connectionsFileName == value)
				{
					return;
				}
				_connectionsFileName = value;
				UpdateWindowTitle();
			}
		}
		
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool ShowFullPathInTitle
		{
			get => _showFullPathInTitle;
            set
			{
				if (_showFullPathInTitle == value)
				{
					return;
				}
				_showFullPathInTitle = value;
				UpdateWindowTitle();
			}
		}
		
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ConnectionInfo SelectedConnection
		{
			get => _selectedConnection;
            set
			{
				if (_selectedConnection == value)
				{
					return;
				}
				_selectedConnection = value;
				UpdateWindowTitle();
			}
		}
        #endregion

        #region Startup & Shutdown
        private void frmMain_Load(object sender, EventArgs e)
        {
            var startup = Stopwatch.StartNew();
            var messageCollector = Runtime.MessageCollector;
            MessageCollectorSetup.SetupMessageCollector(messageCollector, _messageWriters);

            Startup.Instance.InitializeProgram(messageCollector);

            msMain.Location = Point.Empty;
            var settingsLoader = new SettingsLoader(this, messageCollector, _quickConnectToolStrip, msMain);
            settingsLoader.LoadSettings();

            SetMenuDependencies();

            // Has to follow HostToolWindows: the restore sets panel visibility, and there
            // is nothing to show or hide until the tool windows are in their panels. That
            // is also why it cannot live in SettingsLoader with the rest of the settings.
            Layout.HostToolWindows(AppWindows.TreeForm, AppWindows.ConfigForm, AppWindows.ErrorsForm);
            MainLayoutSettings.Restore(Layout, DeviceDpi);

	        LockToolbarPositions(Settings.Default.LockToolbars);
			Settings.Default.PropertyChanged += OnApplicationSettingChanged;

            Runtime.WindowList = new WindowList();

            Runtime.ConnectionsService.ConnectionsLoaded += ConnectionsServiceOnConnectionsLoaded;
            Runtime.ConnectionsService.ConnectionsSaved += ConnectionsServiceOnConnectionsSaved;

            // The follow-up pass had only ever been reached from a DPI *change*, so a first launch
            // on a 144 DPI monitor left every ToolStrip on the process-wide default font and glyph
            // size until the window happened to be dragged somewhere else. It has to run after the
            // tool windows are in their panels, or the sweep cannot reach the strips they own, and
            // above the reveal below, or the toolbars visibly change size after the window appears.
            //
            // Called rather than posted, which is the opposite of what the two DPI hooks do. They
            // post because they fire mid-relayout with a font that is not settled yet; Load runs
            // after the auto-scale pass, so Font and DeviceDpi are already the final values here.
            ApplyDpiFollowUp(DeviceDpi);


            // The window is Opacity = 0 from the designer, so nothing above this line is
            // visible to the user. Everything that decides how the window *looks* has run by
            // now - settings, the tool windows, the restored layout, the menu text - so this
            // is the earliest point at which showing it is honest.
            //
            // What follows it does not shape the window, and some of it is slow in proportion
            // to the connection file: reading and parsing it, one PBKDF2 derivation per stored
            // password, enumerating PuTTY's saved sessions, and then PreviousSessionOpener,
            // which opens every connection that was open last time - RDP controls and all.
            // Run before the reveal, that is all black screen.
            Opacity = 1;
            startup.Stop();
            messageCollector.AddMessage(MessageClass.InformationMsg,
                $"Main window shown after {startup.ElapsedMilliseconds} ms; loading connections", true);

            // Posted rather than called: this returns to the message loop first, so the window
            // is painted before the load starts rather than after it.
            BeginInvoke(new Action(CompleteStartup));
        }

        /// <summary>
        /// The part of startup that runs once the main window is up.
        /// </summary>
        /// <remarks>
        /// Posted from <see cref="frmMain_Load"/>. It is on the UI thread like everything else
        /// here, so it still blocks interaction while it runs - what it no longer blocks is the
        /// window appearing at all. The try/catch is not decoration: an exception on a posted
        /// callback has no caller to reach, and the handler that would catch it - see
        /// <see cref="mRemoteUG.App.CrashLogger"/> - treats a UI-thread exception as fatal and ends
        /// the process. So without it a failure here would take the whole application down.
        /// </remarks>
        private void CompleteStartup()
        {
            var load = Stopwatch.StartNew();
            try
            {
                var credsAndConsSetup = new CredsAndConsSetup();
                credsAndConsSetup.LoadCredsAndCons();

                AppWindows.TreeForm.Focus();

                PuttySessionsManager.Instance.StartWatcher();

                _screenSystemMenu.BuildScreenList();
                SystemEvents.DisplaySettingsChanged += _screenSystemMenu.OnDisplayChanged;

                //Fix missing general panel at the first run
                if (Settings.Default.CreateEmptyPanelOnStartUp)
                {
                    var panelName = !string.IsNullOrEmpty(Settings.Default.StartUpPanelName)
                        ? Settings.Default.StartUpPanelName
                        : Language.strNewPanel;

                    var panelAdder = new PanelAdder();
                    if (!panelAdder.DoesPanelExist(panelName))
                        panelAdder.AddPanel(panelName);
                }

                Runtime.MessageCollector.AddMessage(MessageClass.InformationMsg,
                    $"Startup complete in {load.ElapsedMilliseconds} ms", true);
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("Completing startup failed.", ex);
            }
        }


        private void OnApplicationSettingChanged(object? sender, PropertyChangedEventArgs propertyChangedEventArgs)
	    {
		    if (propertyChangedEventArgs.PropertyName != nameof(Settings.LockToolbars))
				return;

		    LockToolbarPositions(Settings.Default.LockToolbars);
	    }

        /// <summary>
        /// The hook a top-level window actually gets when it moves to a monitor at another scale.
        /// </summary>
        /// <remarks>
        /// Windows sends a top-level window <c>WM_DPICHANGED</c>, which WinForms raises here.
        /// <c>RescaleConstantsForDpi</c> is the child hook, driven by
        /// <c>WM_DPICHANGED_BEFOREPARENT</c>, and overriding it on this form left the sweep below
        /// never running at all - which is why the menus stayed at their old size through a first
        /// attempt at this fix. Both are overridden now: they route to the same idempotent pass, so
        /// it does not matter which of them fires, or whether both do.
        /// </remarks>
        protected override void OnDpiChanged(DpiChangedEventArgs e)
        {
            base.OnDpiChanged(e);
            ScheduleDpiFollowUp(e.DeviceDpiNew);
        }

        protected override void RescaleConstantsForDpi(int deviceDpiOld, int deviceDpiNew)
        {
            base.RescaleConstantsForDpi(deviceDpiOld, deviceDpiNew);
            ScheduleDpiFollowUp(deviceDpiNew);
        }

        /// <summary>
        /// Queues the DPI follow-up rather than running it now.
        /// </summary>
        /// <remarks>
        /// Both hooks fire while WinForms is part-way through its own relayout, and the pass below
        /// reads values that are not final yet - above all this form's font, which is what the
        /// toolbars are given. Posting it puts the work after the message that is being handled,
        /// by which point the framework has finished and everything read is the settled value.
        /// </remarks>
        private void ScheduleDpiFollowUp(int dpi)
        {
            if (!IsHandleCreated || IsDisposed)
                return;

            BeginInvoke(new Action(() => ApplyDpiFollowUp(dpi)));
        }

        /// <summary>
        /// Fixes up everything in this window that a DPI change does not reach by itself.
        /// </summary>
        /// <remarks>
        /// Swept from here, for the whole window, because none of these are reachable from where
        /// they are declared: drop-down menus are separate windows that never receive a DPI change,
        /// and the tool windows that own the other strips sit inside a tab page several parents
        /// away.
        /// <para>
        /// The font is the part that is easy to miss. A ToolStrip does <em>not</em> inherit its
        /// parent's font - measured, a form went from 9pt to 18pt and a Label on it followed while
        /// the MenuStrip stayed at 9pt with nothing of its own set, because ToolStrip reads a
        /// process-wide default instead. So the menu text kept its old size on every DPI change
        /// until it was assigned here.
        /// </para>
        /// </remarks>
        internal void ApplyDpiFollowUp(int dpi)
        {
            if (IsDisposed)
                return;

            // A failure in here is cosmetic, but the handler above this one ends the process, so
            // an exception thrown while relaying out for a new monitor would take every open
            // session with it. A window left looking wrong is the better of the two, and the
            // logged exception is what makes the cause findable. Win32Exception only: this covers
            // handle creation failing part-way through a DPI change, not a bug in the sweep.
            try
            {
                ApplyDpiFollowUpCore(dpi);
            }
            catch (Win32Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage(
                    $"The DPI follow-up for {dpi} DPI failed. The window may be laid out for the " +
                    "monitor it came from until it is moved again.", ex);
            }
        }

        private void ApplyDpiFollowUpCore(int dpi)
        {
            // Captured before the sweep so the log can show both ends of it. The toolbar glyphs
            // were reported staying at their 200% size after a move back to a 100% monitor, and
            // that direction reproduces neither here nor in a headless round trip - so the next
            // round needs the numbers from the machine it happens on rather than another guess.
            var before = DpiScaling.ToolStripsOf(this)
                                   .ToDictionary(strip => strip, Describe);

            DpiScaling.FollowDpiChange(this, dpi, Font);

            // Logged because this cannot be watched where it is developed: the machine that builds
            // this has one monitor, so a DPI change only ever happens on someone else's desk. If a
            // toolbar is ever the wrong size again, this line says whether the pass even ran and
            // what it thought the font and DPI were.
            Runtime.MessageCollector.AddMessage(
                MessageClass.InformationMsg,
                $"DPI follow-up: dpi {dpi}, form font {Font.Name} {Font.SizeInPoints}pt, " +
                $"device dpi {DeviceDpi}, strips {DpiScaling.ToolStripsOf(this).Count()}",
                true);

            // TabControl.ItemSize is measured from the font but does not follow it - measured, it
            // held 100x24 while the font doubled - so the connection tabs have to be told.
            foreach (var connectionWindow in DpiScaling.DescendantsOf(this).OfType<ConnectionWindow>())
                connectionWindow.RefreshTabMetrics(dpi);

            // Same reason as the tabs: the quick connect entry field's width is computed from the
            // font and the DPI rather than inherited from anything, and this pass is the only place
            // that knows which DPI the window settled on.
            foreach (var quickConnect in DpiScaling.DescendantsOf(this).OfType<QuickConnectToolStrip>())
                quickConnect.RefreshEntryFieldMetrics(dpi);

            // And the same again for the connection tree's search row, whose height is measured
            // from the font it ends up with.
            foreach (var tree in DpiScaling.DescendantsOf(this).OfType<ConnectionTreeWindow>())
                tree.RefreshSearchRowMetrics(dpi);

            foreach (var strip in DpiScaling.ToolStripsOf(this))
            {
                before.TryGetValue(strip, out var was);
                Runtime.MessageCollector.AddMessage(
                    MessageClass.InformationMsg,
                    $"DPI follow-up strip {strip.Name}: was {was ?? "(new)"}, now {Describe(strip)}",
                    true);
            }
        }

        /// <summary>Everything about a ToolStrip that a DPI change is supposed to move.</summary>
        private static string Describe(ToolStrip strip)
        {
            var items = string.Join(",", strip.Items.OfType<ToolStripItem>().Select(item => $"{item.Size.Width}x{item.Size.Height}"));
            return $"dpi {strip.DeviceDpi} glyphs {strip.ImageScalingSize.Width} " +
                   $"font {strip.Font.SizeInPoints}pt height {strip.Height} items [{items}]";
        }

	    private void LockToolbarPositions(bool shouldBeLocked)
	    {
		    var toolbars = new ToolStrip[] { _quickConnectToolStrip, msMain };
			foreach (var toolbar in toolbars)
			{
				toolbar.GripStyle = shouldBeLocked
					? ToolStripGripStyle.Hidden
					: ToolStripGripStyle.Visible;
			}
		}

        private void ConnectionsServiceOnConnectionsLoaded(object? sender, ConnectionsLoadedEventArgs connectionsLoadedEventArgs)
        {
            UpdateWindowTitle();
        }

        private void ConnectionsServiceOnConnectionsSaved(object? sender, ConnectionsSavedEventArgs connectionsSavedEventArgs)
        {
            _backupPruner.PruneBackupFiles(connectionsSavedEventArgs.ConnectionFileName, Settings.Default.BackupFileKeepCount);
        }

        private void SetMenuDependencies()
        {
            var connectionInitiator = new ConnectionInitiator();
            fileMenu.TreeWindow = AppWindows.TreeForm;
            fileMenu.ConnectionInitiator = connectionInitiator;

            viewMenu.TsQuickConnect = _quickConnectToolStrip;
            viewMenu.FullscreenHandler = Fullscreen;
            viewMenu.MainForm = this;


            _quickConnectToolStrip.ConnectionInitiator = connectionInitiator;
        }

        private void frmMain_FormClosing(object sender, FormClosingEventArgs e)
		{
            if (!(Runtime.WindowList == null || Runtime.WindowList.Count == 0))
			{
			    var openConnections = 0;
                foreach (BaseWindow window in Runtime.WindowList)
                {
                    var connectionWindow = window as ConnectionWindow;
                    if (connectionWindow != null)
						openConnections = openConnections + connectionWindow.TabController.TabPages.Count;
                }

			    if (openConnections > 0 && (Settings.Default.ConfirmCloseConnection == (int)ConfirmCloseEnum.All | (Settings.Default.ConfirmCloseConnection == (int)ConfirmCloseEnum.Multiple & openConnections > 1) || Settings.Default.ConfirmCloseConnection == (int)ConfirmCloseEnum.Exit))
				{
					var result = CTaskDialog.MessageBox(this, Application.ProductName, Language.strConfirmExitMainInstruction, "", "", "", Language.strCheckboxDoNotShowThisMessageAgain, ETaskDialogButtons.YesNo, ESysIcons.Question, ESysIcons.Question);
					if (CTaskDialog.VerificationChecked)
					{
                        Settings.Default.ConfirmCloseConnection--;
					}
					if (result == DialogResult.No)
					{
						e.Cancel = true;
						return;
					}
				}
			}

            Shutdown.Cleanup(_quickConnectToolStrip, this);
									
			IsClosing = true;
			ApplicationLifecycle.IsShuttingDown = true;

            if (Runtime.WindowList != null)
			{
                foreach (BaseWindow window in Runtime.WindowList)
				{
					window.Close();
				}
			}

			Debug.Print("[END] - " + Convert.ToString(DateTime.Now, CultureInfo.InvariantCulture));
		}
        #endregion
								
        #region Timer
		private void tmrAutoSave_Tick(object sender, EventArgs e)
		{
            Runtime.MessageCollector.AddMessage(MessageClass.DebugMsg, "Doing AutoSave");
			Runtime.ConnectionsService.SaveConnectionsAsync();
		}
        #endregion
		
        #region Window Overrides
        private void frmMain_ResizeBegin(object sender, EventArgs e)
		{
			_inSizeMove = true;
		}

        private void frmMain_Resize(object sender, EventArgs e)
		{
			if (WindowState == FormWindowState.Minimized)
			{
			    if (!Settings.Default.MinimizeToTray) return;
			    if (Runtime.NotificationAreaIcon == null)
			    {
			        Runtime.NotificationAreaIcon = new NotificationAreaIcon();
			    }
			    Hide();
			}
			else
			{
				PreviousWindowState = WindowState;
			}
		}

        private void frmMain_ResizeEnd(object sender, EventArgs e)
		{
			_inSizeMove = false;			
			// This handles activations from clicks that started a size/move operation
			ActivateConnection();
		}				
		
		protected override void WndProc(ref System.Windows.Forms.Message m)
		{
            // Listen for and handle operating system messages
			try
			{
			    // ReSharper disable once SwitchStatementMissingSomeCases
				switch (m.Msg)
				{
				    case NativeMethods.WM_MOUSEACTIVATE:
				        _inMouseActivate = true;
				        break;
				    case NativeMethods.WM_ACTIVATEAPP:
				        _inMouseActivate = false;
				        break;
				    case NativeMethods.WM_ACTIVATE:
				        // Only handle this msg if it was triggered by a click
				        if (NativeMethods.LOWORD(m.WParam) == NativeMethods.WA_CLICKACTIVE)
				        {
				            var controlThatWasClicked = FromChildHandle(NativeMethods.WindowFromPoint(MousePosition))
				                ?? GetChildAtPoint(MousePosition);
				            if (controlThatWasClicked != null)
				            {
				                if (controlThatWasClicked is TreeView ||
				                    controlThatWasClicked is ComboBox ||
				                    controlThatWasClicked is TextBox)
				                {
				                    controlThatWasClicked.Focus();
				                }
				                else if (controlThatWasClicked.CanSelect ||
				                         controlThatWasClicked is MenuStrip ||
				                         controlThatWasClicked is ToolStrip ||
				                         controlThatWasClicked is TabControl)
				                {
                                    // Simulate a mouse event since one wasn't generated by Windows
                                    SimulateClick(controlThatWasClicked);
                                    controlThatWasClicked.Focus();
                                }
				                else
				                {
				                    // This handles activations from clicks that did not start a size/move operation
				                    ActivateConnection();
				                }
				            }
				        }
				        break;
				    case NativeMethods.WM_WINDOWPOSCHANGED:
				        // Ignore this message if the window wasn't activated
				        var windowPos = (NativeMethods.WINDOWPOS)Marshal.PtrToStructure(m.LParam, typeof(NativeMethods.WINDOWPOS));
				        if ((windowPos.flags & NativeMethods.SWP_NOACTIVATE) == 0)
				        {
				            if (!_inMouseActivate && !_inSizeMove)
				                ActivateConnection();
				        }
				        break;
				    case NativeMethods.WM_SYSCOMMAND:
				        var screen = _screenSystemMenu.GetScreenById(m.WParam.ToInt32());
                        if (screen != null)
                            Screens.SendFormToScreen(screen);
				        break;
				}
			}
			catch (Exception ex)
			{
                Runtime.MessageCollector.AddExceptionMessage("frmMain WndProc failed", ex);
            }
									
			base.WndProc(ref m);
		}

        private void SimulateClick(Control control)
        {
            var clientMousePosition = control.PointToClient(MousePosition);
            var temp_wLow = clientMousePosition.X;
            var temp_wHigh = clientMousePosition.Y;
            NativeMethods.SendMessage(control.Handle, NativeMethods.WM_LBUTTONDOWN, (IntPtr)NativeMethods.MK_LBUTTON, (IntPtr)NativeMethods.MAKELPARAM(ref temp_wLow, ref temp_wHigh));
            clientMousePosition.X = temp_wLow;
            clientMousePosition.Y = temp_wHigh;
        }

		private void ActivateConnection()
		{
		    var w = Layout.ActiveDocument as ConnectionWindow;
		    if (w?.TabController.SelectedTab == null) return;
		    var tab = w.TabController.SelectedTab;
		    var ifc = (InterfaceControl)tab.Tag;

		    if (ifc == null) return;

		    ifc.Protocol.Focus();
		    ((ConnectionWindow) ifc.FindForm())?.RefreshInterfaceController();
		}

        private void Layout_ActiveDocumentChanged(object? sender, EventArgs e)
		{
			ActivateConnection();
            var connectionWindow = Layout.ActiveDocument as ConnectionWindow;
		    connectionWindow?.UpdateSelectedConnection();
		}
		
		internal void UpdateWindowTitle()
		{
			if (InvokeRequired)
			{
				Invoke(new MethodInvoker(UpdateWindowTitle));
				return;
			}
									
			var titleBuilder = new StringBuilder(Application.ProductName);
			const string separator = " - ";
									
			if (Runtime.ConnectionsService.IsConnectionsFileLoaded)
			{
				if (!string.IsNullOrEmpty(Runtime.ConnectionsService.ConnectionFileName))
				{
				    titleBuilder.Append(separator);
				    titleBuilder.Append(Settings.Default.ShowCompleteConsPathInTitle
				        ? Runtime.ConnectionsService.ConnectionFileName
                        : Path.GetFileName(Runtime.ConnectionsService.ConnectionFileName));
				}
			}
									
			if (!string.IsNullOrEmpty(SelectedConnection?.Name))
			{
				titleBuilder.Append(separator);
				titleBuilder.Append(SelectedConnection.Name);
			}

            Text = titleBuilder.ToString();
		}
		
        #endregion

        #region Screen Stuff
        public void SetDefaultLayout()
        {
            Layout.ResetToDefaults();
        }
        #endregion

        private void ViewMenu_Opening(object sender, EventArgs e)
        {
            viewMenu.mMenView_DropDownOpening(sender, e);
        }

        private void mainFileMenu1_DropDownOpening(object sender, EventArgs e)
        {
            fileMenu.mMenFile_DropDownOpening(sender, e);
        }
    }
}
