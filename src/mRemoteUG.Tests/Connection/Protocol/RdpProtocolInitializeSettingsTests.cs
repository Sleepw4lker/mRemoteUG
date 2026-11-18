using System.Diagnostics.CodeAnalysis;

using mRemoteUG.Connection;
using mRemoteUG.Connection.Protocol;
using mRemoteUG.Connection.Protocol.RDP;
using mRemoteUG.Security;
using mRemoteUG.UI.Window;

using MSTSCLib;

using NUnit.Framework;

namespace mRemoteUG.Tests.Connection.Protocol;

/// <summary>
/// What <see cref="RdpProtocol.Initialize"/> writes onto the RDP ActiveX control.
/// </summary>
/// <remarks>
/// Each test hosts a real control, runs <c>Initialize()</c> and reads the property back from the
/// OCX, the way <c>RdpProtocol</c> itself reaches it. Nothing connects: the hostname is never
/// dialled, because <c>Connect()</c> is never called.
/// <para>
/// Two values cannot be read back. <c>ClearTextPassword</c> is write-only, and <c>FullScreen</c>
/// reads false until a session exists, so a full-screen session is recognised by its desktop size
/// alone. <c>GetPassword</c> and its two siblings are therefore tested directly as well.
/// </para>
/// <para>
/// The credential tests write <c>Settings.Default.EmptyCredentials</c>, <c>DefaultUsername</c>,
/// <c>DefaultDomain</c> and <c>DefaultPassword</c>, so all four are put back afterwards: other
/// fixtures share the same settings instance.
/// </para>
/// </remarks>
[TestFixture]
[Apartment(ApartmentState.STA)]
[NonParallelizable]
[SuppressMessage("Design", "CA1001",
    Justification = "[TearDown] disposes _window (and _interfaceControl, on its tab page) after every test. "
        + "NUnit would dispose an IDisposable fixture once, after its last test.")]
public class RdpProtocolInitializeSettingsTests
{
    // Rebuilt or snapshotted by [SetUp] before each test, so null! rather than an initializer
    // here; see ADR-0022 on when that is the right answer.
    private ConnectionWindow _window = null!;
    private string _originalEmptyCredentials = null!;
    private string _originalDefaultUsername = null!;
    private string _originalDefaultDomain = null!;
    private string _originalDefaultPassword = null!;

    // Only a test that hosts a session sets these.
    private RdpProtocol? _protocol;
    private InterfaceControl? _interfaceControl;

    [SetUp]
    public void Setup()
    {
        _originalEmptyCredentials = Settings.Default.EmptyCredentials;
        _originalDefaultUsername = Settings.Default.DefaultUsername;
        _originalDefaultDomain = Settings.Default.DefaultDomain;
        _originalDefaultPassword = Settings.Default.DefaultPassword;

        // "noinfo": an empty name stays empty, so the read-back shows only what the connection
        // itself carries. The tests of the other two modes set their own.
        Settings.Default.EmptyCredentials = "noinfo";

        _window = new ConnectionWindow("initialize settings panel");
        _window.Show();
    }

    [TearDown]
    public void TearDown()
    {
        try { _protocol?.Close(); } catch (Exception) { /* the test may never have hosted one */ }
        Pump();
        DeferredControlDisposal.ResetForTests();
        _protocol = null;
        _interfaceControl = null;
        _window?.Dispose();

        Settings.Default.EmptyCredentials = _originalEmptyCredentials;
        Settings.Default.DefaultUsername = _originalDefaultUsername;
        Settings.Default.DefaultDomain = _originalDefaultDomain;
        Settings.Default.DefaultPassword = _originalDefaultPassword;
    }

    #region Not user changeable

    [Test]
    public void FocusIsGrabbedOnConnectAndAutomaticReconnectIsOn()
    {
        MsRdpClient11NotSafeForScripting client = Initialize(new ConnectionInfo());

        Assert.Multiple(() =>
        {
            Assert.That(client.AdvancedSettings2.GrabFocusOnConnect, Is.True);
            Assert.That(client.AdvancedSettings3.EnableAutoReconnect, Is.True);
        });
    }

    #endregion

    #region Credentials

    [Test]
    public void TheConnectionsUserNameAndDomainAreWrittenToTheControl()
    {
        MsRdpClient11NotSafeForScripting client = Initialize(
            new ConnectionInfo { Username = "alice", Domain = "CONTOSO" });

        Assert.Multiple(() =>
        {
            Assert.That(client.UserName, Is.EqualTo("alice"));
            Assert.That(client.Domain, Is.EqualTo("CONTOSO"));
        });
    }

    [Test]
    public void NoCredentialsLeavesUserNameAndDomainUnset()
    {
        MsRdpClient11NotSafeForScripting client = Initialize(
            new ConnectionInfo { Username = "alice", Domain = "CONTOSO" },
            ConnectionInfo.Force.NoCredentials);

        Assert.Multiple(() =>
        {
            Assert.That(client.UserName, Is.Empty);
            Assert.That(client.Domain, Is.Empty);
        });
    }

    [Test]
    public void AnEmptyUserNameAndDomainTakeTheWindowsAccountWhenSetToWindows()
    {
        Settings.Default.EmptyCredentials = "windows";

        MsRdpClient11NotSafeForScripting client = Initialize(new ConnectionInfo { Username = "", Domain = "" });

        Assert.Multiple(() =>
        {
            Assert.That(client.UserName, Is.EqualTo(Environment.UserName));
            Assert.That(client.Domain, Is.EqualTo(Environment.UserDomainName));
        });
    }

    [Test]
    public void AnEmptyUserNameAndDomainTakeTheDefaultsWhenSetToCustom()
    {
        Settings.Default.EmptyCredentials = "custom";
        Settings.Default.DefaultUsername = "fallback-user";
        Settings.Default.DefaultDomain = "FALLBACK";

        MsRdpClient11NotSafeForScripting client = Initialize(new ConnectionInfo { Username = "", Domain = "" });

        Assert.Multiple(() =>
        {
            Assert.That(client.UserName, Is.EqualTo("fallback-user"));
            Assert.That(client.Domain, Is.EqualTo("FALLBACK"));
        });
    }

    [TestCase("windows")]
    [TestCase("custom")]
    [TestCase("noinfo")]
    public void AUserNameAndDomainThatAreSetAreKept(string emptyCredentials)
    {
        Settings.Default.EmptyCredentials = emptyCredentials;
        Settings.Default.DefaultUsername = "fallback-user";
        Settings.Default.DefaultDomain = "FALLBACK";

        Assert.Multiple(() =>
        {
            Assert.That(RdpProtocol.GetUserName("alice"), Is.EqualTo("alice"));
            Assert.That(RdpProtocol.GetDomain("CONTOSO"), Is.EqualTo("CONTOSO"));
        });
    }

    [TestCase(null)]
    [TestCase("")]
    public void AMissingUserNameAndDomainStayEmptyWhenSetToNoInfo(string missing)
    {
        Settings.Default.EmptyCredentials = "noinfo";
        Settings.Default.DefaultUsername = "fallback-user";
        Settings.Default.DefaultDomain = "FALLBACK";

        Assert.Multiple(() =>
        {
            Assert.That(RdpProtocol.GetUserName(missing), Is.EqualTo(missing));
            Assert.That(RdpProtocol.GetDomain(missing), Is.EqualTo(missing));
        });
    }

    [TestCase(null)]
    [TestCase("")]
    public void AMissingUserNameAndDomainTakeTheWindowsAccountWhenSetToWindows(string missing)
    {
        Settings.Default.EmptyCredentials = "windows";

        Assert.Multiple(() =>
        {
            Assert.That(RdpProtocol.GetUserName(missing), Is.EqualTo(Environment.UserName));
            Assert.That(RdpProtocol.GetDomain(missing), Is.EqualTo(Environment.UserDomainName));
        });
    }

    [TestCase(null)]
    [TestCase("")]
    public void AMissingUserNameAndDomainTakeTheDefaultsWhenSetToCustom(string missing)
    {
        Settings.Default.EmptyCredentials = "custom";
        Settings.Default.DefaultUsername = "fallback-user";
        Settings.Default.DefaultDomain = "FALLBACK";

        Assert.Multiple(() =>
        {
            Assert.That(RdpProtocol.GetUserName(missing), Is.EqualTo("fallback-user"));
            Assert.That(RdpProtocol.GetDomain(missing), Is.EqualTo("FALLBACK"));
        });
    }

    [TestCase(null)]
    [TestCase("")]
    public void AMissingPasswordTakesTheDefaultWhenSetToCustom(string missing)
    {
        Settings.Default.EmptyCredentials = "custom";
        DefaultCredentials.Password = "fallback-secret";

        Assert.That(RdpProtocol.GetPassword(missing, "alice", "CONTOSO", "server1"),
                    Is.EqualTo("fallback-secret"));
    }

    [TestCase("windows")]
    [TestCase("noinfo")]
    public void AMissingPasswordStaysEmptyUnlessSetToCustom(string emptyCredentials)
    {
        Settings.Default.EmptyCredentials = emptyCredentials;
        DefaultCredentials.Password = "fallback-secret";

        Assert.That(RdpProtocol.GetPassword("", "alice", "CONTOSO", "server1"), Is.Empty);
    }

    [Test]
    public void AMissingPasswordStaysEmptyWhenNoDefaultIsStored()
    {
        Settings.Default.EmptyCredentials = "custom";
        Settings.Default.DefaultPassword = "";

        Assert.That(RdpProtocol.GetPassword("", "alice", "CONTOSO", "server1"), Is.Empty);
    }

    [TestCase("windows")]
    [TestCase("custom")]
    [TestCase("noinfo")]
    public void APasswordThatIsSetIsKept(string emptyCredentials)
    {
        Settings.Default.EmptyCredentials = emptyCredentials;
        DefaultCredentials.Password = "fallback-secret";

        Assert.That(RdpProtocol.GetPassword("secret", "alice", "CONTOSO", "server1"), Is.EqualTo("secret"));
    }

    #endregion

    #region Resolution

    [Test]
    public void AFixedResolutionIsTheDesktopSize()
    {
        MsRdpClient11NotSafeForScripting client = Initialize(
            new ConnectionInfo { Resolution = RdpProtocol.RDPResolutions.Res1152x864 });

        Assert.Multiple(() =>
        {
            Assert.That(client.DesktopWidth, Is.EqualTo(1152));
            Assert.That(client.DesktopHeight, Is.EqualTo(864));
            Assert.That(client.AdvancedSettings2.SmartSizing, Is.False);
        });
    }

    [Test]
    public void FitToWindowSizesTheDesktopToThePanelWithoutSmartSizing()
    {
        MsRdpClient11NotSafeForScripting client = Initialize(
            new ConnectionInfo { Resolution = RdpProtocol.RDPResolutions.FitToWindow });

        Assert.Multiple(() =>
        {
            Assert.That(client.DesktopWidth, Is.EqualTo(_interfaceControl.Size.Width));
            Assert.That(client.DesktopHeight, Is.EqualTo(_interfaceControl.Size.Height));
            Assert.That(client.AdvancedSettings2.SmartSizing, Is.False);
        });
    }

    [Test]
    public void SmartSizeSizesTheDesktopToThePanelAndTurnsSmartSizingOn()
    {
        MsRdpClient11NotSafeForScripting client = Initialize(
            new ConnectionInfo { Resolution = RdpProtocol.RDPResolutions.SmartSize });

        Assert.Multiple(() =>
        {
            Assert.That(client.DesktopWidth, Is.EqualTo(_interfaceControl.Size.Width));
            Assert.That(client.DesktopHeight, Is.EqualTo(_interfaceControl.Size.Height));
            Assert.That(client.AdvancedSettings2.SmartSizing, Is.True);
        });
    }

    [Test]
    public void AFullscreenResolutionFillsTheScreen()
    {
        MsRdpClient11NotSafeForScripting client = Initialize(
            new ConnectionInfo { Resolution = RdpProtocol.RDPResolutions.Fullscreen });
        Rectangle screen = Screen.FromControl(RdpControl()).Bounds;

        Assert.Multiple(() =>
        {
            Assert.That(client.DesktopWidth, Is.EqualTo(screen.Width));
            Assert.That(client.DesktopHeight, Is.EqualTo(screen.Height));
        });
    }

    [Test]
    public void ForcingFullscreenOverridesAFixedResolution()
    {
        MsRdpClient11NotSafeForScripting client = Initialize(
            new ConnectionInfo { Resolution = RdpProtocol.RDPResolutions.Res1152x864 },
            ConnectionInfo.Force.Fullscreen);
        Rectangle screen = Screen.FromControl(RdpControl()).Bounds;

        Assert.Multiple(() =>
        {
            Assert.That(client.DesktopWidth, Is.EqualTo(screen.Width));
            Assert.That(client.DesktopHeight, Is.EqualTo(screen.Height));
        });
    }

    #endregion

    #region Console session, port, authentication, load balancing

    [TestCase(false, ConnectionInfo.Force.None, false)]
    [TestCase(true, ConnectionInfo.Force.None, true)]
    [TestCase(false, ConnectionInfo.Force.UseConsoleSession, true)]
    [TestCase(true, ConnectionInfo.Force.DontUseConsoleSession, false)]
    public void TheConsoleSessionFollowsTheConnectionUnlessForced(
        bool useConsoleSession, ConnectionInfo.Force force, bool expected)
    {
        MsRdpClient11NotSafeForScripting client = Initialize(
            new ConnectionInfo { UseConsoleSession = useConsoleSession }, force);

        Assert.That(client.AdvancedSettings7.ConnectToAdministerServer, Is.EqualTo(expected));
    }

    [Test]
    public void ANonDefaultPortIsWrittenToTheControl()
    {
        MsRdpClient11NotSafeForScripting client = Initialize(new ConnectionInfo { Port = 4000 });

        Assert.That(client.AdvancedSettings2.RDPPort, Is.EqualTo(4000));
    }

    [TestCase(RdpProtocol.AuthenticationLevel.AuthRequired)]
    [TestCase(RdpProtocol.AuthenticationLevel.WarnOnFailedAuth)]
    public void TheAuthenticationLevelIsWrittenToTheControl(RdpProtocol.AuthenticationLevel level)
    {
        MsRdpClient11NotSafeForScripting client = Initialize(new ConnectionInfo { RDPAuthenticationLevel = level });

        Assert.That(client.AdvancedSettings5.AuthenticationLevel, Is.EqualTo((uint)level));
    }

    [Test]
    public void LoadBalanceInfoIsWrittenToTheControl()
    {
        const string loadBalanceInfo = "tsv://MS Terminal Services Plugin.1.Sessions";

        MsRdpClient11NotSafeForScripting client = Initialize(new ConnectionInfo { LoadBalanceInfo = loadBalanceInfo });

        Assert.That(client.AdvancedSettings2.LoadBalanceInfo, Is.EqualTo(loadBalanceInfo));
    }

    #endregion

    #region Redirection

    [TestCase(true)]
    [TestCase(false)]
    public void EachRedirectionFollowsTheConnection(bool redirect)
    {
        RdpProtocol.RDPSounds sound = redirect
            ? RdpProtocol.RDPSounds.LeaveAtRemoteComputer
            : RdpProtocol.RDPSounds.DoNotPlay;

        MsRdpClient11NotSafeForScripting client = Initialize(new ConnectionInfo
        {
            RedirectDiskDrives = redirect,
            RedirectPorts = redirect,
            RedirectPrinters = redirect,
            RedirectSmartCards = redirect,
            RedirectClipboard = redirect,
            RedirectSound = sound
        });

        Assert.Multiple(() =>
        {
            Assert.That(client.AdvancedSettings2.RedirectDrives, Is.EqualTo(redirect), "drives");
            Assert.That(client.AdvancedSettings2.RedirectPorts, Is.EqualTo(redirect), "ports");
            Assert.That(client.AdvancedSettings2.RedirectPrinters, Is.EqualTo(redirect), "printers");
            Assert.That(client.AdvancedSettings2.RedirectSmartCards, Is.EqualTo(redirect), "smart cards");
            Assert.That(client.AdvancedSettings6.RedirectClipboard, Is.EqualTo(redirect), "clipboard");
            Assert.That(client.SecuredSettings2.AudioRedirectionMode, Is.EqualTo((int)sound), "sound");
        });
    }

    [Test]
    public void RedirectingKeysAppliesKeyCombinationsAtTheRemoteServer()
    {
        MsRdpClient11NotSafeForScripting client = Initialize(new ConnectionInfo { RedirectKeys = true });

        Assert.That(client.SecuredSettings2.KeyboardHookMode, Is.EqualTo(1));
    }

    [Test]
    public void NotRedirectingKeysLeavesTheKeyboardHookModeAlone()
    {
        MsRdpClient11NotSafeForScripting client = Initialize(new ConnectionInfo { RedirectKeys = false });

        Assert.That(client.SecuredSettings2.KeyboardHookMode, Is.Not.EqualTo(1));
    }

    #endregion

    #region Performance flags

    // TS_PERF_DISABLE_WALLPAPER 0x1, TS_PERF_DISABLE_THEMING 0x8,
    // TS_PERF_ENABLE_FONT_SMOOTHING 0x80, TS_PERF_ENABLE_DESKTOP_COMPOSITION 0x100.
    [TestCase(true, true, false, false, 0x0)]
    [TestCase(false, true, false, false, 0x8)]
    [TestCase(true, false, false, false, 0x1)]
    [TestCase(true, true, true, false, 0x80)]
    [TestCase(true, true, false, true, 0x100)]
    [TestCase(false, false, true, true, 0x189)]
    public void ThePerformanceFlagsAreTheSumOfTheConnectionsChoices(
        bool displayThemes, bool displayWallpaper, bool fontSmoothing, bool desktopComposition, int expected)
    {
        MsRdpClient11NotSafeForScripting client = Initialize(new ConnectionInfo
        {
            DisplayThemes = displayThemes,
            DisplayWallpaper = displayWallpaper,
            EnableFontSmoothing = fontSmoothing,
            EnableDesktopComposition = desktopComposition
        });

        Assert.That(client.AdvancedSettings2.PerformanceFlags, Is.EqualTo(expected));
    }

    #endregion

    #region Remote Desktop Gateway

    [TestCase(RdpProtocol.RDGatewayUsageMethod.Always)]
    [TestCase(RdpProtocol.RDGatewayUsageMethod.Detect)]
    public void AGatewayInUseIsWrittenToTheControl(RdpProtocol.RDGatewayUsageMethod usage)
    {
        MsRdpClient11NotSafeForScripting client = Initialize(new ConnectionInfo
        {
            RDGatewayUsageMethod = usage,
            RDGatewayHostname = "gateway.example.com"
        });

        Assume.That(client.TransportSettings.GatewayIsSupported, Is.Not.Zero,
                    "This RDP control supports no gateway, so there is nothing to read back.");
        Assert.Multiple(() =>
        {
            Assert.That(client.TransportSettings.GatewayUsageMethod, Is.EqualTo((uint)usage));
            Assert.That(client.TransportSettings.GatewayHostname, Is.EqualTo("gateway.example.com"));
        });
    }

    [Test]
    public void AGatewaySetToNeverIsNotWrittenToTheControl()
    {
        MsRdpClient11NotSafeForScripting client = Initialize(new ConnectionInfo
        {
            RDGatewayUsageMethod = RdpProtocol.RDGatewayUsageMethod.Never,
            RDGatewayHostname = "gateway.example.com"
        });

        Assert.Multiple(() =>
        {
            Assert.That(client.TransportSettings.GatewayUsageMethod, Is.Zero);
            Assert.That(client.TransportSettings.GatewayHostname, Is.Not.EqualTo("gateway.example.com"));
        });
    }

    #endregion

    /// <summary>
    /// Hosts an RDP session for <paramref name="connectionInfo"/>, runs <c>Initialize()</c> and
    /// returns the OCX it configured.
    /// </summary>
    [MemberNotNull(nameof(_protocol), nameof(_interfaceControl))]
    private MsRdpClient11NotSafeForScripting Initialize(
        ConnectionInfo connectionInfo, ConnectionInfo.Force force = ConnectionInfo.Force.None)
    {
        connectionInfo.Name = "server1";
        connectionInfo.Hostname = "127.0.0.2";
        TabPage tabPage = _window.AddConnectionTab(connectionInfo)
            ?? throw new AssertionException("AddConnectionTab returned no tab page.");

        _protocol = new RdpProtocol { Force = force };
        _interfaceControl = new InterfaceControl(tabPage, _protocol, connectionInfo);
        _protocol.InterfaceControl = _interfaceControl;

        Assert.That(_protocol.Initialize(), Is.True,
                    "RdpProtocol.Initialize() failed - no RDP ActiveX control could be created.");
        Pump();
        return (MsRdpClient11NotSafeForScripting?)RdpControl().GetOcx()
               ?? throw new AssertionException("The RDP control has no OCX.");
    }

    /// <summary>The RDP ActiveX control <c>Initialize()</c> hosted in the panel.</summary>
    private AxHost RdpControl() =>
        (_interfaceControl ?? throw new InvalidOperationException("No session has been hosted yet."))
        .Controls.OfType<AxHost>().Single();

    private static void Pump()
    {
        for (int i = 0; i < 40; i++)
        {
            Application.DoEvents();
            Thread.Sleep(5);
        }
    }
}
