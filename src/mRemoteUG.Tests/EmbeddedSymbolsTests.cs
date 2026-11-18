using System.Reflection.PortableExecutable;

using mRemoteUG.Messages.MessageWriters;

using NUnit.Framework;

namespace mRemoteUG.Tests;

/// <summary>
/// The application's symbols travel inside mRemoteUG.dll, so a logged stack trace names the line.
/// </summary>
/// <remarks>
/// ADR-0041. <c>DebugType</c> is <c>embedded</c> for the application project: the portable PDB is
/// part of the assembly, and no <c>mRemoteUG.pdb</c> is written for the installer's harvest to
/// pack. These run against the copy of the application assembly beside the tests, which is the
/// same build as <c>src\mRemoteUG\bin\&lt;Configuration&gt;</c> - under <c>-c Release</c>, the
/// assembly the package ships.
/// <para>
/// The two tests between them catch either way back. <c>none</c> leaves the stack trace without
/// line numbers; <c>portable</c> keeps them, but only by writing <c>mRemoteUG.pdb</c> next to the
/// assembly again, which the first test refuses and the second sees as a missing debug directory
/// entry.
/// </para>
/// </remarks>
[TestFixture]
public class EmbeddedSymbolsTests
{
    private static string ApplicationAssembly => typeof(TextLogMessageWriter).Assembly.Location;

    [Test]
    public void AnExceptionThrownInTheApplicationNamesItsLine()
    {
        string beside = Path.ChangeExtension(ApplicationAssembly, ".pdb");
        Assert.That(File.Exists(beside), Is.False,
                    $"{beside} exists, so line numbers below could come from it rather than from " +
                    "the assembly. DebugType is no longer embedded - or this is stale output from " +
                    "an earlier build, which a clean build removes.");

        // The null is the point: it is how the test gets an exception thrown inside mRemoteUG.dll.
        ArgumentNullException? exception =
            Assert.Throws<ArgumentNullException>(() => _ = new TextLogMessageWriter(null!));

        string? frame = exception?.StackTrace?
            .Split('\n')
            .FirstOrDefault(line => line.Contains("TextLogMessageWriter..ctor", StringComparison.Ordinal));
        Assert.That(frame, Is.Not.Null, $"the constructor is not on the stack:{Environment.NewLine}" +
                                        exception?.StackTrace);
        Assert.That(frame, Does.Contain(":line "),
                    "the frame thrown inside mRemoteUG.dll carries no line number, so the symbols " +
                    "are not embedded and every stack trace CrashLogger writes loses its lines");
    }

    [Test]
    public void TheApplicationAssemblyCarriesItsPortablePdb()
    {
        using FileStream stream = File.OpenRead(ApplicationAssembly);
        using var reader = new PEReader(stream);

        DebugDirectoryEntryType[] entries = reader.ReadDebugDirectory().Select(entry => entry.Type).ToArray();

        Assert.That(entries, Does.Contain(DebugDirectoryEntryType.EmbeddedPortablePdb),
                    $"{ApplicationAssembly} has no embedded portable PDB (debug directory: " +
                    $"{string.Join(", ", entries)})");
    }
}
