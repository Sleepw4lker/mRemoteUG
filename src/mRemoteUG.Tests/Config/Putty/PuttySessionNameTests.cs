using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;

using mRemoteUG.Config.Putty;
using mRemoteUG.Tests.TestHelpers;

using NUnit.Framework;

namespace mRemoteUG.Tests.Config.Putty;

/// <summary>
/// How the registry key name PuTTY stores a session under becomes the session's name, on the add
/// and the remove side of a refresh alike.
/// </summary>
/// <remarks>Reads a scratch key (<see cref="PuttyRegistryScratch"/>), never the real PuTTY key.</remarks>
[TestFixture]
[SuppressMessage("Design", "CA1001",
    Justification = "[TearDown] disposes _scratch after every test. "
        + "NUnit would dispose an IDisposable fixture once, after its last test.")]
public class PuttySessionNameTests
{
    // Rebuilt for every test in Setup: each test gets a scratch key of its own.
    private PuttyRegistryScratch _scratch = null!;
    private PuttySessionsRegistryProvider _provider = null!;
    private List<NotifyCollectionChangedEventArgs> _changes = null!;

    [SetUp]
    public void Setup()
    {
        _scratch = new PuttyRegistryScratch();
        _provider = _scratch.CreateProvider();
        _changes = new List<NotifyCollectionChangedEventArgs>();
        _provider.PuttySessionsCollectionChanged += (_, args) => _changes.Add(args);
    }

    [TearDown]
    public void TearDown()
    {
        _scratch.Dispose();
    }

    [TestCase("a+b", "a+b")]
    [TestCase("a%20b", "a b")]
    [TestCase("100%25", "100%")]
    [TestCase("a%2Bb", "a+b")]
    [TestCase("M%E4rz", "M\u00E4rz")]
    [TestCase("a+b%20c%2B+", "a+b c++")]
    [TestCase("a%2", "a%2")]
    [TestCase("a%zzb", "a%zzb")]
    [TestCase("a%", "a%")]
    [TestCase("plain", "plain")]
    public void DecodeReadsOnlyPercentEscapes(string stored, string expected)
    {
        Assert.That(PuttySessionName.Decode(stored), Is.EqualTo(expected));
    }

    [TestCase(1252, "%80", "\u20AC")]
    [TestCase(1252, "M%E4rz", "M\u00E4rz")]
    [TestCase(1251, "%CF%F0%E8%E2%E5%F2", "\u041F\u0440\u0438\u0432\u0435\u0442")]
    [TestCase(932, "%93%FA%96%7B", "\u65E5\u672C")]
    [TestCase(932, "%93%FA%96%7B%20%82%A0", "\u65E5\u672C \u3042")]
    [TestCase(1252, "a+b%20c%2B%25", "a+b c+%")]
    [TestCase(932, "a+b%20c%2B%25", "a+b c+%")]
    public void DecodeReadsEscapedBytesInTheGivenCodePage(int codePage, string stored, string expected)
    {
        Encoding encoding = CodePage(codePage);

        Assert.That(PuttySessionName.Decode(stored, encoding), Is.EqualTo(expected));
    }

    [Test]
    public void ALoneLeadByteDecodesToTheCodePagesFallbackCharacterAndDoesNotThrow()
    {
        Encoding shiftJis = CodePage(932);

        Assert.That(PuttySessionName.Decode("a%81", shiftJis), Is.EqualTo("a\u30FB"));
    }

    [Test]
    public void TheProductionDecodingUsesTheMachinesAnsiCodePage()
    {
        Assert.Multiple(() =>
        {
            Assert.That(PuttySessionName.AnsiEncoding.CodePage, Is.EqualTo((int)GetACP()));
            Assert.That(PuttySessionName.Decode("%41"), Is.EqualTo("A"));
            byte[] bytes = PuttySessionName.AnsiEncoding.GetBytes("A");
            Assert.That(bytes, Is.EqualTo(new byte[] { 0x41 }));
        });
    }

    [DllImport("kernel32.dll")]
    private static extern uint GetACP();

    [TestCase("a+b", "a+b")]
    [TestCase("web+db", "web+db")]
    [TestCase("a%20b", "a b")]
    [TestCase("100%25", "100%")]
    [TestCase("a%2Bb", "a+b")]
    [TestCase("M%E4rz", "M\u00E4rz")]
    [TestCase("a+b%20c", "a+b c")]
    public void ASessionIsShownUnderItsDecodedNameAndStaysThroughASecondRead(string stored, string expected)
    {
        _scratch.AddSession(stored, ("HostName", "host.example"), ("Protocol", "ssh"), ("SshProt", 3));

        var first = _provider.GetSessions().ToList();
        var second = _provider.GetSessions().ToList();

        Assert.Multiple(() =>
        {
            Assert.That(first.Select(s => s.Name).ToArray(), Is.EqualTo(new[] { expected }));
            Assert.That(first.Single().PuttySession, Is.EqualTo(expected));
            Assert.That(second, Is.EqualTo(first));
            Assert.That(_changes.Select(c => c.Action), Is.EqualTo(new[] { NotifyCollectionChangedAction.Add }));
        });
    }

    [TestCase("a+b", "a+b")]
    [TestCase("a%2Bb", "a+b")]
    [TestCase("a%20b", "a b")]
    public void GetSessionNamesDecodesTheSameWayAsTheRefresh(string stored, string expected)
    {
        _scratch.AddSession(stored, ("HostName", "host.example"));

        Assert.That(_provider.GetSessionNames(), Does.Contain(expected));
        Assert.That(_provider.GetSessionNames(raw: true), Does.Contain(stored));
    }

    private static Encoding CodePage(int codePage)
    {
        return CodePagesEncodingProvider.Instance.GetEncoding(codePage)
               ?? throw new AssertionException($"code page {codePage} is not available");
    }
}
