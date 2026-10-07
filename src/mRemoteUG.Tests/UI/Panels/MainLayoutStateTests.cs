using System.Threading;
using mRemoteUG.UI.Panels;
using NUnit.Framework;

namespace mRemoteUG.Tests.UI.Panels
{
    /// <summary>
    /// Covers saving and restoring the main window's panel arrangement.
    /// </summary>
    /// <remarks>
    /// The arrangement is three SplitContainers, and the traps are all in the units. splitMain has
    /// its first panel fixed, so its splitter distance is an absolute width; splitLeft and
    /// splitDocuments do not, so WinForms rescales theirs on every resize and only a ratio
    /// survives. Saving the wrong unit fails silently - the window simply reopens somewhere else -
    /// and this application cannot be run on the machine it is built on, so a test is the only
    /// place that difference is visible.
    /// </remarks>
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class MainLayoutStateTests
    {
        private static MainLayoutState Everything(int width, int treeShare, int notificationsShare)
        {
            return new MainLayoutState
            {
                LeftColumnWidth = width,
                TreeSharePermille = treeShare,
                NotificationsSharePermille = notificationsShare,
                ShowTree = true,
                ShowConfig = true,
                ShowNotifications = true
            };
        }

        [Test]
        public void EveryMeasurementSurvivesARoundTrip()
        {
            using var harness = new MainLayoutHarness();

            harness.Layout.ApplyState(Everything(320, 700, 300));
            var captured = harness.Layout.CaptureState();

            Assert.Multiple(() =>
            {
                Assert.That(captured.LeftColumnWidth, Is.EqualTo(320));
                Assert.That(captured.TreeSharePermille, Is.EqualTo(700).Within(5));
                Assert.That(captured.NotificationsSharePermille, Is.EqualTo(300).Within(5));
                Assert.That(captured.IsComplete, Is.True);
            });
        }

        [Test]
        public void VisibilitySurvivesARoundTrip()
        {
            using var harness = new MainLayoutHarness();

            harness.Layout.ApplyState(new MainLayoutState
            {
                LeftColumnWidth = 300,
                TreeSharePermille = 600,
                NotificationsSharePermille = 250,
                ShowTree = true,
                ShowConfig = false,
                ShowNotifications = false
            });
            var captured = harness.Layout.CaptureState();

            Assert.Multiple(() =>
            {
                Assert.That(captured.ShowTree, Is.True);
                Assert.That(captured.ShowConfig, Is.False);
                Assert.That(captured.ShowNotifications, Is.False);
            });
        }

        /// <summary>
        /// Why the two horizontal splits are saved as a ratio and not as pixels.
        /// </summary>
        /// <remarks>
        /// Growing the window by half again would take a saved pixel height of 700-per-1000 down to
        /// roughly 470-per-1000. The ratio holding is what makes the value worth writing to
        /// settings at all.
        /// </remarks>
        [Test]
        public void SharesHoldWhenTheWindowIsResized()
        {
            using var harness = new MainLayoutHarness();

            harness.Layout.ApplyState(Everything(320, 700, 300));
            harness.Resize(1400, 900);
            var captured = harness.Layout.CaptureState();

            Assert.Multiple(() =>
            {
                Assert.That(captured.TreeSharePermille, Is.EqualTo(700).Within(20));
                Assert.That(captured.NotificationsSharePermille, Is.EqualTo(300).Within(20));
            });
        }

        /// <summary>
        /// The left column keeps its width when the window is resized, because splitMain fixes its
        /// first panel. Saving it as a share instead would make it grow with the window.
        /// </summary>
        [Test]
        public void LeftColumnWidthHoldsWhenTheWindowIsResized()
        {
            using var harness = new MainLayoutHarness();

            harness.Layout.ApplyState(Everything(320, 700, 300));
            harness.Resize(1400, 900);

            Assert.That(harness.Layout.CaptureState().LeftColumnWidth, Is.EqualTo(320));
        }

        /// <summary>
        /// Hiding a panel must not cost the user the size they chose for it.
        /// </summary>
        /// <remarks>
        /// Collapsing a SplitContainer panel leaves its SplitterDistance untouched, so the sizes
        /// are still the real ones while the tools are hidden. Hide the left column, quit, come
        /// back and show it again, and it should be exactly where it was - which only works if the
        /// measurements are still saved while it is hidden.
        /// </remarks>
        [Test]
        public void AHiddenLeftColumnKeepsTheSizesItHad()
        {
            using var harness = new MainLayoutHarness();

            harness.Layout.ApplyState(new MainLayoutState
            {
                LeftColumnWidth = 320,
                TreeSharePermille = 700,
                NotificationsSharePermille = 300,
                ShowTree = false,
                ShowConfig = false,
                ShowNotifications = false
            });
            var captured = harness.Layout.CaptureState();

            Assert.Multiple(() =>
            {
                Assert.That(captured.LeftColumnWidth, Is.EqualTo(320));
                Assert.That(captured.TreeSharePermille, Is.EqualTo(700).Within(5));
                Assert.That(captured.NotificationsSharePermille, Is.EqualTo(300).Within(5));
                Assert.That(captured.ShowTree, Is.False);
                Assert.That(captured.ShowConfig, Is.False);
                Assert.That(harness.SplitMain.Panel1Collapsed, Is.True);
            });
        }

        /// <summary>
        /// A width saved on a far larger window, or at a far higher DPI, must not throw when it is
        /// applied to a small one - SplitterDistance does exactly that when the value does not fit.
        /// </summary>
        [Test]
        public void AnOversizedSavedWidthIsClampedRatherThanThrown()
        {
            using var harness = new MainLayoutHarness();

            Assert.DoesNotThrow(() => harness.Layout.ApplyState(Everything(5000, 700, 300)));
            Assert.That(harness.SplitMain.SplitterDistance,
                        Is.LessThanOrEqualTo(harness.SplitMain.Width
                                             - harness.SplitMain.Panel2MinSize
                                             - harness.SplitMain.SplitterWidth));
        }

        /// <summary>
        /// First run: nothing has been saved, so every measurement falls back to the default.
        /// </summary>
        [Test]
        public void MissingMeasurementsFallBackToTheDefaults()
        {
            using var harness = new MainLayoutHarness();

            harness.Layout.ApplyState(new MainLayoutState { ShowTree = true, ShowConfig = true });
            var defaults = harness.Layout.DefaultState();

            Assert.That(harness.SplitMain.SplitterDistance, Is.EqualTo(defaults.LeftColumnWidth));
        }

        [Test]
        public void ResetToDefaultsShowsBothLeftPanelsAndHidesNotifications()
        {
            using var harness = new MainLayoutHarness();

            harness.Layout.ApplyState(Everything(320, 700, 300));
            harness.Layout.ResetToDefaults();
            var captured = harness.Layout.CaptureState();

            Assert.Multiple(() =>
            {
                Assert.That(captured.ShowTree, Is.True);
                Assert.That(captured.ShowConfig, Is.True);
                Assert.That(captured.ShowNotifications, Is.False);
                Assert.That(harness.SplitDocuments.Panel2Collapsed, Is.True);
            });
        }
    }
}
