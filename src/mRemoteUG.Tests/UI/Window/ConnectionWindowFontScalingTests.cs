using System.Diagnostics.CodeAnalysis;

using mRemoteUG.Connection;
using mRemoteUG.Connection.Protocol.RDP;
using mRemoteUG.UI.Window;

using NUnit.Framework;

namespace mRemoteUG.Tests.UI.Window;

/// <summary>
/// A connection panel's tabs, and the session inside them, keep fitting the panel when the
/// main window's font changes - which is what a DPI change does to it.
/// </summary>
/// <remarks>
/// Measured on a dock from a 150% panel to a 200% 3840x2160 monitor: the connection window
/// was 3575x1971, but its tab page was 4428x2266 and the RDP session was sized to match, so
/// the remote desktop was cut off at the right and bottom. The DPI follow-up moves the main
/// window's font from 9pt to 12pt; <see cref="ConnectionWindow"/> scales by font, and its tab
/// control and the <see cref="InterfaceControl"/> were placed by anchors, so the font change
/// scaled them past the window they sit in and nothing brought them back.
/// <para>
/// A font change at a fixed DPI drives the same autoscale, so this needs no per-monitor DPI
/// awareness and runs on the CI runner. Both directions are covered: going down, the same
/// mechanism leaves a session at about three quarters of its panel.
/// </para>
/// </remarks>
[TestFixture]
[Apartment(ApartmentState.STA)]
[SuppressMessage("Design", "CA1001",
    Justification = "[TearDown] disposes _host after every test. "
        + "NUnit would dispose an IDisposable fixture once, after its last test.")]
public class ConnectionWindowFontScalingTests
{
    private Form _host = null!;

    [TearDown]
    public void TearDown()
    {
        _host?.Dispose();
    }

    [TestCase(9f, 12f)]  // docking from 150% to 200%
    [TestCase(12f, 9f)]  // undocking from 200% to 150%
    public void TheTabsAndTheSessionPanelStillFitTheWindowAfterAFontChange(float fromPoints, float toPoints)
    {
        _host = new Form
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-32000, -32000),
            ClientSize = new Size(1200, 800),
            // Keeps its own size through the font change, as the maximised main window did on
            // the measured dock; the font still reaches the connection window as its ambient font.
            AutoScaleMode = AutoScaleMode.None,
            Font = new Font("Segoe UI", fromPoints),
            Opacity = 0,
            ShowInTaskbar = false,
        };

        // Embedded the way MainLayout embeds every connection panel.
        var window = new ConnectionWindow("font scaling")
        {
            TopLevel = false,
            FormBorderStyle = FormBorderStyle.None,
            Dock = DockStyle.Fill,
        };
        _host.Controls.Add(window);
        _host.Show();
        window.Show();

        var info = new ConnectionInfo { Name = "server1", Hostname = "127.0.0.2" };
        TabPage tabPage = window.AddConnectionTab(info)
                          ?? throw new AssertionException("AddConnectionTab returned no tab page.");
        var interfaceControl = new InterfaceControl(tabPage, new RdpProtocol(), info);
        Pump();

        _host.Font = new Font("Segoe UI", toPoints);
        Pump();

        TabControl tabs = window.TabController;
        Assert.Multiple(() =>
        {
            Assert.That(tabs.Left, Is.EqualTo(0), "The tab control has moved off the window's left edge.");
            Assert.That(tabs.Width, Is.EqualTo(window.ClientSize.Width),
                        $"The tab control is {tabs.Width} wide in a window {window.ClientSize.Width} wide.");
            Assert.That(tabs.Bottom, Is.EqualTo(window.ClientSize.Height),
                        $"The tab control ends at {tabs.Bottom} in a window {window.ClientSize.Height} high.");
            Assert.That(interfaceControl.Size, Is.EqualTo(tabPage.ClientSize),
                        "The session panel no longer fills its tab page, so the session is sized to " +
                        "something other than what is on screen.");
        });
    }

    private static void Pump()
    {
        for (int i = 0; i < 20; i++)
        {
            Application.DoEvents();
            Thread.Sleep(5);
        }
    }
}
