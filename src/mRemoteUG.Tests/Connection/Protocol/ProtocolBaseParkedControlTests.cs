using System.Threading;
using System.Windows.Forms;
using mRemoteUG.App;
using mRemoteUG.Connection;
using mRemoteUG.Connection.Protocol;
using mRemoteUG.UI.Window;
using NUnit.Framework;

namespace mRemoteUG.Tests.Connection.Protocol
{
    /// <summary>
    /// Pins what happens when a hosted control is not safe to dispose during the close.
    /// </summary>
    /// <remarks>
    /// Disposing an RDP control that mstscax has not finished with blocks inside the release, on
    /// the UI thread, and a wedged UI thread takes the whole desktop with it - a session with
    /// redirected keys owns a low-level keyboard hook that Windows has to call on that thread. So
    /// such a control must not be disposed here.
    ///
    /// It must not simply be dropped either. A live RDP control that is still undisposed when the
    /// process exits crashes it, and so does one that has merely been unparented - each isolated
    /// down to a single variable. So the control is parked instead: moved straight from the dying
    /// tab into an off-screen holding form, never parentless, and disposed on a later idle turn
    /// once its session has gone down.
    ///
    /// Two things therefore matter here and are asserted together: the control survives the close,
    /// and it leaves the <see cref="InterfaceControl"/> that is about to be disposed. Miss the
    /// second and the InterfaceControl's dispose cascades into the control and blocks after all.
    /// </remarks>
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class ProtocolBaseParkedControlTests
    {
        private sealed class StubbornProtocol : ProtocolBase
        {
            private readonly bool _canBeDisposed;

            public StubbornProtocol(bool canBeDisposed) : base("stubborn")
            {
                _canBeDisposed = canBeDisposed;
                Control = new Panel();
            }

            // Combined with the base, exactly as RdpProtocol does it: the protocol can only
            // withhold consent, never grant it over the base class's shutdown check.
            protected override bool HostedControlCanBeDisposed => base.HostedControlCanBeDisposed && _canBeDisposed;

            public Control HostedControl => Control;

            protected override void CleanupProtocolResources()
            {
            }
        }

        private ConnectionWindow _window;
        private InterfaceControl _interfaceControl;

        [SetUp]
        public void Setup()
        {
            _window = new ConnectionWindow("parking panel");
            _window.Show();
        }

        [TearDown]
        public void TearDown()
        {
            ApplicationLifecycle.IsShuttingDown = false;
            DeferredControlDisposal.ResetForTests();
            _window?.Dispose();
            _window = null;
        }

        [Test]
        public void NothingIsDisposedInlineOnceTheApplicationIsShuttingDown()
        {
            // Every dispose during the close of the main window is another chance to block with the
            // UI still up. They are parked and disposed once the message loop has finished instead.
            var protocol = HostAProtocol(canBeDisposed: true);
            var control = protocol.HostedControl;
            ApplicationLifecycle.IsShuttingDown = true;

            protocol.Close();
            Pump();

            AssertParked(control);
        }

        [Test]
        public void AControlThatIsNotReadyIsParkedRatherThanDisposed()
        {
            var protocol = HostAProtocol(canBeDisposed: false);
            var control = protocol.HostedControl;

            protocol.Close();
            Pump();

            AssertParked(control);
        }

        [Test]
        public void AControlThatIsReadyIsStillDisposedInline()
        {
            // The guard above must not turn into "never dispose anything here", which would push
            // every session through the parking area and make the common case slower for nothing.
            var protocol = HostAProtocol(canBeDisposed: true);
            var control = protocol.HostedControl;

            protocol.Close();
            Pump();

            Assert.Multiple(() =>
            {
                Assert.That(control.IsDisposed, Is.True,
                            "A control that reported itself ready must still be disposed inline.");
                Assert.That(DeferredControlDisposal.Count, Is.Zero, "Nothing should have been parked.");
            });
        }

        private void AssertParked(Control control)
        {
            Assert.Multiple(() =>
            {
                Assert.That(control.IsDisposed, Is.False,
                            "The control was disposed anyway. Disposing one that is not ready is what "
                            + "blocks the UI thread.");
                Assert.That(control.Parent, Is.Not.Null,
                            "The control was left parentless. An unparented live RDP control crashes the "
                            + "process at exit just as an undisposed one does.");
                Assert.That(control.Parent, Is.Not.EqualTo(_interfaceControl),
                            "The control was left in the InterfaceControl, which is disposed two stages "
                            + "later and would cascade straight into the release we just declined to do.");
                Assert.That(DeferredControlDisposal.Count, Is.EqualTo(1),
                            "The control was not handed over for disposal later, so nothing will ever "
                            + "dispose it.");
            });
        }

        private StubbornProtocol HostAProtocol(bool canBeDisposed)
        {
            var connectionInfo = new ConnectionInfo { Name = "server1" };
            var tabPage = _window.AddConnectionTab(connectionInfo);
            Assert.That(tabPage, Is.Not.Null, "AddConnectionTab returned no tab page.");

            var protocol = new StubbornProtocol(canBeDisposed);
            _interfaceControl = new InterfaceControl(tabPage, protocol, connectionInfo);
            protocol.InterfaceControl = _interfaceControl;
            Assert.That(protocol.Initialize(), Is.True, "ProtocolBase.Initialize() failed.");
            return protocol;
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
