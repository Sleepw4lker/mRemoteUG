using System.Diagnostics.CodeAnalysis;

using mRemoteUG.App;
using mRemoteUG.Connection;
using mRemoteUG.Connection.Protocol.RDP;
using mRemoteUG.UI.Window;

using NUnit.Framework;

namespace mRemoteUG.Tests.Connection.Protocol;

/// <summary>
/// Pins that the RDP ActiveX control keeps filling its <see cref="InterfaceControl"/> when the
/// window is resized.
/// </summary>
/// <remarks>
/// Once an ActiveX control is in place, .NET 10 ignores explicit bounds assignments on its
/// <c>AxHost</c>: <c>Size</c>, <c>Bounds</c>, <c>SetBounds</c> and <c>Width</c>/<c>Height</c>
/// all return without changing anything and without throwing. .NET Framework 4.8 honoured
/// every one of them. Only the layout engine still resizes the control.
///
/// <c>RdpProtocol</c> used to clear the control's anchor and drive its size by hand from
/// <c>DoResize()</c>, so on .NET 10 the control simply stopped resizing. The user-visible
/// symptom is that automatic RDP resizing stops - and silently, because <c>ResizeEnd</c> then
/// sees the size it captured at <c>ResizeBegin</c>, concludes nothing moved, and never reaches
/// any of the guards in <c>ReconnectForResize</c> that would have logged a reason.
///
/// This has to drive the real <see cref="RdpProtocol"/> against a real ActiveX control. A
/// plain <see cref="Panel"/> resizes happily either way and would pass against the bug.
/// </remarks>
[TestFixture]
[Apartment(ApartmentState.STA)]
[SuppressMessage("Design", "CA1001",
    Justification = "[TearDown] disposes _window after every test. "
        + "NUnit would dispose an IDisposable fixture once, after its last test.")]
public class ProtocolBaseHostedControlSizingTests
{
    // Rebuilt by [SetUp] before each test, so null! rather than an initializer here;
    // see ADR-0022 on when that is the right answer.
    private ConnectionWindow _window = null!;
    private RdpProtocol? _protocol;

    [SetUp]
    public void Setup()
    {
        _window = new ConnectionWindow("sizing panel");
        _window.Show();
    }

    [TearDown]
    public void TearDown()
    {
        _protocol = null;
        _window?.Dispose();
    }

    [Test]
    public void TheRdpControlStillFillsItsInterfaceControlAfterTheWindowIsResized()
    {
        var connectionInfo = new ConnectionInfo { Name = "server1", Hostname = "127.0.0.2" };
        TabPage tabPage = _window.AddConnectionTab(connectionInfo)
            ?? throw new AssertionException("AddConnectionTab returned no tab page.");

        _protocol = new RdpProtocol();
        var interfaceControl = new InterfaceControl(tabPage, _protocol, connectionInfo);
        _protocol.InterfaceControl = interfaceControl;

        Assert.That(_protocol.Initialize(), Is.True,
                    "RdpProtocol.Initialize() failed - no RDP ActiveX control could be created.");
        Pump();

        // OfType rather than the single child it used to be: the InterfaceControl now also
        // holds the themed connecting overlay (see ConnectingOverlay).
        AxHost rdpControl = interfaceControl.Controls.OfType<AxHost>().SingleOrDefault()
                            ?? throw new AssertionException("No control was hosted in the InterfaceControl.");

        Size sizeBefore = rdpControl.Size;

        _window.Size = new Size(_window.Width + 160, _window.Height + 120);
        Pump();

        Assert.Multiple(() =>
        {
            Assert.That(interfaceControl.Size, Is.Not.EqualTo(sizeBefore),
                        "The InterfaceControl itself did not resize, so this test proves nothing.");
            Assert.That(rdpControl.Size, Is.EqualTo(interfaceControl.Size),
                        "The RDP control did not follow its InterfaceControl. Sessions will never be " +
                        "told to resize, because ResizeEnd compares the control's current size against " +
                        "the size captured at ResizeBegin and finds them equal.");
        });
    }

    /// <summary>
    /// A size change the connection window does not see still reaches the session.
    /// </summary>
    /// <remarks>
    /// Measured on a dock from 150% to 200%: the DPI follow-up's larger font made the tab strip
    /// 12 px taller, so the hosted control lost 12 px while the connection window kept its size.
    /// Sessions followed only the connection window's <c>Resize</c>, so the session stayed 12 px
    /// too tall and mstscax showed scrollbars. A font change on a host that keeps its own size,
    /// followed by the tab strip's re-measure, reproduces exactly that shape.
    /// <para>
    /// There is no session here, so <c>ReconnectForResize</c> stops at its login guard. What is
    /// asserted is that the hosted control's new size was handed to it at all, which the trigger
    /// logs at Debug.
    /// </para>
    /// </remarks>
    [Test]
    public void ASizeChangeInsideTheConnectionWindowIsHandedToTheSessionResize()
    {
        using var host = new Form
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-32000, -32000),
            ClientSize = new Size(1200, 800),
            AutoScaleMode = AutoScaleMode.None,
            Font = new Font("Segoe UI", 9f),
            Opacity = 0,
            ShowInTaskbar = false,
        };
        var window = new ConnectionWindow("follow panel")
        {
            TopLevel = false,
            FormBorderStyle = FormBorderStyle.None,
            Dock = DockStyle.Fill,
        };
        host.Controls.Add(window);
        host.Show();
        window.Show();

        var connectionInfo = new ConnectionInfo { Name = "server2", Hostname = "127.0.0.3" };
        TabPage tabPage = window.AddConnectionTab(connectionInfo)
            ?? throw new AssertionException("AddConnectionTab returned no tab page.");
        _protocol = new RdpProtocol();
        var interfaceControl = new InterfaceControl(tabPage, _protocol, connectionInfo);
        _protocol.InterfaceControl = interfaceControl;
        Assert.That(_protocol.Initialize(), Is.True,
                    "RdpProtocol.Initialize() failed - no RDP ActiveX control could be created.");
        Pump();

        AxHost rdpControl = interfaceControl.Controls.OfType<AxHost>().SingleOrDefault()
                            ?? throw new AssertionException("No control was hosted in the InterfaceControl.");
        Size windowBefore = window.Size;
        Size controlBefore = rdpControl.Size;
        int messagesBefore = Runtime.MessageCollector.Messages.Count();

        // What the DPI follow-up does to a connection window: a larger font, then the tab strip
        // re-measured for the new DPI, as FrmMain does once the change has settled.
        host.Font = new Font("Segoe UI", 12f);
        window.RefreshTabMetrics(192);
        Pump();

        string[] followed = Runtime.MessageCollector.Messages
                                   .Skip(messagesBefore)
                                   .Select(m => m.Text)
                                   .Where(t => t.Contains("following the hosted control at", StringComparison.Ordinal))
                                   .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(window.Size, Is.EqualTo(windowBefore),
                        "The connection window resized, so this does not test a change it cannot see.");
            Assert.That(rdpControl.Size, Is.Not.EqualTo(controlBefore),
                        "The hosted control kept its size, so this proves nothing.");
            Assert.That(followed, Has.Some.EndsWith($"{rdpControl.Width}x{rdpControl.Height}."),
                        "The hosted control's new size never reached the session resize, so the session " +
                        "would keep the old one and mstscax would show scrollbars.");
        });
    }

    private static void Pump()
    {
        for (int i = 0; i < 40; i++)
        {
            Application.DoEvents();
            Thread.Sleep(5);
        }
    }
}
