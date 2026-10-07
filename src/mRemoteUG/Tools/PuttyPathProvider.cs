using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace mRemoteUG.Tools
{
    /// <summary>
    /// Resolves the PuTTY executable used by <see cref="Connection.Protocol.PuttyBase"/>.
    /// mRemoteUG no longer bundles PuTTYNG.exe, so PuTTY is supplied by the user: either
    /// configured explicitly under Tools > Options > Advanced, or discovered next to
    /// mRemoteUG.exe / on PATH so a drop-in install still works.
    /// </summary>
    public static class PuttyPathProvider
    {
        private static readonly string[] ProbeNames = { "PuTTYNG.exe", "putty.exe" };

        /// <summary>The result of the last <see cref="Discover"/>, or null when not yet asked.</summary>
        /// <remarks>
        /// Discovery probes File.Exists for two executable names beside mRemoteUG.exe and in
        /// every PATH entry - on a thirty-entry PATH, sixty filesystem calls. It had no cache,
        /// and ResolvedPath is read on the startup path, whenever a PuTTY session is opened,
        /// and by IsUsable and GetUnusableReason from the options page.
        /// <para>
        /// Neither PATH nor an installed PuTTY changes under a running process in any way
        /// worth re-probing for, but installing PuTTY and then setting the path in Options is
        /// a real sequence - so that page calls <see cref="InvalidateDiscoveryCache"/>.
        /// </para>
        /// </remarks>
        private static string _discovered;

        /// <summary>Forget the discovered path, so the next read probes the disk again.</summary>
        public static void InvalidateDiscoveryCache() => _discovered = null;

        /// <summary>The path configured by the user, trimmed; empty when unset.</summary>
        public static string ConfiguredPath => (Settings.Default.CustomPuttyPath ?? string.Empty).Trim();

        /// <summary>
        /// The executable to launch: the configured path if there is one, otherwise the
        /// first PuTTY found beside mRemoteUG.exe or on PATH. Empty when nothing is found.
        /// </summary>
        public static string ResolvedPath
        {
            get
            {
                var configured = ConfiguredPath;
                return configured.Length > 0 ? configured : Discover();
            }
        }

        public static bool IsUsable => SafeFileExists(ResolvedPath);

        /// <summary>
        /// A message explaining why PuTTY cannot be launched, or null when it can.
        /// </summary>
        public static string GetUnusableReason()
        {
            var configured = ConfiguredPath;
            if (configured.Length > 0)
                return SafeFileExists(configured)
                    ? null
                    : string.Format(Language.strPuttyPathNotFound, configured);

            return Discover().Length > 0 ? null : Language.strPuttyPathNotConfigured;
        }

        /// <summary>
        /// Directory holding the PuTTY executable, or null. Used to locate a portable
        /// PuTTY's putty.conf.
        /// </summary>
        /// <remarks>
        /// The path is checked for invalid characters explicitly rather than relying on
        /// Path.GetDirectoryName to reject it. On .NET Framework that method threw
        /// ArgumentException for a malformed path; modern .NET dropped character validation from
        /// the path APIs, so "C:\|not|a|path" now yields "C:\" rather than throwing. Without this
        /// check a malformed setting would silently resolve to a real directory.
        /// </remarks>
        public static string ResolvedDirectory
        {
            get
            {
                var path = ResolvedPath;
                if (path.Length == 0) return null;
                if (IsMalformed(path)) return null;

                try
                {
                    var directory = Path.GetDirectoryName(path);
                    return string.IsNullOrEmpty(directory) ? null : directory;
                }
                catch (ArgumentException)
                {
                    return null;
                }
            }
        }

        private static bool IsMalformed(string path)
        {
            return path.IndexOfAny(Path.GetInvalidPathChars()) >= 0
                   || Path.GetFileName(path).IndexOfAny(Path.GetInvalidFileNameChars()) >= 0;
        }

        private static string Discover()
        {
            return _discovered ?? (_discovered = Probe());
        }

        private static string Probe()
        {
            var searchRoots = new[] { App.Info.GeneralAppInfo.HomePath }
                .Concat(SplitPath())
                .Where(root => !string.IsNullOrEmpty(root));

            foreach (var root in searchRoots)
            foreach (var name in ProbeNames)
            {
                string candidate;
                try
                {
                    candidate = Path.Combine(root, name);
                }
                catch (ArgumentException)
                {
                    continue;
                }

                if (SafeFileExists(candidate)) return candidate;
            }

            return string.Empty;
        }

        private static IEnumerable<string> SplitPath()
        {
            var path = Environment.GetEnvironmentVariable("PATH");
            return string.IsNullOrEmpty(path)
                ? Enumerable.Empty<string>()
                : path.Split(Path.PathSeparator);
        }

        private static bool SafeFileExists(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            try
            {
                return File.Exists(path);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
