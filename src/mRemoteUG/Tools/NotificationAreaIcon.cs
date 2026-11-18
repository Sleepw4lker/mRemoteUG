using mRemoteUG.App;
using mRemoteUG.Connection;
using mRemoteUG.Container;
using mRemoteUG.Tree;
using mRemoteUG.UI.Forms;


namespace mRemoteUG.Tools;

public class NotificationAreaIcon : IDisposable
{
    // Null only when the constructor failed and logged it; the icon then never shows.
    private readonly NotifyIcon? _nI;
    private readonly ContextMenuStrip? _cMen;
    private readonly ToolStripMenuItem? _cMenCons;
    private readonly ConnectionInitiator _connectionInitiator = new ConnectionInitiator();
    private static readonly FrmMain s_frmMain = FrmMain.Default;

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

            var cMenExit = new ToolStripMenuItem { Text = Language.strMenuExit };
            cMenExit.Click += CMenExit_Click;

            _cMen = new ContextMenuStrip();
            _cMen.Items.AddRange(new ToolStripItem[] { _cMenCons, cMenSep1, cMenExit });

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

            _nI.MouseClick += NI_MouseClick;
            _nI.MouseDoubleClick += NI_MouseDoubleClick;
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
            if (_nI != null)
            {
                _nI.Visible = false;
                _nI.Dispose();
            }

            _cMen?.Dispose();
            Disposed = true;
        }
        catch (Exception ex)
        {
            Runtime.MessageCollector.AddExceptionMessage("Disposing SysTrayIcon failed", ex);
        }

        GC.SuppressFinalize(this);
    }

    private void NI_MouseClick(object? sender, MouseEventArgs e)
    {
        // _cMenCons is set whenever the icon exists to be clicked; the check is for the compiler.
        if (e.Button != MouseButtons.Right || _cMenCons == null)
        {
            return;
        }

        _cMenCons.DropDownItems.Clear();
        ConnectionTreeModel? connectionTreeModel = Runtime.ConnectionsService.ConnectionTreeModel;
        if (connectionTreeModel == null)
        {
            return;
        }

        var menuItemsConverter = new ConnectionsTreeToMenuItemsConverter
        {
            MouseUpEventHandler = ConMenItem_MouseUp
        };

        // ReSharper disable once CoVariantArrayConversion
        ToolStripItem[] rootMenuItems = menuItemsConverter
            .CreateToolStripDropDownItems(connectionTreeModel)
            .ToArray();
        _cMenCons.DropDownItems.AddRange(rootMenuItems);
    }

    private static void NI_MouseDoubleClick(object? sender, MouseEventArgs e)
    {
        if (s_frmMain.Visible)
        {
            HideForm();
        }
        else
        {
            ShowForm();
        }
    }

    private static void ShowForm()
    {
        s_frmMain.Show();
        s_frmMain.WindowState = s_frmMain.PreviousWindowState;

        if (Settings.Default.ShowSystemTrayIcon)
        {
            return;
        }

        Runtime.NotificationAreaIcon?.Dispose();
        Runtime.NotificationAreaIcon = null;
    }

    private static void HideForm()
    {
        s_frmMain.Hide();
        s_frmMain.PreviousWindowState = s_frmMain.WindowState;
    }

    private void ConMenItem_MouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        // Every item the converter builds is tagged with its node; a folder opens nothing.
        if (sender is not ToolStripMenuItem { Tag: ConnectionInfo connectionInfo } || connectionInfo is ContainerInfo)
        {
            return;
        }

        if (s_frmMain.Visible == false)
        {
            ShowForm();
        }

        _connectionInitiator.OpenConnection(connectionInfo);
    }

    private static void CMenExit_Click(object? sender, EventArgs e)
    {
        Shutdown.Quit();
    }
}
