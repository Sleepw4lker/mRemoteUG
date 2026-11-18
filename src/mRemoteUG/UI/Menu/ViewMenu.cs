using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

using mRemoteUG.App;
using mRemoteUG.UI.Forms;
using mRemoteUG.UI.Panels;
using mRemoteUG.UI.Window;

namespace mRemoteUG.UI.Menu;

public class ViewMenu : ToolStripMenuItem
{
    private ToolStripMenuItem _mMenViewConnectionPanels;
    private ToolStripSeparator _mMenViewSep1;
    private ToolStripMenuItem _mMenViewConnections;
    private ToolStripMenuItem _mMenViewConfig;
    private ToolStripMenuItem _mMenViewErrorsAndInfos;
    private ToolStripMenuItem _mMenViewAddConnectionPanel;
    private ToolStripSeparator _mMenViewSep2;
    private ToolStripMenuItem _mMenViewFullscreen;
    private ToolStripMenuItem _mMenViewQuickConnectToolbar;
    private ToolStripSeparator _mMenViewSep3;
    private ToolStripMenuItem _mMenViewJumpTo;
    private ToolStripMenuItem _mMenViewJumpToConnectionsConfig;
    private ToolStripMenuItem _mMenViewJumpToErrorsInfos;
    private ToolStripMenuItem _mMenViewResetLayout;
    private ToolStripMenuItem _mMenViewLockToolbars;
    private ToolStripSeparator _toolStripSeparator1;


    // All three are set by FrmMain.SetMenuDependencies in its Load handler, before the menu can
    // be opened. The designer constructs the menu, so none can be a constructor argument.
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ToolStrip TsQuickConnect { get; set; } = null!;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public FullscreenHandler FullscreenHandler { get; set; } = null!;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public FrmMain MainForm { get; set; } = null!;


    public ViewMenu()
    {
        Initialize();
    }

    [MemberNotNull(
        nameof(_mMenViewConnectionPanels), nameof(_mMenViewSep1), nameof(_mMenViewConnections),
        nameof(_mMenViewConfig), nameof(_mMenViewErrorsAndInfos), nameof(_mMenViewAddConnectionPanel),
        nameof(_mMenViewSep2), nameof(_mMenViewFullscreen), nameof(_mMenViewQuickConnectToolbar),
        nameof(_mMenViewSep3), nameof(_mMenViewJumpTo), nameof(_mMenViewJumpToConnectionsConfig),
        nameof(_mMenViewJumpToErrorsInfos), nameof(_mMenViewResetLayout), nameof(_mMenViewLockToolbars),
        nameof(_toolStripSeparator1))]
    private void Initialize()
    {
        _mMenViewAddConnectionPanel = new ToolStripMenuItem();
        _mMenViewConnectionPanels = new ToolStripMenuItem();
        _mMenViewSep1 = new ToolStripSeparator();
        _mMenViewConnections = new ToolStripMenuItem();
        _mMenViewConfig = new ToolStripMenuItem();
        _mMenViewErrorsAndInfos = new ToolStripMenuItem();
        _mMenViewJumpTo = new ToolStripMenuItem();
        _mMenViewJumpToConnectionsConfig = new ToolStripMenuItem();
        _mMenViewJumpToErrorsInfos = new ToolStripMenuItem();
        _mMenViewResetLayout = new ToolStripMenuItem();
        _mMenViewLockToolbars = new ToolStripMenuItem();
        _mMenViewSep2 = new ToolStripSeparator();
        _mMenViewQuickConnectToolbar = new ToolStripMenuItem();
        _mMenViewSep3 = new ToolStripSeparator();
        _mMenViewFullscreen = new ToolStripMenuItem();
        _toolStripSeparator1 = new ToolStripSeparator();

        // 
        // mMenView
        // 
        DropDownItems.AddRange(new ToolStripItem[] {
        _mMenViewAddConnectionPanel,
        _mMenViewConnectionPanels,
        _mMenViewSep1,
        _mMenViewConnections,
        _mMenViewConfig,
        _mMenViewErrorsAndInfos,
        _toolStripSeparator1,
        _mMenViewJumpTo,
        _mMenViewResetLayout,
        _mMenViewLockToolbars,
        _mMenViewSep2,
        _mMenViewQuickConnectToolbar,
        _mMenViewSep3,
        _mMenViewFullscreen});
        Name = "mMenView";
        Size = new System.Drawing.Size(44, 20);
        Text = Language.strMenuView;
        //DropDownOpening += MMenView_DropDownOpening;
        // 
        // mMenViewAddConnectionPanel
        // 
        _mMenViewAddConnectionPanel.Image = Resources.Panel_Add;
        _mMenViewAddConnectionPanel.Name = "mMenViewAddConnectionPanel";
        _mMenViewAddConnectionPanel.Size = new System.Drawing.Size(228, 22);
        _mMenViewAddConnectionPanel.Text = Language.strMenuAddConnectionPanel;
        _mMenViewAddConnectionPanel.Click += MMenViewAddConnectionPanel_Click;
        // 
        // mMenViewConnectionPanels
        // 
        _mMenViewConnectionPanels.Image = Resources.Panels;
        _mMenViewConnectionPanels.Name = "mMenViewConnectionPanels";
        _mMenViewConnectionPanels.Size = new System.Drawing.Size(228, 22);
        _mMenViewConnectionPanels.Text = Language.strMenuConnectionPanels;
        // 
        // mMenViewSep1
        // 
        _mMenViewSep1.Name = "mMenViewSep1";
        _mMenViewSep1.Size = new System.Drawing.Size(225, 6);
        // 
        // mMenViewConnections
        // 
        _mMenViewConnections.Checked = true;
        _mMenViewConnections.CheckState = CheckState.Checked;
        _mMenViewConnections.Image = Resources.Root;
        _mMenViewConnections.Name = "mMenViewConnections";
        _mMenViewConnections.Size = new System.Drawing.Size(228, 22);
        _mMenViewConnections.Text = Language.strMenuConnections;
        _mMenViewConnections.Click += MMenViewConnections_Click;
        // 
        // mMenViewConfig
        // 
        _mMenViewConfig.Checked = true;
        _mMenViewConfig.CheckState = CheckState.Checked;
        _mMenViewConfig.Image = Resources.cog;
        _mMenViewConfig.Name = "mMenViewConfig";
        _mMenViewConfig.Size = new System.Drawing.Size(228, 22);
        _mMenViewConfig.Text = Language.strMenuConfig;
        _mMenViewConfig.Click += MMenViewConfig_Click;
        // 
        // mMenViewErrorsAndInfos
        // 
        _mMenViewErrorsAndInfos.Checked = true;
        _mMenViewErrorsAndInfos.CheckState = CheckState.Checked;
        _mMenViewErrorsAndInfos.Image = Resources.ErrorsAndInfos;
        _mMenViewErrorsAndInfos.Name = "mMenViewErrorsAndInfos";
        _mMenViewErrorsAndInfos.Size = new System.Drawing.Size(228, 22);
        _mMenViewErrorsAndInfos.Text = Language.strMenuNotifications;
        _mMenViewErrorsAndInfos.Click += MMenViewErrorsAndInfos_Click;
        // 
        // ToolStripSeparator1
        // 
        _toolStripSeparator1.Name = "ToolStripSeparator1";
        _toolStripSeparator1.Size = new System.Drawing.Size(225, 6);
        // 
        // mMenViewJumpTo
        // 
        _mMenViewJumpTo.DropDownItems.AddRange(new ToolStripItem[] {
        _mMenViewJumpToConnectionsConfig,
        _mMenViewJumpToErrorsInfos});
        _mMenViewJumpTo.Image = Resources.JumpTo;
        _mMenViewJumpTo.Name = "mMenViewJumpTo";
        _mMenViewJumpTo.Size = new System.Drawing.Size(228, 22);
        _mMenViewJumpTo.Text = Language.strMenuJumpTo;
        // 
        // mMenViewJumpToConnectionsConfig
        // 
        _mMenViewJumpToConnectionsConfig.Image = Resources.Root;
        _mMenViewJumpToConnectionsConfig.Name = "mMenViewJumpToConnectionsConfig";
        _mMenViewJumpToConnectionsConfig.ShortcutKeys = ((Keys)(((Keys.Control | Keys.Alt)
        | Keys.C)));
        _mMenViewJumpToConnectionsConfig.Size = new System.Drawing.Size(258, 22);
        _mMenViewJumpToConnectionsConfig.Text = Language.strMenuConnectionsAndConfig;
        _mMenViewJumpToConnectionsConfig.Click += MMenViewJumpToConnectionsConfig_Click;
        // 
        // mMenViewJumpToErrorsInfos
        // 
        _mMenViewJumpToErrorsInfos.Image = Resources.InformationSmall;
        _mMenViewJumpToErrorsInfos.Name = "mMenViewJumpToErrorsInfos";
        _mMenViewJumpToErrorsInfos.ShortcutKeys = ((Keys)(((Keys.Control | Keys.Alt)
        | Keys.E)));
        _mMenViewJumpToErrorsInfos.Size = new System.Drawing.Size(258, 22);
        _mMenViewJumpToErrorsInfos.Text = Language.strMenuNotifications;
        _mMenViewJumpToErrorsInfos.Click += MMenViewJumpToErrorsInfos_Click;
        // 
        // mMenViewResetLayout
        // 
        _mMenViewResetLayout.Image = Resources.application_side_tree;
        _mMenViewResetLayout.Name = "mMenViewResetLayout";
        _mMenViewResetLayout.Size = new System.Drawing.Size(228, 22);
        _mMenViewResetLayout.Text = Language.strMenuResetLayout;
        _mMenViewResetLayout.Click += MMenViewResetLayout_Click;
        // 
        // mMenViewLockToolbars
        // 
        _mMenViewLockToolbars.Image = Resources.application_side_tree;
        _mMenViewLockToolbars.Name = "mMenViewLockToolbars";
        _mMenViewLockToolbars.Size = new System.Drawing.Size(228, 22);
        _mMenViewLockToolbars.Text = Language.strLockToolbars;
        _mMenViewLockToolbars.Click += MMenViewLockToolbars_Click;
        // 
        // mMenViewSep2
        // 
        _mMenViewSep2.Name = "mMenViewSep2";
        _mMenViewSep2.Size = new System.Drawing.Size(225, 6);
        // 
        // mMenViewQuickConnectToolbar
        // 
        _mMenViewQuickConnectToolbar.Image = Resources.Play_Quick;
        _mMenViewQuickConnectToolbar.Name = "mMenViewQuickConnectToolbar";
        _mMenViewQuickConnectToolbar.Size = new System.Drawing.Size(228, 22);
        _mMenViewQuickConnectToolbar.Text = Language.strMenuQuickConnectToolbar;
        _mMenViewQuickConnectToolbar.Click += MMenViewQuickConnectToolbar_Click;
        //
        // mMenViewSep3
        // 
        _mMenViewSep3.Name = "mMenViewSep3";
        _mMenViewSep3.Size = new System.Drawing.Size(225, 6);
        // 
        // mMenViewFullscreen
        // 
        _mMenViewFullscreen.Image = Resources.arrow_out;
        _mMenViewFullscreen.Name = "mMenViewFullscreen";
        _mMenViewFullscreen.ShortcutKeys = Keys.F11;
        _mMenViewFullscreen.Size = new System.Drawing.Size(228, 22);
        _mMenViewFullscreen.Text = Language.strMenuFullScreen;
        _mMenViewFullscreen.Checked = Settings.Default.MainFormKiosk;
        _mMenViewFullscreen.Click += MMenViewFullscreen_Click;
    }

    #region View
    internal void MMenView_DropDownOpening(object sender, EventArgs e)
    {
        _mMenViewConnections.Checked = MainForm.Layout.IsToolVisible(AppWindows.TreeForm);
        _mMenViewConfig.Checked = MainForm.Layout.IsToolVisible(AppWindows.ConfigForm);
        _mMenViewErrorsAndInfos.Checked = MainForm.Layout.IsToolVisible(AppWindows.ErrorsForm);
        _mMenViewLockToolbars.Checked = Settings.Default.LockToolbars;

        _mMenViewQuickConnectToolbar.Checked = TsQuickConnect.Visible;

        // Clear() disposes neither the items nor the bitmaps they own, and each item below is
        // built with a fresh Icon.ToBitmap(). Without this the GDI+ handles accumulated for
        // every open of this menu, for the life of the process.
        ToolStripItems.ClearAndDisposeOwned(_mMenViewConnectionPanels.DropDownItems);

        // FrmMain builds the window list in its Load handler, before the menu can be opened.
        WindowList? windows = Runtime.WindowList;
        for (int i = 0; windows != null && i <= windows.Count - 1; i++)
        {
            if (windows[i] is not { } window)
            {
                continue;
            }

            var tItem = new ToolStripMenuItem(window.Text,
                window.Icon?.ToBitmap(), ConnectionPanelMenuItem_Click)
            { Tag = window };
            _mMenViewConnectionPanels.DropDownItems.Add(tItem);
        }

        _mMenViewConnectionPanels.Enabled = _mMenViewConnectionPanels.DropDownItems.Count > 0;
    }

    private void ConnectionPanelMenuItem_Click(object? sender, EventArgs e)
    {
        // Only the items built above are wired here, and each is tagged with its window.
        if (sender is ToolStripMenuItem { Tag: BaseWindow window })
        {
            MainForm.Layout.ActivateDocument(window);
        }
    }

    private void MMenViewConnections_Click(object? sender, EventArgs e)
    {
        if (_mMenViewConnections.Checked == false)
        {
            MainForm.Layout.ShowTool(AppWindows.TreeForm);
            _mMenViewConnections.Checked = true;
        }
        else
        {
            MainForm.Layout.HideTool(AppWindows.TreeForm);
            _mMenViewConnections.Checked = false;
        }
    }

    private void MMenViewConfig_Click(object? sender, EventArgs e)
    {
        if (_mMenViewConfig.Checked == false)
        {
            MainForm.Layout.ShowTool(AppWindows.ConfigForm);
            _mMenViewConfig.Checked = true;
        }
        else
        {
            MainForm.Layout.HideTool(AppWindows.ConfigForm);
            _mMenViewConfig.Checked = false;
        }
    }

    private void MMenViewErrorsAndInfos_Click(object? sender, EventArgs e)
    {
        if (_mMenViewErrorsAndInfos.Checked == false)
        {
            MainForm.Layout.ShowTool(AppWindows.ErrorsForm);
            _mMenViewErrorsAndInfos.Checked = true;
        }
        else
        {
            MainForm.Layout.HideTool(AppWindows.ErrorsForm);
            _mMenViewErrorsAndInfos.Checked = false;
        }
    }

    private void MMenViewJumpToConnectionsConfig_Click(object? sender, EventArgs e)
    {
        if (AppWindows.TreeForm.ContainsFocus)
        {
            MainForm.Layout.ShowTool(AppWindows.ConfigForm);
        }
        else
        {
            MainForm.Layout.ShowTool(AppWindows.TreeForm);
        }
    }

    private void MMenViewJumpToErrorsInfos_Click(object? sender, EventArgs e)
    {
        MainForm.Layout.ShowTool(AppWindows.ErrorsForm);
    }

    private void MMenViewResetLayout_Click(object? sender, EventArgs e)
    {
        DialogResult msgBoxResult = MessageBox.Show(
            Language.strConfirmResetLayout,
            string.Empty,
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (msgBoxResult == DialogResult.Yes)
        {
            MainForm.SetDefaultLayout();
        }
    }

    private void MMenViewLockToolbars_Click(object? sender, EventArgs eventArgs)
    {
        if (Settings.Default.LockToolbars)
        {
            Settings.Default.LockToolbars = false;
            _mMenViewLockToolbars.Checked = false;
        }
        else
        {
            Settings.Default.LockToolbars = true;
            _mMenViewLockToolbars.Checked = true;
        }
    }

    private void MMenViewAddConnectionPanel_Click(object? sender, EventArgs e)
    {
        PanelAdder.AddPanel();
    }

    private void MMenViewQuickConnectToolbar_Click(object? sender, EventArgs e)
    {
        if (_mMenViewQuickConnectToolbar.Checked == false)
        {
            TsQuickConnect.Visible = true;
            _mMenViewQuickConnectToolbar.Checked = true;
        }
        else
        {
            TsQuickConnect.Visible = false;
            _mMenViewQuickConnectToolbar.Checked = false;
        }
    }

    private void MMenViewFullscreen_Click(object? sender, EventArgs e)
    {
        FullscreenHandler.Value = !FullscreenHandler.Value;
        _mMenViewFullscreen.Checked = FullscreenHandler.Value;
    }
    #endregion
}
