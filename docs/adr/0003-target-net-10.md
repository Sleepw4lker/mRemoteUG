---
date: 2026-09-22
status: accepted
---

# Target .NET 10, SDK-style, and drop the Portable edition

The fork inherited .NET Framework 4.8 and legacy `.csproj` files with a pre/post-build script
chain. It now targets `net10.0-windows` with SDK-style projects (`612ff41`, `9acbc72`),
framework-dependent, **x64 only** — the RDP ActiveX control is loaded in-process, so host
bitness has to match the control's.

Two things about the migration are load-bearing and easy to undo by accident:

**`GenerateAssemblyInfo` is narrowed to the version.** It was `false` at the migration, so the SDK
emitted no assembly attributes at all. Since [ADR-0030](0030-derive-the-version-from-git-tags.md)
it is `true` but limited to the three version attributes (`Directory.Build.props`); every other
attribute is turned off there and must stay hand-written in `Properties/AssemblyInfo.cs`. Two of
them matter here:

- `AssemblyCompany` — together with `AssemblyProduct` it shapes the `user.config` directory
  layout. It was empty at the migration, which is the layout the installed base had then. The
  rename ([ADR-0024](0024-rename-to-mremoteug.md)) set it to `Uwe Gradenegger` and moved that
  directory on purpose, with no migration; changing it again relocates every user's settings.
- `[assembly: SupportedOSPlatform("windows10.0.22000.0")]` — without it every WinForms and
  registry call raises CA1416, which is 8,000-plus warnings. The version is the Windows 11
  baseline; see the amendment at the end of this record for why it is spelled here rather
  than in the target framework.

**The version is computed, not written down.** The old `1.76.20.*` wildcard is a hard error
under deterministic builds, so at the migration it became a literal, pinned in two places of
which only `AssemblyInfo.cs` reached the built assembly. Since ADR-0030, `ComputeVersionFromGit`
in `Directory.Build.props` derives it from the git tags and the SDK emits it. The `AssemblyInfo.cs`
files must not declare a version: a hand-written attribute would win over the computed one, and
`VersioningTests` fails if one reappears.

That matters because of what reads it: the MSI takes its `ProductVersion` from
`!(bind.FileVersion.MainExeFile)` (`Includes\Config.wxi`), i.e. off the built executable, and so
off the computed version. `ProductCode` is `*` and the `UpgradeCode` is fixed, so raising the
version is what makes an installed build upgrade in place rather than sit alongside the old one.
The version also stamps the `user.config` directory, but settings still carry over:
`Settings.Default.Upgrade()` copies the previous version's values forward on the first run of a
new one (`SettingsLoader`). The legacy importer that did this at the time, by enumerating
`mRemoteNG*` directories, was deleted with the rename (ADR-0024).

Also in `Directory.Build.props`: `AppendTargetFrameworkToOutputPath` is `false`, keeping output
at `bin\<Configuration>\`. The WiX `HarvestPath`, `MainExeFragment.wxs` and two tests all
resolve that exact layout, as the release scripts did before they were deleted
([ADR-0016](0016-wix-5-and-the-desktop-runtime.md)).

**The Portable edition was dropped in the same pass**, along with its `PORTABLE` compile
constant; the projects now build only Debug and Release, and the solution adds one configuration
over them (`Release Installer`) that builds the WiX projects with everything else in Release. It
doubled every configuration for a packaging
variant, and its settings-beside-the-executable behaviour meant logs and config could be in
either of two places — which made remote diagnosis (see
[ADR-0013](0013-selftest-as-the-verification-mechanism.md)) ambiguous for no benefit.

One consequence worth knowing before opening the solution: **do not open a form in the Visual
Studio designer.** It runs out-of-process, executes control constructors, and rewrites
`InitializeComponent` and the `.resx` on save. Edit `*.Designer.cs` by hand. Related platform
traps are in [docs/platform-findings.md](../platform-findings.md).

**Amended 2026-09-23: the platform attribute carries a version.** It is now
`windows10.0.22000.0`, so CA1416 knows the supported floor rather than reading the assembly as
"all Windows versions". Two alternatives were rejected by measurement, and both are easy to
"fix" back into a problem:

- **The target framework.** `net10.0-windows10.0.22000.0` sets `TargetPlatformVersion` to 10.0,
  which makes the SDK add a `FrameworkReference` on the Windows SDK projections
  (`Microsoft.Windows.SDK.NET.Ref`, CsWinRT). That contradicts
  [ADR-0015](0015-purge-third-party-dependencies.md), which exists to keep the dependency set at
  one `PackageReference`.
- **The `SupportedOSPlatformVersion` property.** A hard `NETSdkError` while
  `TargetPlatformVersion` is 7.0, which it is, because the target framework is a bare
  `net10.0-windows`.

The hand-written attribute is exactly what the SDK emits from that property, and nothing may
compete with it: since assembly info generation came back for the version (ADR-0030), the SDK's
own `SupportedOSPlatform("Windows7.0")` is switched off by name, because beside this one CA1416
went by 7.0 (measured 2026-10-07; `PlatformAttributeTests` pins it). It was verified to work rather than
assumed: a throwaway method marked `[SupportedOSPlatform("windows10.0.22000.0")]` raises CA1416
before the change and not after, so the analyzer honours the attribute and does **not** cap it
against the project's `TargetPlatformMinVersion` of 7.0. Both assemblies carry it, because
the test project references the application (`src/mRemoteUG.Tests` and `src/mRemoteUG`, then
`mRemoteNGTests` and `mRemoteV1`) and a mismatch would make every test a call into a
higher-floor assembly.

*History: written 2026-09-22; corrected 2026-10-07 - `GenerateAssemblyInfo`, the version and
`AssemblyCompany` changed with ADR-0030 and ADR-0024, the legacy importer was deleted by
ADR-0024, and the projects were renamed by ADR-0024.*
