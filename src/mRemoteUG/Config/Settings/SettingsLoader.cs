using System;
using System.Drawing;
using System.Windows.Forms;
using mRemoteUG.App;
using mRemoteUG.Connection.Protocol;
using mRemoteUG.Messages;
using mRemoteUG.Tools;
using mRemoteUG.UI.Controls;
using mRemoteUG.UI.Forms;


namespace mRemoteUG.Config.Settings
{
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
            if (mainForm == null)
                throw new ArgumentNullException(nameof(mainForm));
            if (messageCollector == null)
                throw new ArgumentNullException(nameof(messageCollector));
            if (quickConnectToolStrip == null)
                throw new ArgumentNullException(nameof(quickConnectToolStrip));
		    if (mainMenu == null)
		        throw new ArgumentNullException(nameof(mainMenu));

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
                    SetToolbarsDefault();
				else
                    LoadToolbarsFromSettings();
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


        /// <summary>
        /// Converts a window size saved at one DPI into the equivalent size at the DPI of the
        /// monitor the window is about to reopen on.
        /// </summary>
        /// <remarks>
        /// A saved size is physical pixels, so on its own it means nothing: 1600x1000 fills a
        /// quarter of a 4K panel at 100% and all of it at 200%. Before per-monitor awareness the
        /// question did not arise, because Windows virtualised every size to 96 DPI.
        /// <para>
        /// The location is deliberately not scaled - it is a desktop coordinate, not a measurement,
        /// and the caller already clamps it back on screen. Settings written before this DPI was
        /// recorded have <c>MainFormDpi</c> at 0 and are taken at face value, which is what they
        /// meant when they were written.
        /// </para>
        /// </remarks>
        private static Size RescaleSavedSize(Size savedSize, Point savedLocation)
        {
            var savedDpi = mRemoteUG.Settings.Default.MainFormDpi;
            if (savedDpi <= 0)
                return savedSize;

            var targetDpi = DpiForPoint(savedLocation);
            if (targetDpi <= 0 || targetDpi == savedDpi)
                return savedSize;

            return new Size((int)Math.Round(savedSize.Width * targetDpi / (double)savedDpi),
                            (int)Math.Round(savedSize.Height * targetDpi / (double)savedDpi));
        }

        /// <summary>
        /// Converts a toolbar offset saved at one DPI into the equivalent offset at the current one.
        /// </summary>
        /// <remarks>
        /// Unlike the window location this really is a measurement - an offset within the main
        /// window's ToolStripPanel - so it scales. It shares <c>MainFormDpi</c> because both are
        /// written by the same save.
        /// </remarks>
        private Point RescaleSavedToolbarLocation(Point saved)
        {
            var savedDpi = mRemoteUG.Settings.Default.MainFormDpi;
            var currentDpi = MainForm.DeviceDpi;
            if (savedDpi <= 0 || savedDpi == currentDpi)
                return saved;

            return new Point((int)Math.Round(saved.X * currentDpi / (double)savedDpi),
                             (int)Math.Round(saved.Y * currentDpi / (double)savedDpi));
        }

        /// <summary>
        /// The effective DPI of the monitor nearest a desktop point, or 0 if Windows will not say.
        /// </summary>
        private static int DpiForPoint(Point point)
        {
            var monitor = NativeMethods.MonitorFromPoint(point, NativeMethods.MONITOR_DEFAULTTONEAREST);
            if (monitor == IntPtr.Zero)
                return 0;

            return NativeMethods.GetDpiForMonitor(monitor, NativeMethods.MonitorDpiType.Effective,
                                                  out var dpiX, out _) == 0
                ? (int)dpiX
                : 0;
        }

        private void SetApplicationWindowPositionAndSize()
        {
            MainForm.WindowState = FormWindowState.Normal;
            if (mRemoteUG.Settings.Default.MainFormState == FormWindowState.Normal)
            {
                if (!mRemoteUG.Settings.Default.MainFormLocation.IsEmpty)
                    MainForm.Location = mRemoteUG.Settings.Default.MainFormLocation;
                if (!mRemoteUG.Settings.Default.MainFormSize.IsEmpty)
                    MainForm.Size = RescaleSavedSize(mRemoteUG.Settings.Default.MainFormSize,
                                                     mRemoteUG.Settings.Default.MainFormLocation);
            }
            else
            {
                if (!mRemoteUG.Settings.Default.MainFormRestoreLocation.IsEmpty)
                    MainForm.Location = mRemoteUG.Settings.Default.MainFormRestoreLocation;
                if (!mRemoteUG.Settings.Default.MainFormRestoreSize.IsEmpty)
                    MainForm.Size = RescaleSavedSize(mRemoteUG.Settings.Default.MainFormRestoreSize,
                                                     mRemoteUG.Settings.Default.MainFormRestoreLocation);
            }

            if (mRemoteUG.Settings.Default.MainFormState == FormWindowState.Maximized)
            {
                MainForm.WindowState = FormWindowState.Maximized;
            }

            // Make sure the form is visible on the screen
            const int minHorizontal = 300;
            const int minVertical = 150;
            var screenBounds = Screen.FromHandle(MainForm.Handle).Bounds;
            var newBounds = MainForm.Bounds;

            if (newBounds.Right < screenBounds.Left + minHorizontal)
                newBounds.X = screenBounds.Left + minHorizontal - newBounds.Width;
            if (newBounds.Left > screenBounds.Right - minHorizontal)
                newBounds.X = screenBounds.Right - minHorizontal;
            if (newBounds.Bottom < screenBounds.Top + minVertical)
                newBounds.Y = screenBounds.Top + minVertical - newBounds.Height;
            if (newBounds.Top > screenBounds.Bottom - minVertical)
                newBounds.Y = screenBounds.Bottom - minVertical;

            MainForm.Location = newBounds.Location;
        }

        private void SetAutoSave()
        {
            if (mRemoteUG.Settings.Default.AutoSaveEveryMinutes <= 0) return;
            MainForm.tmrAutoSave.Interval = mRemoteUG.Settings.Default.AutoSaveEveryMinutes * 60000;
            MainForm.tmrAutoSave.Enabled = true;
        }

        private void SetKioskMode()
        {
            if (!mRemoteUG.Settings.Default.MainFormKiosk) return;
            MainForm.Fullscreen.Value = true;
        }

        private static void SetShowSystemTrayIcon()
        {
            if (mRemoteUG.Settings.Default.ShowSystemTrayIcon)
                Runtime.NotificationAreaIcon = new NotificationAreaIcon();
        }

        private void EnsureSettingsAreSavedInNewestVersion()
        {
            if (!mRemoteUG.Settings.Default.DoUpgrade) return;

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
            var toolStripPanel = ToolStripPanelFromString("top");
	        toolStripPanel.Join(_mainMenu, new Point(MainForm.LogicalToDeviceUnits(3), 0));
        }

		private void AddQuickConnectPanel()
		{
		    SetToolstripGripStyle(_quickConnectToolStrip);
            _quickConnectToolStrip.Visible = mRemoteUG.Settings.Default.QuickyTBVisible;
            var toolStripPanel = ToolStripPanelFromString(mRemoteUG.Settings.Default.QuickyTBParentDock);
            // Saved in physical pixels alongside MainFormDpi, so it carries the same caveat as the
            // window size: a toolbar docked at x=600 on a 200% display belongs at x=300 on a 100%
            // one, or it lands off the end of the panel.
            toolStripPanel.Join(_quickConnectToolStrip,
                                RescaleSavedToolbarLocation(mRemoteUG.Settings.Default.QuickyTBLocation));
		}

	    private void SetToolstripGripStyle(ToolStrip toolbar)
	    {
	        toolbar.GripStyle = mRemoteUG.Settings.Default.LockToolbars
	            ? ToolStripGripStyle.Hidden
	            : ToolStripGripStyle.Visible;
        }

		private ToolStripPanel ToolStripPanelFromString(string panel)
		{
			switch (panel.ToLower())
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
}
