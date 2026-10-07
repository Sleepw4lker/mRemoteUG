using mRemoteUG.Tools;
using mRemoteUG.Config.Putty;
using System;
using System.Windows.Forms;
using mRemoteUG.UI.Forms;
// ReSharper disable ArrangeAccessorOwnerBody

namespace mRemoteUG.App
{
    public static class Shutdown
    {
        public static void Quit()
        {
            FrmMain.Default.Close();
            ProgramRoot.CloseSingletonInstanceMutex();
        }

        public static void Cleanup(Control quickConnectToolStrip, FrmMain frmMain)
        {
            try
            {
                StopPuttySessionWatcher();
                DisposeNotificationAreaIcon();
                SaveConnections();
                SaveSettings(quickConnectToolStrip, frmMain);
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage(Language.strSettingsCouldNotBeSavedOrTrayDispose, ex);
            }
        }

        private static void StopPuttySessionWatcher()
        {
            PuttySessionsManager.Instance.StopWatcher();
        }

        private static void DisposeNotificationAreaIcon()
        {
            if (Runtime.NotificationAreaIcon != null && Runtime.NotificationAreaIcon.Disposed == false)
                Runtime.NotificationAreaIcon.Dispose();
        }

        private static void SaveConnections()
        {
            if (Settings.Default.SaveConsOnExit)
                Runtime.ConnectionsService.SaveConnections();
        }

        private static void SaveSettings(Control quickConnectToolStrip, FrmMain frmMain)
        {
            Config.Settings.SettingsSaver.SaveSettings(quickConnectToolStrip, frmMain);
        }

    }
}