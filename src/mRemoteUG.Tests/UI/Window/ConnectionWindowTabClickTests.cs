using System.Diagnostics.CodeAnalysis;

using mRemoteUG.App;
using mRemoteUG.Config;
using mRemoteUG.Connection;
using mRemoteUG.Connection.Protocol;
using mRemoteUG.Connection.Protocol.RDP;
using mRemoteUG.UI.Forms;
using mRemoteUG.UI.Window;

using NUnit.Framework;

namespace mRemoteUG.Tests.UI.Window;

/// <summary>
/// A right-click on a Tab opens the Tab menu for that Tab, and leaves the active Tab active.
/// </summary>
/// <remarks>
/// <para>
/// The menu's item states and its actions refer to the Tab that was right-clicked - the menu's
/// target - and not to <c>TabController.SelectedTab</c>. The native tab control selects a Tab
/// only when the left button goes down, and the right-click does not select one either, so an
/// inactive Tab stays inactive while its menu is open and after a background-safe action on it.
/// </para>
/// <para>
/// The clicks are real button messages sent to the strip's window, so comctl32 sees the
/// button-down and WinForms raises <c>MouseUp</c> from the button-up, as for a user's click.
/// What they do not model: the cursor (the messages carry their own position, and the cursor
/// stays wherever it is), mouse capture by another window, and the foreground window, which
/// the handler reads through <c>MainWindowIsForeground</c> and the tests set both ways. The menu
/// is cancelled in <c>Opening</c> rather than shown, and its items are clicked with
/// <c>PerformClick</c>.
/// </para>
/// </remarks>
[TestFixture]
[Apartment(ApartmentState.STA)]
[SuppressMessage("Design", "CA1001",
    Justification = "[TearDown] disposes _window after every test. "
        + "NUnit would dispose an IDisposable fixture once, after its last test.")]
public class ConnectionWindowTabClickTests
{
    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_RBUTTONDOWN = 0x0204;
    private const int WM_RBUTTONUP = 0x0205;
    private const int MK_RBUTTON = 0x0002;
    private const int WM_MBUTTONDOWN = 0x0207;
    private const int WM_MBUTTONUP = 0x0208;
    private const int MK_MBUTTON = 0x0010;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_MOUSEACTIVATE = 0x0021;
    private const int HTCLIENT = 1;
    private const int WM_NCLBUTTONDOWN = 0x00A1;
    /// <summary>No WM_MOUSEACTIVATE reaches the main window before the activation.</summary>
    private const int NoMouseActivate = 0;

    private sealed class RecordingProtocol : ProtocolBase
    {
        public RecordingProtocol(string name) : base(name)
        {
            Control = new Panel();
        }

        public int CloseCalls { get; private set; }

        public override void Close()
        {
            CloseCalls++;
            base.Close();
        }

        protected override void CleanupProtocolResources()
        {
        }
    }

    /// <summary>A PuTTY Tab as far as the menu can tell: no process is started.</summary>
    private sealed class IdlePutty : PuttyBase
    {
        public IdlePutty()
        {
            Control = new Panel();
        }
    }

    private ConnectionWindow _window = null!;
    private int _originalConfirmClose;
    private int _menuOpenings;
    private TabPage? _selectedWhenTheMenuOpened;
    private bool _puttySettingsOfferedWhenTheMenuOpened;
    private const string SeparatorMark = "----";
    /// <summary>
    /// The menu's items, in order, as the user would see them; a separator is <see cref="SeparatorMark"/>.
    /// </summary>
    private string[] _itemsShownWhenTheMenuOpened = [];

    [SetUp]
    public void SetUp()
    {
        // The default asks before closing a Tab, which would put a modal dialog in the test.
        _originalConfirmClose = Settings.Default.ConfirmCloseConnection;
        Settings.Default.ConfirmCloseConnection = (int)ConfirmCloseMode.Never;

        _window = new ConnectionWindow("tab click panel") { Size = new Size(800, 400) };
        _window.Show();

        _menuOpenings = 0;
        _selectedWhenTheMenuOpened = null;
        _window.cmenTab.Opening += (sender, e) =>
        {
            _menuOpenings++;
            _selectedWhenTheMenuOpened = _window.TabController.SelectedTab;
            // Available, not Visible: Visible also needs the menu itself on screen.
            _puttySettingsOfferedWhenTheMenuOpened = MenuItem("cmenTabPuttySettings").Available;
            _itemsShownWhenTheMenuOpened = _window.cmenTab.Items.Cast<ToolStripItem>()
                                                  .Where(item => item.Available)
                                                  .Select(item => item is ToolStripSeparator
                                                                      ? SeparatorMark
                                                                      : item.Name ?? "")
                                                  .ToArray();
            e.Cancel = true;
        };
    }

    [TearDown]
    public void TearDown()
    {
        _window?.Dispose();
        Settings.Default.ConfirmCloseConnection = _originalConfirmClose;
    }

    [TestCase(true)]
    [TestCase(false)]
    public void RightClickingAnInactiveTabOpensItsMenuAndLeavesItInactive(bool mainWindowIsForeground)
    {
        TabPage activePage = PageOf(HostATab("active"));
        TabPage inactivePage = PageOf(HostATab("inactive"));
        SelectByLeftClick(activePage, mainWindowIsForeground);

        TabPage? selectedAfterButtonDown = RightClick(CentreOf(inactivePage));

        Assume.That(selectedAfterButtonDown, Is.SameAs(activePage),
                    "The tab control selected on the right button going down, which the lab "
                    + "measured it does not do; this test would not model a user's click.");
        Assert.Multiple(() =>
        {
            Assert.That(_menuOpenings, Is.EqualTo(1), "The Tab menu did not open.");
            Assert.That(_window.MenuTargetTab, Is.SameAs(inactivePage),
                        "The menu does not target the Tab that was right-clicked.");
            Assert.That(_selectedWhenTheMenuOpened, Is.SameAs(activePage),
                        "The right-clicked Tab was made active before its menu opened.");
            Assert.That(_window.TabController.SelectedTab, Is.SameAs(activePage),
                        "The right-click changed the active Tab.");
        });
    }

    [Test]
    public void RightClickingTheActiveTabTargetsIt()
    {
        HostATab("other");
        TabPage activePage = PageOf(HostATab("active"));
        SelectByLeftClick(activePage, true);

        RightClick(CentreOf(activePage));

        Assert.Multiple(() =>
        {
            Assert.That(_menuOpenings, Is.EqualTo(1), "The Tab menu did not open.");
            Assert.That(_window.MenuTargetTab, Is.SameAs(activePage));
            Assert.That(_window.TabController.SelectedTab, Is.SameAs(activePage));
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public void DisconnectOnAnInactiveTabsMenuClosesThatTabAndLeavesTheActiveOne(bool mainWindowIsForeground)
    {
        RecordingProtocol active = HostATab("active");
        RecordingProtocol inactive = HostATab("inactive");
        TabPage activePage = PageOf(active);
        TabPage inactivePage = PageOf(inactive);
        SelectByLeftClick(activePage, mainWindowIsForeground);

        var selectionsSeen = new List<TabPage?>();
        _window.TabController.SelectedIndexChanged +=
            (sender, e) => selectionsSeen.Add(_window.TabController.SelectedTab);

        RightClick(CentreOf(inactivePage));
        MenuItem("cmenTabDisconnect").PerformClick();
        Pump();

        Assert.Multiple(() =>
        {
            Assert.That(inactive.CloseCalls, Is.EqualTo(1), "The right-clicked Tab's session was not closed.");
            Assert.That(_window.TabController.TabPages, Does.Not.Contain(inactivePage),
                        "The right-clicked Tab is still in the strip.");
            Assert.That(active.CloseCalls, Is.Zero, "The active Tab's session was closed instead.");
            Assert.That(_window.TabController.SelectedTab, Is.SameAs(activePage),
                        "The active Tab is no longer the active one.");
            Assert.That(selectionsSeen, Does.Not.Contain(inactivePage),
                        "The right-clicked Tab was made active on the way to closing it.");
            Assert.That(_window.MenuTargetTab, Is.Null,
                        "The menu still holds on to the closed Tab.");
        });
    }

    [TestCase(true, TestName = "The menu of an inactive PuTTY Tab offers PuTTY settings")]
    [TestCase(false, TestName = "The menu of an inactive non-PuTTY Tab does not offer PuTTY settings")]
    public void TheMenuStatesDescribeTheRightClickedTab(bool rightClickedTabIsPutty)
    {
        TabPage activePage = PageOf(rightClickedTabIsPutty ? HostATab("active") : HostAPuttyTab("active"));
        TabPage inactivePage = PageOf(rightClickedTabIsPutty ? HostAPuttyTab("inactive") : HostATab("inactive"));
        SelectByLeftClick(activePage, true);

        RightClick(CentreOf(inactivePage));

        Assume.That(_menuOpenings, Is.EqualTo(1), "The Tab menu did not open.");
        Assert.That(_puttySettingsOfferedWhenTheMenuOpened, Is.EqualTo(rightClickedTabIsPutty),
                    "The menu's items describe the active Tab, not the one that was right-clicked.");
    }

    /// <summary>
    /// The Tab menu, as it opens, shows no separator line first, last, or directly after
    /// another: a <c>ToolStrip</c> does not collapse separators around hidden items itself.
    /// </summary>
    /// <remarks>
    /// The PuTTY-then-RDP and RDP-then-PuTTY cases open the menu on one Tab and then the other,
    /// so a separator hidden for the first is shown again for the second where it is due.
    /// </remarks>
    [TestCase("rdp", TestName = "An RDP Tab's menu shows no redundant separator")]
    [TestCase("putty", TestName = "A PuTTY Tab's menu shows no redundant separator")]
    [TestCase("other", TestName = "Another protocol's Tab menu shows no redundant separator")]
    [TestCase("putty", "rdp", TestName = "An RDP Tab's menu after a PuTTY Tab's shows no redundant separator")]
    [TestCase("rdp", "putty", TestName = "A PuTTY Tab's menu after an RDP Tab's shows no redundant separator")]
    public void TheTabMenuShowsNoRedundantSeparator(params string[] protocols)
    {
        var pages = protocols.Select((protocol, i) => PageOf(protocol switch
        {
            "rdp" => HostAnRdpTab($"tab{i}"),
            "putty" => HostAPuttyTab($"tab{i}"),
            _ => HostATab($"tab{i}"),
        })).ToList();
        SelectByLeftClick(pages[0], true);

        foreach (TabPage page in pages)
        {
            RightClick(CentreOf(page));
        }

        Assume.That(_menuOpenings, Is.EqualTo(pages.Count), "The Tab menu did not open each time.");
        string[] shown = _itemsShownWhenTheMenuOpened;
        string layout = string.Join(", ", shown);
        Assert.Multiple(() =>
        {
            Assert.That(shown, Is.Not.Empty, "The menu shows no items.");
            Assert.That(shown.FirstOrDefault(), Is.Not.EqualTo(SeparatorMark),
                        $"The menu starts with a separator: {layout}");
            Assert.That(shown.LastOrDefault(), Is.Not.EqualTo(SeparatorMark),
                        $"The menu ends with a separator: {layout}");
            Assert.That(shown.Zip(shown.Skip(1)).Any(p => p.First == SeparatorMark && p.Second == SeparatorMark),
                        Is.False, $"The menu shows two separators in a row: {layout}");
            // RDP and PuTTY have protocol items above the Tab items; another protocol has none.
            Assert.That(shown.Count(s => s == SeparatorMark), Is.EqualTo(protocols[^1] == "other" ? 0 : 1),
                        $"The menu does not separate its protocol items from the Tab items by one line: {layout}");
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public void RightClickingEmptyStripSpaceOpensNoMenu(bool mainWindowIsForeground)
    {
        HostATab("first");
        TabPage secondPage = PageOf(HostATab("second"));
        SelectByLeftClick(secondPage, mainWindowIsForeground);

        Rectangle lastTab = _window.TabController.GetTabRect(_window.TabController.TabCount - 1);
        var empty = new Point(lastTab.Right + 40, lastTab.Top + lastTab.Height / 2);
        Assume.That(_window.TabController.ClientRectangle.Contains(empty), Is.True,
                    "The strip is too narrow for a point beside its last Tab.");

        RightClick(empty);

        Assert.Multiple(() =>
        {
            Assert.That(_menuOpenings, Is.Zero, "A right-click beside the Tabs opened the Tab menu.");
            Assert.That(_window.TabController.SelectedTab, Is.SameAs(secondPage),
                        "A right-click beside the Tabs changed the selection.");
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public void MiddleClickingAnInactiveTabClosesThatTabAndLeavesTheActiveOne(bool mainWindowIsForeground)
    {
        RecordingProtocol active = HostATab("active");
        RecordingProtocol inactive = HostATab("inactive");
        TabPage activePage = PageOf(active);
        TabPage inactivePage = PageOf(inactive);
        SelectByLeftClick(activePage, mainWindowIsForeground);

        var selectionsSeen = new List<TabPage?>();
        _window.TabController.SelectedIndexChanged +=
            (sender, e) => selectionsSeen.Add(_window.TabController.SelectedTab);

        TabPage? selectedAfterButtonDown = MiddleClick(CentreOf(inactivePage));
        Pump();

        Assume.That(selectedAfterButtonDown, Is.SameAs(activePage),
                    "The tab control selected on the middle button going down; this test would "
                    + "not model the lab's click with an RDP session focused.");
        Assert.Multiple(() =>
        {
            Assert.That(inactive.CloseCalls, Is.EqualTo(1), "The middle-clicked Tab's session was not closed.");
            Assert.That(_window.TabController.TabPages, Does.Not.Contain(inactivePage),
                        "The middle-clicked Tab is still in the strip.");
            Assert.That(active.CloseCalls, Is.Zero, "The active Tab's session was closed instead.");
            Assert.That(_window.TabController.SelectedTab, Is.SameAs(activePage),
                        "The active Tab is no longer the active one.");
            Assert.That(selectionsSeen, Does.Not.Contain(inactivePage),
                        "The middle-clicked Tab was switched to before it was closed.");
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public void MiddleClickingEmptyStripSpaceClosesNothing(bool mainWindowIsForeground)
    {
        RecordingProtocol first = HostATab("first");
        RecordingProtocol second = HostATab("second");
        TabPage secondPage = PageOf(second);
        SelectByLeftClick(secondPage, mainWindowIsForeground);

        MiddleClick(BesideTheLastTab());
        Pump();

        Assert.Multiple(() =>
        {
            Assert.That(first.CloseCalls + second.CloseCalls, Is.Zero, "A middle-click beside the Tabs closed a Tab.");
            Assert.That(_window.TabController.TabCount, Is.EqualTo(2));
            Assert.That(_window.TabController.SelectedTab, Is.SameAs(secondPage));
        });
    }

    /// <summary>
    /// A right- or middle-click that activates the main window - because a hosted PuTTY window
    /// (a top-level window of its own, ADR-0023) or another application had the foreground - does
    /// not select the Tab under the mouse.
    /// </summary>
    /// <remarks>
    /// <c>FrmMain</c> answers a click activation by sending the clicked control a simulated
    /// <c>WM_LBUTTONDOWN</c>, and on the tab strip a left button-down selects the Tab under it.
    /// Which button made the click is read from the <c>WM_MOUSEACTIVATE</c> that comes before the
    /// activation, so the test sends that message to the main window for real. The activation
    /// itself is driven by calling its handler with a position, since <c>WM_ACTIVATE</c> reads the
    /// real cursor, which a test cannot move.
    /// </remarks>
    [TestCase(WM_RBUTTONDOWN)]
    [TestCase(WM_MBUTTONDOWN)]
    public void ANonLeftClickThatActivatesTheMainWindowDoesNotSelectTheTabUnderIt(int buttonDown)
    {
        TabPage activePage = PageOf(HostATab("active"));
        TabPage inactivePage = PageOf(HostATab("inactive"));
        SelectByLeftClick(activePage, false);

        ActivateMainWindowByClick(buttonDown, _window.TabController,
                                  _window.TabController.PointToScreen(CentreOf(inactivePage)));

        Assert.That(_window.TabController.SelectedTab, Is.SameAs(activePage),
                    $"A click (0x{buttonDown:X}) that activated the main window selected the Tab under it.");
    }

    /// <remarks>
    /// <c>NoMouseActivate</c> is a click whose <c>WM_MOUSEACTIVATE</c> never reached the main
    /// window, the button unknown and, by the time the activation is handled, released. Before
    /// #259 every click activation got the simulated button-down; only a click known to be right,
    /// middle or X is treated differently now, so an unknown one still gets it.
    /// </remarks>
    [TestCase(WM_LBUTTONDOWN)]
    [TestCase(NoMouseActivate)]
    [TestCase(WM_NCLBUTTONDOWN)]
    public void ALeftClickThatActivatesTheMainWindowStillSelectsTheTabUnderIt(int buttonDown)
    {
        TabPage activePage = PageOf(HostATab("active"));
        TabPage inactivePage = PageOf(HostATab("inactive"));
        SelectByLeftClick(activePage, false);

        ActivateMainWindowByClick(buttonDown, _window.TabController,
                                  _window.TabController.PointToScreen(CentreOf(inactivePage)));

        Assert.That(_window.TabController.SelectedTab, Is.SameAs(inactivePage),
                    "A left click that activated the main window did not reach the Tab strip, "
                    + "so it takes a second click.");
    }

    /// <summary>
    /// A left click that activates the main window reaches a menu bar or toolbar at once; a right
    /// or middle click gets no simulated left button-down.
    /// </summary>
    [TestCase(typeof(MenuStrip), WM_LBUTTONDOWN, true)]
    [TestCase(typeof(ToolStrip), WM_LBUTTONDOWN, true)]
    [TestCase(typeof(MenuStrip), NoMouseActivate, true)]
    [TestCase(typeof(ToolStrip), NoMouseActivate, true)]
    [TestCase(typeof(MenuStrip), WM_RBUTTONDOWN, false)]
    [TestCase(typeof(ToolStrip), WM_MBUTTONDOWN, false)]
    public void AClickThatActivatesTheMainWindowReachesAStripOnlyAsALeftClick(
        Type stripType, int buttonDown, bool expectLeftButtonDown)
    {
        using var form = new Form();
        var strip = (ToolStrip)Activator.CreateInstance(stripType)!;
        strip.Dock = DockStyle.Top;
        form.Controls.Add(strip);
        form.Show();

        var buttonsSeen = new List<MouseButtons>();
        strip.MouseDown += (sender, e) => buttonsSeen.Add(e.Button);

        ActivateMainWindowByClick(buttonDown, strip, strip.PointToScreen(new Point(strip.Width - 5, strip.Height / 2)));

        Assert.That(buttonsSeen, expectLeftButtonDown ? Is.EqualTo(new[] { MouseButtons.Left }) : Is.Empty,
                    expectLeftButtonDown
                        ? "A left click that activated the main window did not reach the strip, "
                          + "so the first click on the menu bar or toolbar is lost."
                        : "A right or middle click that activated the main window reached the strip "
                          + "as a left button-down.");
    }

    /// <summary>
    /// The panel's own menu (Rename, for the panel's tab in the documents strip) does not open
    /// from the session Tab strip.
    /// </summary>
    /// <remarks>
    /// A right button-up on a window with no menu of its own becomes a <c>WM_CONTEXTMENU</c> that
    /// WinForms passes up to the parent. With the panel menu as the window's
    /// <c>ContextMenuStrip</c>, a right-click beside the Tabs showed Rename, and a right-click on
    /// a Tab showed it after the Tab menu.
    /// </remarks>
    [TestCase(false, TestName = "Right-clicking empty strip space shows no panel menu")]
    [TestCase(true, TestName = "Right-clicking a Tab shows the Tab menu and no panel menu")]
    public void TheSessionStripDoesNotShowThePanelMenu(bool onATab)
    {
        TabPage activePage = PageOf(HostATab("active"));
        TabPage inactivePage = PageOf(HostATab("inactive"));
        SelectByLeftClick(activePage, true);

        var panelMenu = new ContextMenuStrip();
        panelMenu.Items.Add(new ToolStripMenuItem("Rename"));
        _window.OwnContextMenu(panelMenu);
        int panelMenuOpenings = 0;
        panelMenu.Opening += (sender, e) =>
        {
            panelMenuOpenings++;
            e.Cancel = true;
        };

        RightClick(onATab ? CentreOf(inactivePage) : BesideTheLastTab());
        Pump();

        Assert.Multiple(() =>
        {
            Assert.That(panelMenuOpenings, Is.Zero, "The panel's Rename menu opened from the session Tab strip.");
            Assert.That(_menuOpenings, Is.EqualTo(onATab ? 1 : 0), "The Tab menu opened or failed to open.");
        });
    }

    /// <summary>
    /// A click into a session that activates the main window gets the simulated left button-down
    /// it got before #259.
    /// </summary>
    /// <remarks>
    /// <c>ConnectionWindow</c> answers the <c>WM_MOUSEACTIVATE</c> of a click inside a session
    /// itself, so the main window never learns the button; by the time the activation is handled
    /// it may already be released. The hosted control is a selectable stand-in, as the RDP
    /// control's <c>AxHost</c> is.
    /// </remarks>
    [Test]
    public void AClickIntoASessionThatActivatesTheMainWindowStillReachesIt()
    {
        RecordingProtocol session = HostATab("session");
        Control sessionPanel = session.InterfaceControl
                               ?? throw new AssertionException("The protocol has no interface control.");
        var hostedControl = new UserControl { Dock = DockStyle.Fill };
        sessionPanel.Controls.Add(hostedControl);
        Assume.That(hostedControl.CanSelect, Is.True, "The stand-in is not selectable, as the RDP control is.");
        var buttonsSeen = new List<MouseButtons>();
        hostedControl.MouseDown += (sender, e) => buttonsSeen.Add(e.Button);

        ActivateMainWindowByClick(NoMouseActivate, hostedControl, hostedControl.PointToScreen(new Point(10, 10)));

        Assert.That(buttonsSeen, Is.EqualTo(new[] { MouseButtons.Left }),
                    "A click into a session that activated the main window no longer reached it.");
    }

    /// <summary>
    /// A click activation is logged step by step, for the lab cases a test cannot reach: the
    /// WM_MOUSEACTIVATE the main window saw and how it answered, which button the activation
    /// decided on and from what, and whether a click was simulated.
    /// </summary>
    [TestCase(WM_LBUTTONDOWN, "WM_LBUTTONDOWN", "simulated a left button-down")]
    [TestCase(WM_RBUTTONDOWN, "WM_RBUTTONDOWN", "no click simulated")]
    public void AClickActivationIsLogged(int buttonDown, string buttonName, string outcome)
    {
        TabPage activePage = PageOf(HostATab("active"));
        TabPage inactivePage = PageOf(HostATab("inactive"));
        SelectByLeftClick(activePage, false);
        int before = Runtime.MessageCollector.Messages.Count();

        ActivateMainWindowByClick(buttonDown, _window.TabController,
                                  _window.TabController.PointToScreen(CentreOf(inactivePage)));

        string[] logged = Runtime.MessageCollector.Messages.Skip(before)
                                 .Select(m => m.Text ?? "")
                                 .Where(t => t.StartsWith("Activation:", StringComparison.Ordinal))
                                 .ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(logged, Has.Some.Contains("WM_MOUSEACTIVATE").And.Contains(buttonName).And.Contains("answered"),
                        "The WM_MOUSEACTIVATE the main window saw was not logged with its button and answer.");
            Assert.That(logged, Has.Some.Contains("from WM_MOUSEACTIVATE").And.Contains(outcome),
                        "The activation's button decision and its outcome were not logged.");
        });
    }

    /// <summary>
    /// What Windows does for a click on an inactive main window, minus the cursor: a
    /// <c>WM_MOUSEACTIVATE</c> naming the button message, then the click activation.
    /// </summary>
    private static void ActivateMainWindowByClick(int buttonDown, Control clicked, Point screenPosition)
    {
        IntPtr main = FrmMain.Default.Handle;
        if (buttonDown != NoMouseActivate)
        {
            NativeMethods.SendMessage(main, WM_MOUSEACTIVATE, main, (IntPtr)((buttonDown << 16) | HTCLIENT));
        }

        FrmMain.Default.OnClickActivation(clicked, screenPosition);
    }

    private Point BesideTheLastTab()
    {
        Rectangle lastTab = _window.TabController.GetTabRect(_window.TabController.TabCount - 1);
        var empty = new Point(lastTab.Right + 40, lastTab.Top + lastTab.Height / 2);
        Assume.That(_window.TabController.ClientRectangle.Contains(empty), Is.True,
                    "The strip is too narrow for a point beside its last Tab.");
        return empty;
    }

    /// <returns>The selected Tab between the button going down and coming up.</returns>
    private TabPage? MiddleClick(Point location)
    {
        Send(WM_MBUTTONDOWN, MK_MBUTTON, location);
        TabPage? selectedAfterButtonDown = _window.TabController.SelectedTab;
        Send(WM_MBUTTONUP, 0, location);
        return selectedAfterButtonDown;
    }

    private RecordingProtocol HostATab(string name) => Host(new RecordingProtocol(name), name);

    private IdlePutty HostAPuttyTab(string name) => Host(new IdlePutty(), name);

    /// <summary>
    /// An RDP Tab as far as the menu can tell. Not initialized, so no ActiveX control is
    /// created; its Full Screen and Smart Size read false.
    /// </summary>
    private RdpProtocol HostAnRdpTab(string name)
    {
        var connectionInfo = new ConnectionInfo { Name = name };
        TabPage tabPage = _window.AddConnectionTab(connectionInfo)
                          ?? throw new AssertionException("AddConnectionTab returned no tab page.");
        var protocol = new RdpProtocol();
        protocol.InterfaceControl = new InterfaceControl(tabPage, protocol, connectionInfo);
        // What ProtocolBase.Initialize does first, and all the menu reads.
        tabPage.Tag = protocol.InterfaceControl;
        return protocol;
    }

    private T Host<T>(T protocol, string name) where T : ProtocolBase
    {
        var connectionInfo = new ConnectionInfo { Name = name };
        TabPage tabPage = _window.AddConnectionTab(connectionInfo)
                          ?? throw new AssertionException("AddConnectionTab returned no tab page.");

        protocol.InterfaceControl = new InterfaceControl(tabPage, protocol, connectionInfo);
        Assert.That(protocol.Initialize(), Is.True, "ProtocolBase.Initialize() failed.");

        // As ConnectionInitiator wires it: a closed session takes its Tab with it.
        protocol.Closed += _window.OnProtocolClosed;
        return protocol;
    }

    private static TabPage PageOf(ProtocolBase protocol) =>
        protocol.InterfaceControl?.Parent as TabPage
        ?? throw new AssertionException("The protocol was not hosted in a tab page.");

    private ToolStripItem MenuItem(string name) =>
        _window.cmenTab.Items[name] ?? throw new AssertionException($"The Tab menu has no item {name}.");

    /// <summary>
    /// Makes <paramref name="page"/> the active Tab the way a user's left click leaves it: the
    /// native control has selected it on the button going down, and the handler then sees the
    /// button come up.
    /// </summary>
    /// <remarks>
    /// The button-down itself is not sent: comctl32 may start a drag-detect loop on it, which
    /// would wait for real mouse input.
    /// </remarks>
    private void SelectByLeftClick(TabPage page, bool mainWindowIsForeground)
    {
        _window.MainWindowIsForeground = () => mainWindowIsForeground;
        _window.TabController.SelectedTab = page;
        Send(WM_LBUTTONUP, 0, CentreOf(page));
        Assume.That(_window.TabController.SelectedTab, Is.SameAs(page), "The setup could not select the Tab.");
    }

    /// <returns>The selected Tab between the button going down and coming up.</returns>
    private TabPage? RightClick(Point location)
    {
        Send(WM_RBUTTONDOWN, MK_RBUTTON, location);
        TabPage? selectedAfterButtonDown = _window.TabController.SelectedTab;
        Send(WM_RBUTTONUP, 0, location);
        return selectedAfterButtonDown;
    }

    private Point CentreOf(TabPage page)
    {
        Rectangle rect = _window.TabController.GetTabRect(_window.TabController.TabPages.IndexOf(page));
        // Left of centre, away from the close button the selected Tab draws at its right edge.
        return new Point(rect.Left + rect.Width / 4, rect.Top + rect.Height / 2);
    }

    private void Send(int message, int keys, Point location) =>
        NativeMethods.SendMessage(_window.TabController.Handle, message, (IntPtr)keys,
                                  (IntPtr)((location.Y << 16) | (location.X & 0xFFFF)));

    private static void Pump()
    {
        for (int i = 0; i < 40; i++)
        {
            Application.DoEvents();
            Thread.Sleep(5);
        }
    }
}
