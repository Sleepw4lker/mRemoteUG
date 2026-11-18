using System.Reflection;
using System.Text.RegularExpressions;

using mRemoteUG.App.Info;

using NUnit.Framework;

namespace mRemoteUG.Tests;

/// <summary>
/// The shape of the three version attributes the build stamps onto the application assembly.
/// </summary>
/// <remarks>
/// The version is derived from the git tags rather than written down in the tree, so nothing
/// in source says what it should be and a test cannot assert a number. What it can assert is
/// the shape, and the shape is what the rest of the system depends on:
/// <list type="bullet">
/// <item>The roaming <c>user.config</c> path keys off <c>AssemblyVersion</c>. Holding its
/// fourth field at 0 is what keeps a user's settings in place across rolling builds; a build
/// number leaking into it would move the settings directory on every single build.</item>
/// <item>The MSI binds its <c>ProductVersion</c> off the executable's <c>FileVersion</c>, so
/// that has to be four plain numeric fields - Windows Installer will not parse anything
/// else.</item>
/// <item>The informational version is what reaches the log, and the commit hash in it is the
/// only way to identify a binary running on a machine this one cannot reach.</item>
/// </list>
/// </remarks>
[TestFixture]
public class VersioningTests
{
    private static Assembly Application => typeof(GeneralAppInfo).Assembly;

    [Test]
    public void TheAssemblyVersionHoldsItsFourthFieldAtZero()
    {
        Version version = AssemblyVersion();

        Assert.That(version.Revision, Is.Zero,
                    $"AssemblyVersion is {version}. Its fourth field feeds the roaming " +
                    "user.config path, so anything but 0 moves every user's settings on " +
                    "every build.");
    }

    [Test]
    public void TheFileVersionIsFourNumericFields()
    {
        string? fileVersion = Application.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;

        Assert.That(fileVersion, Is.Not.Null.And.Not.Empty);
        Assert.That(fileVersion, Does.Match(@"^\d+\.\d+\.\d+\.\d+$"),
                    "the MSI binds ProductVersion off this, and Windows Installer parses " +
                    "nothing but four numeric fields");
    }

    /// <summary>
    /// The first three fields are the same number in both, so the MSI and the assembly agree
    /// about which release this is. Only the fourth may differ - that is the build number.
    /// </summary>
    [Test]
    public void TheFileVersionAndTheAssemblyVersionAgreeOnTheFirstThreeFields()
    {
        Version assemblyVersion = AssemblyVersion();
        var fileVersion = new Version(FileVersion());

        Assert.Multiple(() =>
        {
            Assert.That(fileVersion.Major, Is.EqualTo(assemblyVersion.Major));
            Assert.That(fileVersion.Minor, Is.EqualTo(assemblyVersion.Minor));
            Assert.That(fileVersion.Build, Is.EqualTo(assemblyVersion.Build));
        });
    }

    [Test]
    public void TheInformationalVersionCarriesTheFileVersionAndTheCommit()
    {
        string? informational = Application
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        string fileVersion = FileVersion();

        Assert.That(informational, Is.Not.Null.And.Not.Empty);
        Assert.That(informational, Does.Match(@"^\d+\.\d+\.\d+\.\d+\+g[0-9a-f]+(\.dirty)?$"),
                    $"the informational version is '{informational}'. It is expected to read " +
                    "<file version>+g<commit>, optionally suffixed .dirty - that is what the " +
                    "About dialog trims and what the log keeps.");
        Assert.That(informational, Does.StartWith(fileVersion + "+"));
    }

    /// <summary>
    /// Nothing in the tree may pin the version any more.
    /// </summary>
    /// <remarks>
    /// This is the regression guard for the arrangement that was replaced: the version used to
    /// be written down in both <c>Directory.Build.props</c> and <c>AssemblyInfo.cs</c>, only
    /// one of the two reached the binary, and a bump that edited the wrong one shipped the old
    /// number in silence. Neither file carries a version now, and a hand-written attribute
    /// reappearing would take precedence over the computed one without failing anything else.
    /// </remarks>
    [Test]
    public void NoAssemblyInfoFileDeclaresAVersionByHand()
    {
        string repositoryRoot = TestRepositoryRoot();
        var offenders = new System.Collections.Generic.List<string>();

        foreach (string file in System.IO.Directory.GetFiles(
                     System.IO.Path.Combine(repositoryRoot, "src"),
                     "AssemblyInfo.cs",
                     System.IO.SearchOption.AllDirectories))
        {
            string text = System.IO.File.ReadAllText(file);
            if (Regex.IsMatch(text, @"(?m)^\[assembly: Assembly(File)?Version\("))
            {
                offenders.Add(file);
            }
        }

        Assert.That(offenders, Is.Empty,
                    "a hand-written version attribute is back. It wins over the one the build " +
                    "computes from the git tags, so the binary would stop matching the tag it " +
                    "was built from: " + string.Join(", ", offenders));
    }

    private static Version AssemblyVersion() =>
        Application.GetName().Version
        ?? throw new AssertionException("the application assembly carries no AssemblyVersion.");

    private static string FileVersion() =>
        Application.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version
        ?? throw new AssertionException("the application assembly carries no AssemblyFileVersion.");

    private static string TestRepositoryRoot(
        [System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "")
    {
        // This file sits at <root>\src\mRemoteUG.Tests\VersioningTests.cs.
        string directory = System.IO.Path.GetDirectoryName(sourceFilePath)
                           ?? throw new AssertionException($"{sourceFilePath} has no directory.");
        return System.IO.Path.GetFullPath(System.IO.Path.Combine(directory, "..", ".."));
    }
}
