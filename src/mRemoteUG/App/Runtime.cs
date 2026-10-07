using mRemoteUG.App.Info;
using mRemoteUG.Connection;
using mRemoteUG.Messages;
using mRemoteUG.Security;
using mRemoteUG.Tools;
using mRemoteUG.Tree.Root;
using mRemoteUG.UI;
using mRemoteUG.UI.Forms;
using mRemoteUG.UI.TaskDialog;
using System;
using System.IO;
using System.Security;
using System.Windows.Forms;

namespace mRemoteUG.App
{
    public static class Runtime
    {
        // Null until FrmMain builds it, and MainFileMenu and FrmMain both test for that.
        public static WindowList? WindowList { get; set; }
        public static MessageCollector MessageCollector { get; } = new MessageCollector();
        // Only exists while the tray icon is switched on. Assigned null by both the icon
        // itself and the appearance page when it is switched off, and tested for in four places.
        public static NotificationAreaIcon? NotificationAreaIcon { get; set; }
        public static SecureString EncryptionKey { get; set; } = new RootNodeInfo(RootNodeType.Connection).PasswordString.ConvertToSecureString();
        public static ConnectionsService ConnectionsService { get; } = new ConnectionsService();

        #region Connections Loading/Saving
        /// <summary>
        /// 
        /// </summary>
        /// <param name="withDialog">
        /// Should we show the file selection dialog to allow the user to select
        /// a connection file
        /// </param>
        public static void LoadConnections(bool withDialog = false)
        {
            var connectionFileName = "";

            try
            {
                if (withDialog)
                {
                    var loadDialog = DialogFactory.BuildLoadConnectionsDialog();
                    if (loadDialog.ShowDialog() != DialogResult.OK)
                        return;

                    connectionFileName = loadDialog.FileName;
                }
                else
                {
                    connectionFileName = ConnectionsService.GetStartupConnectionFileName();
                }

                ConnectionsService.LoadConnections(false, connectionFileName);
            }
            catch (Exception ex)
            {
                if (ex is FileNotFoundException && !withDialog)
                {
                    MessageCollector.AddExceptionMessage(string.Format(Language.strConnectionsFileCouldNotBeLoadedNew, connectionFileName), ex, MessageClass.InformationMsg);

                    string[] commandButtons =
                    {
                        Language.ConfigurationCreateNew,
                        Language.ConfigurationCustomPath,
                        Language.ConfigurationImportFile,
                        Language.strMenuExit
                    };

                    var answered = false;
                    while (!answered)
                    {
                        try
                        {
                            CTaskDialog.ShowTaskDialogBox(
                                GeneralAppInfo.ProductName, 
                                Language.ConnectionFileNotFound, 
                                "", "", "", "", "", 
                                string.Join(" | ", commandButtons), 
                                ETaskDialogButtons.None, 
                                ESysIcons.Question, 
                                ESysIcons.Question);

                            switch (CTaskDialog.CommandButtonResult)
                            {
                                case 0:
                                    ConnectionsService.NewConnectionsFile(connectionFileName);
                                    answered = true;
                                    break;
                                case 1:
                                    LoadConnections(true);
                                    answered = true;
                                    break;
                                case 2:
                                    ConnectionsService.NewConnectionsFile(connectionFileName);
                                    Import.ImportFromFile(ConnectionsService.ConnectionTreeModel.RootNodes[0]);
                                    answered = true;
                                    break;
                                case 3:
                                    Application.Exit();
                                    answered = true;
                                    break;
                            }                               
                        }
                        catch (Exception exc)
                        {
                            MessageCollector.AddExceptionMessage(string.Format(Language.strConnectionsFileCouldNotBeLoadedNew, connectionFileName), exc, MessageClass.InformationMsg);
                        }
                    }
                    return;
                }

                MessageCollector.AddExceptionMessage(string.Format(Language.strConnectionsFileCouldNotBeLoaded, connectionFileName), ex);
                if (connectionFileName != ConnectionsService.GetStartupConnectionFileName())
                {
                    LoadConnections(withDialog);
                }
                else
                {
                    // Deliberately a message box rather than a message through the collector, so
                    // the Pop-ups settings do not apply: Application.Exit() follows on the next
                    // line, and a filtered-away message would leave the user with an application
                    // that vanished and an explanation only in a file they are not looking at. The
                    // AddExceptionMessage above has already put it in the log, where the Logging
                    // settings do apply.
                    MessageBox.Show(FrmMain.Default,
                        string.Format(Language.strErrorStartupConnectionFileLoad, Environment.NewLine, Application.ProductName, ConnectionsService.GetStartupConnectionFileName(), MiscTools.GetExceptionMessageRecursive(ex)),
                        @"Could not load startup file.", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Application.Exit();
                }
            }
        }
        #endregion
    }
}