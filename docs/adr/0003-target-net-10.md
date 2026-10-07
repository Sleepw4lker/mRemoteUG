---
date: 2026-09-22
---

# Target .NET 10, SDK-style, and drop the Portable edition

The fork inherited .NET Framework 4.8 and legacy `.csproj` files with a pre/post-build script
chain. It now targets `net10.0-windows` with SDK-style projects (`612ff41`, `9acbc72`),
framework-dependent, **x64 only** — the RDP ActiveX control is loaded in-process, so host
bitness has to match the control's.

Two things about the migration are load-bearing and easy to undo by accident:

**`GenerateAssemblyInfo` is `false`**, so the SDK emits no assembly attributes and two of them
must stay hand-written in `Properties/AssemblyInfo.cs`:

- `AssemblyCompany("")` — an empty company name is what produces the `user.config` directory
  layout the existing installed base already has. Filling it in relocates every user's
  settings.
- `[assembly: SupportedOSPlatform("windows10.0.22000.0")]` — without it every WinForms and
  registry call raises CA1416, which is 8,000-plus warnings. The version is the Windows 11
  baseline; see the amendment at the end of this record for why it is spelled here rather
  than in the target framework.

**The version is pinned, in two places, and only one of them is effective.** The old
`1.76.20.*` wildcard is a hard error under deterministic builds, so it had to become a literal.
But because `GenerateAssemblyInfo` is false, the SDK emits no version attributes, so
`<Version>`/`<FileVersion>`/`<AssemblyVersion>` in `Directory.Build.props` do **not** reach the
built assembly — `AssemblyVersion` and `AssemblyFileVersion` in `Properties\AssemblyInfo.cs` are
what land in it. The props values are kept in step deliberately, but editing only them changes
nothing. **Change both.**

That matters because of what reads it: the MSI takes its `ProductVersion` from
`!(bind.FileVersion.MainExeFile)` (`Includes\Config.wxi`), i.e. off the built executable, and so
off `AssemblyInfo.cs`. `ProductCode` is `*` and the `UpgradeCode` is fixed, so raising the version
is what makes an installed build upgrade in place rather than sit alongside the old one. The
version also stamps the `user.config` directory, but settings still carry over: the legacy
importer enumerates `mRemoteNG*` directories by last-write time and does not compare versions.

Also in `Directory.Build.props`: `AppendTargetFrameworkToOutputPath` is `false`, keeping output
at `bin\<Configuration>\`. The WiX `HarvestPath`, `MainExeFragment.wxs`, the release scripts
and two tests all resolve that exact layout.

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

The hand-written attribute is exactly what the SDK emits from that property, and because
`GenerateAssemblyInfo` is `false` nothing competes with it. It was verified to work rather than
assumed: a throwaway method marked `[SupportedOSPlatform("windows10.0.22000.0")]` raises CA1416
before the change and not after, so the analyzer honours the attribute and does **not** cap it
against the project's `TargetPlatformMinVersion` of 7.0. Both assemblies carry it, because
`mRemoteNGTests` references `mRemoteV1` and a mismatch would make every test a call into a
higher-floor assembly.
