using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

using mRemoteUG.App;
using mRemoteUG.Connection;
using mRemoteUG.Connection.Protocol.SSH;
using mRemoteUG.UI.Forms;
using mRemoteUG.UI.Window;

using NUnit.Framework;

namespace mRemoteUG.Tests.Connection.Protocol;

/// <summary>
/// A hosted PuTTY is told about a DPI change, from both places this application hears of one.
/// </summary>
/// <remarks>
/// Windows sends <c>WM_DPICHANGED</c> to top-level windows only, and a reparented PuTTY is not
/// one, so it kept the font scaling it started with until the session was reconnected (#262).
/// PuTTY 0.85 rebuilds its fonts on <c>WM_DPICHANGED</c> from the DPI in <c>wParam</c>, and reads
/// the suggested window rectangle from <c>lParam</c>; docs/platform-findings.md has the source
/// lines and the probe against a real PuTTY, which this test does not repeat.
/// <para>
/// No PuTTY runs here. A native window in this process stands in for it and records the messages
/// it receives. What is asserted is the message: which DPI, which rectangle, and how often.
/// </para>
/// </remarks>
[TestFixture]
[NonParallelizable]
[Apartment(ApartmentState.STA)]
[SuppressMessage("Design", "CA1001",
    Justification = "[TearDown] disposes _host after every test. "
        + "NUnit would dispose an IDisposable fixture once, after its last test.")]
public class PuttyBaseTellsPuttyItsDpiTests
{
    private const int WM_DPICHANGED = 0x02E0;
    private const int WS_CHILD = 0x40000000;
    private const int WS_VISIBLE = 0x10000000;

    private Form _host = null!;
    private ConnectionWindow _window = null!;
    private ProtocolSSH2 _protocol = null!;
    private InterfaceControl _interfaceControl = null!;
    private StandInPuttyWindow _putty = null!;

    [SetUp]
    public void Setup()
    {
        _host = new Form
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-32000, -32000),
            ClientSize = new Size(1200, 800),
            Opacity = 0,
            ShowInTaskbar = false,
        };
        _window = new ConnectionWindow("putty is told its dpi")
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

        _putty = new StandInPuttyWindow();
        _putty.CreateHandle(new CreateParams
        {
            Parent = _interfaceControl.Handle,
            Style = WS_CHILD | WS_VISIBLE,
            Width = 300,
            Height = 200,
        });
        _protocol.PuttyHandle = _putty.Handle;
    }

    [TearDown]
    public void TearDown()
    {
        _putty?.DestroyHandle();
        _host?.Dispose();
    }

    [Test]
    public void TheWindowsOwnDpiChangeTellsPuttyTheNewDpiAndWhereItIs()
    {
        _protocol.NotifyDpiChanged(144);

        Assert.That(NativeMethods.GetWindowRect(_putty.Handle, out NativeMethods.RECT where), Is.True);
        Assert.That(_putty.Received, Has.Count.EqualTo(1), "PuTTY was not sent exactly one WM_DPICHANGED.");
        DpiChange change = _putty.Received[0];
        Assert.Multiple(() =>
        {
            Assert.That(change.DpiX, Is.EqualTo(144), "LOWORD(wParam) is the X DPI PuTTY rebuilds its font for.");
            Assert.That(change.DpiY, Is.EqualTo(144), "HIWORD(wParam) is the Y DPI PuTTY rebuilds its font for.");
            Assert.That(change.Rect, Is.EqualTo((where.left, where.top, where.right, where.bottom)),
                        "PuTTY dereferences lParam as the RECT it moves to when it is not maximised; " +
                        "it must be where the window already is.");
        });
    }

    [Test]
    public void TheMainWindowsFollowUpTellsPuttyTheDpiTheWindowSettledOn()
    {
        FrmMain.FollowDpiChangeIn(_host, 192);

        Assert.That(_putty.SentDpis(), Is.EqualTo("192"),
                    "The FrmMain follow-up did not reach the PuTTY in a connection window's Tab.");
    }

    [Test]
    public void OneDpiChangeSeenByBothRoutesReachesPuttyOnce()
    {
        _protocol.NotifyDpiChanged(144);
        FrmMain.FollowDpiChangeIn(_host, 144);

        Assert.That(_putty.SentDpis(), Is.EqualTo("144"),
                    "Each route rebuilds PuTTY's fonts again for a DPI it already has.");
    }

    [Test]
    public void ADockAndAnUndockAreBothPassedOn()
    {
        _protocol.NotifyDpiChanged(192);
        FrmMain.FollowDpiChangeIn(_host, 192);
        _protocol.NotifyDpiChanged(144);
        FrmMain.FollowDpiChangeIn(_host, 144);

        Assert.That(_putty.SentDpis(), Is.EqualTo("192,144"));
    }

    [Test]
    public void ASessionWithoutAPuttyWindowIsLeftAlone()
    {
        _protocol.PuttyHandle = IntPtr.Zero;

        Assert.DoesNotThrow(() => _protocol.NotifyDpiChanged(144));
        Assert.DoesNotThrow(() => FrmMain.FollowDpiChangeIn(_host, 144));
        Assert.That(_putty.Received, Is.Empty);
    }

    private readonly record struct DpiChange(int DpiX, int DpiY, (int Left, int Top, int Right, int Bottom) Rect);

    /// <summary>Records every WM_DPICHANGED, and the RECT its lParam points at.</summary>
    private sealed class StandInPuttyWindow : NativeWindow
    {
        public List<DpiChange> Received { get; } = [];

        public string SentDpis() => string.Join(",", Received.Select(c => c.DpiX));

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_DPICHANGED)
            {
                long wParam = m.WParam.ToInt64();
                NativeMethods.RECT rect = Marshal.PtrToStructure<NativeMethods.RECT>(m.LParam);
                Received.Add(new DpiChange(
                    (int)(wParam & 0xFFFF),
                    (int)((wParam >> 16) & 0xFFFF),
                    (rect.left, rect.top, rect.right, rect.bottom)));
                m.Result = IntPtr.Zero;
                return;
            }

            base.WndProc(ref m);
        }
    }
}
