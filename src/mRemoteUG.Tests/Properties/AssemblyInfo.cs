using System.Reflection;
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

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("mRemoteUG.Tests")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("mRemoteUG.Tests")]
[assembly: AssemblyCopyright("Copyright ©  2016")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible 
// to COM components.  If you need to access a type in this assembly from 
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("4e9583d3-2b1b-419d-bda5-f06e70e03b50")]

// No version attributes here: they are computed from the git tags in Directory.Build.props
// and emitted by the SDK. See ADR-0030.

