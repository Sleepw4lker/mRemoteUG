using System.Threading;
using mRemoteUG.Config.Settings;
using NUnit.Framework;

namespace mRemoteUG.Tests.UI.Panels
{
    /// <summary>
    /// Covers the trip through user settings: what gets written on the way out, and what the
    /// window is put back into on the way in.
    /// </summary>
    /// <remarks>
    /// The DPI rescale is the part worth guarding. A saved splitter distance is physical pixels,
    /// so on its own it means nothing - a left column dragged to 400px on a 200% display is the
    /// same column as 200px on a 100% one, and reopening it at 400px there makes it twice as wide
    /// as the user left it. The same caveat already applies to the saved window size and the
    /// toolbar offset, and all three share Settings.MainFormDpi because one save writes them all.
    /// <para>
    /// These mutate Settings.Default in memory and put it back afterwards. Nothing here calls
    /// Save(), so the user.config on disk is never touched.
    /// </para>
    /// </remarks>
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class MainLayoutSettingsTests
    {
        private int _mainFormDpi;
        private int _leftColumnWidth;
        private int _treeShare;
        private int _notificationsShare;
        private bool _showTree;
        private bool _showConfig;
        private bool _showNotifications;
        private bool _resetPanels;

        [SetUp]
        public void CaptureSettings()
        {
            var settings = mRemoteUG.Settings.Default;
            _mainFormDpi = settings.MainFormDpi;
            _leftColumnWidth = settings.MainLayoutLeftColumnWidth;
            _treeShare = settings.MainLayoutTreeSharePermille;
            _notificationsShare = settings.MainLayoutNotificationsSharePermille;
            _showTree = settings.MainLayoutShowTree;
            _showConfig = settings.MainLayoutShowConfig;
            _showNotifications = settings.MainLayoutShowNotifications;
            _resetPanels = settings.ResetPanels;
        }

        [TearDown]
        public void RestoreSettings()
        {
            var settings = mRemoteUG.Settings.Default;
            settings.MainFormDpi = _mainFormDpi;
            settings.MainLayoutLeftColumnWidth = _leftColumnWidth;
            settings.MainLayoutTreeSharePermille = _treeShare;
            settings.MainLayoutNotificationsSharePermille = _notificationsShare;
            settings.MainLayoutShowTree = _showTree;
            settings.MainLayoutShowConfig = _showConfig;
            settings.MainLayoutShowNotifications = _showNotifications;
            settings.ResetPanels = _resetPanels;
        }

        private static void Store(int dpi, int width, int treeShare, int notificationsShare)
        {
            var settings = mRemoteUG.Settings.Default;
            settings.ResetPanels = false;
            settings.MainFormDpi = dpi;
            settings.MainLayoutLeftColumnWidth = width;
            settings.MainLayoutTreeSharePermille = treeShare;
            settings.MainLayoutNotificationsSharePermille = notificationsShare;
            settings.MainLayoutShowTree = true;
            settings.MainLayoutShowConfig = true;
            settings.MainLayoutShowNotifications = true;
        }

        [Test]
        public void ASavedLayoutIsRestoredAtTheDpiItWasSavedAt()
        {
            using var harness = new MainLayoutHarness();
            Store(dpi: 96, width: 320, treeShare: 700, notificationsShare: 300);

            MainLayoutSettings.Restore(harness.Layout, 96);

            Assert.That(harness.SplitMain.SplitterDistance, Is.EqualTo(320));
        }

        [Test]
        public void AWidthSavedAtAHigherDpiIsScaledDown()
        {
            using var harness = new MainLayoutHarness();
            Store(dpi: 192, width: 400, treeShare: 700, notificationsShare: 300);

            MainLayoutSettings.Restore(harness.Layout, 96);

            Assert.That(harness.SplitMain.SplitterDistance, Is.EqualTo(200));
        }

        [Test]
        public void AWidthSavedAtALowerDpiIsScaledUp()
        {
            using var harness = new MainLayoutHarness();
            Store(dpi: 96, width: 200, treeShare: 700, notificationsShare: 300);

            MainLayoutSettings.Restore(harness.Layout, 144);

            Assert.That(harness.SplitMain.SplitterDistance, Is.EqualTo(300));
        }

        /// <summary>
        /// Settings written before the DPI was recorded have it at 0, and mean what they said
        /// when they were written.
        /// </summary>
        [Test]
        public void AWidthWithNoRecordedDpiIsTakenAtFaceValue()
        {
            using var harness = new MainLayoutHarness();
            Store(dpi: 0, width: 320, treeShare: 700, notificationsShare: 300);

            MainLayoutSettings.Restore(harness.Layout, 192);

            Assert.That(harness.SplitMain.SplitterDistance, Is.EqualTo(320));
        }

        /// <summary>
        /// What --resetpanels is for: an escape hatch from a saved layout that is in the way.
        /// </summary>
        [Test]
        public void ResetPanelsIgnoresTheSavedLayout()
        {
            using var harness = new MainLayoutHarness();
            Store(dpi: 96, width: 320, treeShare: 700, notificationsShare: 300);
            mRemoteUG.Settings.Default.MainLayoutShowConfig = false;
            mRemoteUG.Settings.Default.ResetPanels = true;

            MainLayoutSettings.Restore(harness.Layout, 96);

            Assert.Multiple(() =>
            {
                Assert.That(harness.SplitMain.SplitterDistance,
                            Is.EqualTo(harness.Layout.DefaultState().LeftColumnWidth));
                Assert.That(harness.Layout.CaptureState().ShowConfig, Is.True);
            });
        }

        /// <summary>
        /// First run: nothing has been saved, so the defaults stand rather than a half-read state.
        /// </summary>
        [Test]
        public void AnUnsavedLayoutFallsBackToTheDefaults()
        {
            using var harness = new MainLayoutHarness();
            Store(dpi: 0, width: 0, treeShare: 0, notificationsShare: 0);

            MainLayoutSettings.Restore(harness.Layout, 96);

            Assert.That(harness.SplitMain.SplitterDistance,
                        Is.EqualTo(harness.Layout.DefaultState().LeftColumnWidth));
        }

        [Test]
        public void SaveWritesBackWhatTheWindowIsShowing()
        {
            using var harness = new MainLayoutHarness();
            Store(dpi: 96, width: 320, treeShare: 700, notificationsShare: 300);
            MainLayoutSettings.Restore(harness.Layout, 96);

            harness.Layout.HideTool(harness.ConfigWindow);
            MainLayoutSettings.Save(harness.Layout);

            Assert.Multiple(() =>
            {
                Assert.That(mRemoteUG.Settings.Default.MainLayoutShowConfig, Is.False);
                Assert.That(mRemoteUG.Settings.Default.MainLayoutShowTree, Is.True);
                Assert.That(mRemoteUG.Settings.Default.MainLayoutLeftColumnWidth, Is.EqualTo(320));
            });
        }
    }
}
