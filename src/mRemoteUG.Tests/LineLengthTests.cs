using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

using NUnit.Framework;

namespace mRemoteUG.Tests;

/// <summary>
/// Checks the 120-character line length that <c>max_line_length</c> in <c>.editorconfig</c> sets.
/// </summary>
/// <remarks>
/// No analyzer enforces <c>max_line_length</c>; editors only draw a guide. Generated code
/// (<c>*.Designer.cs</c>, <c>*.g.cs</c>, the files <c>.editorconfig</c> marks as
/// <c>generated_code</c>) is left out, as the conventions apply to hand-written code only. So is a
/// comment line that holds nothing but a URL, perhaps numbered: a URL cannot be wrapped.
/// </remarks>
[TestFixture]
public class LineLengthTests
{
    private static readonly string[] s_sourceRoots = { "src", "Tools" };

    private const int MaxLineLength = 120;

    [Test]
    public void NoHandWrittenLineIsLongerThanTheLimit()
    {
        string[] offenders = SourceFiles()
                             .SelectMany(file => File.ReadLines(file)
                                                     .Select((line, i) => (file, number: i + 1, line)))
                             .Where(x => x.line.Length > MaxLineLength && !IsUrlOnly(x.line))
                             .Select(x => $"{Path.GetFileName(x.file)}:{x.number} ({x.line.Length})")
                             .ToArray();

        Assert.That(offenders, Is.Empty,
                    $"Lines longer than {MaxLineLength} characters: {string.Join(", ", offenders)}");
    }

    [Test]
    public void TheScanFindsTheHandWrittenFilesOnly()
    {
        // Guards the test above against passing because it read nothing, or the wrong files.
        string[] names = SourceFiles().Select(file => Path.GetFileName(file)).ToArray();

        Assert.That(names, Has.Member("LineLengthTests.cs"));
        Assert.That(names, Has.Member("FrmMain.cs"));
        Assert.That(names, Has.Member("DesktopRuntimeChecker.cs"));
        Assert.That(names, Has.Member("Emit.cs"));
        Assert.That(names, Has.None.EndsWith(".Designer.cs").IgnoreCase);
        Assert.That(names, Has.None.EndsWith(".g.cs"));
    }

    private static bool IsUrlOnly(string line)
    {
        return Regex.IsMatch(line, @"^\s*//\s*(\d+\.\s+)?https?://\S+$");
    }

    private static bool IsGenerated(string file)
    {
        return file.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase)
               || file.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Every hand-written C# file in the repository's <c>src</c> and <c>Tools</c>.</summary>
    private static string[] SourceFiles([CallerFilePath] string sourceFilePath = "")
    {
        string testsDirectory = Path.GetDirectoryName(sourceFilePath)
                             ?? throw new InvalidOperationException("Could not locate the test source.");
        string repository = Path.GetFullPath(Path.Combine(testsDirectory, "..", ".."));

        Assert.That(Directory.Exists(Path.Combine(repository, "src", "mRemoteUG")), Is.True,
                    $"Could not find the repository at {repository}.");

        string obj = $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}";
        string bin = $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}";
        return s_sourceRoots
               .SelectMany(dir => Directory.GetFiles(Path.Combine(repository, dir), "*.cs",
                                                     SearchOption.AllDirectories))
               .Where(f => !f.Contains(obj, StringComparison.Ordinal) && !f.Contains(bin, StringComparison.Ordinal))
               .Where(f => !IsGenerated(f))
               .ToArray();
    }
}
