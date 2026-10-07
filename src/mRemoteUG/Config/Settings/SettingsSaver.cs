using System;
using System.Windows.Forms;
using mRemoteUG.App;
using mRemoteUG.Tools;
using mRemoteUG.UI.Forms;

namespace mRemoteUG.Config.Settings
{
    public static class SettingsSaver
    {
        public static void SaveSettings(
            Control quickConnectToolStrip,
            FrmMain frmMain)
        {
            try
            {
                var windowPlacement = new WindowPlacement(FrmMain.Default);
                if (frmMain.WindowState == FormWindowState.Minimized & windowPlacement.RestoreToMaximized)
                {
                    frmMain.Opacity = 0;
                    frmMain.WindowState = FormWindowState.Maximized;
                }

                mRemoteUG.Settings.Default.MainFormLocation = frmMain.Location;
                mRemoteUG.Settings.Default.MainFormSize = frmMain.Size;

                // These are physical pixels, and under per-monitor DPI awareness that means they
                // are only meaningful alongside the DPI they were measured at. Without this, a
                // window sized on a 200% display reopens at twice the size on a 100% one.
                mRemoteUG.Settings.Default.MainFormDpi = frmMain.DeviceDpi;

                if (frmMain.WindowState != FormWindowState.Normal)
                {
                    mRemoteUG.Settings.Default.MainFormRestoreLocation = frmMain.RestoreBounds.Location;
                    mRemoteUG.Settings.Default.MainFormRestoreSize = frmMain.RestoreBounds.Size;
                }

                mRemoteUG.Settings.Default.MainFormState = frmMain.WindowState;

                if (frmMain.Fullscreen != null)
                {
                    mRemoteUG.Settings.Default.MainFormKiosk = frmMain.Fullscreen.Value;
                }

                mRemoteUG.Settings.Default.FirstStart = false;
                mRemoteUG.Settings.Default.ResetPanels = false;
                mRemoteUG.Settings.Default.ResetToolbars = false;
                mRemoteUG.Settings.Default.NoReconnect = false;

                SaveQuickConnectToolbarLocation(quickConnectToolStrip);

                // Measured in the DPI written just above, so it has to be saved in the
                // same pass as MainFormDpi to stay meaningful.
                MainLayoutSettings.Save(frmMain.Layout);

                mRemoteUG.Settings.Default.Save();

            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("Saving settings failed", ex);
            }
        }

        private static void SaveQuickConnectToolbarLocation(Control quickConnectToolStrip)
        {
            mRemoteUG.Settings.Default.QuickyTBLocation = quickConnectToolStrip.Location;
            mRemoteUG.Settings.Default.QuickyTBVisible = quickConnectToolStrip.Visible;

            if (quickConnectToolStrip.Parent != null)
            {
                mRemoteUG.Settings.Default.QuickyTBParentDock = quickConnectToolStrip.Parent.Dock.ToString();
            }
        }

    }
}
