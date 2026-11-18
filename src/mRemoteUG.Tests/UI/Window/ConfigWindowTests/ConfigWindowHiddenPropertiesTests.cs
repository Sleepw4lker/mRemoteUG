using mRemoteUG.Connection;
using mRemoteUG.Connection.Protocol;
using mRemoteUG.Connection.Protocol.RDP;
using mRemoteUG.Container;
using mRemoteUG.Tree.Root;
using mRemoteUG.UI.Window;

using NUnit.Framework;

namespace mRemoteUG.Tests.UI.Window.ConfigWindowTests;

/// <summary>
/// Which properties the Config window's grid hides, per selected object, protocol, RDP setting
/// and inheritance flag, asked of <see cref="ConfigWindowRules.HiddenProperties"/> without a form.
/// </summary>
/// <remarks>
/// Compared as sets: the rules can name a property twice, and the grid hides it either way.
/// </remarks>
[TestFixture]
internal sealed class ConfigWindowHiddenPropertiesTests
{
    /// <summary>The RDP settings that only mean something on RDP, which the other protocols hide.</summary>
    private static readonly string[] s_rdpOnly =
    {
        "CacheBitmaps", "Colors", "DisplayThemes", "DisplayWallpaper", "EnableFontSmoothing",
        "EnableDesktopComposition", "Domain", "RDGatewayDomain", "RDGatewayHostname", "RDGatewayPassword",
        "RDGatewayUsageMethod", "RDGatewayUseConnectionCredentials", "RDGatewayUsername",
        "RDPAuthenticationLevel", "RDPMinutesToIdleTimeout", "RDPAlertIdleTimeout", "LoadBalanceInfo",
        "RedirectDiskDrives", "RedirectKeys", "RedirectPorts", "RedirectPrinters", "RedirectSmartCards",
        "RedirectClipboard", "RedirectSound", "Resolution", "AutomaticResize", "UseConsoleSession",
        "UseCredSsp", "SoundQuality",
    };

    /// <summary>What RDP hides: only the PuTTY session.</summary>
    private static readonly string[] s_rdpHides = { "PuttySession" };

    /// <summary>The credentials Telnet, Rlogin and RAW hide on top of the RDP settings.</summary>
    private static readonly string[] s_credentials = { "Username", "Password" };

    /// <summary>What a folder hides on RDP.</summary>
    private static readonly string[] s_folderHidesOnRdp = { "PuttySession", "Hostname" };

    /// <summary>What the default connection hides on top of its protocol's list.</summary>
    private static readonly string[] s_defaultConnectionHides = { "Hostname", "Name" };

    /// <summary>A root shows its name and its password protection, and nothing of a connection.</summary>
    private static readonly string[] s_rootHides = s_rdpOnly
        .Concat(new[]
        {
            "Icon", "Panel", "Hostname", "Username", "Protocol", "Port", "PuttySession", "MacAddress",
            "UserField", "Description",
        })
        .ToArray();

    [Test]
    public void AConnectionsRootHidesEverythingButItsNameAndPassword()
    {
        string[] hidden = ConfigWindowRules.HiddenProperties(new RootNodeInfo(RootNodeType.Connection));

        Assert.That(hidden.Distinct(), Is.EquivalentTo(s_rootHides));
    }

    [Test]
    public void ThePuttySessionsRootHidesItsPasswordToo()
    {
        string[] hidden = ConfigWindowRules.HiddenProperties(new RootNodeInfo(RootNodeType.PuttySessions));

        Assert.That(hidden.Distinct(), Is.EquivalentTo(s_rootHides.Append("Password")));
    }

    private static IEnumerable<TestCaseData> ProtocolCases()
    {
        yield return new TestCaseData(ProtocolType.RDP, s_rdpHides).SetName("RDP");
        foreach (ProtocolType ssh in new[] { ProtocolType.SSH1, ProtocolType.SSH2 })
        {
            yield return new TestCaseData(ssh, s_rdpOnly).SetName(ssh.ToString());
        }

        foreach (ProtocolType raw in new[] { ProtocolType.Telnet, ProtocolType.Rlogin, ProtocolType.RAW })
        {
            yield return new TestCaseData(raw, s_rdpOnly.Concat(s_credentials).ToArray())
                .SetName(raw.ToString());
        }

        // The protocols this build no longer has are kept only so old files parse; nothing is
        // hidden for them by protocol.
        foreach (ProtocolType removed in Enum.GetValues<ProtocolType>().Where(p => !ProtocolTypes.IsSupported(p)))
        {
            yield return new TestCaseData(removed, Array.Empty<string>()).SetName("Removed protocol " + (int)removed);
        }
    }

    [TestCaseSource(nameof(ProtocolCases))]
    public void EachProtocolHidesWhatDoesNotApplyToIt(ProtocolType protocol, string[] expected)
    {
        ConnectionInfo connection = ConnectionShowingEveryRdpSetting(protocol);

        Assert.That(ConfigWindowRules.HiddenProperties(connection).Distinct(), Is.EquivalentTo(expected));
    }

    [TestCase(0, true)]
    [TestCase(-1, true)]
    [TestCase(1, false)]
    public void TheIdleAlertIsHiddenWithoutAnIdleTimeout(int minutes, bool hidden)
    {
        ConnectionInfo connection = ConnectionShowingEveryRdpSetting(ProtocolType.RDP);
        connection.RDPMinutesToIdleTimeout = minutes;

        AssertHiddenBeyondRdpBaseline(connection, OnlyIf(hidden, "RDPAlertIdleTimeout"));
    }

    private static IEnumerable<TestCaseData> GatewayCases()
    {
        string[] allGateway =
        {
            "RDGatewayDomain", "RDGatewayHostname", "RDGatewayPassword", "RDGatewayUseConnectionCredentials",
            "RDGatewayUsername",
        };
        string[] gatewayCredentials = { "RDGatewayDomain", "RDGatewayPassword", "RDGatewayUsername" };

        foreach (RdpProtocol.RDGatewayUsageMethod method in Enum.GetValues<RdpProtocol.RDGatewayUsageMethod>())
        {
            foreach (RdpProtocol.RDGatewayUseConnectionCredentials credentials in
                     Enum.GetValues<RdpProtocol.RDGatewayUseConnectionCredentials>())
            {
                string[] expected = method == RdpProtocol.RDGatewayUsageMethod.Never
                    ? allGateway
                    : credentials == RdpProtocol.RDGatewayUseConnectionCredentials.Yes
                        ? gatewayCredentials
                        : Array.Empty<string>();
                yield return new TestCaseData(method, credentials, expected)
                    .SetName($"Gateway {method}, credentials {credentials}");
            }
        }
    }

    [TestCaseSource(nameof(GatewayCases))]
    public void TheGatewaySettingsShowOnlyWhenAGatewayIsUsed(
        RdpProtocol.RDGatewayUsageMethod method,
        RdpProtocol.RDGatewayUseConnectionCredentials credentials,
        string[] expected)
    {
        ConnectionInfo connection = ConnectionShowingEveryRdpSetting(ProtocolType.RDP);
        connection.RDGatewayUsageMethod = method;
        connection.RDGatewayUseConnectionCredentials = credentials;

        AssertHiddenBeyondRdpBaseline(connection, expected);
    }

    private static IEnumerable<TestCaseData> ResolutionCases() =>
        Enum.GetValues<RdpProtocol.RDPResolutions>().Select(resolution => new TestCaseData(
            resolution,
            resolution != RdpProtocol.RDPResolutions.FitToWindow
            && resolution != RdpProtocol.RDPResolutions.Fullscreen));

    [TestCaseSource(nameof(ResolutionCases))]
    public void AutomaticResizeShowsOnlyForAResolutionThatFollowsTheWindow(
        RdpProtocol.RDPResolutions resolution,
        bool hidden)
    {
        ConnectionInfo connection = ConnectionShowingEveryRdpSetting(ProtocolType.RDP);
        connection.Resolution = resolution;

        AssertHiddenBeyondRdpBaseline(connection, OnlyIf(hidden, "AutomaticResize"));
    }

    private static IEnumerable<TestCaseData> SoundCases() =>
        Enum.GetValues<RdpProtocol.RDPSounds>().Select(sound => new TestCaseData(
            sound,
            sound != RdpProtocol.RDPSounds.BringToThisComputer));

    [TestCaseSource(nameof(SoundCases))]
    public void SoundQualityShowsOnlyWhenTheSoundIsBroughtToThisComputer(RdpProtocol.RDPSounds sound, bool hidden)
    {
        ConnectionInfo connection = ConnectionShowingEveryRdpSetting(ProtocolType.RDP);
        connection.RedirectSound = sound;

        AssertHiddenBeyondRdpBaseline(connection, OnlyIf(hidden, "SoundQuality"));
    }

    /// <summary>
    /// Every inheritance flag there is, on RDP and on SSH, so a flag added later is covered without
    /// touching this file: what is inherited is not edited here, so its property is hidden.
    /// </summary>
    private static IEnumerable<TestCaseData> InheritanceFlagCases()
    {
        foreach (string flag in new ConnectionInfoInheritance(null, true).GetProperties().Select(p => p.Name))
        {
            foreach (ProtocolType protocol in new[] { ProtocolType.RDP, ProtocolType.SSH2 })
            {
                yield return new TestCaseData(flag, protocol).SetName($"{flag} inherited, {protocol}");
            }
        }
    }

    [TestCaseSource(nameof(InheritanceFlagCases))]
    public void AnInheritedPropertyIsHidden(string flag, ProtocolType protocol)
    {
        ConnectionInfo connection = ConnectionShowingEveryRdpSetting(protocol);
        string[] withoutInheritance = ConfigWindowRules.HiddenProperties(connection);
        (typeof(ConnectionInfoInheritance).GetProperty(flag)
         ?? throw new AssertionException($"no inheritance flag named {flag}")).SetValue(connection.Inheritance, true);

        string[] hidden = ConfigWindowRules.HiddenProperties(connection);

        Assert.That(hidden.Distinct(), Is.EquivalentTo(withoutInheritance.Append(flag).Distinct()));
    }

    [Test]
    public void AFolderHidesTheHostName()
    {
        var folder = new ContainerInfo();
        ShowEveryRdpSetting(folder, ProtocolType.RDP);

        Assert.That(
            ConfigWindowRules.HiddenProperties(folder).Distinct(),
            Is.EquivalentTo(s_folderHidesOnRdp));
    }

    [Test]
    public void TheDefaultConnectionHidesByProtocolAndItsNameAndHostNameButNothingInherited()
    {
        // The default connection's inheritance is the default inheritance, whatever it holds: the
        // flags are not looked at. A plain connection with the same settings and nothing
        // inherited gives the protocol's part.
        var plain = new ConnectionInfo { Protocol = DefaultConnectionInfo.Instance.Protocol };
        CopyRdpSettings(DefaultConnectionInfo.Instance, plain);
        plain.Inheritance.TurnOffInheritanceCompletely();

        string[] expected = ConfigWindowRules.HiddenProperties(plain)
            .Concat(s_defaultConnectionHides)
            .Distinct()
            .ToArray();

        Assert.That(
            ConfigWindowRules.HiddenProperties(DefaultConnectionInfo.Instance).Distinct(),
            Is.EquivalentTo(expected));
    }

    private static IEnumerable<TestCaseData> NotAConnectionCases()
    {
        yield return new TestCaseData(null).SetName("Nothing selected");
        yield return new TestCaseData(new ConnectionInfo().Inheritance).SetName("Inheritance shown");
        yield return new TestCaseData(DefaultConnectionInheritance.Instance).SetName("Default inheritance shown");
    }

    [TestCaseSource(nameof(NotAConnectionCases))]
    public void NothingIsHiddenForAnythingButAConnection(object? selectedObject)
    {
        Assert.That(ConfigWindowRules.HiddenProperties(selectedObject), Is.Empty);
    }

    private static string[] OnlyIf(bool hidden, string property) =>
        hidden ? new[] { property } : Array.Empty<string>();

    private static void AssertHiddenBeyondRdpBaseline(ConnectionInfo connection, string[] expected)
    {
        Assert.That(
            ConfigWindowRules.HiddenProperties(connection).Distinct(),
            Is.EquivalentTo(expected.Append("PuttySession")));
    }

    /// <summary>
    /// A connection with no parent, nothing inherited, and every RDP setting that can be hidden
    /// switched to the value that shows it.
    /// </summary>
    private static ConnectionInfo ConnectionShowingEveryRdpSetting(ProtocolType protocol)
    {
        var connection = new ConnectionInfo();
        ShowEveryRdpSetting(connection, protocol);
        return connection;
    }

    private static void ShowEveryRdpSetting(ConnectionInfo connection, ProtocolType protocol)
    {
        connection.Inheritance.TurnOffInheritanceCompletely();
        connection.Protocol = protocol;
        connection.RDPMinutesToIdleTimeout = 1;
        connection.RDGatewayUsageMethod = RdpProtocol.RDGatewayUsageMethod.Always;
        connection.RDGatewayUseConnectionCredentials = RdpProtocol.RDGatewayUseConnectionCredentials.No;
        connection.Resolution = RdpProtocol.RDPResolutions.FitToWindow;
        connection.RedirectSound = RdpProtocol.RDPSounds.BringToThisComputer;
    }

    private static void CopyRdpSettings(ConnectionInfo from, ConnectionInfo to)
    {
        to.RDPMinutesToIdleTimeout = from.RDPMinutesToIdleTimeout;
        to.RDGatewayUsageMethod = from.RDGatewayUsageMethod;
        to.RDGatewayUseConnectionCredentials = from.RDGatewayUseConnectionCredentials;
        to.Resolution = from.Resolution;
        to.RedirectSound = from.RedirectSound;
    }
}
