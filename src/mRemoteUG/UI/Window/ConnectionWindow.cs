using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using mRemoteUG.App;
using mRemoteUG.Messages;
using mRemoteUG.App.Info;
using mRemoteUG.Config;
using mRemoteUG.Connection;
using mRemoteUG.Connection.Protocol;
using mRemoteUG.Connection.Protocol.RDP;
using mRemoteUG.Container;
using mRemoteUG.Tools;
using mRemoteUG.Tree;
using mRemoteUG.UI.Forms;
using mRemoteUG.UI.Forms.Input;
using mRemoteUG.UI.TaskDialog;
using Message = System.Windows.Forms.Message;
using System.ComponentModel;

namespace mRemoteUG.UI.Window
{
	public partial class ConnectionWindow : BaseWindow
    {
        public TabControl TabController;
        private readonly IConnectionInitiator _connectionInitiator = new ConnectionInitiator();

        // Tab strip metrics as measured at 96 DPI. Owner-draw is not reached by the auto-scale
        // pass - it runs on every paint with whatever numbers it is given - so these are scaled by
        // hand into the fields below and recomputed whenever the DPI changes.
        private const int LogicalCloseButtonSize = 14;
        private const int LogicalIconSize = 16;
        private const int LogicalTabContentPadding = 4;
        private const int LogicalCloseGlyphInset = 4;
        private const float LogicalCloseGlyphPenWidth = 1.5f;

        private int _closeButtonSize = LogicalCloseButtonSize;
        private int _iconSize = LogicalIconSize;
        private int _tabContentPadding = LogicalTabContentPadding;
        private int _closeGlyphInset = LogicalCloseGlyphInset;
        private float _closeGlyphPenWidth = LogicalCloseGlyphPenWidth;

        private Color _tabTextColor = SystemColors.ControlText;
        private Color _tabTextInactiveColor = SystemColors.GrayText;

        #region Public Methods
        public ConnectionWindow(string formText = "")
        {
            if (formText == "")
            {
                formText = Language.strNewPanel;
            }

            InitializeComponent();
            ApplyTabMetricsForDpi(DeviceDpi);
            SetEventHandlers();
            // ReSharper disable once VirtualMemberCallInConstructor
            Text = formText;
            TabText = formText;
        }

        private void SetEventHandlers()
        {
            SetFormEventHandlers();
            SetTabControllerEventHandlers();
            SetContextMenuEventHandlers();
        }

        private void SetFormEventHandlers()
        {
            Load += Connection_Load;
            FormClosing += Connection_FormClosing;
            FormClosed += Connection_FormClosed;

            // A connection panel is always hosted in the main window now, so its resize
            // notifications come from there for the lifetime of the panel.
            FrmMain.Default.ResizeBegin += Connection_ResizeBegin;
            FrmMain.Default.ResizeEnd += Connection_ResizeEnd;
        }

        private void Connection_FormClosed(object? sender, FormClosedEventArgs e)
        {
            FrmMain.Default.ResizeBegin -= Connection_ResizeBegin;
            FrmMain.Default.ResizeEnd -= Connection_ResizeEnd;
        }

        private void SetTabControllerEventHandlers()
        {
            TabController.DrawItem += TabController_DrawItem;
            TabController.DragDrop += TabController_DragDrop;
            TabController.DragOver += TabController_DragOver;
            TabController.SelectedIndexChanged += TabController_SelectionChanged;
            TabController.MouseDown += TabController_MouseDown;
            TabController.MouseMove += TabController_MouseMove;
            TabController.MouseUp += TabController_MouseUp;
        }

        private void SetContextMenuEventHandlers()
        {
            cmenTabFullscreen.Click += (sender, args) => ToggleFullscreen();
            cmenTabSmartSize.Click += (sender, args) => ToggleSmartSize();
            cmenTabRenameTab.Click += (sender, args) => RenameTab();
            cmenTabDuplicateTab.Click += (sender, args) => DuplicateTab();
            cmenTabReconnect.Click += (sender, args) => Reconnect();
            cmenTabDisconnect.Click += (sender, args) => CloseTabMenu();

            cmenTabPuttySettings.Click += (sender, args) => ShowPuttySettingsDialog();

            // VNC and External Tools menu items stay hidden; those features are gone.
            cmenTabViewOnly.Visible = false;
            cmenTabStartChat.Visible = false;
            cmenTabTransferFile.Visible = false;
            cmenTabRefreshScreen.Visible = false;
            cmenTabSendSpecialKeys.Visible = false;
            cmenTabExternalApps.Visible = false;
        }

        public TabPage AddConnectionTab(ConnectionInfo connectionInfo)
        {
            try
            {
                var nTab = new ConnectionTabPage
                {
                    Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
                };

                if (Settings.Default.ShowProtocolOnTabs)
                    nTab.Text = connectionInfo.Protocol + @": ";
                else
                    nTab.Text = "";

                nTab.Text += connectionInfo.Name;

                if (Settings.Default.ShowLogonInfoOnTabs)
                {
                    nTab.Text += @" (";
                    if (connectionInfo.Domain != "")
                        nTab.Text += connectionInfo.Domain;

                    if (connectionInfo.Username != "")
                    {
                        if (connectionInfo.Domain != "")
                            nTab.Text += @"\";
                        nTab.Text += connectionInfo.Username;
                    }

                    nTab.Text += @")";
                }

                nTab.Text = nTab.Text.Replace("&", "&&");

                var conIcon = ConnectionIcon.TabIconFor(connectionInfo.Icon, LogicalToDeviceUnits(16));
                if (conIcon != null)
                    nTab.Icon = conIcon;

                if (Settings.Default.OpenTabsRightOfSelected)
                    TabController.TabPages.Insert(TabController.SelectedIndex + 1, nTab);
                else
                    TabController.TabPages.Add(nTab);

                TabController.SelectedTab = nTab;
                _ignoreChangeSelectedTabClick = false;
                UpdateTabItemSize();

                return nTab;
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("AddConnectionTab (UI.Window.ConnectionWindow) failed", ex);
            }

            return null;
        }

        public void UpdateSelectedConnection()
        {
            if (TabController.SelectedTab == null)
            {
	            FrmMain.Default.SelectedConnection = null;
            }
            else
            {
                var interfaceControl = TabController.SelectedTab?.Tag as InterfaceControl;
	            FrmMain.Default.SelectedConnection = interfaceControl?.Info;
            }
        }
        #endregion

        #region Form
        private void Connection_Load(object? sender, EventArgs e)
        {
            ApplyLanguage();
        }


        private void ApplyLanguage()
        {
            cmenTabFullscreen.Text = Language.strMenuFullScreenRDP;
            cmenTabSmartSize.Text = Language.strMenuSmartSize;
            cmenTabViewOnly.Text = Language.strMenuViewOnly;
            cmenTabStartChat.Text = Language.strMenuStartChat;
            cmenTabTransferFile.Text = Language.strMenuTransferFile;
            cmenTabRefreshScreen.Text = Language.strMenuRefreshScreen;
            cmenTabSendSpecialKeys.Text = Language.strMenuSendSpecialKeys;
            cmenTabSendSpecialKeysCtrlAltDel.Text = Language.strMenuCtrlAltDel;
            cmenTabSendSpecialKeysCtrlEsc.Text = Language.strMenuCtrlEsc;
            cmenTabExternalApps.Text = Language.strMenuExternalTools;
            cmenTabRenameTab.Text = Language.strMenuRenameTab;
            cmenTabDuplicateTab.Text = Language.strMenuDuplicateTab;
            cmenTabReconnect.Text = Language.strMenuReconnect;
            cmenTabDisconnect.Text = Language.strMenuDisconnect;
            cmenTabPuttySettings.Text = Language.strPuttySettings;
        }

        private void Connection_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (!FrmMain.Default.IsClosing &&
                (Settings.Default.ConfirmCloseConnection == (int)ConfirmCloseEnum.All & TabController.TabPages.Count > 0 ||
                Settings.Default.ConfirmCloseConnection == (int)ConfirmCloseEnum.Multiple & TabController.TabPages.Count > 1))
            {
                var result = CTaskDialog.MessageBox(this, GeneralAppInfo.ProductName, string.Format(Language.strConfirmCloseConnectionPanelMainInstruction, Text), "", "", "", Language.strCheckboxDoNotShowThisMessageAgain, ETaskDialogButtons.YesNo, ESysIcons.Question, ESysIcons.Question);
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

            try
            {
                // Snapshot: Protocol.Close() now completes synchronously when called on the UI
                // thread, and it removes the tab page. TabPageCollection's enumerator has no
                // version check, so mutating during the loop wouldn't throw - it would silently
                // skip pages, leaving sessions un-torn-down.
                var tabs = TabController.TabPages.Cast<TabPage>().ToArray();
                var stopwatch = Stopwatch.StartNew();
                Runtime.MessageCollector.AddMessage(MessageClass.DebugMsg,
                    $"Close: panel '{Text}' closing {tabs.Length} tab(s)", true);

                foreach (var tabP in tabs)
                {
                    if (tabP.Tag == null) continue;
                    var interfaceControl = (InterfaceControl)tabP.Tag;
                    interfaceControl.Protocol.Close();
                }

                // Teardowns triggered by a disconnect wait for an idle turn, and once this window
                // is gone no idle turn is coming. Anything still queued runs now or never.
                Connection.Protocol.ProtocolBase.DrainQueuedTeardownsNow();

                Runtime.MessageCollector.AddMessage(MessageClass.InformationMsg,
                    $"Close: panel '{Text}' finished {tabs.Length} tab(s) in {stopwatch.ElapsedMilliseconds} ms", true);
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("UI.Window.Connection.Connection_FormClosing() failed", ex);
            }
        }

        public new event EventHandler ResizeBegin;
        private void Connection_ResizeBegin(object? sender, EventArgs e)
        {
            ResizeBegin?.Invoke(this, e);
        }

        public new event EventHandler ResizeEnd;
        private void Connection_ResizeEnd(object? sender, EventArgs e)
        {
            ResizeEnd?.Invoke(sender, e);
        }
        #endregion

        #region TabController
        private void CloseConnectionTab()
        {
            try
            {
                var selectedTab = TabController.SelectedTab;
                if (selectedTab == null) return;
                if (Settings.Default.ConfirmCloseConnection == (int)ConfirmCloseEnum.All)
                {
                    var result = CTaskDialog.MessageBox(this, GeneralAppInfo.ProductName, string.Format(Language.strConfirmCloseConnectionMainInstruction, selectedTab.Text), "", "", "", Language.strCheckboxDoNotShowThisMessageAgain, ETaskDialogButtons.YesNo, ESysIcons.Question, ESysIcons.Question);
                    if (CTaskDialog.VerificationChecked)
                    {
                        Settings.Default.ConfirmCloseConnection--;
                    }
                    if (result == DialogResult.No)
                    {
                        return;
                    }
                }

                if (selectedTab.Tag != null)
                {
                    var interfaceControl = (InterfaceControl)selectedTab.Tag;
                    interfaceControl.Protocol.Close();
                }
                else
                {
                    CloseTab(selectedTab);
                }
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("UI.Window.Connection.CloseConnectionTab() failed", ex);
            }

            UpdateSelectedConnection();
        }

        private void TabController_DoubleClickTab()
        {
            _firstClickTicks = 0;
            if (Settings.Default.DoubleClickOnTabClosesIt)
            {
                CloseConnectionTab();
            }
        }

        #region Drag and Drop
        private void TabController_DragDrop(object? sender, DragEventArgs e)
        {
            if (!ConnectionInfoDataObject.TryGetConnections(e.Data, out var modelObjects)) return;
            foreach (var model in modelObjects)
            {
                var modelAsContainer = model as ContainerInfo;
                var modelAsConnection = model as ConnectionInfo;
                if (modelAsContainer != null)
                    _connectionInitiator.OpenConnection(modelAsContainer);
                else if (modelAsConnection != null)
                    _connectionInitiator.OpenConnection(modelAsConnection);
            }
        }

        private void TabController_DragOver(object? sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.None;
            if (!ConnectionInfoDataObject.TryGetConnections(e.Data, out var modelObjects)) return;
            if (!modelObjects.OfType<ConnectionInfo>().Any()) return;
            e.Effect = DragDropEffects.Move;
        }
        #endregion

        #region Owner-drawn tab rendering
        // Native TabControl has no per-tab close button, icon, or custom text color support,
        // so tabs are owner-drawn. Only the active tab shows a close button, matching the
        // previous (Crownwood MultiDocument-style) tab strip's behavior.

        // TabController.SizeMode is Fixed (see Designer) so every tab shares one width taken from
        // ItemSize, rather than each tab auto-sizing to its own Text - ItemSize is a no-op unless
        // SizeMode is Fixed. The native auto-sizing that Normal/OwnerDrawFixed would otherwise use
        // only accounts for the text, not the icon and close button this owner-draw paints beside
        // it, so ItemSize has to be computed here from the widest tab's text instead.
        /// <summary>
        /// Re-derives the owner-drawn tab metrics for a DPI and re-measures the strip.
        /// </summary>
        private void ApplyTabMetricsForDpi(int dpi)
        {
            _closeButtonSize = DpiScaling.Scale(LogicalCloseButtonSize, dpi);
            _iconSize = DpiScaling.Scale(LogicalIconSize, dpi);
            _tabContentPadding = DpiScaling.Scale(LogicalTabContentPadding, dpi);
            _closeGlyphInset = DpiScaling.Scale(LogicalCloseGlyphInset, dpi);
            _closeGlyphPenWidth = DpiScaling.Scale(LogicalCloseGlyphPenWidth, dpi);
            UpdateTabItemSize();
        }

        protected override void RescaleConstantsForDpi(int deviceDpiOld, int deviceDpiNew)
        {
            base.RescaleConstantsForDpi(deviceDpiOld, deviceDpiNew);
            ApplyTabMetricsForDpi(deviceDpiNew);
        }

        /// <summary>
        /// Re-measures the tab strip once a DPI change has fully settled.
        /// </summary>
        /// <remarks>
        /// <see cref="RescaleConstantsForDpi"/> above runs part-way through the change, and
        /// <see cref="UpdateTabItemSize"/> measures the tab text with the font the control has at
        /// that moment - which is still the old one, so the strip comes out sized for the monitor
        /// it just left. <c>FrmMain</c> calls this afterwards, when the font is the new one.
        /// </remarks>
        internal void RefreshTabMetrics(int dpi)
        {
            if (IsDisposed)
                return;

            ApplyTabMetricsForDpi(dpi);
        }

        private void UpdateTabItemSize()
        {
            if (TabController.TabPages.Count == 0) return;

            var maxTextWidth = 0;
            using (var g = TabController.CreateGraphics())
            {
                foreach (TabPage page in TabController.TabPages)
                {
                    var textWidth = TextRenderer.MeasureText(g, page.Text, TabController.Font).Width;
                    if (textWidth > maxTextWidth)
                        maxTextWidth = textWidth;
                }
            }

            var width = _tabContentPadding + _iconSize + _tabContentPadding + maxTextWidth +
                        _tabContentPadding + _closeButtonSize + _tabContentPadding;
            var height = Math.Max(_iconSize, _closeButtonSize) + _tabContentPadding * 2;
            TabController.ItemSize = new Size(width, height);
        }

        private void TabController_DrawItem(object? sender, DrawItemEventArgs e)
        {
            // The native control can ask for an index the managed collection does not hold:
            // reordering a page (Remove then Insert) leaves a window in each call where the two
            // sides disagree by one, and the native side may repaint synchronously inside it.
            // See docs/platform-findings.md, "Owner-drawn tab strip".
            if (e.Index < 0 || e.Index >= TabController.TabPages.Count) return;

            var page = TabController.TabPages[e.Index] as ConnectionTabPage;
            var bounds = TabController.GetTabRect(e.Index);
            var selected = e.Index == TabController.SelectedIndex;

            using (var backBrush = new SolidBrush(TabController.BackColor))
                e.Graphics.FillRectangle(backBrush, bounds);

            var contentRect = bounds;
            contentRect.Inflate(-_tabContentPadding, -_tabContentPadding);

            var textLeft = contentRect.Left;
            if (page?.Icon != null)
            {
                var iconRect = new Rectangle(textLeft, contentRect.Top + (contentRect.Height - _iconSize) / 2, _iconSize, _iconSize);
                e.Graphics.DrawIcon(page.Icon, iconRect);
                textLeft += _iconSize + _tabContentPadding;
            }

            var textColor = selected ? _tabTextColor : _tabTextInactiveColor;
            var textRight = contentRect.Right;
            if (selected)
            {
                var closeRect = GetCloseButtonRect(bounds);
                textRight = closeRect.Left - _tabContentPadding;
                DrawCloseButton(e.Graphics, closeRect, textColor);
            }

            var textRect = new Rectangle(textLeft, contentRect.Top, Math.Max(0, textRight - textLeft), contentRect.Height);
            TextRenderer.DrawText(e.Graphics, page?.Text, TabController.Font, textRect, textColor,
                TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);

            if (selected)
            {
                using (var pen = new Pen(_tabTextColor))
                    e.Graphics.DrawLine(pen, bounds.Left, bounds.Bottom - 1, bounds.Right, bounds.Bottom - 1);
            }
        }

        // Instance methods rather than static: the metrics they use are now per-DPI fields.
        private Rectangle GetCloseButtonRect(Rectangle tabBounds)
        {
            var y = tabBounds.Top + (tabBounds.Height - _closeButtonSize) / 2;
            return new Rectangle(tabBounds.Right - _closeButtonSize - _tabContentPadding, y,
                                 _closeButtonSize, _closeButtonSize);
        }

        private void DrawCloseButton(Graphics g, Rectangle rect, Color color)
        {
            using (var pen = new Pen(color, _closeGlyphPenWidth))
            {
                var inflated = Rectangle.Inflate(rect, -_closeGlyphInset, -_closeGlyphInset);
                g.DrawLine(pen, inflated.Left, inflated.Top, inflated.Right, inflated.Bottom);
                g.DrawLine(pen, inflated.Left, inflated.Bottom, inflated.Right, inflated.Top);
            }
        }

        private TabPage TabPageFromPoint(Point point)
        {
            for (var i = 0; i < TabController.TabPages.Count; i++)
            {
                if (TabController.GetTabRect(i).Contains(point))
                    return TabController.TabPages[i];
            }
            return null;
        }

        private bool IsOnCloseButton(Point point)
        {
            if (TabController.SelectedIndex < 0) return false;
            var tabRect = TabController.GetTabRect(TabController.SelectedIndex);
            return GetCloseButtonRect(tabRect).Contains(point);
        }
        #endregion
        #endregion

        #region Tab Menu
        private void ShowPuttySettingsDialog()
        {
            try
            {
                var interfaceControl = TabController.SelectedTab?.Tag as InterfaceControl;
                var puttyBase = interfaceControl?.Protocol as PuttyBase;
                puttyBase?.ShowSettingsDialog();
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("ShowPuttySettingsDialog (UI.Window.ConnectionWindow) failed", ex);
            }
        }

        private void ShowHideMenuButtons()
        {
            try
            {
                var interfaceControl = (InterfaceControl)TabController.SelectedTab?.Tag;
                if (interfaceControl == null) return;

                var rdp = interfaceControl.Protocol as RdpProtocol;
                if (rdp != null)
                {
                    cmenTabFullscreen.Visible = true;
                    cmenTabFullscreen.Checked = rdp.Fullscreen;
                    cmenTabSmartSize.Visible = true;
                    cmenTabSmartSize.Checked = rdp.SmartSize;
                }
                else
                {
                    cmenTabFullscreen.Visible = false;
                    cmenTabSmartSize.Visible = false;
                }

                cmenTabPuttySettings.Visible = interfaceControl.Protocol is PuttyBase;
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("ShowHideMenuButtons (UI.Window.ConnectionWindow) failed", ex);
            }
        }
        #endregion

        #region Tab Actions
        private void ToggleSmartSize()
        {
            try
            {
                if (!(TabController.SelectedTab?.Tag is InterfaceControl)) return;
                var interfaceControl = (InterfaceControl)TabController.SelectedTab?.Tag;

                var protocol = interfaceControl.Protocol as RdpProtocol;
                protocol?.ToggleSmartSize();
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("ToggleSmartSize (UI.Window.ConnectionWindow) failed", ex);
            }
        }

        private void ToggleFullscreen()
        {
            try
            {
                var interfaceControl = TabController.SelectedTab?.Tag as InterfaceControl;
                var rdp = interfaceControl?.Protocol as RdpProtocol;
                rdp?.ToggleFullscreen();
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("ToggleFullscreen (UI.Window.ConnectionWindow) failed", ex);
            }
        }

        private void CloseTabMenu()
        {
            try
            {
                var interfaceControl = TabController.SelectedTab?.Tag as InterfaceControl;
                interfaceControl?.Protocol.Close();
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("CloseTabMenu (UI.Window.ConnectionWindow) failed", ex);
            }
        }

        private void DuplicateTab()
        {
            try
            {
                var interfaceControl = TabController.SelectedTab?.Tag as InterfaceControl;
                if (interfaceControl == null) return;
                _connectionInitiator.OpenConnection(interfaceControl.Info, ConnectionInfo.Force.DoNotJump);
                _ignoreChangeSelectedTabClick = false;
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("DuplicateTab (UI.Window.ConnectionWindow) failed", ex);
            }
        }

        private void Reconnect()
        {
            try
            {
                var interfaceControl = TabController.SelectedTab?.Tag as InterfaceControl;
                if (interfaceControl == null) return;
                interfaceControl.Protocol.Close();
                _connectionInitiator.OpenConnection(interfaceControl.Info, ConnectionInfo.Force.DoNotJump);
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("Reconnect (UI.Window.ConnectionWindow) failed", ex);
            }
        }

        private void RenameTab()
        {
            try
            {
                var newTitle = TabController.SelectedTab.Text;
                if (input.InputBox(Language.strNewTitle, Language.strNewTitle + ":", ref newTitle) == DialogResult.OK && !string.IsNullOrEmpty(newTitle))
                {
                    TabController.SelectedTab.Text = newTitle.Replace("&", "&&");
                    UpdateTabItemSize();
                }
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("RenameTab (UI.Window.ConnectionWindow) failed", ex);
            }
        }

        #endregion

        #region Protocols
        public void Prot_Event_Closed(object sender)
        {
            var protocolBase = sender as ProtocolBase;
            var tabPage = protocolBase?.InterfaceControl.Parent as TabPage;
            if (tabPage != null)
                CloseTab(tabPage);
        }
        #endregion

        #region Tabs
        private delegate void CloseTabDelegate(TabPage tabToBeClosed, int retriesLeft);

        private void CloseTab(TabPage tabToBeClosed)
        {
            CloseTab(tabToBeClosed, 1);
        }

        // retriesLeft bounds the COMException retry. The RDP ActiveX can briefly leave the tab
        // control in a state where Remove() throws; retrying once clears it. Retrying forever
        // (as this used to) turns that into an uncatchable StackOverflowException.
        private void CloseTab(TabPage tabToBeClosed, int retriesLeft)
        {
            if (tabToBeClosed.Disposing || tabToBeClosed.IsDisposed)
                return;

            if (TabController.InvokeRequired)
            {
                CloseTabDelegate s = CloseTab;

                try
                {
                    TabController.Invoke(s, tabToBeClosed, retriesLeft);
                }
                catch (COMException ex)
                {
                    if (retriesLeft > 0)
                        CloseTab(tabToBeClosed, retriesLeft - 1);
                    else
                        Runtime.MessageCollector.AddExceptionMessage("Couldn't close tab - out of COM retries", ex);
                }
                catch (Exception ex)
                {
                    Runtime.MessageCollector.AddExceptionMessage("Couldn't close tab", ex);
                }
            }
            else
            {
                var stopwatch = Stopwatch.StartNew();
                try
                {
                    TabController.TabPages.Remove(tabToBeClosed);
                    // Remove does not dispose: WinForms reparents the page's live window handle to
                    // the thread's parking window, where it waits for a finalizer. One window handle
                    // per Session closed, for the life of the process.
                    //
                    // What makes this safe is stage 3 of the teardown, not stage 5. The hosted control
                    // has already left the page by the time the Closed event brings us here - disposed
                    // if its session was down, moved to the holding form if it was parked - so the
                    // cascade below reaches only the InterfaceControl, which stage 5 then disposes
                    // again harmlessly. Disposing the page while an ActiveX control was still inside it
                    // is the ordering ADR-0005 exists to avoid.
                    //
                    // The page's Icon is a plain auto-property holding an Icon from ConnectionIcon's
                    // shared (name, size) cache, and TabPage.Dispose does not reach it. That matters:
                    // the cache hands the same Icon to every tab that asks for that name and size.
                    tabToBeClosed.Dispose();
                    _ignoreChangeSelectedTabClick = false;
                    Runtime.MessageCollector.AddMessage(MessageClass.DebugMsg,
                        $"Close timing: tab page removed and disposed in {stopwatch.ElapsedMilliseconds} ms", true);
                }
                catch (COMException ex)
                {
                    if (retriesLeft > 0)
                        CloseTab(tabToBeClosed, retriesLeft - 1);
                    else
                        Runtime.MessageCollector.AddExceptionMessage("Couldn't close tab - out of COM retries", ex);
                }
                catch (Exception ex)
                {
                    Runtime.MessageCollector.AddExceptionMessage("Couldn't close tab", ex);
                }

                if (TabController.TabPages.Count == 0)
                {
                    Close();
                }
            }
        }

        private bool _ignoreChangeSelectedTabClick;
        private bool _suppressSelectionChangedEvent;
        private void TabController_SelectionChanged(object? sender, EventArgs e)
        {
            if (_suppressSelectionChangedEvent) return;
            _ignoreChangeSelectedTabClick = true;
            UpdateSelectedConnection();
            FocusInterfaceController();
            RefreshInterfaceController();
        }

        private int _firstClickTicks;
        private Rectangle _doubleClickRectangle;
        private void TabController_MouseUp(object? sender, MouseEventArgs e)
        {
            try
            {
                if (InTabDrag)
                {
                    TabController_PageDragEnd(sender, e);
                    _dragArmed = false;
                    return;
                }
                _dragArmed = false;

                if (e.Button == MouseButtons.Left && IsOnCloseButton(e.Location))
                {
                    CloseConnectionTab();
                    return;
                }

                if (!(NativeMethods.GetForegroundWindow() == FrmMain.Default.Handle) && !_ignoreChangeSelectedTabClick)
                {
                    var clickedTab = TabPageFromPoint(e.Location);
                    if (clickedTab != null && TabController.SelectedTab != clickedTab)
                    {
                        NativeMethods.SetForegroundWindow(Handle);
                        TabController.SelectedTab = clickedTab;
                    }
                }
                _ignoreChangeSelectedTabClick = false;

                switch (e.Button)
                {
                    case MouseButtons.Left:
                        var currentTicks = Environment.TickCount;
                        var elapsedTicks = currentTicks - _firstClickTicks;
                        if (elapsedTicks > SystemInformation.DoubleClickTime || !_doubleClickRectangle.Contains(MousePosition))
                        {
                            _firstClickTicks = currentTicks;
                            _doubleClickRectangle = new Rectangle(MousePosition.X - SystemInformation.DoubleClickSize.Width / 2, MousePosition.Y - SystemInformation.DoubleClickSize.Height / 2, SystemInformation.DoubleClickSize.Width, SystemInformation.DoubleClickSize.Height);
                            FocusInterfaceController();
                        }
                        else
                        {
                            TabController_DoubleClickTab();
                        }
                        break;
                    case MouseButtons.Middle:
                        CloseConnectionTab();
                        break;
                    case MouseButtons.Right:
                        if (TabController.SelectedTab?.Tag == null) return;
                        ShowHideMenuButtons();
                        NativeMethods.SetForegroundWindow(Handle);
                        cmenTab.Show(TabController, e.Location);
                        break;
                }
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("TabController_MouseUp (UI.Window.ConnectionWindow) failed", ex);
            }
        }

        private void FocusInterfaceController()
        {
            try
            {
                var interfaceControl = TabController.SelectedTab?.Tag as InterfaceControl;
                interfaceControl?.Protocol?.Focus();
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("FocusIC (UI.Window.ConnectionWindow) failed", ex);
            }
        }

        public void RefreshInterfaceController()
        {
            try
            {
                // No-op for RDP; VNC-specific screen refresh was removed with the VNC protocol.
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("RefreshIC (UI.Window.Connection) failed", ex);
            }
        }
        #endregion

        #region Window Overrides
        protected override void WndProc(ref Message m)
        {
            try
            {
                if (m.Msg == NativeMethods.WM_MOUSEACTIVATE)
                {
                    var selectedTab = TabController.SelectedTab;
                    if (selectedTab == null) return;
                    {
                        var tabClientRectangle = selectedTab.RectangleToScreen(selectedTab.ClientRectangle);
                        if (tabClientRectangle.Contains(MousePosition))
                        {
                            var interfaceControl = selectedTab.Tag as InterfaceControl;
                            if (interfaceControl?.Info != null && ProtocolTypes.IsSupported(interfaceControl.Info.Protocol))
                            {
                                interfaceControl.Protocol.Focus();
                                return; // Do not pass to base class
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("UI.Window.Connection.WndProc() failed.", ex);
            }

            base.WndProc(ref m);
        }
        #endregion

        #region Tab drag and drop
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool InTabDrag { get; set; }

        private Point _dragStartPoint;
        private bool _dragArmed;

        private void TabController_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || IsOnCloseButton(e.Location))
            {
                _dragArmed = false;
                return;
            }
            _dragStartPoint = e.Location;
            _dragArmed = TabPageFromPoint(e.Location) != null;
        }

        private void TabController_MouseMove(object? sender, MouseEventArgs e)
        {
            if (!_dragArmed || e.Button != MouseButtons.Left) return;

            if (!InTabDrag)
            {
                if (Math.Abs(e.X - _dragStartPoint.X) < SystemInformation.DragSize.Width / 2 &&
                    Math.Abs(e.Y - _dragStartPoint.Y) < SystemInformation.DragSize.Height / 2)
                    return;
                TabController_PageDragStart(sender, e);
            }

            TabController_PageDragMove(sender, e);
        }

        private void TabController_PageDragStart(object? sender, MouseEventArgs e)
        {
            Cursor = Cursors.SizeWE;
            TabController.Capture = true;
        }

        private void TabController_PageDragMove(object? sender, MouseEventArgs e)
        {
            InTabDrag = true;

            var sourceTab = TabController.SelectedTab;
            var destinationTab = TabPageFromPoint(e.Location);

            if (destinationTab == null || !TabController.TabPages.Contains(destinationTab) || sourceTab == destinationTab)
                return;

            var targetIndex = TabController.TabPages.IndexOf(destinationTab);

            _suppressSelectionChangedEvent = true;
            TabController.TabPages.Remove(sourceTab);
            TabController.TabPages.Insert(targetIndex, sourceTab);
            TabController.SelectedTab = sourceTab;
            _suppressSelectionChangedEvent = false;
        }

        private void TabController_PageDragEnd(object? sender, MouseEventArgs e)
        {
            Cursor = Cursors.Default;
            TabController.Capture = false;
            InTabDrag = false;
            var interfaceControl = TabController?.SelectedTab?.Tag as InterfaceControl;
            interfaceControl?.Protocol.Focus();
        }
        #endregion
    }
}
