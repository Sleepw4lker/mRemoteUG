using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using mRemoteUG.Connection;
using mRemoteUG.Connection.Protocol;
using mRemoteUG.Messages;
using mRemoteUG.Tests.TestHelpers;

using NUnit.Framework;

namespace mRemoteUG.Tests.Connection;

[TestFixture]
public class ConnectionsServiceQuickConnectTests
{
    private bool _identifyTabs;
    private bool _loadFromCustomLocation;
    // Snapshots taken by [SetUp] before each test and put back by [TearDown], so null! rather
    // than an initializer here; see ADR-0022 on when that is the right answer.
    private string _customPath = null!;
    private string _defaultPanel = null!;

    [SetUp]
    public void Setup()
    {
        _identifyTabs = Settings.Default.IdentifyQuickConnectTabs;
        _loadFromCustomLocation = Settings.Default.LoadConsFromCustomLocation;
        _customPath = Settings.Default.CustomConsPath;
        _defaultPanel = DefaultConnectionInfo.Instance.Panel;
    }

    [TearDown]
    public void TearDown()
    {
        Settings.Default.IdentifyQuickConnectTabs = _identifyTabs;
        Settings.Default.LoadConsFromCustomLocation = _loadFromCustomLocation;
        Settings.Default.CustomConsPath = _customPath;
        DefaultConnectionInfo.Instance.Panel = _defaultPanel;
    }

    [Test]
    public void ABareHostGetsTheProtocolsDefaultPort()
    {
        ConnectionInfo connection = QuickConnect("server", ProtocolType.RDP);

        Assert.That(connection.Hostname, Is.EqualTo("server"));
        Assert.That(connection.Port, Is.EqualTo(3389));
        Assert.That(connection.Protocol, Is.EqualTo(ProtocolType.RDP));
        Assert.That(connection.IsQuickConnect, Is.True);
    }

    [TestCase(ProtocolType.SSH2, 22)]
    [TestCase(ProtocolType.SSH1, 22)]
    [TestCase(ProtocolType.Telnet, 23)]
    public void TheDefaultPortFollowsTheChosenProtocol(ProtocolType protocol, int port)
    {
        ConnectionInfo connection = QuickConnect("server", protocol);

        Assert.That(connection.Protocol, Is.EqualTo(protocol));
        Assert.That(connection.Port, Is.EqualTo(port));
    }

    [TestCase("server:4000", "server", 4000)]
    [TestCase("server.example.com:1", "server.example.com", 1)]
    [TestCase("192.0.2.10:3390", "192.0.2.10", 3390)]
    [TestCase("[2001:db8::1]:3390", "[2001:db8::1]", 3390)]
    public void AnExplicitPortIsUsed(string text, string host, int port)
    {
        ConnectionInfo connection = QuickConnect(text, ProtocolType.RDP);

        Assert.That(connection.Hostname, Is.EqualTo(host));
        Assert.That(connection.Port, Is.EqualTo(port));
    }

    [Test]
    public void TheHostIsLowercasedByTheUriParser()
    {
        ConnectionInfo connection = QuickConnect("SERVER", ProtocolType.RDP);

        Assert.That(connection.Hostname, Is.EqualTo("server"));
    }

    [TestCase("")]
    [TestCase(":3389")]
    public void NoHostGivesNoConnection(string text)
    {
        using var recorder = new MessageRecorder();

        Assert.That(ConnectionsService.CreateQuickConnect(text, ProtocolType.RDP), Is.Null);
    }

    [Test]
    public void ATextThatIsNoHostIsReportedAndGivesNoConnection()
    {
        using var recorder = new MessageRecorder();

        ConnectionInfo? connection = ConnectionsService.CreateQuickConnect("server:notaport", ProtocolType.RDP);

        Assert.That(connection, Is.Null);
        Assert.That(recorder.Messages.Any(m => m.Class == MessageClass.ErrorMsg), Is.True);
    }

    [Test]
    public void TheNameIsTheHostWhenQuickConnectTabsAreNotIdentified()
    {
        Settings.Default.IdentifyQuickConnectTabs = false;

        ConnectionInfo connection = QuickConnect("server:4000", ProtocolType.RDP);

        Assert.That(connection.Name, Is.EqualTo("server"));
    }

    [Test]
    [SuppressMessage("Performance", "CA1863",
                     Justification = "The format is a localized resource read at run time; "
                                     + "a cached CompositeFormat would freeze the UI language.")]
    public void TheNameIsMarkedWhenQuickConnectTabsAreIdentified()
    {
        Settings.Default.IdentifyQuickConnectTabs = true;

        ConnectionInfo connection = QuickConnect("server:4000", ProtocolType.RDP);

        string expected = string.Format(CultureInfo.CurrentCulture, Language.strQuick, "server");
        Assert.That(connection.Name, Is.EqualTo(expected));
        Assert.That(connection.Name, Does.Contain("server"));
        Assert.That(connection.Name, Is.Not.EqualTo("server"));
    }

    [Test]
    public void AnEmptyPanelFallsBackToGeneral()
    {
        DefaultConnectionInfo.Instance.Panel = "";

        ConnectionInfo connection = QuickConnect("server", ProtocolType.RDP);

        Assert.That(connection.Panel, Is.EqualTo(Language.strGeneral));
    }

    [Test]
    public void ANullPanelFallsBackToGeneral()
    {
        // The setter coerces null to empty; passing it anyway is what this test is about.
        DefaultConnectionInfo.Instance.Panel = null!;

        ConnectionInfo connection = QuickConnect("server", ProtocolType.RDP);

        Assert.That(connection.Panel, Is.EqualTo(Language.strGeneral));
    }

    [Test]
    public void ThePanelOfTheDefaultConnectionIsKept()
    {
        DefaultConnectionInfo.Instance.Panel = "Favourites";

        ConnectionInfo connection = QuickConnect("server", ProtocolType.RDP);

        Assert.That(connection.Panel, Is.EqualTo("Favourites"));
    }

    [Test]
    public void TheOtherSettingsComeFromTheDefaultConnection()
    {
        string originalDescription = DefaultConnectionInfo.Instance.Description;
        try
        {
            DefaultConnectionInfo.Instance.Description = "from the defaults";

            ConnectionInfo connection = QuickConnect("server", ProtocolType.RDP);

            Assert.That(connection.Description, Is.EqualTo("from the defaults"));
        }
        finally
        {
            DefaultConnectionInfo.Instance.Description = originalDescription;
        }
    }

    [Test]
    public void TheStartupFileIsTheCustomOneWhenThatIsSwitchedOn()
    {
        Settings.Default.LoadConsFromCustomLocation = true;
        Settings.Default.CustomConsPath = @"C:\somewhere\custom.xml";

        Assert.That(ConnectionsService.GetStartupConnectionFileName(), Is.EqualTo(@"C:\somewhere\custom.xml"));
    }

    [Test]
    public void TheStartupFileIsTheDefaultOneWhenNoCustomLocationIsSet()
    {
        Settings.Default.LoadConsFromCustomLocation = false;
        Settings.Default.CustomConsPath = @"C:\somewhere\custom.xml";

        Assert.That(
            ConnectionsService.GetStartupConnectionFileName(),
            Is.EqualTo(ConnectionsService.GetDefaultStartupConnectionFileName()));
        Assert.That(ConnectionsService.GetStartupConnectionFileName(), Is.Not.EqualTo(@"C:\somewhere\custom.xml"));
    }

    /// <summary>A quick connection the test expects to be made; failing the test if it is not.</summary>
    private static ConnectionInfo QuickConnect(string text, ProtocolType protocol) =>
        ConnectionsService.CreateQuickConnect(text, protocol)
        ?? throw new AssertionException($"\"{text}\" gave no connection");
}
