---
date: 2026-09-22
---

# Build the installer with WiX 5 and require the .NET Desktop Runtime

The installer was a WiX v3 Votive project — a Visual Studio project type with no .NET Core
support and no command-line story. `a128786` migrated it to WiX Toolset 5.0.2 as an SDK-style
`.wixproj`, with `WixToolset.Sdk`, `UI.wixext`, `Util.wixext` and `Heat` all **pinned to
5.0.2**. Pinned rather than floating because WiX's minor releases have changed authoring
behaviour before, and an installer that silently builds differently is worse than one that
fails to build.

`7ce57f3` then replaced the prerequisite check. The old one used WiX's NetFx extension and its
`SetWIX_IS_NETFRAMEWORK_40_OR_LATER_INSTALLED` action; that extension is no longer used, and
the requirement is now the **.NET Windows Desktop Runtime**, detected by a custom action
(`CheckIfDotNetDesktopRuntimeInstalled`) scheduled after `AppSearch` and before
`LaunchConditions`.

The alternative was self-contained or single-file publishing, which would make the runtime check
unnecessary. Rejected: the RDP ActiveX control is loaded in-process and the application is x64
only ([ADR-0003](0003-target-net-10.md)), so a self-contained build trades a one-time
prerequisite install for a much larger MSI on every update, with no isolation benefit for a
desktop application that follows the system's WinForms theming
([ADR-0009](0009-system-colour-theme-at-startup.md)) anyway.

The consequence to remember is a coupling that is invisible from either side: the MSI reads
`ProductVersion` off the built executable via `!(bind.FileVersion.MainExeFile)`, and the WiX
`HarvestPath`, `MainExeFragment.wxs`, the release scripts and two tests all resolve
`bin\<Configuration>\` with no target-framework subfolder. That is why
`AppendTargetFrameworkToOutputPath` is `false` and why the version is a literal in
`Directory.Build.props`. Changing either breaks the installer, not the build.

`e2f3cc8` removed the installer's dead Windows 7 conditions, consistent with the `supportedOS`
entries dropped in [ADR-0010](0010-per-monitor-v2.md).

**Amended 2026-09-23: the floor is now Windows 11.** The `VersionNT >= 602` condition that
`e2f3cc8` left behind was Windows 8, on the stated grounds that 602 was the oldest platform
.NET 10 supports. That was wrong even then — .NET 10 floors at Windows 10 1607 — so the
installer was more permissive than the manifest, the README and the code all believed.

The surprising part, and the reason this is recorded rather than left as a number: **`VersionNT`
cannot express Windows 11.** Windows 10, Windows 11 and Server 2016 and newer all report
`1000`, and there is no `1100`. The baseline has to be a build number.

**Amended 2026-09-24: the build number comes from the registry, not from `WindowsBuild`.** The
first form of the floor, `VersionNT >= 1000 AND WindowsBuild >= 22000`, refused a fully patched
Windows 11 26200 with its own "requires Windows 11 (build 22000)" message. Inside `msiexec.exe`
the properties are `VersionNT` 603 and `WindowsBuild` 9600 on every machine: msiexec's
compatibility manifest declares only the Windows 8.1 `supportedOS` GUID, so the version APIs it
calls are shimmed to 6.3.9600. A verbose log of a silent run of the package on this machine
shows `Property(S): VersionNT = 603` and `Property(S): WindowsBuild = 9600`.

The measurement that justified the first form was wrong in a way worth remembering. It opened
the package through the `WindowsInstaller.Installer` COM object from PowerShell and read
`WindowsBuild` 26200 — true, but only because the session ran inside PowerShell, which is
manifested for Windows 10. **An in-process session reports the host process's version, so a
launch condition has to be checked through `msiexec.exe`**, for example
`msiexec /i package.msi /qn /l*v probe.log REQUIREDDOTNETMAJORVERSION=99`, which stops at a
launch condition and leaves every property value in the log. See the Installer section of
[platform-findings.md](../platform-findings.md).

The condition is now `WINDOWSCURRENTBUILD >= 22000`, where `WINDOWSCURRENTBUILD` is a
`RegistrySearch` on `HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\CurrentBuildNumber`,
which is not shimmed. `AppSearch` runs it before `LaunchConditions` in both sequences. The value
is `REG_SZ`, and Windows Installer compares a property against an integer literal numerically:
`"26200" >= 22000` is True while `"19045"`, `""` and `"abc"` are False, so a machine with no
readable value is refused rather than admitted. Verified through msiexec on 26200: the silent
run's log reports `WINDOWSCURRENTBUILD = 26200` from msiexec's own `AppSearch`, and the
condition evaluated against that value in a Windows Installer session answers True, with
`WINDOWSCURRENTBUILD >= 99999` False as the negative control. Not verified: a full elevated run
through the UI sequence, which needs an interactive install.

A build-number floor **admits Server 2025** (26100), which is kernel-equivalent to Windows 11
24H2, while still excluding Server 2022 (20348). Deliberate: it is not filtered with
`MsiNTProductType` because nothing here needs a client SKU and a jump host is a real place to
install a connection manager.

`InstallerVersion` went from `200` to `500` in the same pass; 200 claimed a Windows 2000-era
engine.
