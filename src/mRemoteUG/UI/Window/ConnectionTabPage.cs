using System.Drawing;
using System.Windows.Forms;
using System.ComponentModel;

namespace mRemoteUG.UI.Window
{
    // A thin TabPage subclass adding the one extra per-tab property this window's
    // owner-drawn tab strip needs that the native TabPage doesn't have: a per-tab icon.
    public class ConnectionTabPage : TabPage
    {
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Icon Icon { get; set; }
    }
}
