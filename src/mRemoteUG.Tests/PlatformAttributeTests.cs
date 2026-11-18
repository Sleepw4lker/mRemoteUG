using System.Reflection;
using System.Runtime.Versioning;

using mRemoteUG.App.Info;

using NUnit.Framework;

namespace mRemoteUG.Tests;

/// <summary>
/// The one <c>SupportedOSPlatform</c> each assembly declares, and the Windows 11 floor it
/// carries.
/// </summary>
/// <remarks>
/// The attribute is hand-written in <c>Properties\AssemblyInfo.cs</c>, because the TFM that
/// would generate it brings in the Windows SDK projections (ADR-0003). Once assembly info
/// generation came back on for the version (ADR-0030), the SDK started emitting its own
/// <c>SupportedOSPlatform("Windows7.0")</c> beside it - the attribute allows several, so
/// nothing failed - and CA1416 took the 7.0 one: measured, a call to an API that needs
/// Windows 10 19041 warned "reachable on: 'windows' 7.0 and later". The analyzer reads what
/// the compiler sees, which is what reflection sees here.
/// </remarks>
[TestFixture]
public class PlatformAttributeTests
{
    private const string Windows11 = "windows10.0.22000.0";

    private static readonly object[] s_assemblies =
    {
        new object[] { typeof(GeneralAppInfo).Assembly },
        new object[] { typeof(PlatformAttributeTests).Assembly },
    };

    [TestCaseSource(nameof(s_assemblies))]
    public void EachAssemblyDeclaresExactlyOneSupportedPlatformTheWindows11Floor(Assembly assembly)
    {
        string[] declared = assembly.GetCustomAttributes<SupportedOSPlatformAttribute>()
                               .Select(a => a.PlatformName)
                               .ToArray();

        Assert.That(declared, Is.EqualTo(new[] { Windows11 }),
                    $"{assembly.GetName().Name} declares {string.Join(", ", declared)}. A second " +
                    "SupportedOSPlatform - the SDK's own Windows7.0 - is what CA1416 then goes by, " +
                    "and the Windows 11 floor stops counting.");
    }
}
