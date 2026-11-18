using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

using NUnit.Framework;

namespace mRemoteUG.Tests;

/// <summary>
/// Checks namespace casing, which the naming rules in <c>.editorconfig</c> no longer do.
/// </summary>
/// <remarks>
/// The root namespace <c>mRemoteUG</c> breaks the PascalCase rule, and renaming it would move
/// the section <c>user.config</c> keeps the settings under, so every existing user would lose
/// theirs. That is an exception in <c>docs/conventions/csharp.md</c>. A naming rule cannot name
/// one namespace, so the exception takes namespaces out of IDE1006 altogether; this test puts
/// back the part of the rule the exception does not need to switch off.
/// </remarks>
[TestFixture]
public class NamespaceNamingTests
{
    private static readonly string[] s_sourceRoots = { "src", "Tools" };

    private const string Root = "mRemoteUG";

    [Test]
    public void EveryNamespaceSegmentBelowTheRootIsPascalCase()
    {
        string[] offenders = SourceFiles()
                             .SelectMany(file => Namespaces(file).Select(ns => (file, ns)))
                             .Where(x => !IsCompliant(x.ns))
                             .Select(x => $"{x.ns} ({Path.GetFileName(x.file)})")
                             .Distinct()
                             .Order(StringComparer.Ordinal)
                             .ToArray();

        Assert.That(offenders, Is.Empty,
                    "Namespace segments must begin with an upper-case letter; only the root " +
                    $"'{Root}' is exempt: {string.Join(", ", offenders)}");
    }

    [Test]
    public void TheScanFindsTheNamespaces()
    {
        // Guards the test above against passing because it read nothing.
        string[] namespaces = SourceFiles().SelectMany(Namespaces).ToArray();

        Assert.That(namespaces, Has.Some.EqualTo("mRemoteUG.App"));
        Assert.That(namespaces, Has.Some.EqualTo("mRemoteUG.Tests"));
        Assert.That(namespaces, Has.Some.StartsWith("mRemoteUG.Installer"));
        Assert.That(namespaces, Has.Some.EqualTo("IconGen"));
    }

    private static bool IsCompliant(string ns)
    {
        string[] segments = ns.Split('.');
        return segments.Select((segment, i) => i == 0 && segment == Root || char.IsUpper(segment[0])).All(ok => ok);
    }

    private static IEnumerable<string> Namespaces(string file)
    {
        return File.ReadLines(file)
                   .Select(line => Regex.Match(line, @"^\s*namespace\s+([\w.]+)"))
                   .Where(m => m.Success)
                   .Select(m => m.Groups[1].Value);
    }

    /// <summary>Every C# file in the repository's <c>src</c> and <c>Tools</c>, build output excluded.</summary>
    private static string[] SourceFiles([CallerFilePath] string sourceFilePath = "")
    {
        string testsDirectory = Path.GetDirectoryName(sourceFilePath)
                             ?? throw new InvalidOperationException("Could not locate the test source.");
        string repository = Path.GetFullPath(Path.Combine(testsDirectory, "..", ".."));

        Assert.That(Directory.Exists(Path.Combine(repository, "src", Root)), Is.True,
                    $"Could not find the repository at {repository}.");

        return s_sourceRoots
               .SelectMany(dir => Directory.GetFiles(Path.Combine(repository, dir), "*.cs",
                                                     SearchOption.AllDirectories))
               .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
               .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
               .ToArray();
    }
}
