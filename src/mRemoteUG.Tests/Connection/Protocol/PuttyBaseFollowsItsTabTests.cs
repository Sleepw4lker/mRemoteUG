using System.Diagnostics.CodeAnalysis;

using mRemoteUG.App;
using mRemoteUG.Connection;
using mRemoteUG.Connection.Protocol.SSH;
using mRemoteUG.UI.Window;

using NUnit.Framework;

namespace mRemoteUG.Tests.Connection.Protocol;

/// <summary>
/// A hosted PuTTY window keeps matching its Tab when the Tab changes size inside a connection
/// window that does not.
/// </summary>
/// <remarks>
/// Measured for RDP on a dock from 150% to 200%: the DPI follow-up re-measured the tab strip for
/// the larger font, 12 px taller, so the Tab page lost 12 px while the connection window kept its
/// size. PuTTY followed only the connection window's <c>Resize</c> and the DPI change itself,
/// which runs before that re-measure, so its window stayed 12 px too tall (or, undocking, 12 px
/// short) until the connection window was next resized.
/// <para>
/// No PuTTY runs here. <c>PuttyBase.Resize</c> moves whatever window <c>PuttyHandle</c> names, so
/// a bare native child window stands in for it, as PuTTYNG's window is a child of the panel too.
/// Native rather than a WinForms control, because a font change autoscales a control's bounds and
/// would move the stand-in by itself. What is asserted is the size that window is moved to.
/// </para>
/// </remarks>
[TestFixture]
[NonParallelizable]
[Apartment(ApartmentState.STA)]
[SuppressMessage("Design", "CA1001",
    Justification = "[TearDown] disposes _host after every test. "
        + "NUnit would dispose an IDisposable fixture once, after its last test.")]
public class PuttyBaseFollowsItsTabTests
{
    private Form _host = null!;
    private ConnectionWindow _window = null!;
    private ProtocolSSH2 _protocol = null!;
    private InterfaceControl _interfaceControl = null!;
    private NativeWindow _standInPuttyWindow = null!;

    [SetUp]
    public void Setup()
    {
        _host = new Form
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-32000, -32000),
            ClientSize = new Size(1200, 800),
            // Keeps its own size through the font change, as a maximised main window does on a
            // dock; the font still reaches the connection window as its ambient font.
            AutoScaleMode = AutoScaleMode.None,
            Font = new Font("Segoe UI", 9f),
            Opacity = 0,
            ShowInTaskbar = false,
        };
        _window = new ConnectionWindow("putty follows its tab")
        {
            TopLevel = false,
            FormBorderStyle = FormBorderStyle.None,
            Dock = DockStyle.Fill,
        };
        _host.Controls.Add(_window);
        _host.Show();
        _window.Show();

        var connectionInfo = new ConnectionInfo { Name = "server3", Hostname = "127.0.0.4" };
        TabPage tabPage = _window.AddConnectionTab(connectionInfo)
                          ?? throw new AssertionException("AddConnectionTab returned no tab page.");
        _protocol = new ProtocolSSH2();
        _interfaceControl = new InterfaceControl(tabPage, _protocol, connectionInfo);
        _protocol.InterfaceControl = _interfaceControl;
        Assert.That(_protocol.Initialize(), Is.True, "ProtocolSSH2.Initialize() failed.");

        _standInPuttyWindow = new NativeWindow();
        _standInPuttyWindow.CreateHandle(new CreateParams
        {
            Parent = _interfaceControl.Handle,
            Style = WS_CHILD | WS_VISIBLE,
            Width = 10,
            Height = 10,
        });
        _protocol.PuttyHandle = _standInPuttyWindow.Handle;
        _protocol.Resize(this, EventArgs.Empty);
        Pump();

        Assert.That(PuttySize(), Is.EqualTo(_interfaceControl.Size),
                    "The first Resize did not size the stand-in window, so nothing below means anything.");
    }

    [TearDown]
    public void TearDown()
    {
        _standInPuttyWindow?.DestroyHandle();
        _host?.Dispose();
    }

    [Test]
    public void ATabThatChangesSizeInsideAnUnchangedConnectionWindowResizesPutty()
    {
        Size windowBefore = _window.Size;
        Size panelBefore = _interfaceControl.Size;

        RescaleTheTabStripAsTheDpiFollowUpDoes();
        Pump();

        Assert.Multiple(() =>
        {
            Assert.That(_window.Size, Is.EqualTo(windowBefore),
                        "The connection window resized, so this does not test a change it cannot see.");
            Assert.That(_interfaceControl.Size, Is.Not.EqualTo(panelBefore),
                        "The session panel kept its size, so this proves nothing.");
            Assert.That(PuttySize(), Is.EqualTo(_interfaceControl.Size),
                        "PuTTY kept the size of the panel from before the tab strip was re-measured, so " +
                        "its last row is clipped (or a strip below it is left uncovered).");
        });
    }

    [Test]
    public void DuringADragPuttyIsLeftToTheConnectionWindowAndCaughtUpAtTheEnd()
    {
        Size puttyBefore = PuttySize();

        // A drag of the main window across monitors: the DPI change, and so the tab strip
        // re-measure, happens between ResizeBegin and ResizeEnd.
        _protocol.ResizeBegin(this, EventArgs.Empty);
        RescaleTheTabStripAsTheDpiFollowUpDoes();
        Pump();
        Size puttyDuringDrag = PuttySize();
        _protocol.ResizeEnd(this, EventArgs.Empty);
        Pump();

        Assert.Multiple(() =>
        {
            Assert.That(puttyDuringDrag, Is.EqualTo(puttyBefore),
                        "PuTTY was moved from the posted follow-up during a drag, on top of the " +
                        "connection window's own Resize.");
            Assert.That(PuttySize(), Is.EqualTo(_interfaceControl.Size),
                        "PuTTY was not brought to its panel's size when the drag ended.");
        });
    }

    /// <summary>
    /// What the DPI follow-up does to a connection window: a larger font, then the tab strip
    /// re-measured for the new DPI, as FrmMain does once the change has settled.
    /// </summary>
    private void RescaleTheTabStripAsTheDpiFollowUpDoes()
    {
        _host.Font = new Font("Segoe UI", 12f);
        _window.RefreshTabMetrics(192);
    }

    private const int WS_CHILD = 0x40000000;
    private const int WS_VISIBLE = 0x10000000;

    private Size PuttySize()
    {
        Assert.That(NativeMethods.GetWindowRect(_standInPuttyWindow.Handle, out NativeMethods.RECT rect), Is.True,
                    "GetWindowRect failed on the stand-in PuTTY window.");
        return new Size(rect.right - rect.left, rect.bottom - rect.top);
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
