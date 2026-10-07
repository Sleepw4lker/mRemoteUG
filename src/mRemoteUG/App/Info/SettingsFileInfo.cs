using System;
using System.IO;
using System.Windows.Forms;

namespace mRemoteUG.App.Info
{
    public static class SettingsFileInfo
    {
        /// <summary>
        /// The per-user directory holding the settings and the connection file.
        /// </summary>
        /// <remarks>
        /// The one definition of this path. Roaming on purpose: settings and the connection
        /// file are small, and following the user to another machine in the same domain is the
        /// point.
        /// </remarks>
        public static string SettingsPath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Application.ProductName);

        /// <summary>
        /// The per-user directory the log is written to.
        /// </summary>
        /// <remarks>
        /// Deliberately not <see cref="SettingsPath"/>. The log is machine-specific noise -
        /// nobody wants it following them around a roaming profile - and local-only also means
        /// it is excluded from any roaming profile size quota.
        /// </remarks>
        public static string LogsPath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Application.ProductName);
    }
}
