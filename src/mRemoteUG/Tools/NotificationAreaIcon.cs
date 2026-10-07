using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using mRemoteUG.App;
using mRemoteUG.Connection;
using mRemoteUG.Container;
using mRemoteUG.UI.Forms;


namespace mRemoteUG.Tools
{
    public class NotificationAreaIcon
    {
        private readonly NotifyIcon _nI;
        private readonly ContextMenuStrip _cMen;
        private readonly ToolStripMenuItem _cMenCons;
        private readonly IConnectionInitiator _connectionInitiator = new ConnectionInitiator();
        private static readonly FrmMain FrmMain = FrmMain.Default;

        public bool Disposed { get; private set; }

        public NotificationAreaIcon()
        {
            try
            {
                _cMenCons = new ToolStripMenuItem
                {
                    Text = Language.strConnections,
                    Image = Resources.Root
                };

                var cMenSep1 = new ToolStripSeparator();

                var cMenExit = new ToolStripMenuItem {Text = Language.strMenuExit};
                cMenExit.Click += cMenExit_Click;

                _cMen = new ContextMenuStrip();
                _cMen.Items.AddRange(new ToolStripItem[] {_cMenCons, cMenSep1, cMenExit});

                _nI = new NotifyIcon
                {
                    Text = @"mRemoteUG",
                    BalloonTipText = @"mRemoteUG",
                    // A NotifyIcon with no icon shows nothing at all, so this is the one
                    // place that cannot simply be left unset. SystemIcons.Application is
                    // the default Windows application icon - the same one the executable
                    // itself now shows, since it carries no icon resource of its own.
                    Icon = SystemIcons.Application,
                    ContextMenuStrip = _cMen,
                    Visible = true
                };

                _nI.MouseClick += nI_MouseClick;
                _nI.MouseDoubleClick += nI_MouseDoubleClick;
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("Creating new SysTrayIcon failed", ex);
            }
        }

        public void Dispose()
        {
            try
            {
                _nI.Visible = false;
                _nI.Dispose();
                _cMen.Dispose();
                Disposed = true;
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("Disposing SysTrayIcon failed", ex);
            }
        }

        private void nI_MouseClick(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right) return;
            _cMenCons.DropDownItems.Clear();
            var menuItemsConverter = new ConnectionsTreeToMenuItemsConverter
            {
                MouseUpEventHandler = ConMenItem_MouseUp
            };

            // ReSharper disable once CoVariantArrayConversion
            ToolStripItem[] rootMenuItems = menuItemsConverter.CreateToolStripDropDownItems(Runtime.ConnectionsService.ConnectionTreeModel).ToArray();
            _cMenCons.DropDownItems.AddRange(rootMenuItems);
        }

        private static void nI_MouseDoubleClick(object? sender, MouseEventArgs e)
        {
            if (FrmMain.Visible)
                HideForm();
            else
                ShowForm();
        }

        private static void ShowForm()
        {
            FrmMain.Show();
            FrmMain.WindowState = FrmMain.PreviousWindowState;

            if (Settings.Default.ShowSystemTrayIcon) return;
            Runtime.NotificationAreaIcon.Dispose();
            Runtime.NotificationAreaIcon = null;
        }

        private static void HideForm()
        {
            FrmMain.Hide();
            FrmMain.PreviousWindowState = FrmMain.WindowState;
        }

        private void ConMenItem_MouseUp(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            if (((ToolStripMenuItem)sender).Tag is ContainerInfo) return;
            if (FrmMain.Visible == false)
                ShowForm();
            _connectionInitiator.OpenConnection((ConnectionInfo) ((ToolStripMenuItem) sender).Tag);
        }

        private static void cMenExit_Click(object? sender, EventArgs e)
        {
            Shutdown.Quit();
        }
    }
}