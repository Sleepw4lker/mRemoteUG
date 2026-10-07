using System;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using mRemoteUG.Connection;
using mRemoteUG.Connection.Protocol;
using mRemoteUG.Connection.Protocol.RDP;
using mRemoteUG.UI.Controls;
using mRemoteUG.UI.Window;
using NUnit.Framework;

namespace mRemoteUG.Tests.Connection.Protocol
{
    /// <summary>
    /// An RDP session never shows the OCX's own white connecting page.
    /// </summary>
    /// <remarks>
    /// The overlay has to be in place before the OCX is ever painted, which means during
    /// <see cref="RdpProtocol.Initialize"/> rather than at <c>Connect()</c>: Initialize pumps
    /// messages while it waits for the control's window handle, and the white page is drawn in
    /// that window.
    /// <para>
    /// There is no test here of a real connection attempt. This machine has no route to an RDP
    /// server, and a connect that neither succeeds nor fails promptly leaves a control mid-connect
    /// - ADR-0005 territory, and driving it from the suite hung the test host rather than proving
    /// anything. The one removal path reachable from here is the close.
    /// </para>
    /// </remarks>
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class RdpProtocolConnectingOverlayTests
    {
        private ConnectionWindow _window;
        private RdpProtocol _protocol;

        [SetUp]
        public void Setup()
        {
            _window = new ConnectionWindow("connecting overlay panel");
            _window.Show();
        }

        [TearDown]
        public void TearDown()
        {
            try { _protocol?.Close(); } catch (Exception) { /* the test may already have closed it */ }
            Pump();
            DeferredControlDisposal.ResetForTests();
            _protocol = null;
            _window?.Dispose();
            _window = null;
        }

        [Test]
        public void TheOverlayStandsInFrontOfTheRdpControlFromTheMomentItExists()
        {
            var interfaceControl = HostAnRdpSession("127.0.0.2");

            var overlay = interfaceControl.Controls.OfType<ConnectingOverlay>().SingleOrDefault();
            var rdpControl = interfaceControl.Controls.OfType<AxHost>().SingleOrDefault();

            Assert.Multiple(() =>
            {
                Assert.That(rdpControl, Is.Not.Null,
                            "No RDP control was hosted, so this test proves nothing.");
                Assert.That(overlay, Is.Not.Null,
                            "Nothing covers the RDP control, so the user sees the OCX's white page.");
                Assert.That(interfaceControl.Controls.GetChildIndex(overlay),
                            Is.LessThan(interfaceControl.Controls.GetChildIndex(rdpControl)),
                            "The overlay is behind the RDP control. A lower index is nearer the front.");
            });
        }

        [Test]
        public void ClosingASessionThatNeverConnectedTakesTheOverlayAway()
        {
            // The removal path this machine can actually drive. Nothing here has a route to an RDP
            // server, so a real connect cannot be tested from here: it neither succeeds nor fails
            // promptly, and closing a control that is still mid-connect is ADR-0005's minefield -
            // driving it from the test suite hung the test host rather than proving anything.
            // What the close path does share with every other way the wait ends is
            // HideTheConnectingOverlay, so that much is pinned here and the rest is logged rather
            // than asserted - see the "Connecting overlay" lines in mRemoteUG.log.
            var interfaceControl = HostAnRdpSession("127.0.0.2");
            Assert.That(interfaceControl.Controls.OfType<ConnectingOverlay>().Any(), Is.True,
                        "Nothing was covering the session, so this test proves nothing.");

            _protocol.Close();
            Pump();

            Assert.That(interfaceControl.Controls.OfType<ConnectingOverlay>().Any(), Is.False,
                        "The overlay outlived the session it was covering.");
        }

        private InterfaceControl HostAnRdpSession(string hostname)
        {
            var connectionInfo = new ConnectionInfo { Name = "server1", Hostname = hostname };
            var tabPage = _window.AddConnectionTab(connectionInfo);
            Assert.That(tabPage, Is.Not.Null, "AddConnectionTab returned no tab page.");

            _protocol = new RdpProtocol();
            var interfaceControl = new InterfaceControl(tabPage, _protocol, connectionInfo);
            _protocol.InterfaceControl = interfaceControl;

            Assert.That(_protocol.Initialize(), Is.True,
                        "RdpProtocol.Initialize() failed - no RDP ActiveX control could be created.");
            Pump();
            return interfaceControl;
        }

        private static void Pump()
        {
            for (var i = 0; i < 40; i++)
            {
                Application.DoEvents();
                Thread.Sleep(5);
            }
        }
    }
}
