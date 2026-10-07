using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;
using WixToolset.Dtf.WindowsInstaller;

namespace mRemoteUG.Installer.CustomActions
{
    /// <summary>
    /// Detects whether a new enough .NET Windows Desktop Runtime is present.
    /// </summary>
    /// <remarks>
    /// Two details make this awkward enough to be worth a custom action rather than a
    /// util:RegistrySearch:
    ///
    /// 1. The key exists only in the 32-bit registry view. The .NET runtime installers are 32-bit
    ///    MSIs, so they write under Wow6432Node even when installing the x64 runtime. This package
    ///    is 64-bit, so a plain registry search reads the 64-bit view, finds nothing, and blocks
    ///    installation on a machine that has the runtime. Hence RegistryView.Registry32.
    ///
    /// 2. There is no version value to compare against. The key holds one DWORD per installed
    ///    version, named by the version itself ("10.0.12" = 1), so establishing "10.0 or newer"
    ///    means enumerating value names and parsing them.
    /// </remarks>
    public class DesktopRuntimeChecker
    {
        private const string SharedFxKey =
            @"SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App";

        private readonly Session _session;

        public DesktopRuntimeChecker(Session session)
        {
            _session = session;
        }

        /// <summary>
        /// Sets <paramref name="resultProperty"/> to "1" when a Desktop Runtime of at least
        /// <paramref name="requiredMajorVersion"/> is installed, otherwise "0".
        /// </summary>
        public void Execute(int requiredMajorVersion, string resultProperty)
        {
            var installed = GetInstalledVersions();

            if (installed.Count == 0)
                _session.Log("DesktopRuntimeChecker: no Microsoft.WindowsDesktop.App versions found.");
            else
                _session.Log("DesktopRuntimeChecker: found " +
                             string.Join(", ", installed.Select(v => v.ToString()).ToArray()));

            var satisfied = installed.Any(v => v.Major >= requiredMajorVersion);
            _session.Log($"DesktopRuntimeChecker: required major version {requiredMajorVersion}, satisfied = {satisfied}");
            _session[resultProperty] = satisfied ? "1" : "0";
        }

        private List<Version> GetInstalledVersions()
        {
            var versions = new List<Version>();
            try
            {
                // Explicitly the 32-bit view; see the remarks above.
                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
                using (var key = baseKey.OpenSubKey(SharedFxKey))
                {
                    if (key == null)
                    {
                        _session.Log($"DesktopRuntimeChecker: key HKLM\\{SharedFxKey} (32-bit view) not present.");
                        return versions;
                    }

                    foreach (var name in key.GetValueNames())
                    {
                        if (Version.TryParse(name, out var version))
                            versions.Add(version);
                        else
                            _session.Log($"DesktopRuntimeChecker: ignoring unparseable value name '{name}'.");
                    }
                }
            }
            catch (Exception ex)
            {
                // A detection failure must not abort the install; the launch condition reports it.
                _session.Log("DesktopRuntimeChecker: registry read failed: " + ex);
            }

            return versions;
        }
    }
}
