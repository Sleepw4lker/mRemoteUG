using System.Diagnostics.CodeAnalysis;

using mRemoteUG.Connection;
using mRemoteUG.Connection.Protocol;
using mRemoteUG.UI;
using mRemoteUG.UI.Window;

using NUnit.Framework;

namespace mRemoteUG.Tests.Connection.Protocol;

/// <summary>
/// Pins the chain that delivers window resizes to a protocol.
/// </summary>
/// <remarks>
/// ProtocolBase finds the window to subscribe to with
/// <c>InterfaceControl.GetContainerControl() as ConnectionWindow</c>. When that cast yields
/// null the protocol silently subscribes to nothing, and automatic RDP resizing stops working
/// with no error anywhere. These tests build the same control hierarchy the application does
/// and assert the subscription really happened.
/// </remarks>
[TestFixture]
[NonParallelizable]
[Apartment(ApartmentState.STA)]
[SuppressMessage("Design", "CA1001",
    Justification = "[TearDown] disposes _window after every test. "
        + "NUnit would dispose an IDisposable fixture once, after its last test.")]
public class ProtocolBaseResizeWiringTests
{
    private sealed class ResizeRecordingProtocol : ProtocolBase
    {
        private readonly Panel _hostedControl = new();

        public ResizeRecordingProtocol() : base("test")
        {
            Control = _hostedControl;
        }

        public readonly List<string> ResizeEvents = new List<string>();

        /// <summary>The window ProtocolBase decided to subscribe to, or null if it found none.</summary>
        public ConnectionWindow? SubscribedWindow => ConnectionWindow;

        public Control HostedControl => _hostedControl;

        public override void ResizeBegin(object? sender, EventArgs e) => ResizeEvents.Add("begin");
        public override void Resize(object? sender, EventArgs e) => ResizeEvents.Add("resize");
        public override void ResizeEnd(object? sender, EventArgs e) => ResizeEvents.Add("end");

        protected override void CleanupProtocolResources() { }
    }

    // Rebuilt by [SetUp] before each test, so null! rather than an initializer here;
    // see ADR-0022 on when that is the right answer.
    private ConnectionWindow _window = null!;
    private ResizeRecordingProtocol _protocol = null!;

    [SetUp]
    public void Setup()
    {
        _window = new ConnectionWindow("test panel");
        _window.Show();
        _protocol = new ResizeRecordingProtocol();
    }

    [TearDown]
    public void TearDown()
    {
        _window?.Dispose();
    }

    /// <summary>
    /// Builds the hierarchy ConnectionInitiator builds:
    /// ConnectionWindow -> TabController -> ConnectionTabPage -> InterfaceControl.
    /// </summary>
    private InterfaceControl HostProtocolInATab()
    {
        var connectionInfo = new ConnectionInfo { Name = "server1" };
        TabPage tabPage = _window.AddConnectionTab(connectionInfo)
            ?? throw new AssertionException("AddConnectionTab returned no tab page.");

        return new InterfaceControl(tabPage, _protocol, connectionInfo);
    }

    [Test]
    public void TheTabPageIsParentedUnderTheConnectionWindow()
    {
        // Guards the premise of the test below: if this fails, the hierarchy itself changed.
        InterfaceControl interfaceControl = HostProtocolInATab();

        Control? walker = interfaceControl;
        var chain = new List<string>();
        while (walker != null)
        {
            chain.Add(walker.GetType().Name);
            walker = walker.Parent;
        }

        Assert.That(chain, Does.Contain(nameof(ConnectionWindow)),
                    "InterfaceControl is not under a ConnectionWindow. Chain: " + string.Join(" -> ", chain));
    }

    [Test]
    public void SettingInterfaceControlSubscribesTheProtocolToItsConnectionWindow()
    {
        InterfaceControl interfaceControl = HostProtocolInATab();

        _protocol.InterfaceControl = interfaceControl;

        Assert.That(_protocol.SubscribedWindow, Is.SameAs(_window),
                    "ProtocolBase did not find the ConnectionWindow hosting it, so it subscribed to no " +
                    "resize events at all. Automatic RDP resizing depends on this subscription.");
    }

    [Test]
    public void ResizingTheConnectionWindowReachesTheProtocol()
    {
        // The symptom the user sees: the window is resized and the protocol never hears about it.
        InterfaceControl interfaceControl = HostProtocolInATab();
        _protocol.InterfaceControl = interfaceControl;
        _protocol.ResizeEvents.Clear();

        _window.Size = new System.Drawing.Size(_window.Width + 120, _window.Height + 80);
        Application.DoEvents();

        Assert.That(_protocol.ResizeEvents, Does.Contain("resize"),
                    "Resizing the ConnectionWindow did not reach the protocol.");
    }

    [Test]
    public void MovingTheProtocolToAnotherWindowStopsTheOldWindowsResizesReachingIt()
    {
        _protocol.InterfaceControl = HostProtocolInATab();
        using var otherWindow = new ConnectionWindow("other panel");
        otherWindow.Show();
        var connectionInfo = new ConnectionInfo { Name = "server1" };
        TabPage otherTab = otherWindow.AddConnectionTab(connectionInfo)
            ?? throw new AssertionException("AddConnectionTab returned no tab page.");
        _protocol.InterfaceControl = new InterfaceControl(otherTab, _protocol, connectionInfo);
        Assume.That(_protocol.SubscribedWindow, Is.SameAs(otherWindow));
        _protocol.ResizeEvents.Clear();

        ResizeTheWindow();

        Assert.That(_protocol.ResizeEvents, Is.Empty,
                    "The window the protocol left still delivers its resizes to it.");
    }

    [Test]
    public void AClosedProtocolNoLongerHearsItsWindowResize()
    {
        // A resize reaching a protocol whose controls are disposed is a resize of a dead session.
        _protocol.InterfaceControl = HostProtocolInATab();
        _protocol.Close();
        _protocol.ResizeEvents.Clear();

        ResizeTheWindow();

        Assert.That(_protocol.ResizeEvents, Is.Empty,
                    "The window still delivers resizes to a protocol that has been closed.");
    }

    [Test]
    public void CloseClearsTheTabPagesReferenceToTheInterfaceControl()
    {
        // Initialize hangs the InterfaceControl on its tab page's Tag, which is how the window
        // finds the session behind a tab. A closed session left there is still found.
        InterfaceControl interfaceControl = HostProtocolInATab();
        Control tabPage = interfaceControl.Parent
                          ?? throw new AssertionException("The InterfaceControl is on no tab page.");
        _protocol.InterfaceControl = interfaceControl;
        Assume.That(_protocol.Initialize(), Is.True);
        Assume.That(tabPage.Tag, Is.SameAs(interfaceControl));

        _protocol.Close();

        Assert.That(tabPage.Tag, Is.Null, "The tab page still points at the closed session.");
    }

    [Test]
    public void InitializeNamesTheHostedControlAfterTheProtocol()
    {
        _protocol.InterfaceControl = HostProtocolInATab();

        Assume.That(_protocol.Initialize(), Is.True);

        Assert.That(_protocol.HostedControl.Name, Is.EqualTo("test"));
    }

    [Test]
    public void TheUiThreadInvokerMarshalsThroughTheConnectionWindow()
    {
        // The window outlives the InterfaceControl during a teardown, so it is the better of the
        // two to post the teardown through.
        _protocol.InterfaceControl = HostProtocolInATab();

        Assert.That(PrimaryOf(_protocol), Is.SameAs(_window));
    }

    [Test]
    public void WithoutAConnectionWindowTheUiThreadInvokerMarshalsThroughTheInterfaceControl()
    {
        using var looseParent = new Panel();
        var interfaceControl = new InterfaceControl(looseParent, _protocol, new ConnectionInfo { Name = "server1" });

        _protocol.InterfaceControl = interfaceControl;

        Assert.Multiple(() =>
        {
            Assert.That(_protocol.SubscribedWindow, Is.Null);
            Assert.That(PrimaryOf(_protocol), Is.SameAs(interfaceControl));
        });
    }

    [Test]
    public void SettingTheInterfaceControlAgainKeepsTheFirstUiThreadInvoker()
    {
        // The first set is the one known to run on the UI thread; see the setter's comment.
        _protocol.InterfaceControl = HostProtocolInATab();
        IUiThreadInvoker? first = _protocol.UiThreadInvoker;

        _protocol.InterfaceControl = HostProtocolInATab();

        Assert.That(_protocol.UiThreadInvoker, Is.SameAs(first));
    }

    [Test]
    public void SettingNoInterfaceControlCapturesNoUiThreadInvoker()
    {
        _protocol.InterfaceControl = null;

        Assert.That(_protocol.UiThreadInvoker, Is.Null,
                    "An invoker was captured with nothing to marshal through.");
    }

    private static Control PrimaryOf(ProtocolBase protocol)
    {
        return protocol.UiThreadInvoker is ControlUiThreadInvoker invoker
            ? invoker.PrimaryForTests
            : throw new AssertionException("Expected a ControlUiThreadInvoker, but was "
                                           + (protocol.UiThreadInvoker?.GetType().Name ?? "null") + ".");
    }

    private void ResizeTheWindow()
    {
        _window.Size = new System.Drawing.Size(_window.Width + 120, _window.Height + 80);
        Application.DoEvents();
    }
}
