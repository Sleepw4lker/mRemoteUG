using System;
using System.Windows.Forms;
using System.ComponentModel;

namespace mRemoteUG.UI.Panels
{
    /// <summary>
    /// A TabControl whose tab strip can be hidden. Native TabControl always reserves
    /// room for its tabs; returning non-zero from TCM_ADJUSTRECT leaves the display
    /// rectangle covering the whole control, so the selected page paints over the strip.
    /// </summary>
    public class DocumentTabControl : TabControl
    {
        private const int TCM_ADJUSTRECT = 0x1328;

        private bool _showTabStrip = true;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool ShowTabStrip
        {
            get => _showTabStrip;
            set
            {
                if (_showTabStrip == value)
                    return;
                _showTabStrip = value;
                // the display rectangle changes, so the pages have to be re-laid out
                PerformLayout();
                Invalidate(true);
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == TCM_ADJUSTRECT && !ShowTabStrip && !DesignMode)
            {
                m.Result = (IntPtr)1;
                return;
            }

            base.WndProc(ref m);
        }
    }
}
