using System.Diagnostics.CodeAnalysis;

using mRemoteUG.Connection;
using mRemoteUG.Connection.Protocol.RDP;
using mRemoteUG.UI.Window;

using NUnit.Framework;

namespace mRemoteUG.Tests.Connection.Protocol;

/// <summary>
/// A resize the session could not take is made good when it logs on, at the Tab's size now.
/// </summary>
/// <remarks>
/// Measured in the lab (#246): a laptop undocked from 200% to 150%, which also changed the
/// network, so the control reconnected by itself. Every resize during the reconnect was refused
/// with <c>E_UNEXPECTED</c> and forgotten. The logon after the reconnect then sent the size the
/// control reported, 3462x1915 - the docked size - at 150%, into a Tab of 1627x1011: scrollbars,
/// and the text stayed at 200%. Nothing corrected it until the laptop was docked again.
/// <para>
/// The sequence is replayed here against <see cref="RdpSessionStandIn"/>. The DPIs and the
/// reported size are the measured ones; the Tab is whatever size the test window gives it.
/// </para>
/// </remarks>
[TestFixture]
[Apartment(ApartmentState.STA)]
[SuppressMessage("Design", "CA1001",
    Justification = "[TearDown] disposes _window after every test. "
        + "NUnit would dispose an IDisposable fixture once, after its last test.")]
public class RdpResizeAcrossLogonTests
{
    private const int DockedDpi = 192;
    private const int UndockedDpi = 144;
    private const int DiscReasonNetworkLost = 2308;

    private ConnectionWindow _window = null!;
    private RdpSessionStandIn _session = null!;

    [TearDown]
    public void TearDown()
    {
        _window?.Dispose();
    }

    [Test]
    public void ALogonAfterResizesRefusedDuringAReconnectSendsTheTabSizeAtTheNewScale()
    {
        OpenASession();
        _session.LogOn();
        _session.ForgetCalls();

        UndockWhileTheControlReconnects();
        _session.RefuseForNow = false;
        _session.Raise("RDPEvent_OnAutoReconnected");
        _session.Raise("RDPEvent_OnLoginComplete");

        Assert.Multiple(() =>
        {
            Assert.That(_session.Taken, Is.EqualTo(new[] { TabSizeAt(UndockedDpi) }),
                        "The logon after the reconnect did not send the Tab's size at the new scale, " +
                        "as one request.");
            Assert.That(_session.Reconnects, Is.Zero, "A refusal for now was answered with a reconnect.");
        });

        _session.Resize();

        Assert.That(_session.Taken, Has.Count.EqualTo(1),
                    "A resize to the size and scale the session was just asked for went out again.");
    }

    [Test]
    public void ALogonSendsTheTabSizeNotTheSizeTheSessionWasCreatedAt()
    {
        OpenASession();
        _session.Dpi = UndockedDpi;
        _session.Resize();
        _session.Raise("RDPEvent_OnConnected");
        _session.ForgetCalls();

        _session.Raise("RDPEvent_OnLoginComplete");

        Assert.That(_session.Taken, Is.EqualTo(new[] { TabSizeAt(UndockedDpi) }),
                    "A resize dropped before logon was not made good when the logon completed.");
    }

    [Test]
    public void ADpiChangeAfterARefusedResizeSendsTheTabSizeNotTheReportedOne()
    {
        OpenASession();
        _session.LogOn();
        _session.RefuseForNow = true;
        _session.Dpi = UndockedDpi;
        _session.Resize();
        _session.ForgetCalls();
        _session.RefuseForNow = false;

        _session.NotifyDpiChanged(UndockedDpi);

        Assert.That(_session.Taken, Is.EqualTo(new[] { TabSizeAt(UndockedDpi) }),
                    "With the refused request forgotten, the DPI change sent the size the control " +
                    "reports, which lags behind.");
    }

    [Test]
    public void AFixedResolutionSessionKeepsItsResolutionAndTakesTheNewScale()
    {
        var fixedSize = new Size(1280, 1024);
        OpenASession(RdpProtocol.RDPResolutions.Res1280x1024);
        _session.Reported = fixedSize;
        _session.LogOn();
        _session.ForgetCalls();

        UndockWhileTheControlReconnects();
        _session.RefuseForNow = false;
        _session.Raise("RDPEvent_OnAutoReconnected");
        _session.Raise("RDPEvent_OnLoginComplete");

        Assert.Multiple(() =>
        {
            Assert.That(_session.Taken, Is.EqualTo(new[] { Layout(fixedSize, UndockedDpi) }),
                        "A fixed-resolution session should only have been told its new scale.");
            Assert.That(_session.Reconnects, Is.Zero);
        });
    }

    [Test]
    public void ARefusalForNowIsTriedOncePerEventAndNeverByReconnecting()
    {
        OpenASession();
        _session.LogOn();
        _session.ForgetCalls();

        UndockWhileTheControlReconnects();
        int refusedDuringTheReconnect = _session.Attempts.Count;
        _session.Raise("RDPEvent_OnAutoReconnected");
        _session.Raise("RDPEvent_OnLoginComplete");
        PumpFor(TimeSpan.FromMilliseconds(300));

        Assert.Multiple(() =>
        {
            Assert.That(refusedDuringTheReconnect, Is.EqualTo(4),
                        "Three resizes and a DPI change should each have made one attempt.");
            Assert.That(_session.Attempts, Has.Count.EqualTo(refusedDuringTheReconnect + 1),
                        "The logon should make one attempt, and nothing should retry it by itself.");
            Assert.That(_session.Taken, Is.Empty);
            Assert.That(_session.Reconnects, Is.Zero, "A refusal for now was answered with a reconnect.");
        });
    }

    /// <summary>The measured undock: the session drops, then every resize and the DPI change are refused.</summary>
    private void UndockWhileTheControlReconnects()
    {
        _session.Raise("RDPEvent_OnAutoReconnecting2", DiscReasonNetworkLost, false, 1, 20);
        _session.RefuseForNow = true;
        _session.Dpi = UndockedDpi;
        _session.Resize();
        _session.Resize();
        _session.Resize();
        _session.NotifyDpiChanged(UndockedDpi);
    }

    private void OpenASession(RdpProtocol.RDPResolutions resolution = RdpProtocol.RDPResolutions.FitToWindow)
    {
        _window = new ConnectionWindow("resize across logon panel");
        _window.Show();
        _window.ClientSize = new Size(900, 650);
        var info = new ConnectionInfo
        {
            Name = "server5",
            Hostname = "127.0.0.6",
            Resolution = resolution,
            AutomaticResize = true
        };
        TabPage tabPage = _window.AddConnectionTab(info)
            ?? throw new AssertionException("AddConnectionTab returned no tab page.");
        _session = new RdpSessionStandIn { Dpi = DockedDpi };
        _session.InterfaceControl = new InterfaceControl(tabPage, _session, info);
        Assert.That(_session.Initialize(), Is.True,
                    "RdpProtocol.Initialize() failed - no RDP ActiveX control could be created.");

        // Whatever the layout posted while the Tab was built runs now, not in the middle of a test.
        PumpFor(TimeSpan.FromMilliseconds(100));
        _session.ForgetCalls();

        Size tab = TabSize();
        Assume.That(Math.Min(tab.Width, tab.Height), Is.AtLeast(RdpDisplayScale.MinSessionDimension),
                    $"The test window gave the session a {tab.Width}x{tab.Height} Tab, too small for a layout.");
        Assume.That(tab, Is.Not.EqualTo(_session.Reported),
                    "The Tab happens to be the reported size, so these tests cannot tell them apart.");
    }

    private Size TabSize()
    {
        Control control = _session.HostedControl
                          ?? throw new AssertionException("Initialize left no hosted control.");
        return control.Size;
    }

    /// <summary>The Tab's size as the protocol accepts it: an odd width is rounded down.</summary>
    private SessionLayout TabSizeAt(int dpi)
    {
        Size tab = TabSize();
        return Layout(new Size(tab.Width - (tab.Width % 2), tab.Height), dpi);
    }

    private static SessionLayout Layout(Size size, int dpi) =>
        new(size, RdpDisplayScale.DesktopScaleFactor(dpi), RdpDisplayScale.DeviceScaleFactor(dpi));

    private static void PumpFor(TimeSpan duration)
    {
        DateTime until = DateTime.UtcNow + duration;
        while (DateTime.UtcNow < until)
        {
            Application.DoEvents();
            Thread.Sleep(10);
        }
    }
}
