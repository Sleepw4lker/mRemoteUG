using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace mRemoteUG.UI.Controls
{
    /// <summary>
    /// A themed panel that stands in front of a session's hosted control until the session has
    /// something of its own to show.
    /// </summary>
    /// <remarks>
    /// This exists because the RDP OCX paints its own connecting page and will not be talked out
    /// of the colour it uses. Measured on mstscax 10.0.26100: the client area is filled pure white
    /// from the moment the control has a window - before <c>Connect()</c> is called, not only
    /// during a connection attempt - and <c>AxHost.BackColor</c>, the one colour the container can
    /// offer it, is accepted, read back unchanged, and ignored on screen. That is ADR-0009's
    /// theme, taken once from Windows at startup, being contradicted by the largest control in the
    /// window.
    /// <para>
    /// So the fix is not a colour but a cover. The overlay is a sibling of the hosted control
    /// rather than a child of it: an AxHost is an ActiveX window, and a managed child inside one
    /// is at the mercy of whatever the OCX does to its own window. Siblings clip against each
    /// other through WS_CLIPSIBLINGS, which WinForms sets on every control, so the front one wins.
    /// </para>
    /// </remarks>
    public sealed class ConnectingOverlay : Panel
    {
        private readonly Label _caption;

        public ConnectingOverlay(string caption)
        {
            // From SystemColors rather than from a literal, so this follows whatever
            // Application.SetColorMode settled on at startup. In light mode that is the same white
            // the OCX was painting, which is the point: nothing changes for a light-mode user.
            BackColor = SystemColors.Window;
            ForeColor = SystemColors.WindowText;
            Dock = DockStyle.Fill;
            TabStop = false;

            // No Font of its own. The default is inherited from the parent chain, which is where
            // --largefont's Application.SetDefaultFont and the per-monitor DPI scaling both
            // arrive (ADR-0010, ADR-0011).
            _caption = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = SystemColors.WindowText,
                UseMnemonic = false,
                Text = caption ?? string.Empty
            };

            Controls.Add(_caption);
        }

        /// <summary>
        /// The line shown in the middle of the overlay.
        /// </summary>
        // Never designer-serialised: this control is created in code, and WFO1000 asks every
        // public control property to say so one way or the other.
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Caption
        {
            get { return _caption.Text; }
            set { _caption.Text = value ?? string.Empty; }
        }

        /// <summary>
        /// Puts a new overlay in front of <paramref name="hosted"/>, as a sibling of it.
        /// </summary>
        /// <returns>
        /// The overlay, or null when <paramref name="hosted"/> has no parent to be a sibling in.
        /// Null rather than an exception: this is decoration, and a connection must not fail over
        /// it.
        /// </returns>
        public static ConnectingOverlay ShowOver(Control hosted, string caption)
        {
            var host = hosted?.Parent;
            if (host == null)
                return null;

            var overlay = new ConnectingOverlay(caption);
            host.Controls.Add(overlay);

            // Adding is not enough on its own. The hosted control is usually added first and would
            // otherwise be in front, and index 0 is the front of the z-order.
            overlay.BringToFront();
            return overlay;
        }

        /// <summary>
        /// Takes the overlay out of its host and disposes it. Safe to call more than once.
        /// </summary>
        /// <remarks>
        /// More than one event can be the one that ends the wait - connected, disconnected, a
        /// fatal error, the tab being closed - and in some orders several of them arrive.
        /// </remarks>
        public void Remove()
        {
            if (IsDisposed)
                return;

            Parent?.Controls.Remove(this);
            Dispose();
        }
    }
}
