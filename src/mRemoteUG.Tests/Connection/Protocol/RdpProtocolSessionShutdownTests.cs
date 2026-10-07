using System;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using mRemoteUG.Connection;
using mRemoteUG.Connection.Protocol;
using mRemoteUG.Connection.Protocol.RDP;
using mRemoteUG.UI.Window;
using NUnit.Framework;

namespace mRemoteUG.Tests.Connection.Protocol
{
    /// <summary>
    /// Pins that an RDP control is never disposed while its session is still up.
    /// </summary>
    /// <remarks>
    /// Closing a tab whose session is still live is what freezes the application. The log from the
    /// target machine separates the two populations exactly:
    ///
    /// <list type="bullet">
    /// <item>Ten sessions that had already disconnected before Close ran (Connected=0) disposed in
    /// 44-73 ms each, every one of them.</item>
    /// <item>Five sessions closed by middle-clicking a live tab (Connected=1) disposed in 72-124 ms
    /// four times, and on the fifth blocked the UI thread inside the dispose for 47.5 seconds.</item>
    /// </list>
    ///
    /// The close handshake does not separate them, because it is not asking the same question.
    /// <c>RequestClose()</c> answers <c>controlCloseWaitForEvents</c> and the control then raises
    /// <c>OnConfirmClose</c> - "yes, go ahead and close me" - which is not a disconnect. Taking that
    /// as permission to destroy the control meant disposing one whose session teardown had barely
    /// started, and mstscax blocks inside the release until the network side finishes.
    ///
    /// So the handshake is necessary but not sufficient: the session has to actually be down. A
    /// session that will not go down within the budget has its control parked rather than disposed
    /// - see <see cref="DeferredControlDisposal"/> - and disposed on a later idle turn instead.
    ///
    /// This has to drive the real <see cref="RdpProtocol"/> - the whole defect lives in what it
    /// consults before deciding. Only the connection state is faked, because holding a real session
    /// up needs a server to hold it up, and the machine this runs on has no route to one.
    /// </remarks>
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class RdpProtocolSessionShutdownTests
    {
        /// <summary>A settled session that never reports itself gone.</summary>
        private sealed class SessionThatNeverGoesDown : RdpProtocol
        {
            protected override short QueryConnectedState() => 1;

            protected override bool LoginHasCompleted => true;

            public Control HostedControl => Control;
        }

        /// <summary>
        /// A session caught between OnConnected and OnLoginComplete - still negotiating licensing,
        /// authentication and the initial desktop. This is the tab closed moments after logon.
        /// </summary>
        private sealed class SessionMidLogon : RdpProtocol
        {
            public bool LoggedIn;
            public short Connected = 1;
            public int DisconnectCalls;

            protected override bool LoginHasCompleted => LoggedIn;

            protected override short QueryConnectedState() => Connected;

            protected override void DisconnectTheSession()
            {
                DisconnectCalls++;
                Connected = 0;
            }

            public Control HostedControl => Control;
        }

        /// <summary>An RDP control whose session is already gone, as on a disconnect-driven close.</summary>
        private sealed class SessionAlreadyDown : RdpProtocol
        {
            protected override short QueryConnectedState() => 0;

            public Control HostedControl => Control;
        }

        private ConnectionWindow _window;
        private TimeSpan _originalTimeout;

        [SetUp]
        public void Setup()
        {
            _originalTimeout = RdpProtocol.SessionDownTimeout;
            RdpProtocol.SessionDownTimeout = TimeSpan.FromMilliseconds(150);
            _window = new ConnectionWindow("session shutdown panel");
            _window.Show();
        }

        [TearDown]
        public void TearDown()
        {
            RdpProtocol.SessionDownTimeout = _originalTimeout;
            // Nothing may outlive the fixture: a live RDP control still alive at process exit
            // crashes the process, and would take the whole test run down with it.
            DeferredControlDisposal.ResetForTests();
            _window?.Dispose();
            _window = null;
        }

        [Test]
        public void AControlWhoseSessionIsStillUpIsNotDisposed()
        {
            var protocol = Host(new SessionThatNeverGoesDown());
            var control = protocol.HostedControl;

            protocol.Close();
            Pump();

            Assert.Multiple(() =>
            {
                Assert.That(control.IsDisposed, Is.False,
                            "The control was disposed while its session still reported itself connected. "
                            + "That is the dispose that blocked the UI thread for 47 seconds.");
                Assert.That(control.Parent, Is.Not.EqualTo(protocol.InterfaceControl),
                            "The control was left in the InterfaceControl, which is disposed two stages "
                            + "later and walks straight back into the release we just declined to do.");
                Assert.That(DeferredControlDisposal.Count, Is.EqualTo(1),
                            "The control was not handed over for disposal later, so it would leak - and "
                            + "a live RDP control reaching process exit crashes the process.");
            });
        }

        [Test]
        public void AControlWhoseSessionIsDownIsStillDisposed()
        {
            // The guard above must not turn into "never dispose anything", which is upstream's
            // behaviour and leaks every control the application has ever hosted. Every one of the
            // ten already-disconnected sessions in the log disposed cleanly, and must keep doing so.
            var protocol = Host(new SessionAlreadyDown());
            var control = protocol.HostedControl;

            protocol.Close();
            Pump();

            Assert.That(control.IsDisposed, Is.True,
                        "A session that has already gone down must still be disposed.");
        }

        [Test]
        public void WaitingForTheSessionToGoDownIsBounded()
        {
            // Whatever the outcome, the close has to come back. The wait pumps, so the application
            // stays responsive throughout it - unlike the dispose it exists to avoid.
            var protocol = Host(new SessionThatNeverGoesDown());
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            protocol.Close();

            Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(5)),
                        "Close() did not come back inside the budget it was given.");
        }

        [Test]
        public void ClosingMidLogonDoesNotInterruptTheSession()
        {
            // A session between OnConnected and OnLoginComplete is still negotiating. Telling it to
            // disconnect makes mstscax unwind a handshake that is still in flight, and it does that
            // on the calling thread - ours. Closing a tab moments after logon must therefore ask it
            // for nothing at all.
            var protocol = Host(new SessionMidLogon());
            var control = protocol.HostedControl;

            protocol.Close();
            Pump();

            Assert.Multiple(() =>
            {
                Assert.That(protocol.DisconnectCalls, Is.Zero,
                            "The teardown told a session that had not finished logging on to disconnect. "
                            + "That call is unbounded and runs on the UI thread.");
                Assert.That(control.IsDisposed, Is.False, "The control was disposed mid-logon.");
                Assert.That(DeferredControlDisposal.Count, Is.EqualTo(1),
                            "The control was neither disposed nor parked, so nothing will finish it.");
            });
        }

        [Test]
        public void AParkedMidLogonSessionIsDisconnectedOnceItHasSettled()
        {
            // The other half: "don't touch it yet" must not become "never touch it". Once the
            // session settles, the parked control is disconnected and disposed on an idle turn.
            var protocol = Host(new SessionMidLogon());
            var control = protocol.HostedControl;
            protocol.Close();
            Pump();

            DeferredControlDisposal.RunOneIdleTurnForTests();
            Assert.That(protocol.DisconnectCalls, Is.Zero, "Disconnected while still logging on.");

            protocol.LoggedIn = true;
            DeferredControlDisposal.RunOneIdleTurnForTests();

            Assert.Multiple(() =>
            {
                Assert.That(protocol.DisconnectCalls, Is.EqualTo(1),
                            "The settled session was never asked to disconnect.");
                Assert.That(control.IsDisposed, Is.True,
                            "The control was never disposed, so it leaks and crashes the process at exit.");
            });
        }

        [Test]
        public void ClosingMidLogonStillReturnsImmediately()
        {
            // The tab must close at once. Making the user wait for a logon to finish before their
            // click takes effect would be its own freeze, and would strand them on a session that
            // is stuck connecting.
            var protocol = Host(new SessionMidLogon());
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            protocol.Close();

            Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromMilliseconds(500)),
                        "Close() waited on a session that was still logging on.");
        }

        private T Host<T>(T protocol) where T : RdpProtocol
        {
            var connectionInfo = new ConnectionInfo { Name = "server1", Hostname = "127.0.0.2" };
            var tabPage = _window.AddConnectionTab(connectionInfo);
            Assert.That(tabPage, Is.Not.Null, "AddConnectionTab returned no tab page.");

            protocol.InterfaceControl = new InterfaceControl(tabPage, protocol, connectionInfo);
            Assert.That(protocol.Initialize(), Is.True,
                        "RdpProtocol.Initialize() failed - no RDP ActiveX control could be created.");
            Pump();

            Assert.That(protocol.InterfaceControl.Controls.OfType<AxHost>().SingleOrDefault(), Is.Not.Null,
                        "No control was hosted in the InterfaceControl, so this test proves nothing.");
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
