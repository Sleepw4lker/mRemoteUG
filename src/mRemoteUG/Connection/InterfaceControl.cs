using System.ComponentModel;

using mRemoteUG.App;
using mRemoteUG.Connection.Protocol;


namespace mRemoteUG.Connection;

public sealed partial class InterfaceControl
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ProtocolBase Protocol { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ConnectionInfo Info { get; set; }


    public InterfaceControl(Control parent, ProtocolBase protocol, ConnectionInfo info)
    {
        Protocol = protocol;
        Info = info;
        try
        {
            Parent = parent;

            // Docked, not anchored. An anchor keeps the distance to each edge it was given, and
            // the font-based autoscale of a DPI change scaled that distance too, so after a dock
            // the session panel no longer matched its tab page and the session was sized wrong.
            // Docking also lays the control out at once, so its size is there for the RDP
            // client to read before it connects.
            Dock = DockStyle.Fill;
            InitializeComponent();
        }
        catch (Exception ex)
        {
            Runtime.MessageCollector.AddMessage(
                Messages.MessageClass.ErrorMsg,
                "Couldn\'t create new InterfaceControl" + Environment.NewLine + ex.Message);
        }
    }

    /// <summary>
    /// Tells the hosted protocol that this panel is now being rendered at a different DPI.
    /// </summary>
    /// <remarks>
    /// AfterParent rather than BeforeParent: by the time this arrives the panel has already
    /// been given its new bounds, so a protocol that sizes a session from them reads the size
    /// it is about to have rather than the one it is leaving.
    /// </remarks>
    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);

        try
        {
            Protocol?.NotifyDpiChanged(DeviceDpi);
        }
        catch (Exception ex)
        {
            Runtime.MessageCollector.AddMessage(Messages.MessageClass.WarningMsg,
                "A protocol failed to handle a DPI change" + Environment.NewLine + ex.Message, true);
        }
    }
}
