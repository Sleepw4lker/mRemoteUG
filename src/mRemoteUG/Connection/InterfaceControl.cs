using mRemoteUG.App;
using mRemoteUG.Connection.Protocol;
using System;
using System.Drawing;
using System.Windows.Forms;
using System.ComponentModel;


namespace mRemoteUG.Connection
{
	public sealed partial class InterfaceControl
    {
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ProtocolBase Protocol { get; set; }
	    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	    public ConnectionInfo Info { get; set; }


		public InterfaceControl(Control parent, ProtocolBase protocol, ConnectionInfo info)
		{
			try
			{
				Protocol = protocol;
				Info = info;
                Parent = parent;
                Location = new Point(0, 0);
                Size = Parent.Size;
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
				InitializeComponent();
			}
			catch (Exception ex)
			{
				Runtime.MessageCollector.AddMessage(Messages.MessageClass.ErrorMsg, "Couldn\'t create new InterfaceControl" + Environment.NewLine + ex.Message);
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
}
