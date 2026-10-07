using System;
using mRemoteUG.UI.Panels;

namespace mRemoteUG.Config.Settings
{
    /// <summary>
    /// Carries the main window's panel arrangement to and from user settings.
    /// </summary>
    /// <remarks>
    /// Kept apart from <see cref="SettingsLoader"/> because the restore cannot run where the rest
    /// of the settings are loaded: that happens before the tool windows are hosted in their
    /// panels, so anything written there is overwritten moments later. The save side is called
    /// from <see cref="SettingsSaver"/> like everything else.
    /// </remarks>
    public static class MainLayoutSettings
    {
        /// <summary>
        /// Records the current arrangement. Leaves <c>Settings.Default.Save()</c> to the caller.
        /// </summary>
        public static void Save(MainLayout layout)
        {
            if (layout == null)
                return;

            var state = layout.CaptureState();

            // A measurement of 0 means it could not be taken - the panel was collapsed, or the
            // window was never laid out - so the stored value stands. That is what lets someone
            // hide the whole left column, quit, and still find it at its old width when they show
            // it again.
            if (state.LeftColumnWidth > 0)
                mRemoteUG.Settings.Default.MainLayoutLeftColumnWidth = state.LeftColumnWidth;
            if (state.TreeSharePermille > 0)
                mRemoteUG.Settings.Default.MainLayoutTreeSharePermille = state.TreeSharePermille;
            if (state.NotificationsSharePermille > 0)
                mRemoteUG.Settings.Default.MainLayoutNotificationsSharePermille =
                    state.NotificationsSharePermille;

            mRemoteUG.Settings.Default.MainLayoutShowTree = state.ShowTree;
            mRemoteUG.Settings.Default.MainLayoutShowConfig = state.ShowConfig;
            mRemoteUG.Settings.Default.MainLayoutShowNotifications = state.ShowNotifications;
        }

        /// <summary>
        /// Puts the window back the way it was left, or into the defaults when there is nothing
        /// saved and when <c>--resetpanels</c> asks for them.
        /// </summary>
        public static void Restore(MainLayout layout, int currentDpi)
        {
            if (layout == null)
                return;

            var state = ReadState(currentDpi);

            if (mRemoteUG.Settings.Default.ResetPanels || !state.IsComplete)
            {
                layout.ResetToDefaults();
                return;
            }

            layout.ApplyState(state);
        }

        private static MainLayoutState ReadState(int currentDpi)
        {
            return new MainLayoutState
            {
                LeftColumnWidth = RescaleWidth(
                    mRemoteUG.Settings.Default.MainLayoutLeftColumnWidth, currentDpi),
                TreeSharePermille = mRemoteUG.Settings.Default.MainLayoutTreeSharePermille,
                NotificationsSharePermille =
                    mRemoteUG.Settings.Default.MainLayoutNotificationsSharePermille,
                ShowTree = mRemoteUG.Settings.Default.MainLayoutShowTree,
                ShowConfig = mRemoteUG.Settings.Default.MainLayoutShowConfig,
                ShowNotifications = mRemoteUG.Settings.Default.MainLayoutShowNotifications
            };
        }

        /// <summary>
        /// Converts a left column width saved at one DPI into the equivalent at the current one.
        /// </summary>
        /// <remarks>
        /// The same caveat as the window size and the toolbar offset, and it shares their
        /// <c>MainFormDpi</c> because all three are written by the same save: a column dragged to
        /// 400px on a 200% display is half as wide as it should be when it reopens on a 100% one.
        /// Settings written before that DPI was recorded have it at 0 and are taken at face value.
        /// </remarks>
        private static int RescaleWidth(int saved, int currentDpi)
        {
            var savedDpi = mRemoteUG.Settings.Default.MainFormDpi;
            if (saved <= 0 || savedDpi <= 0 || currentDpi <= 0 || savedDpi == currentDpi)
                return saved;

            return (int)Math.Round(saved * currentDpi / (double)savedDpi);
        }
    }
}
