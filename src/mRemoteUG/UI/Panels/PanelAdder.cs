using System;
using System.Linq;
using System.Windows.Forms;
using mRemoteUG.App;
using mRemoteUG.Messages;
using mRemoteUG.UI.Forms;
using mRemoteUG.UI.Forms.Input;
using mRemoteUG.UI.Window;

namespace mRemoteUG.UI.Panels
{
    public class PanelAdder
    {
        public Form AddPanel(string title = "", bool noTabber = false)
        {
            try
            {
                var connectionForm = new ConnectionWindow();
                BuildConnectionWindowContextMenu(connectionForm);
                SetConnectionWindowTitle(title, connectionForm);
                ShowConnectionWindow(connectionForm);
                PrepareTabControllerSupport(noTabber, connectionForm);
                return connectionForm;
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddMessage(MessageClass.ErrorMsg, "Couldn\'t add panel" + Environment.NewLine + ex.Message);
                return null;
            }
        }

        public bool DoesPanelExist(string panelName)
        {
            return Runtime.WindowList?.OfType<ConnectionWindow>().Any(w => w.TabText == panelName)
                ?? false;
        }

        private static void ShowConnectionWindow(ConnectionWindow connectionForm)
        {
            FrmMain.Default.Layout.AddDocument(connectionForm);
        }

        private static void PrepareTabControllerSupport(bool noTabber, ConnectionWindow connectionForm)
        {
            if (noTabber)
                connectionForm.TabController.Dispose();
            else
                Runtime.WindowList.Add(connectionForm);
        }

        private static void SetConnectionWindowTitle(string title, ConnectionWindow connectionForm)
        {
            if (title == "")
                title = Language.strNewPanel;
            connectionForm.SetFormText(title.Replace("&", "&&"));
        }

        /// <summary>
        /// Menu shown when a connection panel tab is right-clicked. MainLayout resolves
        /// the tab under the cursor and shows this menu.
        /// </summary>
        private static void BuildConnectionWindowContextMenu(ConnectionWindow pnlcForm)
        {
            var cMen = new ContextMenuStrip();
            cMen.Items.Add(CreateRenameMenuItem(pnlcForm));
            pnlcForm.OwnContextMenu(cMen);
        }

        private static ToolStripMenuItem CreateRenameMenuItem(ConnectionWindow pnlcForm)
        {
            var cMenRen = new ToolStripMenuItem
            {
                Text = Language.strRename,
                Image = Resources.Rename,
                Tag = pnlcForm
            };
            cMenRen.Click += cMenConnectionPanelRename_Click;
            return cMenRen;
        }

        private static void cMenConnectionPanelRename_Click(object? sender, EventArgs e)
        {
            try
            {
                var conW = (ConnectionWindow)((ToolStripMenuItem)sender).Tag;

                var nTitle = "";
                input.InputBox(Language.strNewTitle, Language.strNewTitle + ":", ref nTitle);

                if (!string.IsNullOrEmpty(nTitle))
                {
                    conW.SetFormText(nTitle.Replace("&", "&&"));
                }
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("cMenConnectionPanelRename_Click: Caught Exception: ", ex);
            }
        }
    }
}
