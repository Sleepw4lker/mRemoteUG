namespace mRemoteUG.UI.Panels
{
    /// <summary>
    /// The part of the main window arrangement that is worth remembering between runs.
    /// </summary>
    /// <remarks>
    /// Deliberately a plain value object that knows nothing about settings: the measurements are
    /// fiddly enough to want testing without a user.config behind them, so the marshalling lives
    /// in <c>Config.Settings.MainLayoutSettings</c> instead.
    /// <para>
    /// The shares are integers in permille rather than a <c>double</c> fraction because settings
    /// round-trip through a culture-sensitive type converter. A 0.6 written as "0,6" on a German
    /// machine and read back as 6 somewhere else is a silent corruption that an integer cannot
    /// have.
    /// </para>
    /// </remarks>
    public class MainLayoutState
    {
        /// <summary>The denominator the two shares are expressed in.</summary>
        public const int ShareScale = 1000;

        /// <summary>
        /// Width of the left tool column in device pixels, or 0 when it could not be measured.
        /// </summary>
        /// <remarks>
        /// Pixels rather than a share because splitMain has its first panel fixed: that splitter
        /// distance is an absolute width which WinForms carries unchanged across window resizes.
        /// Being physical pixels it is only meaningful next to the DPI it was taken at, which is
        /// the caller's problem - see Settings.MainFormDpi, saved in the same pass.
        /// </remarks>
        public int LeftColumnWidth { get; set; }

        /// <summary>
        /// The connection tree's share of the left column, out of <see cref="ShareScale"/>, or 0
        /// when it could not be measured.
        /// </summary>
        /// <remarks>
        /// A share and not pixels because splitLeft has no fixed panel, so WinForms rescales its
        /// splitter distance proportionally on every resize. A pixel height saved at one window
        /// size means nothing at another.
        /// </remarks>
        public int TreeSharePermille { get; set; }

        /// <summary>
        /// The notifications panel's share of the document area, out of <see cref="ShareScale"/>,
        /// or 0 when it could not be measured. Stored the way it reads rather than as the
        /// documents' share, so the saved number matches the default constant.
        /// </summary>
        public int NotificationsSharePermille { get; set; }

        public bool ShowTree { get; set; }

        public bool ShowConfig { get; set; }

        public bool ShowNotifications { get; set; }

        /// <summary>
        /// Whether every measurement is present, and so whether this is worth restoring at all
        /// rather than falling back to the defaults.
        /// </summary>
        public bool IsComplete =>
            LeftColumnWidth > 0 && TreeSharePermille > 0 && NotificationsSharePermille > 0;
    }
}
