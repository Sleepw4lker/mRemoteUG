using mRemoteUG.App;
using mRemoteUG.Connection.Protocol;
using mRemoteUG.Messages;
using mRemoteUG.Tools;
using mRemoteUG.UI.Controls;
using mRemoteUG.UI.Forms;


namespace mRemoteUG.Config.Settings;

public class SettingsLoader
{
    private readonly MessageCollector _messageCollector;
    private readonly MenuStrip _mainMenu;
    private readonly QuickConnectToolStrip _quickConnectToolStrip;

    private FrmMain MainForm { get; }


    public SettingsLoader(
        FrmMain mainForm,
        MessageCollector messageCollector,
        QuickConnectToolStrip quickConnectToolStrip,
        MenuStrip mainMenu)
    {
        ArgumentNullException.ThrowIfNull(mainForm);

        ArgumentNullException.ThrowIfNull(messageCollector);

        ArgumentNullException.ThrowIfNull(quickConnectToolStrip);

        ArgumentNullException.ThrowIfNull(mainMenu);

        MainForm = mainForm;
        _messageCollector = messageCollector;
        _quickConnectToolStrip = quickConnectToolStrip;
        _mainMenu = mainMenu;
    }

    #region Public Methods
    public void LoadSettings()
    {
        try
        {
            EnsureSettingsAreSavedInNewestVersion();

            SetPuttyPath();
            SetApplicationWindowPositionAndSize();
            SetKioskMode();

            SetShowSystemTrayIcon();
            SetAutoSave();
            SetAlwaysShowPanelTabs();

            if (mRemoteUG.Settings.Default.ResetToolbars)
            {
                SetToolbarsDefault();
            }
            else
            {
                LoadToolbarsFromSettings();
            }
        }
        catch (Exception ex)
        {
            _messageCollector.AddExceptionMessage("Loading settings failed", ex);
        }
    }

    private static void SetPuttyPath()
    {
        // mRemoteUG no longer ships PuTTY; PuttyPathProvider resolves the user's copy.
        PuttyBase.PuttyPath = PuttyPathProvider.ResolvedPath;
    }

    private static void SetAlwaysShowPanelTabs()
    {
        FrmMain.Default.Layout.UpdateTabStripVisibility();
    }

    private void SetApplicationWindowPositionAndSize()
    {
        MainForm.WindowState = FormWindowState.Normal;
        if (mRemoteUG.Settings.Default.MainFormState == FormWindowState.Normal)
        {
            if (!mRemoteUG.Settings.Default.MainFormLocation.IsEmpty)
            {
                MainForm.Location = mRemoteUG.Settings.Default.MainFormLocation;
            }

            if (!mRemoteUG.Settings.Default.MainFormSize.IsEmpty)
            {
                MainForm.Size = MainFormPlacement.RescaleSavedSize(
                    mRemoteUG.Settings.Default.MainFormSize,
                    mRemoteUG.Settings.Default.MainFormLocation,
                    mRemoteUG.Settings.Default.MainFormDpi,
                    MainFormPlacement.DpiForPoint);
            }
        }
        else
        {
            if (!mRemoteUG.Settings.Default.MainFormRestoreLocation.IsEmpty)
            {
                MainForm.Location = mRemoteUG.Settings.Default.MainFormRestoreLocation;
            }

            if (!mRemoteUG.Settings.Default.MainFormRestoreSize.IsEmpty)
            {
                MainForm.Size = MainFormPlacement.RescaleSavedSize(
                    mRemoteUG.Settings.Default.MainFormRestoreSize,
                    mRemoteUG.Settings.Default.MainFormRestoreLocation,
                    mRemoteUG.Settings.Default.MainFormDpi,
                    MainFormPlacement.DpiForPoint);
            }
        }

        if (mRemoteUG.Settings.Default.MainFormState == FormWindowState.Maximized)
        {
            MainForm.WindowState = FormWindowState.Maximized;
        }

        // Make sure the form is visible on the screen. The screen is read first, as it always was:
        // reading Handle can create the window, and with it a DPI rescale that changes Bounds.
        Rectangle screenBounds = Screen.FromHandle(MainForm.Handle).Bounds;
        MainForm.Location = MainFormPlacement.KeepOnScreen(MainForm.Bounds, screenBounds);
    }

    private void SetAutoSave()
    {
        if (mRemoteUG.Settings.Default.AutoSaveEveryMinutes <= 0)
        {
            return;
        }

        MainForm.tmrAutoSave.Interval = mRemoteUG.Settings.Default.AutoSaveEveryMinutes * 60000;
        MainForm.tmrAutoSave.Enabled = true;
    }

    private void SetKioskMode()
    {
        if (!mRemoteUG.Settings.Default.MainFormKiosk)
        {
            return;
        }

        MainForm.Fullscreen.Value = true;
    }

    private static void SetShowSystemTrayIcon()
    {
        if (mRemoteUG.Settings.Default.ShowSystemTrayIcon)
        {
            Runtime.NotificationAreaIcon = new NotificationAreaIcon();
        }
    }

    private void EnsureSettingsAreSavedInNewestVersion()
    {
        if (!mRemoteUG.Settings.Default.DoUpgrade)
        {
            return;
        }

        try
        {
            mRemoteUG.Settings.Default.Save();
            mRemoteUG.Settings.Default.Upgrade();
        }
        catch (Exception ex)
        {
            _messageCollector.AddExceptionMessage("Settings.Upgrade() failed", ex);
        }
        mRemoteUG.Settings.Default.DoUpgrade = false;
    }

    private void SetToolbarsDefault()
    {
        // A 96 DPI offset, applied long after the auto-scale pass has run.
        ToolStripPanelFromString("top").Join(_quickConnectToolStrip,
                                             new Point(MainForm.LogicalToDeviceUnits(300), 0));
        _quickConnectToolStrip.Visible = true;
    }

    private void LoadToolbarsFromSettings()
    {
        ResetAllToolbarLocations();
        AddMainMenuPanel();
        AddQuickConnectPanel();
    }

    /// <summary>
    /// This prevents odd positioning issues due to toolbar load order.
    /// Since all toolbars start in this temp panel, no toolbar load
    /// can be blocked by pre-existing toolbars.
    /// </summary>
    private void ResetAllToolbarLocations()
    {
        var tempToolStrip = new ToolStripPanel();
        tempToolStrip.Join(_mainMenu);
        tempToolStrip.Join(_quickConnectToolStrip);
    }

    private void AddMainMenuPanel()
    {
        SetToolstripGripStyle(_mainMenu);
        ToolStripPanel toolStripPanel = ToolStripPanelFromString("top");
        toolStripPanel.Join(_mainMenu, new Point(MainForm.LogicalToDeviceUnits(3), 0));
    }

    private void AddQuickConnectPanel()
    {
        SetToolstripGripStyle(_quickConnectToolStrip);
        _quickConnectToolStrip.Visible = mRemoteUG.Settings.Default.QuickyTBVisible;
        ToolStripPanel toolStripPanel = ToolStripPanelFromString(mRemoteUG.Settings.Default.QuickyTBParentDock);
        // Saved in physical pixels alongside MainFormDpi, so it carries the same caveat as the
        // window size: a toolbar docked at x=600 on a 200% display belongs at x=300 on a 100%
        // one, or it lands off the end of the panel.
        toolStripPanel.Join(_quickConnectToolStrip,
                            MainFormPlacement.RescaleSavedToolbarLocation(
                                mRemoteUG.Settings.Default.QuickyTBLocation,
                                mRemoteUG.Settings.Default.MainFormDpi,
                                MainForm.DeviceDpi));
    }

    private static void SetToolstripGripStyle(ToolStrip toolbar)
    {
        toolbar.GripStyle = mRemoteUG.Settings.Default.LockToolbars
            ? ToolStripGripStyle.Hidden
            : ToolStripGripStyle.Visible;
    }

    private ToolStripPanel ToolStripPanelFromString(string panel)
    {
        switch (panel.ToLowerInvariant())
        {
            case "top":
                return MainForm.tsContainer.TopToolStripPanel;
            case "bottom":
                return MainForm.tsContainer.BottomToolStripPanel;
            case "left":
                return MainForm.tsContainer.LeftToolStripPanel;
            case "right":
                return MainForm.tsContainer.RightToolStripPanel;
            default:
                return MainForm.tsContainer.TopToolStripPanel;
        }
    }
    #endregion
}
