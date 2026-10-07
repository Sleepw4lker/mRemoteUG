using System;
using System.Drawing;
using System.Windows.Forms;

namespace mRemoteUG.UI.Panels
{
    /// <summary>
    /// The tab control the connection sessions sit in, painting its own background.
    /// </summary>
    /// <remarks>
    /// The tabs are owner-drawn by ConnectionWindow, which fills one rectangle per item and nothing
    /// else. Everything around them - the band to the right of the last tab above all - was left to
    /// comctl32, whose Tab theme parts have no dark variant and are not re-themed by
    /// Application.SetColorMode (ADR-0009). So that band stayed light while the rest of the window
    /// was dark.
    /// <para>
    /// Two things here are the opposite of the obvious, and both were measured rather than reasoned:
    /// </para>
    /// <para>
    /// <em>Overriding OnPaintBackground does nothing.</em> A TabControl wraps SysTabControl32 and
    /// does not set ControlStyles.UserPaint, so the framework never raises it. Setting that style
    /// would raise it, and would also take item drawing away from comctl32 - which is what sends
    /// the WM_DRAWITEM the owner-draw depends on.
    /// </para>
    /// <para>
    /// <em>The background colour had to be given to the control before any of this could work.</em>
    /// See <see cref="BackColor"/>: TabControl returns a constant from it. Painting with it was
    /// painting the theme's own colour back over the theme, which looks like a fill that does
    /// nothing - a fill that <em>was</em> running and could not be seen.
    /// </para>
    /// </remarks>
    public class SessionTabControl : TabControl
    {
        private const int WM_ERASEBKGND = 0x0014;

        private Color _backColor = SystemColors.Control;

        /// <summary>The colour this control paints behind and around its tabs.</summary>
        /// <remarks>
        /// Overridden because <see cref="TabControl"/> does not carry one: its own implementation
        /// returns <see cref="SystemColors.Control"/> whatever is assigned and its setter is empty.
        /// Measured, by assigning a colour and reading it straight back.
        /// <para>
        /// The default is the same system colour the framework would have returned, so this follows
        /// the light/dark setting for free and nothing has to assign it - a SystemColors value is a
        /// handle resolved on every read. What the override buys is that the value can now be
        /// something else, which is the only way the painting below can be tested at all: in a
        /// light process the theme's band and SystemColors.Control are both #F0F0F0, so a test that
        /// asserts the band is Control passes just as well against the defect.
        /// </para>
        /// </remarks>
        public override Color BackColor
        {
            get => _backColor;
            set
            {
                if (_backColor == value)
                    return;

                _backColor = value;
                Invalidate();
            }
        }

        /// <remarks>
        /// Answering the erase is enough: comctl32 draws the tab items and the pane border in
        /// WM_PAINT but does not fill the strip again, so what is put down here survives. Returning
        /// 1 is what says so - leave it to the base and the themed fill happens instead.
        /// </remarks>
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_ERASEBKGND && m.WParam != IntPtr.Zero)
            {
                using (var graphics = Graphics.FromHdc(m.WParam))
                using (var brush = new SolidBrush(BackColor))
                    graphics.FillRectangle(brush, ClientRectangle);

                m.Result = (IntPtr)1;
                return;
            }

            base.WndProc(ref m);
        }
    }
}
