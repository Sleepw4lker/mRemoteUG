using System.Windows.Forms;

namespace mRemoteUG.UI
{
    /// <summary>
    /// Which colour theme the user asked for.
    /// </summary>
    /// <remarks>
    /// This mirrors <see cref="SystemColorMode"/> one for one and exists only so that the
    /// persisted setting is ours rather than the framework's. The settings designer writes an
    /// enum to <c>user.config</c> by name, and <see cref="SystemColorMode"/> spells light mode
    /// "Classic" - a word that would appear in a user-visible file for an option this
    /// application calls "Light", and could not be renamed later without a settings migration.
    /// </remarks>
    public enum UiTheme
    {
        /// <summary>Follow the Windows setting, and whatever it changes to.</summary>
        System = 0,

        /// <summary>Always light, whatever Windows is set to.</summary>
        Light = 1,

        /// <summary>Always dark, whatever Windows is set to.</summary>
        Dark = 2
    }

    /// <summary>
    /// Translates a <see cref="UiTheme"/> into the value WinForms wants.
    /// </summary>
    public static class UiThemes
    {
        /// <summary>
        /// The <see cref="SystemColorMode"/> to hand <see cref="Application.SetColorMode"/>.
        /// </summary>
        /// <remarks>
        /// Anything unrecognised - a hand-edited <c>user.config</c>, or a value written by a
        /// later version - falls back to following the system, which is the default.
        /// </remarks>
        public static SystemColorMode ToColorMode(this UiTheme theme)
        {
            switch (theme)
            {
                case UiTheme.Light:
                    return SystemColorMode.Classic;
                case UiTheme.Dark:
                    return SystemColorMode.Dark;
                default:
                    return SystemColorMode.System;
            }
        }
    }
}
