using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using mRemoteUG.UI.Window;

namespace mRemoteUG.UI.Panels
{
    /// <summary>
    /// Owns the main window static layout and replaces the DockPanelSuite DockPanel.
    ///
    ///   splitMain (vertical)
    ///     Panel1  splitLeft (horizontal)   Panel1 = connection tree, Panel2 = config
    ///     Panel2  splitDocuments (horizontal)
    ///               Panel1 = tabDocuments, one tab per connection panel
    ///               Panel2 = notifications, collapsed until something is logged
    ///
    /// Tool windows stay Forms and are embedded with TopLevel = false, so the existing
    /// window classes keep working unchanged.
    /// </summary>
    public class MainLayout
    {
        private const int DefaultLeftColumnWidth = 230;
        private const int DefaultTreeSharePermille = 600;
        private const int DefaultNotificationsSharePermille = 250;

        private readonly SplitContainer _splitMain;
        private readonly SplitContainer _splitLeft;
        private readonly SplitContainer _splitDocuments;
        private readonly DocumentTabControl _tabDocuments;

        private readonly Dictionary<BaseWindow, ToolSlot> _tools = new Dictionary<BaseWindow, ToolSlot>();

        /// <summary>
        /// Raised when a different connection panel becomes the active document.
        /// </summary>
        public event EventHandler ActiveDocumentChanged;

        public MainLayout(SplitContainer splitMain,
                          SplitContainer splitLeft,
                          SplitContainer splitDocuments,
                          DocumentTabControl tabDocuments)
        {
            _splitMain = splitMain;
            _splitLeft = splitLeft;
            _splitDocuments = splitDocuments;
            _tabDocuments = tabDocuments;

            _tabDocuments.SelectedIndexChanged += (o, e) => ActiveDocumentChanged?.Invoke(this, EventArgs.Empty);
            _tabDocuments.MouseUp += TabDocuments_MouseUp;
        }

        #region Tool windows

        /// <summary>
        /// Embeds the three fixed tool windows into their panels. Call once, before
        /// ResetToDefaults.
        /// </summary>
        public void HostToolWindows(BaseWindow tree, BaseWindow config, BaseWindow notifications)
        {
            RegisterTool(tree, _splitLeft.Panel1, ToolPosition.LeftTop);
            RegisterTool(config, _splitLeft.Panel2, ToolPosition.LeftBottom);
            RegisterTool(notifications, _splitDocuments.Panel2, ToolPosition.Notifications);
        }

        private void RegisterTool(BaseWindow window, Control host, ToolPosition position)
        {
            if (window == null || _tools.ContainsKey(window))
                return;

            Embed(window, host);
            _tools[window] = new ToolSlot { Position = position, Visible = true };
        }

        private static void Embed(Form window, Control host)
        {
            window.TopLevel = false;
            window.FormBorderStyle = FormBorderStyle.None;
            window.Dock = DockStyle.Fill;
            host.Controls.Add(window);
            window.Show();
        }

        public bool IsToolVisible(BaseWindow window)
        {
            return _tools.TryGetValue(window, out var slot) && slot.Visible;
        }

        public void ShowTool(BaseWindow window)
        {
            SetToolVisible(window, true);
            window.Show();
            window.Focus();
        }

        public void HideTool(BaseWindow window)
        {
            SetToolVisible(window, false);
        }

        private void SetToolVisible(BaseWindow window, bool visible)
        {
            if (!_tools.TryGetValue(window, out var slot))
                return;

            slot.Visible = visible;
            ApplyToolVisibility();
        }

        private void ApplyToolVisibility()
        {
            var leftTop = IsVisible(ToolPosition.LeftTop);
            var leftBottom = IsVisible(ToolPosition.LeftBottom);

            if (!leftTop && !leftBottom)
            {
                // A SplitContainer will not collapse both panels, so drop the whole column
                _splitMain.Panel1Collapsed = true;
            }
            else
            {
                _splitMain.Panel1Collapsed = false;
                _splitLeft.Panel1Collapsed = !leftTop;
                _splitLeft.Panel2Collapsed = !leftBottom;
            }

            _splitDocuments.Panel2Collapsed = !IsVisible(ToolPosition.Notifications);
        }

        private bool IsVisible(ToolPosition position)
        {
            return _tools.Values.Any(s => s.Position == position && s.Visible);
        }

        #endregion

        #region Documents

        /// <summary>
        /// The connection panel currently shown in the document area.
        /// </summary>
        public BaseWindow ActiveDocument => _tabDocuments.SelectedTab?.Tag as BaseWindow;

        public IEnumerable<BaseWindow> Documents =>
            _tabDocuments.TabPages.Cast<TabPage>().Select(p => p.Tag as BaseWindow).Where(w => w != null);

        public void AddDocument(BaseWindow window)
        {
            if (window == null || FindPage(window) != null)
                return;

            var page = new TabPage(window.TabText) { Tag = window };
            Embed(window, page);

            window.TabTextChanged += Document_TabTextChanged;
            window.FormClosed += Document_FormClosed;

            _tabDocuments.TabPages.Add(page);
            _tabDocuments.SelectedTab = page;
            UpdateTabStripVisibility();
        }

        public void RemoveDocument(BaseWindow window)
        {
            var page = FindPage(window);
            if (page == null)
                return;

            window.TabTextChanged -= Document_TabTextChanged;
            window.FormClosed -= Document_FormClosed;

            page.Controls.Remove(window);
            _tabDocuments.TabPages.Remove(page);
            page.Dispose();
            UpdateTabStripVisibility();
        }

        public void ActivateDocument(BaseWindow window)
        {
            var page = FindPage(window);
            if (page == null)
                return;

            _tabDocuments.SelectedTab = page;
            window.Focus();
        }

        private TabPage FindPage(BaseWindow window)
        {
            return _tabDocuments.TabPages.Cast<TabPage>().FirstOrDefault(p => ReferenceEquals(p.Tag, window));
        }

        private void Document_TabTextChanged(object? sender, EventArgs e)
        {
            var window = sender as BaseWindow;
            var page = window == null ? null : FindPage(window);
            if (page != null)
                page.Text = window.TabText;
        }

        private void Document_FormClosed(object? sender, FormClosedEventArgs e)
        {
            if (sender is BaseWindow window)
                RemoveDocument(window);
        }

        /// <summary>
        /// Mirrors the old DocumentStyle switch: with AlwaysShowPanelTabs off, a lone
        /// connection panel is shown without a tab strip.
        /// </summary>
        public void UpdateTabStripVisibility()
        {
            _tabDocuments.ShowTabStrip =
                Settings.Default.AlwaysShowPanelTabs || _tabDocuments.TabPages.Count > 1;
        }

        private void TabDocuments_MouseUp(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
                return;

            TabPage clicked = null;
            for (var i = 0; i < _tabDocuments.TabPages.Count; i++)
            {
                if (!_tabDocuments.GetTabRect(i).Contains(e.Location))
                    continue;
                clicked = _tabDocuments.TabPages[i];
                break;
            }

            var window = clicked?.Tag as BaseWindow;
            if (window?.ContextMenuStrip == null)
                return;

            _tabDocuments.SelectedTab = clicked;
            window.ContextMenuStrip.Show(_tabDocuments, e.Location);
        }

        #endregion

        #region Persistence

        /// <summary>
        /// Restores the default arrangement, replacing the DockLeftPortion/DockTo calls
        /// in the old SetDefaultLayout.
        /// </summary>
        public void ResetToDefaults()
        {
            ApplyState(DefaultState());
        }

        /// <summary>
        /// The arrangement used when nothing has been saved, or when the user asks for the
        /// defaults back.
        /// </summary>
        public MainLayoutState DefaultState()
        {
            return new MainLayoutState
            {
                // The two shares are ratios and so carry no DPI with them; this one is a pixel
                // measurement taken at 96 DPI, and it reaches SplitterDistance long after the
                // auto-scale pass has run, so it has to be scaled here.
                LeftColumnWidth = _splitMain.LogicalToDeviceUnits(DefaultLeftColumnWidth),
                TreeSharePermille = DefaultTreeSharePermille,
                NotificationsSharePermille = DefaultNotificationsSharePermille,
                ShowTree = true,
                ShowConfig = true,
                ShowNotifications = false
            };
        }

        /// <summary>
        /// Reads back the parts of the arrangement that are worth remembering between runs.
        /// </summary>
        /// <remarks>
        /// Collapsing a panel leaves its SplitterDistance alone, so every measurement here stays
        /// readable while a tool is hidden - which is what lets someone hide the left column, quit,
        /// and still find it at its own width when they show it again.
        /// <para>
        /// A measurement is left at 0 only when it cannot be taken at all, which the caller reads
        /// as "keep whatever was stored last time". That covers a window closed before it was ever
        /// laid out, where the arithmetic below would be dividing by nothing.
        /// </para>
        /// </remarks>
        public MainLayoutState CaptureState()
        {
            var state = new MainLayoutState
            {
                ShowTree = IsVisible(ToolPosition.LeftTop),
                ShowConfig = IsVisible(ToolPosition.LeftBottom),
                ShowNotifications = IsVisible(ToolPosition.Notifications)
            };

            if (UsableSize(_splitMain) > 0)
                state.LeftColumnWidth = _splitMain.SplitterDistance;

            state.TreeSharePermille = SharePermille(_splitLeft);

            var documentsShare = SharePermille(_splitDocuments);
            if (documentsShare > 0)
                state.NotificationsSharePermille = MainLayoutState.ShareScale - documentsShare;

            return state;
        }

        /// <summary>
        /// Puts the window back into a saved arrangement, falling back to the default for any
        /// measurement the state does not carry.
        /// </summary>
        /// <remarks>
        /// Visibility goes first so the collapse state is settled before any splitter distance is
        /// assigned: SetSplitterDistance clamps against the panel minimums, and clamping against
        /// the wrong ones is how a restore silently lands somewhere else.
        /// </remarks>
        public void ApplyState(MainLayoutState state)
        {
            if (state == null)
                return;

            var defaults = DefaultState();

            SetSlotVisible(ToolPosition.LeftTop, state.ShowTree);
            SetSlotVisible(ToolPosition.LeftBottom, state.ShowConfig);
            SetSlotVisible(ToolPosition.Notifications, state.ShowNotifications);
            ApplyToolVisibility();

            SetSplitterDistance(_splitMain,
                state.LeftColumnWidth > 0 ? state.LeftColumnWidth : defaults.LeftColumnWidth);

            SetSplitterDistance(_splitLeft, DistanceFromShare(_splitLeft,
                state.TreeSharePermille > 0 ? state.TreeSharePermille : defaults.TreeSharePermille));

            var notifications = state.NotificationsSharePermille > 0
                ? state.NotificationsSharePermille
                : defaults.NotificationsSharePermille;
            SetSplitterDistance(_splitDocuments,
                DistanceFromShare(_splitDocuments, MainLayoutState.ShareScale - notifications));

            UpdateTabStripVisibility();
        }

        private void SetSlotVisible(ToolPosition position, bool visible)
        {
            foreach (var slot in _tools.Values.Where(s => s.Position == position))
                slot.Visible = visible;
        }

        /// <summary>
        /// The space a splitter actually divides: both panels, without the splitter itself.
        /// </summary>
        private static int UsableSize(SplitContainer split)
        {
            return (split.Orientation == Orientation.Vertical ? split.Width : split.Height)
                   - split.SplitterWidth;
        }

        private static int SharePermille(SplitContainer split)
        {
            var usable = UsableSize(split);
            return usable <= 0
                ? 0
                : (int)((long)split.SplitterDistance * MainLayoutState.ShareScale / usable);
        }

        private static int DistanceFromShare(SplitContainer split, int permille)
        {
            return (int)((long)UsableSize(split) * permille / MainLayoutState.ShareScale);
        }

        private static void SetSplitterDistance(SplitContainer split, int distance)
        {
            // SplitterDistance throws if it does not fit the current size, which happens
            // while the form is still being laid out.
            var min = split.Panel1MinSize;
            var max = (split.Orientation == Orientation.Vertical ? split.Width : split.Height)
                      - split.Panel2MinSize - split.SplitterWidth;
            if (max <= min)
                return;

            split.SplitterDistance = Math.Min(Math.Max(distance, min), max);
        }

        #endregion

        private enum ToolPosition
        {
            LeftTop,
            LeftBottom,
            Notifications
        }

        private class ToolSlot
        {
            public ToolPosition Position { get; set; }
            public bool Visible { get; set; }
        }
    }
}
