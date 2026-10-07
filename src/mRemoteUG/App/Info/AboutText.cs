using System;

namespace mRemoteUG.App.Info
{
    /// <summary>
    /// What the About dialog says: which version this is, whose work it is built on, and what it
    /// may be used under.
    /// </summary>
    /// <remarks>
    /// Kept apart from <see cref="UI.Menu.HelpMenu"/> for the same reason
    /// <c>CTaskDialog.BuildPage</c> is kept apart from showing a dialog - a shown task dialog is
    /// modal, so the only way to assert this text is to be able to compose it without showing
    /// anything.
    /// <para>
    /// It is worth having somewhere findable. Almost none of this application was written by the
    /// person whose name is now on it, and this dialog is the only place in the running program
    /// that says so.
    /// </para>
    /// </remarks>
    internal static class AboutText
    {
        /// <summary>
        /// The body of the dialog: version, what this is, and who holds the copyright.
        /// </summary>
        internal static string Content =>
            $"Version {GeneralAppInfo.ApplicationVersion}" + Environment.NewLine +
            Language.strAboutFork + Environment.NewLine + Environment.NewLine +
            GeneralAppInfo.Copyright;

        /// <summary>
        /// The lineage and the third-party notices, shown in the dialog's expander so they are
        /// there without crowding the four lines above.
        /// </summary>
        internal static string Credits => Language.strAboutCredits;
    }
}
