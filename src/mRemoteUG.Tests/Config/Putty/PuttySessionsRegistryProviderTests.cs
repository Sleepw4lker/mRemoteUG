using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime;

using mRemoteUG.Config.Putty;
using mRemoteUG.Connection;
using mRemoteUG.Connection.Protocol;
using mRemoteUG.Tests.TestHelpers;

using NUnit.Framework;

namespace mRemoteUG.Tests.Config.Putty;

/// <summary>
/// How one read of PuTTY's registry turns into session names and <see cref="PuttySessionInfo"/>s.
/// </summary>
/// <remarks>
/// Every test reads a scratch key (<see cref="PuttyRegistryScratch"/>), never the user's real
/// PuTTY sessions.
/// </remarks>
[TestFixture]
[SuppressMessage("Design", "CA1001",
    Justification = "[TearDown] disposes _scratch after every test. "
        + "NUnit would dispose an IDisposable fixture once, after its last test.")]
public class PuttySessionsRegistryProviderTests
{
    // Rebuilt for every test in Setup: each test gets a scratch key of its own.
    private PuttyRegistryScratch _scratch = null!;
    private PuttySessionsRegistryProvider _provider = null!;

    /// <summary>How many times a disposal test reads the registry.</summary>
    private const int Reads = 200;

    [SetUp]
    public void Setup()
    {
        _scratch = new PuttyRegistryScratch();
        _provider = _scratch.CreateProvider();
    }

    [TearDown]
    public void TearDown()
    {
        _scratch.Dispose();
    }

    [Test]
    public void ReadsPuttysOwnKeyByDefault()
    {
        Assert.That(new PuttySessionsRegistryProvider().SessionsKeyPath,
                    Is.EqualTo(@"Software\SimonTatham\PuTTY\Sessions"));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void GetSessionNamesIsEmptyWithoutASessionsKey(bool raw)
    {
        Assert.That(_provider.GetSessionNames(raw), Is.Empty);
    }

    [TestCase(true, "Default%20Settings")]
    [TestCase(false, "Default Settings")]
    public void GetSessionNamesPutsDefaultSettingsFirstEvenWhenPuttyHasNotSavedIt(bool raw, string expected)
    {
        _scratch.AddSession("alpha");

        Assert.That(_provider.GetSessionNames(raw), Is.EqualTo(new[] { expected, "alpha" }));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void GetSessionNamesIsJustDefaultSettingsForAnEmptySessionsKey(bool raw)
    {
        _scratch.CreateSessionsKey();

        Assert.That(_provider.GetSessionNames(raw),
                    Is.EqualTo(new[] { raw ? "Default%20Settings" : "Default Settings" }));
    }

    [TestCase(true, "Default%20Settings")]
    [TestCase(false, "Default Settings")]
    public void GetSessionNamesListsSavedDefaultSettingsOnce(bool raw, string expected)
    {
        _scratch.AddSession("alpha");
        _scratch.AddSession("Default%20Settings");

        Assert.That(_provider.GetSessionNames(raw).Count(name => name == expected), Is.EqualTo(1));
    }

    [TestCase("My%20Session", "My Session")]
    [TestCase("100%25", "100%")]
    [TestCase("a+b", "a+b")]
    [TestCase("caf%E9", "caf\u00e9")]
    [TestCase("back%5Cslash", "back\\slash")]
    [TestCase("plain", "plain")]
    public void GetSessionNamesDecodesTheNamesPuttyStores(string stored, string expected)
    {
        _scratch.AddSession(stored);

        Assert.Multiple(() =>
        {
            Assert.That(_provider.GetSessionNames(), Does.Contain(expected));
            Assert.That(_provider.GetSessionNames(raw: true), Does.Contain(stored));
        });
    }

    [TestCase(null)]
    [TestCase("")]
    public void GetSessionIsNullWithoutAName(string name)
    {
        _scratch.AddSession("alpha", ("HostName", "alpha.example"));

        Assert.That(_provider.GetSession(name), Is.Null);
    }

    [Test]
    public void GetSessionIsNullWithoutASessionsKey()
    {
        Assert.That(_provider.GetSession("alpha"), Is.Null);
    }

    [Test]
    public void GetSessionIsNullForASessionThatIsNotSaved()
    {
        _scratch.AddSession("alpha", ("HostName", "alpha.example"));

        Assert.That(_provider.GetSession("beta"), Is.Null);
    }

    [Test]
    public void GetSessionNamesClosesTheKeysItOpens()
    {
        _scratch.AddSession("alpha");

        Assert.That(HandlesLeftOpenBy(() => _provider.GetSessionNames()), Is.LessThan(Reads / 2));
    }

    [Test]
    public void GetSessionClosesTheKeysItOpens()
    {
        _scratch.AddSession("s", ("HostName", "h"), ("Protocol", "ssh"), ("SshProt", 3));

        Assert.That(HandlesLeftOpenBy(() => _provider.GetSession("s")), Is.LessThan(Reads / 2));
    }

    [Test]
    public void GetSessionClosesTheSessionsKeyWhenTheSessionIsMissing()
    {
        _scratch.AddSession("alpha");

        Assert.That(HandlesLeftOpenBy(() => _provider.GetSession("missing")), Is.LessThan(Reads / 2));
    }

    [TestCase("serial")]
    [TestCase("unknown")]
    public void GetSessionClosesTheKeysItOpensForAnUnsupportedProtocol(string puttyProtocol)
    {
        _scratch.AddSession("s", ("HostName", "h"), ("Protocol", puttyProtocol));

        Assert.That(HandlesLeftOpenBy(() => _provider.GetSession("s")), Is.LessThan(Reads / 2));
    }

    [Test]
    public void GetSessionTakesOverHostAndUserNameAndDecodesTheName()
    {
        _scratch.AddSession("My%20Session", ("HostName", "host.example"), ("UserName", "admin"),
                            ("Protocol", "ssh"), ("SshProt", 3), ("PortNumber", 2222));

        PuttySessionInfo session = ReadSession("My%20Session");

        Assert.Multiple(() =>
        {
            Assert.That(session.Name, Is.EqualTo("My Session"));
            Assert.That(session.PuttySession, Is.EqualTo("My Session"));
            Assert.That(session.Hostname, Is.EqualTo("host.example"));
            Assert.That(session.Username, Is.EqualTo("admin"));
            Assert.That(session.Protocol, Is.EqualTo(ProtocolType.SSH2));
            Assert.That(session.Port, Is.EqualTo(2222));
        });
    }

    [Test]
    public void GetSessionKeepsAPlusSignInTheName()
    {
        _scratch.AddSession("a+b", ("HostName", "host.example"));

        Assert.That(ReadSession("a+b").Name, Is.EqualTo("a+b"));
    }

    [Test]
    public void GetSessionFillsMissingHostAndUserNameWithEmptyStrings()
    {
        _scratch.AddSession("bare", ("Protocol", "telnet"));

        PuttySessionInfo session = ReadSession("bare");

        Assert.Multiple(() =>
        {
            Assert.That(session.Hostname, Is.EqualTo(""));
            Assert.That(session.Username, Is.EqualTo(""));
        });
    }

    [TestCase("raw", ProtocolType.RAW)]
    [TestCase("rlogin", ProtocolType.Rlogin)]
    [TestCase("telnet", ProtocolType.Telnet)]
    [TestCase("Telnet", ProtocolType.Telnet)]
    [TestCase("RLOGIN", ProtocolType.Rlogin)]
    public void GetSessionMapsPuttysProtocolName(string puttyProtocol, ProtocolType expected)
    {
        _scratch.AddSession("s", ("HostName", "h"), ("Protocol", puttyProtocol));

        Assert.That(ReadSession("s").Protocol, Is.EqualTo(expected));
    }

    /// <summary>
    /// PuTTY's <c>SshProt</c>: 0 SSH-1 only, 1 SSH-1 with SSH-2 fallback, 2 SSH-2 with SSH-1
    /// fallback, 3 SSH-2 only.
    /// </summary>
    [TestCase(0, ProtocolType.SSH1)]
    [TestCase(1, ProtocolType.SSH1)]
    [TestCase(2, ProtocolType.SSH2)]
    [TestCase(3, ProtocolType.SSH2)]
    [TestCase(7, ProtocolType.SSH2)]
    public void GetSessionMapsTheSshVersion(int sshProt, ProtocolType expected)
    {
        _scratch.AddSession("s", ("HostName", "h"), ("Protocol", "ssh"), ("SshProt", sshProt));

        Assert.That(ReadSession("s").Protocol, Is.EqualTo(expected));
    }

    [TestCase(null, ProtocolType.SSH2)]
    [TestCase("", ProtocolType.SSH2)]
    [TestCase("abc", ProtocolType.SSH2)]
    [TestCase(0, ProtocolType.SSH1)]
    [TestCase(1, ProtocolType.SSH1)]
    [TestCase(2, ProtocolType.SSH2)]
    [TestCase(3, ProtocolType.SSH2)]
    [TestCase(4, ProtocolType.SSH2)]
    public void GetSessionMapsASshVersionThatIsMissingEmptyOrNotANumber(object? sshProt, ProtocolType expected)
    {
        if (sshProt == null)
        {
            _scratch.AddSession("s", ("HostName", "h"), ("Protocol", "ssh"));
        }
        else
        {
            _scratch.AddSession("s", ("HostName", "h"), ("Protocol", "ssh"), ("SshProt", sshProt));
        }

        Assert.That(ReadSession("s").Protocol, Is.EqualTo(expected));
    }

    [TestCase("")]
    [TestCase(null)]
    public void GetSessionTreatsAMissingProtocolAsSsh(string? protocol)
    {
        if (protocol == null)
        {
            _scratch.AddSession("s", ("HostName", "h"), ("SshProt", 3));
        }
        else
        {
            _scratch.AddSession("s", ("HostName", "h"), ("Protocol", protocol), ("SshProt", 3));
        }

        Assert.That(ReadSession("s").Protocol, Is.EqualTo(ProtocolType.SSH2));
    }

    [TestCase("serial")]
    [TestCase("supdup")]
    [TestCase("bare-ssh")]
    public void GetSessionSkipsProtocolsThisApplicationCannotOpen(string puttyProtocol)
    {
        _scratch.AddSession("s", ("HostName", "h"), ("Protocol", puttyProtocol));

        Assert.That(_provider.GetSession("s"), Is.Null);
    }

    [TestCase("ssh", 22)]
    [TestCase("telnet", 23)]
    [TestCase("rlogin", 513)]
    [TestCase("raw", 23)]
    public void GetSessionUsesTheProtocolsDefaultPortWithoutAPortNumber(string puttyProtocol, int expected)
    {
        _scratch.AddSession("s", ("HostName", "h"), ("Protocol", puttyProtocol), ("SshProt", 3));

        Assert.That(ReadSession("s").Port, Is.EqualTo(expected));
    }

    [TestCase("telnet", 23)]
    [TestCase("rlogin", 513)]
    public void GetSessionUsesTheProtocolsDefaultPortForPortNumberZero(string puttyProtocol, int expected)
    {
        _scratch.AddSession("s", ("HostName", "h"), ("Protocol", puttyProtocol), ("PortNumber", 0));

        Assert.That(ReadSession("s").Port, Is.EqualTo(expected));
    }

    [TestCase("ssh", 2222)]
    [TestCase("telnet", 2323)]
    [TestCase("raw", 1)]
    public void GetSessionTakesOverACustomPort(string puttyProtocol, int port)
    {
        _scratch.AddSession("s", ("HostName", "h"), ("Protocol", puttyProtocol), ("SshProt", 3),
                            ("PortNumber", port));

        Assert.That(ReadSession("s").Port, Is.EqualTo(port));
    }

    [Test]
    public void GetSessionReadsSavedDefaultSettingsLikeAnyOtherSession()
    {
        _scratch.AddSession("Default%20Settings", ("Protocol", "ssh"), ("SshProt", 3), ("PortNumber", 22));

        PuttySessionInfo session = ReadSession("Default%20Settings");

        Assert.Multiple(() =>
        {
            Assert.That(session.Name, Is.EqualTo("Default Settings"));
            Assert.That(session.Hostname, Is.EqualTo(""));
            Assert.That(session.Protocol, Is.EqualTo(ProtocolType.SSH2));
        });
    }

    /// <summary>
    /// How many more handles the process holds after <paramref name="read"/> has run
    /// <see cref="Reads"/> times.
    /// </summary>
    /// <remarks>
    /// An undisposed <see cref="Microsoft.Win32.RegistryKey"/> keeps its handle until the
    /// finaliser closes it, so the garbage collector is kept out of the loop: a collection in the
    /// middle would close some of the leaked handles and hide the leak. A leak shows as one or
    /// two handles per read, hundreds in all; a few handles opened elsewhere in the process in
    /// the meantime stay far below <see cref="Reads"/> / 2.
    /// </remarks>
    private static int HandlesLeftOpenBy(Action read)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        read();
        int before = ProcessHandleCount();

        bool noGcRegion = GC.TryStartNoGCRegion(16 * 1024 * 1024);
        try
        {
            for (int i = 0; i < Reads; i++)
            {
                read();
            }

            return ProcessHandleCount() - before;
        }
        finally
        {
            if (noGcRegion && GCSettings.LatencyMode == GCLatencyMode.NoGCRegion)
            {
                GC.EndNoGCRegion();
            }
        }
    }

    private static int ProcessHandleCount()
    {
        using var process = Process.GetCurrentProcess();
        return process.HandleCount;
    }

    /// <summary>
    /// The session the provider reads for <paramref name="sessionName"/>, which the test expects to exist.
    /// </summary>
    private PuttySessionInfo ReadSession(string sessionName)
    {
        return _provider.GetSession(sessionName)
               ?? throw new AssertionException($"no session was read for {sessionName}");
    }
}
