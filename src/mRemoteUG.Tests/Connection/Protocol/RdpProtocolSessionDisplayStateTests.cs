using System;
using System.Drawing;
using System.Reflection;
using System.Threading;
using mRemoteUG.Connection.Protocol.RDP;
using NUnit.Framework;

namespace mRemoteUG.Tests.Connection.Protocol
{
    /// <summary>
    /// A new session does not inherit the last one's state.
    /// </summary>
    /// <remarks>
    /// <see cref="RdpProtocol"/> remembers the size and scale it last asked a session for, so that
    /// a resize gesture - which raises Resize per frame - only talks to the session when something
    /// has actually changed. That cache is per-session state on an object that opens more than
    /// one session.
    /// <para>
    /// Left behind, it makes a resize pass decide there is nothing to do because the new session
    /// came up at the size the old one had. Note that neither automatic reconnect comes through
    /// <see cref="RdpProtocol.Connect"/> at all - <c>tmrReconnect_Elapsed</c> calls
    /// <c>_rdpClient.Connect()</c> directly, and mstscax's own <c>EnableAutoReconnect</c> never
    /// enters managed code - so this resets the state for a session opened afresh, and the
    /// display-scale passes deliberately do not rely on it.
    /// </para>
    /// <para>
    /// Only the forgetting is pinned here, not its consequence. Reaching the consequence needs a
    /// session to come up, and this machine has no route to an RDP server; the fields are private
    /// because nothing outside the class has any business reading them, so the test reads them the
    /// way <c>DpiChangeTests</c> reaches <c>RescaleConstantsForDpi</c> - by reflection.
    /// </para>
    /// </remarks>
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class RdpProtocolSessionDisplayStateTests
    {
        [Test]
        public void ConnectForgetsTheSizeAndScaleTheLastSessionWasAskedFor()
        {
            var protocol = new RdpProtocol();
            SetField(protocol, "_lastRequestedSessionSize", new Size(1920, 1080));
            SetField(protocol, "_lastRequestedSessionScale", (200u, 180u));

            ConnectAsFarAsThisMachineCan(protocol);

            Assert.Multiple(() =>
            {
                Assert.That(GetField(protocol, "_lastRequestedSessionSize"), Is.EqualTo(Size.Empty),
                            "The new session inherited the last one's size, so a display-scale pass " +
                            "at its size will decide there is nothing to do.");
                Assert.That(GetField(protocol, "_lastRequestedSessionScale"), Is.EqualTo((0u, 0u)),
                            "The new session inherited the last one's scale, so it will never be " +
                            "told what scale it is being viewed at.");
            });
        }

        [Test]
        public void ConnectForgetsWhyTheLastSessionSkippedItsResizes()
        {
            var protocol = new RdpProtocol();
            SetField(protocol, "_lastResizeSkipReason", "login is not complete");

            ConnectAsFarAsThisMachineCan(protocol);

            Assert.That(GetField(protocol, "_lastResizeSkipReason"), Is.Null,
                        "The skip reason is logged only on a transition, so carrying it over means " +
                        "the new session never logs why its resizes are being skipped.");
        }

        [Test]
        public void ConnectKeepsTheRememberedDpi()
        {
            var protocol = new RdpProtocol();
            protocol.NotifyDpiChanged(144);

            ConnectAsFarAsThisMachineCan(protocol);

            Assert.That(GetField(protocol, "_lastKnownDpi"), Is.EqualTo(144),
                        "Connect cleared the remembered DPI along with the per-session state. It is " +
                        "not per-session: the monitor does not change because a session did.");
        }

        [Test]
        public void ConnectForgetsThatTheLastSessionWasUp()
        {
            var protocol = new RdpProtocol();
            SetField(protocol, "_sessionConnected", true);

            ConnectAsFarAsThisMachineCan(protocol);

            Assert.That(GetField(protocol, "_sessionConnected"), Is.False,
                        "A session that has not reached OnConnected is being treated as up, so a " +
                        "DPI change arriving mid-connect would talk to a client that has no session.");
        }

        /// <summary>
        /// The one piece of display state that is deliberately <em>not</em> per-session.
        /// </summary>
        /// <remarks>
        /// Everything else in this fixture is state a new session must not inherit. The DPI is the
        /// opposite: it describes the monitor the window is on, which outlives any one session, and
        /// <c>tmrReconnect_Elapsed</c> needs it in order to declare the scale again before the
        /// reconnect it drives. That method runs on a timer thread and so cannot read
        /// <c>Control.DeviceDpi</c> for itself - without the remembered value a session that
        /// reconnected after the window had changed monitors would be re-created at the scale of the
        /// monitor the tab was opened on. See ADR-0028.
        /// </remarks>
        [Test]
        public void ADpiChangeIsRememberedForTheReconnectPath()
        {
            var protocol = new RdpProtocol();

            Assert.That(GetField(protocol, "_lastKnownDpi"), Is.EqualTo(RdpDisplayScale.DefaultDpi),
                        "the remembered DPI should start at the 100% baseline");

            protocol.NotifyDpiChanged(144);

            Assert.That(GetField(protocol, "_lastKnownDpi"), Is.EqualTo(144),
                        "a DPI change was not remembered, so a reconnect would declare the scale of " +
                        "whichever monitor the tab was opened on.");
        }

        /// <summary>
        /// Runs <see cref="RdpProtocol.Connect"/> up to the first call on the OCX.
        /// </summary>
        /// <remarks>
        /// No RDP control was ever created here, so every call on <c>_rdpClient</c> throws and
        /// Connect returns false having logged it. That is far enough: the per-session state is
        /// reset before the first of them, which is the whole of what this fixture is about. A real
        /// connect cannot be driven from this machine at all - see
        /// <see cref="RdpProtocolConnectingOverlayTests"/>.
        /// </remarks>
        private static void ConnectAsFarAsThisMachineCan(RdpProtocol protocol)
        {
            Assert.That(protocol.Connect(), Is.False,
                        "Connect claimed to have opened a connection without an RDP control. " +
                        "This fixture no longer exercises what it thinks it does.");
        }

        private static FieldInfo Field(string name)
        {
            var field = typeof(RdpProtocol).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"RdpProtocol has no field named {name} any more.");
            return field;
        }

        private static void SetField(RdpProtocol protocol, string name, object value) =>
            Field(name).SetValue(protocol, value);

        private static object GetField(RdpProtocol protocol, string name) =>
            Field(name).GetValue(protocol);
    }
}
