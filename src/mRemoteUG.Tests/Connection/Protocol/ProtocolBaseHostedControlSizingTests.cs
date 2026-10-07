using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using mRemoteUG.Connection;
using mRemoteUG.Connection.Protocol.RDP;
using mRemoteUG.UI.Window;
using NUnit.Framework;

namespace mRemoteUG.Tests.Connection.Protocol
{
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
    public class ProtocolBaseHostedControlSizingTests
    {
        private ConnectionWindow _window;
        private RdpProtocol _protocol;

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
            _window = null;
        }

        [Test]
        public void TheRdpControlStillFillsItsInterfaceControlAfterTheWindowIsResized()
        {
            var connectionInfo = new ConnectionInfo { Name = "server1", Hostname = "127.0.0.2" };
            var tabPage = _window.AddConnectionTab(connectionInfo);
            Assert.That(tabPage, Is.Not.Null, "AddConnectionTab returned no tab page.");

            _protocol = new RdpProtocol();
            var interfaceControl = new InterfaceControl(tabPage, _protocol, connectionInfo);
            _protocol.InterfaceControl = interfaceControl;

            Assert.That(_protocol.Initialize(), Is.True,
                        "RdpProtocol.Initialize() failed - no RDP ActiveX control could be created.");
            Pump();

            // OfType rather than the single child it used to be: the InterfaceControl now also
            // holds the themed connecting overlay (see ConnectingOverlay).
            var rdpControl = interfaceControl.Controls.OfType<AxHost>().SingleOrDefault();
            Assert.That(rdpControl, Is.Not.Null, "No control was hosted in the InterfaceControl.");

            var sizeBefore = rdpControl.Size;

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
