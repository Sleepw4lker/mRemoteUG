using System.Diagnostics.CodeAnalysis;

using mRemoteUG.App;
using mRemoteUG.Connection;
using mRemoteUG.Connection.Protocol;
using mRemoteUG.UI.Forms;
using mRemoteUG.UI.Window;

using NUnit.Framework;

namespace mRemoteUG.Tests.UI.Forms;

/// <summary>
/// What the main window focuses when it is activated with a session selected (#269).
/// </summary>
/// <remarks>
/// <para>
/// Driven in the message order the lab measured with build 2.1.1.429. A click on the menu bar
/// or a toolbar from another program sends no <c>WM_MOUSEACTIVATE</c> to the main window (the
/// strip answers it itself), then <c>WM_WINDOWPOSCHANGED</c>, <c>WM_ACTIVATEAPP</c> and
/// <c>WM_ACTIVATE WA_CLICKACTIVE</c>. Alt+Tab, the taskbar and restore send the same without a
/// click: <c>WA_ACTIVE</c>.
/// </para>
/// <para>
/// The handlers are called directly with what <c>FrmMain.WndProc</c> reads out of the
/// messages, because <c>WM_ACTIVATE</c>'s handler reads the real cursor, which a test cannot
/// move. <c>WM_ACTIVATEAPP</c> is sent for real: it ends any mouse activation an earlier test
/// left behind on the shared main window. What the tests do not model is the foreground: a
/// hosted PuTTY takes it when focused, and here its focus is only counted.
/// </para>
/// </remarks>
[TestFixture]
[Apartment(ApartmentState.STA)]
[SuppressMessage("Design", "CA1001",
    Justification = "[TearDown] disposes _window and _stripForm after every test. "
        + "NUnit would dispose an IDisposable fixture once, after its last test.")]
public class FrmMainActivationTests
{
    private const int WM_ACTIVATEAPP = 0x001C;
    private const int SWP_NOSIZE = 0x0001;
    private const int SWP_NOMOVE = 0x0002;

    /// <summary>A PuTTY session as far as activation can tell: no process, its focus counted.</summary>
    private sealed class CountingPutty : PuttyBase
    {
        public CountingPutty()
        {
            Control = new Panel();
        }

        public int FocusCalls { get; private set; }

        public override void Focus() => FocusCalls++;
    }

    /// <summary>A session whose control lives in this process, as the RDP control does.</summary>
    private sealed class CountingInProcessSession : ProtocolBase
    {
        public CountingInProcessSession() : base("in-process")
        {
            Control = new Panel();
        }

        public int FocusCalls { get; private set; }

        public override void Focus() => FocusCalls++;

        protected override void CleanupProtocolResources()
        {
        }
    }

    private ConnectionWindow _window = null!;
    private Form _stripForm = null!;
    private MenuStrip _menuStrip = null!;
    private ToolStripMenuItem _fileMenu = null!;
    private Func<ConnectionWindow?> _originalActiveConnectionWindow = null!;
    private List<MouseButtons> _menuStripButtonsDown = null!;

    [SetUp]
    public void SetUp()
    {
        _window = new ConnectionWindow("activation panel") { Size = new Size(800, 400) };
        _window.Show();

        _originalActiveConnectionWindow = FrmMain.Default.ActiveConnectionWindow;
        FrmMain.Default.ActiveConnectionWindow = () => _window;

        // A stand-in for msMain: the main window is never shown in a test.
        _stripForm = new Form();
        _menuStrip = new MenuStrip { Dock = DockStyle.Top };
        _fileMenu = new ToolStripMenuItem("File");
        _menuStrip.Items.Add(_fileMenu);
        _stripForm.Controls.Add(_menuStrip);
        _stripForm.Show();
        _menuStripButtonsDown = new List<MouseButtons>();
        _fileMenu.MouseDown += (sender, e) => _menuStripButtonsDown.Add(e.Button);

        // The main window's handle exists, and no mouse activation is left over from another test.
        NativeMethods.SendMessage(FrmMain.Default.Handle, WM_ACTIVATEAPP, IntPtr.Zero, IntPtr.Zero);
        Pump();
    }

    [TearDown]
    public void TearDown()
    {
        Pump();
        FrmMain.Default.ActiveConnectionWindow = _originalActiveConnectionWindow;
        _stripForm?.Dispose();
        _window?.Dispose();
    }

    [Test]
    public void AClickOnTheMenuBarFromAnotherProgramDoesNotFocusASelectedPuttySession()
    {
        CountingPutty putty = Host(new CountingPutty(), "putty");

        ActivateByClickOnTheMenuBar();
        Pump();

        Assert.Multiple(() =>
        {
            Assert.That(putty.FocusCalls, Is.Zero,
                        "The PuTTY session was focused, which takes the foreground away from the click: "
                        + "the first click on the menu bar is lost.");
            Assert.That(_menuStripButtonsDown, Is.EqualTo(new[] { MouseButtons.Left }),
                        "The click that activated the main window did not reach the menu bar.");
        });
    }

    [Test]
    public void AnActivationWithoutAClickStillFocusesASelectedPuttySession()
    {
        CountingPutty putty = Host(new CountingPutty(), "putty");

        // Alt+Tab, the taskbar, restore: no WM_MOUSEACTIVATE, and WA_ACTIVE.
        FrmMain.Default.OnWindowPosChanged(SWP_NOSIZE | SWP_NOMOVE);
        FrmMain.Default.OnActivate(NativeMethods.WA_ACTIVE, null, Point.Empty);
        Pump();

        Assert.That(putty.FocusCalls, Is.EqualTo(1),
                    "An activation without a click no longer puts the keyboard into the PuTTY session.");
    }

    [Test]
    public void APositionChangeWithoutAnActivationAfterItStillFocusesASelectedPuttySession()
    {
        CountingPutty putty = Host(new CountingPutty(), "putty");

        FrmMain.Default.OnWindowPosChanged(SWP_NOSIZE | SWP_NOMOVE);
        Pump();

        Assert.That(putty.FocusCalls, Is.EqualTo(1),
                    "A WM_WINDOWPOSCHANGED that no WM_ACTIVATE followed no longer focuses the session.");
    }

    [Test]
    public void ADeactivationBeforeTheDeferredFocusDropsIt()
    {
        CountingPutty putty = Host(new CountingPutty(), "putty");

        FrmMain.Default.OnWindowPosChanged(SWP_NOSIZE | SWP_NOMOVE);
        FrmMain.Default.OnActivate(NativeMethods.WA_INACTIVE, null, Point.Empty);
        Pump();

        Assert.That(putty.FocusCalls, Is.Zero,
                    "The PuTTY session was focused after the main window had lost the activation, "
                    + "taking the foreground back from whatever took it.");
    }

    /// <summary>
    /// A click activation whose target is not known (a click the main window cannot place)
    /// focuses the session as before.
    /// </summary>
    [Test]
    public void AClickActivationOnNoKnownControlStillFocusesASelectedPuttySession()
    {
        CountingPutty putty = Host(new CountingPutty(), "putty");

        FrmMain.Default.OnWindowPosChanged(SWP_NOSIZE | SWP_NOMOVE);
        FrmMain.Default.OnActivate(NativeMethods.WA_CLICKACTIVE, null, Point.Empty);
        Pump();

        Assert.That(putty.FocusCalls, Is.EqualTo(1));
    }

    /// <summary>
    /// A session hosted in this process (RDP) is focused at <c>WM_WINDOWPOSCHANGED</c>, as the
    /// lab confirmed works: the click on the menu bar keeps the foreground there.
    /// </summary>
    [Test]
    public void AnInProcessSessionIsStillFocusedAtThePositionChange()
    {
        CountingInProcessSession session = Host(new CountingInProcessSession(), "rdp");

        FrmMain.Default.OnWindowPosChanged(SWP_NOSIZE | SWP_NOMOVE);

        Assert.That(session.FocusCalls, Is.EqualTo(1),
                    "The in-process session was not focused at WM_WINDOWPOSCHANGED.");
    }

    [Test]
    public void AClickOnTheMenuBarWithAPuttySessionSelectedIsLogged()
    {
        Host(new CountingPutty(), "putty");
        int before = Runtime.MessageCollector.Messages.Count();

        ActivateByClickOnTheMenuBar();
        Pump();

        string[] logged = Runtime.MessageCollector.Messages.Skip(before)
                                 .Select(m => m.Text ?? "")
                                 .Where(t => t.StartsWith("Activation:", StringComparison.Ordinal))
                                 .ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(logged, Has.Some.Contains("WM_WINDOWPOSCHANGED").And.Contains("waits for WM_ACTIVATE"),
                        "The held-back focus was not logged at WM_WINDOWPOSCHANGED.");
            Assert.That(logged, Has.Some.Contains("not focused").And.Contains("MenuStrip"),
                        "The click activation did not log that it left the session unfocused.");
            Assert.That(logged, Has.None.Contains("focusing the"),
                        "A session was focused for a click on the menu bar.");
        });
    }

    /// <summary>
    /// The order the lab measured for a click on <c>msMain</c> from another program: no
    /// <c>WM_MOUSEACTIVATE</c>, then <c>WM_WINDOWPOSCHANGED</c>, then the click activation.
    /// </summary>
    private void ActivateByClickOnTheMenuBar()
    {
        FrmMain.Default.OnWindowPosChanged(SWP_NOSIZE | SWP_NOMOVE);
        NativeMethods.SendMessage(FrmMain.Default.Handle, WM_ACTIVATEAPP, (IntPtr)1, IntPtr.Zero);
        Rectangle item = _fileMenu.Bounds;
        FrmMain.Default.OnActivate(NativeMethods.WA_CLICKACTIVE, _menuStrip,
                                   _menuStrip.PointToScreen(new Point(item.Left + 5, item.Top + item.Height / 2)));
    }

    private T Host<T>(T protocol, string name) where T : ProtocolBase
    {
        var connectionInfo = new ConnectionInfo { Name = name };
        TabPage tabPage = _window.AddConnectionTab(connectionInfo)
                          ?? throw new AssertionException("AddConnectionTab returned no tab page.");

        protocol.InterfaceControl = new InterfaceControl(tabPage, protocol, connectionInfo);
        Assert.That(protocol.Initialize(), Is.True, "ProtocolBase.Initialize() failed.");
        _window.TabController.SelectedTab = tabPage;
        Assume.That(_window.TabController.SelectedTab?.Tag, Is.SameAs(protocol.InterfaceControl),
                    "The hosted session is not the selected Tab's.");
        return protocol;
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
