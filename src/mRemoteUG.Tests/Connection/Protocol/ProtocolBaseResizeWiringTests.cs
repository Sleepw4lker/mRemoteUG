using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;
using mRemoteUG.Connection;
using mRemoteUG.Connection.Protocol;
using mRemoteUG.UI.Window;
using NUnit.Framework;

namespace mRemoteUG.Tests.Connection.Protocol
{
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
    [Apartment(ApartmentState.STA)]
    public class ProtocolBaseResizeWiringTests
    {
        private sealed class ResizeRecordingProtocol : ProtocolBase
        {
            public ResizeRecordingProtocol() : base("test")
            {
                Control = new Panel();
            }

            public readonly List<string> ResizeEvents = new List<string>();

            /// <summary>The window ProtocolBase decided to subscribe to, or null if it found none.</summary>
            public ConnectionWindow SubscribedWindow => ConnectionWindow;

            public override void ResizeBegin(object? sender, EventArgs e) => ResizeEvents.Add("begin");
            public override void Resize(object? sender, EventArgs e) => ResizeEvents.Add("resize");
            public override void ResizeEnd(object? sender, EventArgs e) => ResizeEvents.Add("end");

            protected override void CleanupProtocolResources() { }
        }

        private ConnectionWindow _window;
        private ResizeRecordingProtocol _protocol;

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
            _protocol = null;
            _window?.Dispose();
            _window = null;
        }

        /// <summary>
        /// Builds the hierarchy ConnectionInitiator builds:
        /// ConnectionWindow -> TabController -> ConnectionTabPage -> InterfaceControl.
        /// </summary>
        private InterfaceControl HostProtocolInATab()
        {
            var connectionInfo = new ConnectionInfo { Name = "server1" };
            var tabPage = _window.AddConnectionTab(connectionInfo);
            Assert.That(tabPage, Is.Not.Null, "AddConnectionTab returned no tab page.");

            return new InterfaceControl(tabPage, _protocol, connectionInfo);
        }

        [Test]
        public void TheTabPageIsParentedUnderTheConnectionWindow()
        {
            // Guards the premise of the test below: if this fails, the hierarchy itself changed.
            var interfaceControl = HostProtocolInATab();

            Control walker = interfaceControl;
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
            var interfaceControl = HostProtocolInATab();

            _protocol.InterfaceControl = interfaceControl;

            Assert.That(_protocol.SubscribedWindow, Is.SameAs(_window),
                        "ProtocolBase did not find the ConnectionWindow hosting it, so it subscribed to no " +
                        "resize events at all. Automatic RDP resizing depends on this subscription.");
        }

        [Test]
        public void ResizingTheConnectionWindowReachesTheProtocol()
        {
            // The symptom the user sees: the window is resized and the protocol never hears about it.
            var interfaceControl = HostProtocolInATab();
            _protocol.InterfaceControl = interfaceControl;
            _protocol.ResizeEvents.Clear();

            _window.Size = new System.Drawing.Size(_window.Width + 120, _window.Height + 80);
            Application.DoEvents();

            Assert.That(_protocol.ResizeEvents, Does.Contain("resize"),
                        "Resizing the ConnectionWindow did not reach the protocol.");
        }
    }
}
