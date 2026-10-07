using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

// Normally emitted by the SDK from the net10.0-windows target, but GenerateAssemblyInfo is
// off because the attributes here are maintained by hand. Without it, every call into
// WinForms or the registry raises CA1416 ("this call site is reachable on all platforms"),
// because nothing tells the analyzer the assembly is Windows-only.
//
// The version is the Windows 11 baseline, and it is declared here rather than by moving the
// target framework to net10.0-windows10.0.22000.0. That TFM would set TargetPlatformVersion
// to 10.0, which makes the SDK add a FrameworkReference on the Windows SDK projections
// (CsWinRT) - a dependency this project does not want. Setting the SupportedOSPlatformVersion
// property instead is a hard build error while TargetPlatformVersion is 7.0. This attribute is
// exactly what the SDK would have emitted from that property, and is subject to neither.
[assembly: SupportedOSPlatform("windows10.0.22000.0")]

[assembly: InternalsVisibleTo("mRemoteUG.Tests")]



// General Information about an assembly is controlled through the following
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.

// Review the values of the assembly attributes

[assembly: AssemblyTitle("mRemoteUG")]
[assembly: AssemblyDescription("A lean RDP and SSH connections manager; a fork of mRemoteNG")]
[assembly: AssemblyCompany("Uwe Gradenegger")]
[assembly: AssemblyProduct("mRemoteUG")]
[assembly: AssemblyCopyright("Copyright © 2026 Uwe Gradenegger. Based on mRemoteNG: " +
                            "© 2019 mRemoteNG Dev Team; 2010-2013 Riley McArdle; 2007-2009 Felix Deimel")]
[assembly: AssemblyTrademark("")]

[assembly: ComVisible(false)]

//The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("A99669B2-FAEB-11DE-995A-826C56D89593")]

// The version is deliberately not here. It is computed from the git tags by
// ComputeVersionFromGit in Directory.Build.props and emitted by the SDK - see ADR-0030. A
// hand-written AssemblyVersion or AssemblyFileVersion would win over the computed one and
// silently detach the binary from the tag it was built from, which is the failure the old
// two-places-one-truth arrangement actually had.
//
// VersioningTests fails if one reappears, and pins the shape of all three computed attributes.

[assembly: NeutralResourcesLanguage("en")]