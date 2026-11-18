using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;

using mRemoteUG.App;
using mRemoteUG.App.Info;
using mRemoteUG.Config;
using mRemoteUG.Connection;
using mRemoteUG.Connection.Protocol;
using mRemoteUG.Connection.Protocol.RDP;
using mRemoteUG.Container;
using mRemoteUG.Messages;
using mRemoteUG.Tree;
using mRemoteUG.UI.Forms;
using mRemoteUG.UI.Forms.Input;
using mRemoteUG.UI.Panels;
using mRemoteUG.UI.TaskDialog;

using Message = System.Windows.Forms.Message;

namespace mRemoteUG.UI.Window;

public partial class ConnectionWindow : BaseWindow
{
    // Assigned by InitializeComponent in ConnectionWindow.Designer.cs, which the constructor runs
    // first; the designer file cannot say so with [MemberNotNull].
    public TabControl TabController { get; private set; } = null!;
    private readonly ConnectionInitiator _connectionInitiator = new ConnectionInitiator();

    // Tab strip metrics as measured at 96 DPI. Owner-draw is not reached by the auto-scale
    // pass - it runs on every paint with whatever numbers it is given - so these are scaled by
    // hand into the fields below and recomputed whenever the DPI changes.
    internal const int LogicalCloseButtonSize = 14;
    internal const int LogicalIconSize = 16;
    internal const int LogicalTabContentPadding = 4;
    private const int LogicalCloseGlyphInset = 4;
    private const float LogicalCloseGlyphPenWidth = 1.5f;

    /// <summary>
    /// The widest a Tab's title is shown; a longer one is cut with "…" and gets a tooltip (#261).
    /// </summary>
    /// <remarks>A first value, to be settled by looking at it on real monitors.</remarks>
    internal const int LogicalMaxTitleWidth = 240;

    private int _closeButtonSize = LogicalCloseButtonSize;
    private int _iconSize = LogicalIconSize;
    private int _tabContentPadding = LogicalTabContentPadding;
    private int _closeGlyphInset = LogicalCloseGlyphInset;
    private float _closeGlyphPenWidth = LogicalCloseGlyphPenWidth;
    private int _maxTitleWidth = LogicalMaxTitleWidth;

    private readonly Color _tabTextColor = SystemColors.ControlText;
    private readonly Color _tabTextInactiveColor = SystemColors.GrayText;

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
        TabController.FontChanged += TabController_FontChanged;
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

    public TabPage? AddConnectionTab(ConnectionInfo connectionInfo)
    {
        try
        {
            var nTab = new ConnectionTabPage
            {
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
            };

            string title = "";
            if (Settings.Default.ShowProtocolOnTabs)
            {
                title = connectionInfo.Protocol + @": ";
            }

            title += connectionInfo.Name;

            if (Settings.Default.ShowLogonInfoOnTabs)
            {
                title += @" (";
                if (connectionInfo.Domain != "")
                {
                    title += connectionInfo.Domain;
                }

                if (connectionInfo.Username != "")
                {
                    if (connectionInfo.Domain != "")
                    {
                        title += @"\";
                    }

                    title += connectionInfo.Username;
                }

                title += @")";
            }

            nTab.SetTitle(title, TabController.Font, _maxTitleWidth);

            nTab.IconName = connectionInfo.Icon;
            ApplyTabIcon(nTab);

            if (Settings.Default.OpenTabsRightOfSelected)
            {
                TabController.TabPages.Insert(TabController.SelectedIndex + 1, nTab);
            }
            else
            {
                TabController.TabPages.Add(nTab);
            }

            TabController.SelectedTab = nTab;
            _ignoreChangeSelectedTabClick = false;

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

    [SuppressMessage("Performance", "CA1863",
                     Justification = "The format is a localized resource read at run time; "
                                     + "a cached CompositeFormat would freeze the UI language.")]
    private void Connection_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!FrmMain.Default.IsClosing &&
            (Settings.Default.ConfirmCloseConnection == (int)ConfirmCloseMode.All &
             TabController.TabPages.Count > 0 ||
             Settings.Default.ConfirmCloseConnection == (int)ConfirmCloseMode.Multiple &
             TabController.TabPages.Count > 1))
        {
            DialogResult result = CTaskDialog.MessageBox(
                this,
                GeneralAppInfo.ProductName,
                string.Format(
                    CultureInfo.CurrentCulture,
                    Language.strConfirmCloseConnectionPanelMainInstruction,
                    Text),
                "",
                "",
                "",
                Language.strCheckboxDoNotShowThisMessageAgain,
                ETaskDialogButtons.YesNo,
                ESysIcons.Question,
                ESysIcons.Question);
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
            TabPage[] tabs = TabController.TabPages.Cast<TabPage>().ToArray();
            var stopwatch = Stopwatch.StartNew();
            Runtime.MessageCollector.AddMessage(MessageClass.DebugMsg,
                $"Close: panel '{Text}' closing {tabs.Length} tab(s)", true);

            foreach (TabPage tabP in tabs)
            {
                if (tabP.Tag == null)
                {
                    continue;
                }

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

    public new event EventHandler? ResizeBegin;
    private void Connection_ResizeBegin(object? sender, EventArgs e)
    {
        ResizeBegin?.Invoke(this, e);
    }

    public new event EventHandler? ResizeEnd;
    private void Connection_ResizeEnd(object? sender, EventArgs e)
    {
        ResizeEnd?.Invoke(sender, e);
    }
    #endregion

    #region TabController
    [SuppressMessage("Performance", "CA1863",
                     Justification = "The format is a localized resource read at run time; "
                                     + "a cached CompositeFormat would freeze the UI language.")]
    private void CloseConnectionTab(TabPage? tabToClose)
    {
        try
        {
            if (tabToClose == null)
            {
                return;
            }

            if (Settings.Default.ConfirmCloseConnection == (int)ConfirmCloseMode.All)
            {
                DialogResult result = CTaskDialog.MessageBox(
                    this,
                    GeneralAppInfo.ProductName,
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Language.strConfirmCloseConnectionMainInstruction,
                        TabTitleOf(tabToClose)),
                    "",
                    "",
                    "",
                    Language.strCheckboxDoNotShowThisMessageAgain,
                    ETaskDialogButtons.YesNo,
                    ESysIcons.Question,
                    ESysIcons.Question);
                if (CTaskDialog.VerificationChecked)
                {
                    Settings.Default.ConfirmCloseConnection--;
                }
                if (result == DialogResult.No)
                {
                    return;
                }
            }

            if (tabToClose.Tag != null)
            {
                var interfaceControl = (InterfaceControl)tabToClose.Tag;
                interfaceControl.Protocol.Close();
            }
            else
            {
                CloseTab(tabToClose);
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
            CloseConnectionTab(TabController.SelectedTab);
        }
    }

    #region Drag and Drop
    private void TabController_DragDrop(object? sender, DragEventArgs e)
    {
        if (!ConnectionInfoDataObject.TryGetConnections(e.Data, out IList<ConnectionInfo>? modelObjects))
        {
            return;
        }

        foreach (ConnectionInfo model in modelObjects)
        {
            var modelAsContainer = model as ContainerInfo;
            var modelAsConnection = model as ConnectionInfo;
            if (modelAsContainer != null)
            {
                _connectionInitiator.OpenConnection(modelAsContainer);
            }
            else if (modelAsConnection != null)
            {
                _connectionInitiator.OpenConnection(modelAsConnection);
            }
        }
    }

    private void TabController_DragOver(object? sender, DragEventArgs e)
    {
        e.Effect = DragDropEffects.None;
        if (!ConnectionInfoDataObject.TryGetConnections(e.Data, out IList<ConnectionInfo>? modelObjects))
        {
            return;
        }

        if (!modelObjects.OfType<ConnectionInfo>().Any())
        {
            return;
        }

        e.Effect = DragDropEffects.Move;
    }
    #endregion

    #region Owner-drawn tab rendering
    // Native TabControl has no per-tab close button, icon, or custom text color support,
    // so tabs are owner-drawn. Only the active tab shows a close button, matching the
    // previous (Crownwood MultiDocument-style) tab strip's behavior.

    // Each Tab is sized by comctl32 from its own text (SizeMode Normal, see Designer). That
    // measures only the text, not the icon and close button this owner-draw paints beside it, so
    // they are given room through the padding comctl32 adds on each side of the text. The text
    // it measures is the display title, cut at a maximum width; the full title is kept on the
    // ConnectionTabPage. Before #261 every Tab shared one Fixed width taken from the widest
    // title, and nothing narrowed it again when that Tab closed.
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
        _maxTitleWidth = DpiScaling.Scale(LogicalMaxTitleWidth, dpi);

        // DrawIcon stretches whatever frame a Tab holds into _iconSize, so a frame picked at the
        // old DPI goes blurry - the toolbars' defect in ADR-0010's 2026-09-25 amendment.
        foreach (ConnectionTabPage page in TabController.TabPages.OfType<ConnectionTabPage>())
        {
            ApplyTabIcon(page);
        }

        MeasureTabStrip();
    }

    /// <summary>
    /// The room on each side of a Tab's text: the icon on the left, the close button on the right.
    /// </summary>
    /// <remarks>
    /// comctl32's padding is the same on both sides, so it is sized for the wider of the two; the
    /// narrower side keeps the difference spare.
    /// </remarks>
    private int TabSideRoom => _tabContentPadding + Math.Max(_iconSize, _closeButtonSize) + _tabContentPadding;

    /// <summary>
    /// Gives the strip the padding and height for the current metrics and font, and shortens and
    /// re-measures every Tab's title.
    /// </summary>
    /// <remarks>
    /// Runs from both DPI routes and on every font change of the strip. The window's own hook runs
    /// part-way through a DPI change, while the font is still the old monitor's (see
    /// <see cref="RefreshTabMetrics"/>); the font change that follows brings it back here, so the
    /// titles are cut against the font they are drawn in whichever route arrives last.
    /// <para>
    /// Sends messages and sets items, and pumps nothing, so it is safe wherever it runs - see
    /// "Owner-drawn tab strip" in docs/platform-findings.md.
    /// </para>
    /// </remarks>
    private void MeasureTabStrip()
    {
        if (TabController is SessionTabControl strip)
        {
            strip.ItemPadding = new Point(TabSideRoom, _tabContentPadding);
        }

        // Only the height: comctl32 takes the width of a Tab from its text unless SizeMode is Fixed.
        // The height is the one every Tab had before #261, from the icon and close button.
        int height = Math.Max(_iconSize, _closeButtonSize) + _tabContentPadding * 2;
        TabController.ItemSize = new Size(0, height);

        foreach (ConnectionTabPage page in TabController.TabPages.OfType<ConnectionTabPage>())
        {
            page.ShowTitle(TabController.Font, _maxTitleWidth);
        }
    }

    private void TabController_FontChanged(object? sender, EventArgs e)
    {
        MeasureTabStrip();
    }

    /// <summary>The full title of a Tab, whatever the strip shows of it.</summary>
    internal static string TabTitleOf(TabPage page) =>
        page is ConnectionTabPage connectionTab ? connectionTab.Title : page.Text;

    /// <summary>Gives a Tab a new full title, cut and measured for the strip.</summary>
    internal void RetitleTab(TabPage page, string title)
    {
        if (page is ConnectionTabPage connectionTab)
        {
            connectionTab.SetTitle(title, TabController.Font, _maxTitleWidth);
        }
        else
        {
            page.Text = title.Replace("&", "&&", StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// The Tabs whose native rectangle does not hold their icon, title and close button at the
    /// current metrics and font, described for the self-test; empty when every Tab fits.
    /// </summary>
    internal IEnumerable<string> TabsThatDoNotFit()
    {
        for (int i = 0; i < TabController.TabPages.Count; i++)
        {
            if (TabController.TabPages[i] is not ConnectionTabPage page)
            {
                continue;
            }

            int text = TextRenderer.MeasureText(page.DisplayTitle, TabController.Font, Size.Empty,
                                                ConnectionTabPage.MeasureFlags).Width;
            int needed = TabSideRoom * 2 + text;
            int width = TabController.GetTabRect(i).Width;
            if (width < needed || text > _maxTitleWidth)
            {
                yield return $"'{page.Title}' is {width} px wide for {needed} px of content, " +
                             $"title {text} of at most {_maxTitleWidth} px";
            }
        }
    }

    /// <summary>
    /// Gives a Tab its connection icon as a frame of the size the strip draws it at.
    /// </summary>
    /// <remarks>
    /// <see cref="_iconSize"/> rather than <c>LogicalToDeviceUnits</c>: that reads the cached
    /// <c>DeviceDpi</c>, and the frame has to match the rectangle, whatever the cache says.
    /// </remarks>
    private void ApplyTabIcon(ConnectionTabPage page)
    {
        Icon? icon = ConnectionIcon.TabIconFor(page.IconName, _iconSize);
        if (icon != null)
        {
            page.Icon = icon;
        }
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
    /// <see cref="MeasureTabStrip"/> cuts the titles with the font the control has at that
    /// moment - which is still the old one. The font change that follows measures the strip
    /// again, and <c>FrmMain</c> calls this afterwards as well, when the font is the new one: a
    /// driven change was seen to reach the strip only through this route (#256), so neither is
    /// relied on alone.
    /// </remarks>
    internal void RefreshTabMetrics(int dpi)
    {
        if (IsDisposed)
        {
            return;
        }

        ApplyTabMetricsForDpi(dpi);
    }

    private void TabController_DrawItem(object? sender, DrawItemEventArgs e)
    {
        // The native control can ask for an index the managed collection does not hold:
        // reordering a page (Remove then Insert) leaves a window in each call where the two
        // sides disagree by one, and the native side may repaint synchronously inside it.
        // See docs/platform-findings.md, "Owner-drawn tab strip".
        if (e.Index < 0 || e.Index >= TabController.TabPages.Count)
        {
            return;
        }

        var page = TabController.TabPages[e.Index] as ConnectionTabPage;
        Rectangle bounds = TabController.GetTabRect(e.Index);
        bool selected = e.Index == TabController.SelectedIndex;

        using (var backBrush = new SolidBrush(TabController.BackColor))
        {
            e.Graphics.FillRectangle(backBrush, bounds);
        }

        Rectangle contentRect = bounds;
        contentRect.Inflate(-_tabContentPadding, -_tabContentPadding);

        int textLeft = contentRect.Left;
        if (page?.Icon != null)
        {
            var iconRect = new Rectangle(
                textLeft,
                contentRect.Top + (contentRect.Height - _iconSize) / 2,
                _iconSize,
                _iconSize);
            e.Graphics.DrawIcon(page.Icon, iconRect);
            textLeft += _iconSize + _tabContentPadding;
        }

        Color textColor = selected ? _tabTextColor : _tabTextInactiveColor;
        int textRight = contentRect.Right;
        if (selected)
        {
            Rectangle closeRect = GetCloseButtonRect(bounds);
            textRight = closeRect.Left - _tabContentPadding;
            DrawCloseButton(e.Graphics, closeRect, textColor);
        }

        // The display title, cut to fit already, drawn as comctl32 measured it: NoPadding, or the
        // margin TextRenderer adds by default does not fit and the title is cut a second time.
        var textRect = new Rectangle(textLeft, contentRect.Top, Math.Max(0, textRight - textLeft), contentRect.Height);
        TextRenderer.DrawText(e.Graphics, page?.DisplayTitle ?? TabController.TabPages[e.Index].Text,
            TabController.Font, textRect, textColor,
            ConnectionTabPage.MeasureFlags
            | TextFormatFlags.EndEllipsis
            | TextFormatFlags.VerticalCenter
            | TextFormatFlags.Left);

        if (selected)
        {
            using (var pen = new Pen(_tabTextColor))
            {
                e.Graphics.DrawLine(pen, bounds.Left, bounds.Bottom - 1, bounds.Right, bounds.Bottom - 1);
            }
        }
    }

    // Instance methods rather than static: the metrics they use are now per-DPI fields.
    private Rectangle GetCloseButtonRect(Rectangle tabBounds)
    {
        int y = tabBounds.Top + (tabBounds.Height - _closeButtonSize) / 2;
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

    private TabPage? TabPageFromPoint(Point point)
    {
        for (int i = 0; i < TabController.TabPages.Count; i++)
        {
            if (TabController.GetTabRect(i).Contains(point))
            {
                return TabController.TabPages[i];
            }
        }
        return null;
    }

    private bool IsOnCloseButton(Point point)
    {
        if (TabController.SelectedIndex < 0)
        {
            return false;
        }

        Rectangle tabRect = TabController.GetTabRect(TabController.SelectedIndex);
        return GetCloseButtonRect(tabRect).Contains(point);
    }
    #endregion
    #endregion

    #region Tab Menu
    /// <summary>
    /// The Tab the Tab menu was last opened on, which its item states and every item on it
    /// refer to. Not necessarily the selected Tab: a right-click leaves an inactive Tab inactive.
    /// </summary>
    /// <remarks>
    /// Set on every right-click that opens the menu, and cleared when that Tab is closed rather
    /// than when the menu closes, so the item handlers do not depend on whether the menu's
    /// <c>Closed</c> event comes before or after their <c>Click</c>.
    /// </remarks>
    internal TabPage? MenuTargetTab { get; private set; }

    private InterfaceControl? MenuTargetControl => MenuTargetTab?.Tag as InterfaceControl;

    /// <summary>
    /// Makes the menu's target the active Tab, for an item that needs its session in front.
    /// </summary>
    private InterfaceControl? BringMenuTargetToFront()
    {
        TabPage? target = MenuTargetTab;
        if (target != null && TabController.TabPages.Contains(target) && TabController.SelectedTab != target)
        {
            TabController.SelectedTab = target;
            // SelectionChanged sets this for a selection still waiting for its mouse-up; this
            // one came from a menu item, so no mouse-up follows to clear it.
            _ignoreChangeSelectedTabClick = false;
        }

        return MenuTargetControl;
    }

    // Switches to the target first: PuTTY's own settings dialog is posted to a PuTTY window that
    // is hidden with its Tab, and SetForegroundWindow on it is unmeasured there.
    private void ShowPuttySettingsDialog()
    {
        try
        {
            InterfaceControl? interfaceControl = BringMenuTargetToFront();
            var puttyBase = interfaceControl?.Protocol as PuttyBase;
            puttyBase?.ShowSettingsDialog();
        }
        catch (Exception ex)
        {
            Runtime.MessageCollector.AddExceptionMessage(
                "ShowPuttySettingsDialog (UI.Window.ConnectionWindow) failed",
                ex);
        }
    }

    private void ShowHideMenuButtons()
    {
        try
        {
            InterfaceControl? interfaceControl = MenuTargetControl;
            if (interfaceControl == null)
            {
                return;
            }

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
        finally
        {
            HideRedundantSeparators(cmenTab.Items);
        }
    }

    /// <summary>
    /// Shows each separator only between two groups of shown items: never first, never last,
    /// never directly after another separator.
    /// </summary>
    /// <remarks>
    /// A <c>ToolStrip</c> draws a separator whether or not anything around it is shown, so
    /// hiding every item between two separators left two lines in a row (#267). Decided from
    /// <c>Available</c>, the visibility an item asks for: <c>Visible</c> also needs the menu
    /// itself on screen and reads false while it is closed. Every separator is set each time,
    /// so one hidden for the last Tab is shown again where this Tab needs it.
    /// </remarks>
    internal static void HideRedundantSeparators(ToolStripItemCollection items)
    {
        ToolStripSeparator? pending = null;
        bool shownItemAbove = false;
        foreach (ToolStripItem item in items)
        {
            if (item is ToolStripSeparator separator)
            {
                // Hidden unless a shown item comes after it; only the last of a run can be shown.
                separator.Available = false;
                if (shownItemAbove)
                {
                    pending = separator;
                }
            }
            else if (item.Available)
            {
                if (pending != null)
                {
                    pending.Available = true;
                    pending = null;
                }

                shownItemAbove = true;
            }
        }
    }
    #endregion

    #region Tab Actions
    // Smart Size and Full Screen switch to the target first: both change the mstscax control's
    // display, and what it does with that while its Tab is hidden is unmeasured. Full Screen
    // also takes over the screen, which a user expects to show the Tab they chose.
    private void ToggleSmartSize()
    {
        try
        {
            if (BringMenuTargetToFront() is not InterfaceControl interfaceControl)
            {
                return;
            }

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
            var rdp = BringMenuTargetToFront()?.Protocol as RdpProtocol;
            rdp?.ToggleFullscreen();
        }
        catch (Exception ex)
        {
            Runtime.MessageCollector.AddExceptionMessage("ToggleFullscreen (UI.Window.ConnectionWindow) failed", ex);
        }
    }

    // Disconnect, Duplicate, Reconnect and Rename act on the target where it is. Closing an
    // inactive Tab's session is what middle-click already does; Duplicate and Reconnect open a
    // new Session, which becomes the active Tab as every newly opened Session does.
    private void CloseTabMenu()
    {
        try
        {
            MenuTargetControl?.Protocol.Close();
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
            InterfaceControl? interfaceControl = MenuTargetControl;
            if (interfaceControl == null)
            {
                return;
            }

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
            InterfaceControl? interfaceControl = MenuTargetControl;
            if (interfaceControl == null)
            {
                return;
            }

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
            // Held in a local: the target's session can still drop while the dialog is open,
            // which closes the Tab and clears MenuTargetTab.
            TabPage? targetTab = MenuTargetTab;
            if (targetTab == null)
            {
                return;
            }

            string newTitle = TabTitleOf(targetTab);
            if (InputDialog.InputBox(Language.strNewTitle, Language.strNewTitle + ":", ref newTitle) == DialogResult.OK
                && !string.IsNullOrEmpty(newTitle)
                && !targetTab.IsDisposed)
            {
                RetitleTab(targetTab, newTitle);
            }
        }
        catch (Exception ex)
        {
            Runtime.MessageCollector.AddExceptionMessage("RenameTab (UI.Window.ConnectionWindow) failed", ex);
        }
    }

    #endregion

    #region Protocols
    public void OnProtocolClosed(object sender)
    {
        var protocolBase = sender as ProtocolBase;
        var tabPage = protocolBase?.InterfaceControl?.Parent as TabPage;
        if (tabPage != null)
        {
            CloseTab(tabPage);
        }
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
        {
            return;
        }

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
                {
                    CloseTab(tabToBeClosed, retriesLeft - 1);
                }
                else
                {
                    Runtime.MessageCollector.AddExceptionMessage("Couldn't close tab - out of COM retries", ex);
                }
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
                if (MenuTargetTab == tabToBeClosed)
                {
                    MenuTargetTab = null;
                }
                _ignoreChangeSelectedTabClick = false;
                Runtime.MessageCollector.AddMessage(MessageClass.DebugMsg,
                    $"Close timing: tab page removed and disposed in {stopwatch.ElapsedMilliseconds} ms", true);
            }
            catch (COMException ex)
            {
                if (retriesLeft > 0)
                {
                    CloseTab(tabToBeClosed, retriesLeft - 1);
                }
                else
                {
                    Runtime.MessageCollector.AddExceptionMessage("Couldn't close tab - out of COM retries", ex);
                }
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
        if (_suppressSelectionChangedEvent)
        {
            return;
        }

        _ignoreChangeSelectedTabClick = true;
        UpdateSelectedConnection();
        FocusInterfaceController();
        RefreshInterfaceController();
    }

    private int _firstClickTicks;
    private Rectangle _doubleClickRectangle;

    /// <summary>
    /// Whether the main window is the foreground window when a click on the strip ends.
    /// </summary>
    /// <remarks>
    /// A seam for the tests: a test process cannot make a window the foreground one reliably,
    /// and the click handling differs between the two cases.
    /// </remarks>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal Func<bool> MainWindowIsForeground { get; set; } =
        () => NativeMethods.GetForegroundWindow() == FrmMain.Default.Handle;

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
                CloseConnectionTab(TabController.SelectedTab);
                return;
            }

            // Middle-click closes the Tab under the mouse, active or not, without switching to it,
            // and nothing on the empty strip. Handled before the background-window path below,
            // which would select the clicked Tab first.
            if (e.Button == MouseButtons.Middle)
            {
                _ignoreChangeSelectedTabClick = false;
                CloseConnectionTab(TabPageFromPoint(e.Location));
                return;
            }

            // Not for a right-click, which opens the clicked Tab's menu without switching to it.
            if (e.Button != MouseButtons.Right && !MainWindowIsForeground() && !_ignoreChangeSelectedTabClick)
            {
                TabPage? clickedTab = TabPageFromPoint(e.Location);
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
                    int currentTicks = Environment.TickCount;
                    int elapsedTicks = currentTicks - _firstClickTicks;
                    if (elapsedTicks > SystemInformation.DoubleClickTime
                        || !_doubleClickRectangle.Contains(MousePosition))
                    {
                        _firstClickTicks = currentTicks;
                        _doubleClickRectangle = new Rectangle(
                            MousePosition.X - SystemInformation.DoubleClickSize.Width / 2,
                            MousePosition.Y - SystemInformation.DoubleClickSize.Height / 2,
                            SystemInformation.DoubleClickSize.Width,
                            SystemInformation.DoubleClickSize.Height);
                        FocusInterfaceController();
                    }
                    else
                    {
                        TabController_DoubleClickTab();
                    }
                    break;
                case MouseButtons.Right:
                    // Null on the empty strip beside the Tabs, where a right-click opens no menu.
                    TabPage? rightClickedTab = TabPageFromPoint(e.Location);
                    if (rightClickedTab?.Tag == null)
                    {
                        return;
                    }

                    // The menu acts on the Tab it was opened on, which stays inactive if it was:
                    // the selection, the focus and the selected connection are left alone.
                    MenuTargetTab = rightClickedTab;
                    ShowHideMenuButtons();
                    NativeMethods.SetForegroundWindow(Handle);
                    cmenTab.Show(TabController, e.Location);
                    break;
            }
        }
        catch (Exception ex)
        {
            Runtime.MessageCollector.AddExceptionMessage(
                "TabController_MouseUp (UI.Window.ConnectionWindow) failed",
                ex);
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

    [SuppressMessage("Performance", "CA1822",
                     Justification = "FrmMain calls it through the window that hosts the focused session and "
                                     + "skips it when there is none; the refresh is a placeholder that stays an "
                                     + "instance member so that call contract holds.")]
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
                TabPage? selectedTab = TabController.SelectedTab;
                if (selectedTab == null)
                {
                    return;
                }

                {
                    Rectangle tabClientRectangle = selectedTab.RectangleToScreen(selectedTab.ClientRectangle);
                    if (tabClientRectangle.Contains(MousePosition))
                    {
                        var interfaceControl = selectedTab.Tag as InterfaceControl;
                        if (interfaceControl?.Info != null && ProtocolTypes.IsSupported(interfaceControl.Info.Protocol))
                        {
                            // Answered here, so the message goes no further up: the main window
                            // does not see it and decides the activating button another way.
                            FrmMain.LogActivation(
                                $"WM_MOUSEACTIVATE at panel '{TabText}': "
                                + $"0x{NativeMethods.HIWORD(m.LParam):X} inside the session of Tab "
                                + $"'{selectedTab.Text}', "
                                + $"focusing its {interfaceControl.Protocol.GetType().Name}, answered 0 "
                                + "without passing it on");
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
        if (!_dragArmed || e.Button != MouseButtons.Left)
        {
            return;
        }

        if (!InTabDrag)
        {
            if (Math.Abs(e.X - _dragStartPoint.X) < SystemInformation.DragSize.Width / 2 &&
                Math.Abs(e.Y - _dragStartPoint.Y) < SystemInformation.DragSize.Height / 2)
            {
                return;
            }

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

        TabPage? sourceTab = TabController.SelectedTab;
        TabPage? destinationTab = TabPageFromPoint(e.Location);

        // A tab is being dragged, so one is selected; the check on sourceTab is for the compiler.
        if (sourceTab == null
            || destinationTab == null
            || !TabController.TabPages.Contains(destinationTab)
            || sourceTab == destinationTab)
        {
            return;
        }

        int targetIndex = TabController.TabPages.IndexOf(destinationTab);

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
