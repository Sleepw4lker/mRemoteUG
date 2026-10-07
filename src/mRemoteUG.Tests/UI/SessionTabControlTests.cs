using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using mRemoteUG.UI.Panels;
using mRemoteUG.UI.Window;
using NUnit.Framework;

namespace mRemoteUG.Tests.UI
{
    /// <summary>
    /// The session tab strip paints its own background rather than letting the theme do it.
    /// </summary>
    /// <remarks>
    /// Reported as a light band beside the tabs while the rest of the window was dark. The tabs
    /// themselves are owner-drawn and were always right; nothing painted the strip around them,
    /// so comctl32 filled it from the Tab theme parts, which have no dark variant and which
    /// Application.SetColorMode does not re-theme (ADR-0009).
    /// </remarks>
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class SessionTabControlTests
    {
        /// <summary>
        /// The strip beside the last tab is painted in the control's own colour.
        /// </summary>
        /// <remarks>
        /// BackColor is set to a colour comctl32 would never choose, and that is the whole point of
        /// the test rather than an oddity of it. At 96 DPI in a light process the themed band and
        /// SystemColors.Control are both #F0F0F0, so asserting that the band "is Control" passes
        /// just as well against the defect. What separates them is whether the band *follows* the
        /// control.
        /// </remarks>
        [Test]
        public void TheStripBesideTheTabsIsPaintedByTheControl()
        {
            using (var host = new Form
            {
                StartPosition = FormStartPosition.Manual,
                Location = new Point(-32000, -32000),
                Opacity = 0,
                ClientSize = new Size(400, 200)
            })
            using (var tabs = new SessionTabControl
            {
                Dock = DockStyle.Fill,
                DrawMode = TabDrawMode.OwnerDrawFixed
            })
            {
                tabs.BackColor = Color.Magenta;
                tabs.TabPages.Add(new TabPage("one"));
                host.Controls.Add(tabs);
                host.Show();
                host.PerformLayout();

                using (var shot = new Bitmap(tabs.Width, tabs.Height))
                {
                    tabs.DrawToBitmap(shot, new Rectangle(0, 0, tabs.Width, tabs.Height));

                    var lastTab = tabs.GetTabRect(tabs.TabCount - 1);
                    var band = new Point(Math.Min(lastTab.Right + 20, tabs.Width - 2),
                                         lastTab.Top + lastTab.Height / 2);

                    // A DrawToBitmap that renders nothing would leave the bitmap black and make
                    // every colour assertion below meaningless, so it is ruled out first: this
                    // says the harness works, not that the control does.
                    Assert.That(shot.GetPixel(2, 2).ToArgb(), Is.Not.EqualTo(Color.Black.ToArgb()),
                                "DrawToBitmap produced an empty bitmap, so this test cannot say " +
                                "anything about the control");

                    Assert.That(shot.GetPixel(band.X, band.Y).ToArgb(), Is.EqualTo(Color.Magenta.ToArgb()),
                                $"the strip beside the last tab painted {shot.GetPixel(band.X, band.Y)} " +
                                "instead of the control's own BackColor, so the theme is still " +
                                "painting it");
                }
            }
        }
        /// <summary>
        /// The connection window builds its tab strip out of this control, not a stock TabControl.
        /// </summary>
        /// <remarks>
        /// The promotion is one line in a generated designer file, which is exactly the kind of
        /// thing a designer round trip puts back - and putting it back returns the light band with
        /// nothing else changing, so no other test would notice. ConnectionWindow cannot be reached
        /// by the sweeps in ThemeTests or --selftest, both of which need a parameterless
        /// constructor, so it is asserted here by name.
        /// </remarks>
        [Test]
        public void TheConnectionWindowBuildsItsTabStripFromThisControl()
        {
            using (var window = new ConnectionWindow("tab strip promotion"))
            {
                Assert.That(window.TabController, Is.InstanceOf<SessionTabControl>(),
                            "the session tab strip is a stock TabControl again, so the band beside " +
                            "the tabs is painted by the theme and stays light in dark mode");
            }
        }
    }
}
