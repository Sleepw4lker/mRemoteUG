using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using mRemoteUG.UI;
using mRemoteUG.UI.Window;
using NUnit.Framework;

namespace mRemoteUG.Tests.UI.Window
{
    /// <summary>
    /// The connection tree window keeps its shape through a DPI change, in both directions.
    /// </summary>
    /// <remarks>
    /// Two reports on a scaled monitor sit behind this: a blank band between the tree and the
    /// session area, and - after dragging from a 200% monitor back to a 100% one - a toolbar drawn
    /// over the top of the tree. Both came from the same place. Everything in this window except
    /// the toolbar was positioned by an Anchor and a designer literal, so the distance to each edge
    /// was a number worked out once, against a client size the window no longer has by the time it
    /// is embedded and stretched into its host panel.
    /// <para>
    /// These tests are deliberately geometric rather than property-based. A window can hold every
    /// correct value and still lay out wrongly, which is exactly what happened.
    /// </para>
    /// </remarks>
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class ConnectionTreeWindowDpiTests
    {
        private const int Dpi96 = 96;
        private const int Dpi192 = 192;

        /// <summary>
        /// Drives the framework's own DPI rescale, the way DpiChangeTests does.
        /// </summary>
        /// <remarks>
        /// Note that this does not change DeviceDpi - it runs the rescale with the numbers it is
        /// given. Anything that reads DeviceDpi for itself therefore measures 96 on both legs of a
        /// round trip and cannot be tested here, which is why the window's own metrics take the DPI
        /// as an argument.
        /// </remarks>
        private static void ChangeDpi(Control control, int from, int to)
        {
            var method = typeof(Control).GetMethod("RescaleConstantsForDpi",
                                                   BindingFlags.Instance | BindingFlags.NonPublic);
            method.Invoke(control, new object[] { from, to });
        }

        /// <summary>
        /// Shows the window off-screen and invisible.
        /// </summary>
        /// <remarks>
        /// Control.Visible is false for every child of a form that has never been shown, whatever
        /// the child's own setting, so a window that is merely constructed measures as having no
        /// controls and every check below passes without testing anything.
        /// </remarks>
        private static ConnectionTreeWindow ShowOffScreen()
        {
            var window = new ConnectionTreeWindow
            {
                StartPosition = FormStartPosition.Manual,
                Location = new Point(-32000, -32000),
                Opacity = 0
            };
            window.Show();
            window.PerformLayout();
            return window;
        }

        /// <summary>
        /// Asserts two controls do not share any pixels.
        /// </summary>
        /// <remarks>
        /// Rectangle.Intersect(a, b).IsEmpty is not an overlap test: IsEmpty is true only when x, y,
        /// width and height are all zero, so two controls whose edges merely touch read as
        /// overlapping.
        /// </remarks>
        private static void AssertNoOverlap(Control reference, Control first, Control second, string when)
        {
            var one = BoundsIn(first, reference);
            var other = BoundsIn(second, reference);
            var shared = Rectangle.Intersect(one, other);
            Assert.That(shared.Width > 0 && shared.Height > 0, Is.False,
                        $"{when}: {first.Name} at {one} overlaps {second.Name} at " +
                        $"{other}, sharing {shared}");
        }

        /// <summary>
        /// A control-s bounds in some ancestor-s coordinates.
        /// </summary>
        /// <remarks>
        /// Control.Bounds is relative to the immediate parent, so comparing two controls that sit
        /// at different depths compares numbers from different coordinate spaces - which reads as
        /// an overlap between things that are nowhere near each other. Everything here is brought
        /// into the window-s own client coordinates first.
        /// </remarks>
        private static Rectangle BoundsIn(Control control, Control reference)
        {
            return reference.RectangleToClient(control.Parent.RectangleToScreen(control.Bounds));
        }

        private static MenuStrip ToolbarOf(ConnectionTreeWindow window)
        {
            return DpiScaling.ToolStripsOf(window).OfType<MenuStrip>().Single(s => s.Name == "msMain");
        }

        /// <summary>
        /// A toolbar that grows with the DPI moves the tree down rather than covering it.
        /// </summary>
        [Test]
        public void AGrowingToolbarPushesTheTreeDownInsteadOfCoveringIt()
        {
            using (var window = ShowOffScreen())
            {
                var toolbar = ToolbarOf(window);
                var tree = window.ConnectionTree;
                var toolbarHeightBefore = toolbar.Height;

                ChangeDpi(toolbar, Dpi96, Dpi192);
                using (var doubled = new Font(window.Font.FontFamily, window.Font.SizeInPoints * 2))
                    DpiScaling.FollowDpiChange(window, Dpi192, doubled);
                window.PerformLayout();

                // Without this the rest proves nothing: if the toolbar never grew, of course it
                // does not overlap anything.
                Assert.That(toolbar.Height, Is.GreaterThan(toolbarHeightBefore),
                            "the toolbar did not grow, so this test cannot say anything");

                AssertNoOverlap(window, toolbar, tree, "after a 96 to 192 change");
                Assert.That(BoundsIn(tree, window).Top, Is.GreaterThanOrEqualTo(BoundsIn(toolbar, window).Bottom),
                            "the tree starts above the bottom of the toolbar");
            }
        }
        /// <summary>
        /// The tree reaches the edges of the tool window it is embedded in.
        /// </summary>
        /// <remarks>
        /// Embedded the way MainLayout does it - TopLevel off, no border, docked to fill a host
        /// panel - because that sequence is where the reported blank band appears.
        /// <para>
        /// Honest about what this is: a guard, not a reproduction. It was written to fail against
        /// the anchored layout and did not, so the band needs something this harness does not have
        /// - most likely a real DPI change between the window being laid out and being embedded,
        /// which is the same thing the nested submenu defect could never be pinned down without.
        /// What it does hold is the invariant the docked layout provides and the anchored one only
        /// happened to: the tree reaches both edges whatever the window is resized to.
        /// </para>
        /// </remarks>
        [Test]
        public void TheTreeReachesTheEdgesOfTheToolWindow()
        {
            using (var host = new Form
            {
                StartPosition = FormStartPosition.Manual,
                Location = new Point(-32000, -32000),
                Opacity = 0,
                ClientSize = new Size(420, 500)
            })
            using (var window = new ConnectionTreeWindow())
            {
                var panel = new Panel { Dock = DockStyle.Fill };
                host.Controls.Add(panel);

                window.TopLevel = false;
                window.FormBorderStyle = FormBorderStyle.None;
                window.Dock = DockStyle.Fill;
                panel.Controls.Add(window);
                window.Show();
                host.Show();
                host.PerformLayout();
                window.PerformLayout();

                var tree = BoundsIn(window.ConnectionTree, window);

                Assert.That(window.ClientSize.Width, Is.GreaterThan(0), "the window was never laid out");
                Assert.That(tree.Left, Is.EqualTo(0), "the tree does not start at the left edge");
                Assert.That(tree.Right, Is.EqualTo(window.ClientSize.Width),
                            $"the tree stops {window.ClientSize.Width - tree.Right} pixels short of the " +
                            "right edge of its window, which is the blank band");
            }
        }

        /// <summary>
        /// A round trip out to 200% and back leaves every metric where it started.
        /// </summary>
        /// <remarks>
        /// The outward leg was the only one any test drove. Dragging a window to a 200% monitor and
        /// back was reported as leaving the toolbar glyphs large and the toolbar over the tree, so
        /// the return leg is the interesting one - and asserting the outward leg happened first is
        /// what stops this passing because nothing moved at all.
        /// </remarks>
        [Test]
        public void ARoundTripOutTo192AndBackPutsEveryMetricBack()
        {
            using (var window = ShowOffScreen())
            {
                var toolbar = ToolbarOf(window);
                var tree = window.ConnectionTree;
                var baseFont = window.Font;

                var glyphsAt96 = toolbar.ImageScalingSize;
                var toolbarAt96 = toolbar.Height;
                var searchRowAt96 = window.SearchRow.Height;
                var treeTopAt96 = BoundsIn(tree, window).Top;

                ChangeDpi(toolbar, Dpi96, Dpi192);
                ChangeDpi(tree, Dpi96, Dpi192);
                using (var doubled = new Font(baseFont.FontFamily, baseFont.SizeInPoints * 2))
                {
                    DpiScaling.FollowDpiChange(window, Dpi192, doubled);
                    window.RefreshSearchRowMetrics(Dpi192);
                }

                window.PerformLayout();
                Assert.That(toolbar.ImageScalingSize.Width, Is.EqualTo(32),
                            "the outward leg did not happen, so the return leg proves nothing");

                ChangeDpi(toolbar, Dpi192, Dpi96);
                ChangeDpi(tree, Dpi192, Dpi96);
                DpiScaling.FollowDpiChange(window, Dpi96, baseFont);
                window.RefreshSearchRowMetrics(Dpi96);
                window.PerformLayout();

                Assert.That(toolbar.ImageScalingSize, Is.EqualTo(glyphsAt96),
                            $"the toolbar glyphs came back from 200% at {toolbar.ImageScalingSize} " +
                            $"rather than {glyphsAt96}");
                Assert.That(window.SearchRow.Height, Is.EqualTo(searchRowAt96),
                            "the search row kept its 200% height");
                Assert.That(toolbar.Height, Is.EqualTo(toolbarAt96), "the toolbar kept its 200% height");
                Assert.That(BoundsIn(tree, window).Top, Is.EqualTo(treeTopAt96),
                            "the tree did not go back to where it started");
                AssertNoOverlap(window, toolbar, tree, "after a 96 to 192 to 96 round trip");
            }
        }

    }
}
