using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using mRemoteUG.UI.Controls;
using NUnit.Framework;

namespace mRemoteUG.Tests.UI.Controls
{
    /// <summary>
    /// The panel that stands in front of a session's control until the session has something to
    /// show.
    /// </summary>
    /// <remarks>
    /// Reported as a white page that "hurts the eyes" in dark mode. The page is painted by
    /// mstscax, not by this application: the OCX fills its client area white and draws
    /// <c>ConnectingText</c> on it, and it does that before a connection is attempted as well as
    /// during one. Measured, the OCX ignores every colour this side can offer it - setting
    /// <c>AxHost.BackColor</c> is accepted and read back and changes nothing on screen - so the
    /// only way to keep the theme is to put something themed in front of it.
    /// </remarks>
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class ConnectingOverlayTests
    {
        private static Form OffScreenForm()
        {
            return new Form
            {
                StartPosition = FormStartPosition.Manual,
                Location = new Point(-32000, -32000),
                Opacity = 0,
                ClientSize = new Size(320, 240)
            };
        }

        [Test]
        public void ItTakesItsColoursFromTheTheme()
        {
            using (var overlay = new ConnectingOverlay("Connecting..."))
            {
                Assert.Multiple(() =>
                {
                    Assert.That(overlay.BackColor, Is.EqualTo(SystemColors.Window),
                                "The overlay must follow the theme, which is the whole point of it.");
                    Assert.That(overlay.ForeColor, Is.EqualTo(SystemColors.WindowText),
                                "Window text on a window background, or the caption is unreadable in "
                                + "one of the two themes.");
                });
            }
        }

        [Test]
        public void ItPaintsEveryPartOfItselfInTheThemeWindowColour()
        {
            // Not a pixel test of the covering itself. DrawToBitmap walks the child controls and
            // paints them in an order of its own, so it will happily draw a control that is behind
            // another one on top of it - the covering is Windows' WS_CLIPSIBLINGS doing, and the
            // only place it can honestly be measured is on a real screen (see the mstscax section
            // of docs/platform-findings.md). What this pins is the other half: that the overlay is
            // opaque everywhere, rather than leaving the OCX showing through gaps of its own.
            using (var host = OffScreenForm())
            {
                var beneath = new Panel { Dock = DockStyle.Fill, BackColor = Color.Magenta };
                host.Controls.Add(beneath);
                host.Show();

                var overlay = ConnectingOverlay.ShowOver(beneath, "Connecting...");
                Assert.That(overlay, Is.Not.Null, "Nothing was shown, so this test proves nothing.");

                host.PerformLayout();
                Application.DoEvents();

                using (var shot = new Bitmap(overlay.Width, overlay.Height))
                {
                    overlay.DrawToBitmap(shot, new Rectangle(0, 0, overlay.Width, overlay.Height));

                    var window = SystemColors.Window.ToArgb();
                    var magenta = Color.Magenta.ToArgb();

                    for (var x = 1; x < shot.Width; x += 8)
                    {
                        for (var y = 1; y < shot.Height; y += 8)
                        {
                            Assert.That(shot.GetPixel(x, y).ToArgb(), Is.Not.EqualTo(magenta),
                                        $"The overlay has a hole at {x},{y}. In the application that "
                                        + "is the RDP OCX, painting white.");
                        }
                    }

                    Assert.That(shot.GetPixel(2, 2).ToArgb(), Is.EqualTo(window),
                                "The corner is not the theme's window colour, so the overlay is either "
                                + "not painting or not filling its host.");
                }
            }
        }

        [Test]
        public void ItIsPutInFrontOfTheControlItCovers()
        {
            using (var host = OffScreenForm())
            {
                var beneath = new Panel { Dock = DockStyle.Fill };
                host.Controls.Add(beneath);
                host.Show();

                var overlay = ConnectingOverlay.ShowOver(beneath, "Connecting...");

                Assert.Multiple(() =>
                {
                    Assert.That(overlay.Parent, Is.SameAs(host),
                                "The overlay must be a sibling of the control it covers. Parenting it "
                                + "into an AxHost puts a managed child inside an ActiveX control.");
                    Assert.That(host.Controls.GetChildIndex(overlay), Is.Zero,
                                "Index 0 is the front of the z-order. Behind the RDP control the "
                                + "overlay is invisible and pointless.");
                    Assert.That(overlay.Bounds, Is.EqualTo(host.ClientRectangle),
                                "The overlay must fill the host, or the OCX shows around its edges.");
                });
            }
        }

        [Test]
        public void ItShowsTheCaptionItWasGiven()
        {
            using (var host = OffScreenForm())
            {
                var beneath = new Panel { Dock = DockStyle.Fill };
                host.Controls.Add(beneath);
                host.Show();

                var overlay = ConnectingOverlay.ShowOver(beneath, "Connecting...");

                Assert.That(overlay.Caption, Is.EqualTo("Connecting..."));
            }
        }

        [Test]
        public void ShowingItOverAnUnparentedControlDoesNothing()
        {
            using (var orphan = new Panel())
            {
                Assert.That(ConnectingOverlay.ShowOver(orphan, "Connecting..."), Is.Null,
                            "With no parent there is nothing to be a sibling of, and throwing here "
                            + "would abort a connection over a cosmetic detail.");
            }
        }

        [Test]
        public void RemovingItTakesItOutOfTheHostAndDisposesIt()
        {
            using (var host = OffScreenForm())
            {
                var beneath = new Panel { Dock = DockStyle.Fill };
                host.Controls.Add(beneath);
                host.Show();

                var overlay = ConnectingOverlay.ShowOver(beneath, "Connecting...");
                overlay.Remove();

                Assert.Multiple(() =>
                {
                    Assert.That(host.Controls.Contains(overlay), Is.False,
                                "The overlay outlived the connection attempt and is still covering the "
                                + "session.");
                    Assert.That(overlay.IsDisposed, Is.True, "The overlay was removed but never disposed.");
                });
            }
        }

        [Test]
        public void RemovingItTwiceIsHarmless()
        {
            using (var host = OffScreenForm())
            {
                var beneath = new Panel { Dock = DockStyle.Fill };
                host.Controls.Add(beneath);
                host.Show();

                var overlay = ConnectingOverlay.ShowOver(beneath, "Connecting...");
                overlay.Remove();

                // Several events can be the one that ends the wait - connected, disconnected, a
                // fatal error, the tab being closed - and more than one of them arrives in some
                // orders. Each of them calls this.
                Assert.DoesNotThrow(() => overlay.Remove());
            }
        }
    }
}
