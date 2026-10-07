using System.Threading;
using System.Windows.Forms;
using mRemoteUG.Connection;
using mRemoteUG.Connection.Protocol;
using mRemoteUG.Tools;
using mRemoteUG.UI.Window;
using NUnit.Framework;

namespace mRemoteUG.Tests.Connection.Protocol
{
    /// <summary>
    /// What a protocol stops holding once its Session is down.
    /// </summary>
    /// <remarks>
    /// The teardown disposes the right objects in the right order (ADR-0005) but has always kept
    /// its references to them afterwards. That matters because a protocol outlives its own
    /// teardown: the parked-control probe captures it, and nothing ever detaches the four
    /// <see cref="ConnectionInitiator"/> subscriptions. So whatever the protocol still points at is
    /// reachable for as long as any of those roots lives, disposed or not.
    /// </remarks>
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class ProtocolBaseTeardownReleaseTests
    {
        private sealed class QuietProtocol : ProtocolBase
        {
            public QuietProtocol() : base("quiet")
            {
                Control = new Panel();
            }

            public Control HostedControl => Control;

            public ReconnectGroup CurrentReconnectGroup => ReconnectGroup;

            public void Replace(ReconnectGroup next) => ReplaceReconnectGroup(next);

            protected override void CleanupProtocolResources()
            {
            }
        }

        private ConnectionWindow _window;

        [SetUp]
        public void Setup()
        {
            DeferredControlDisposal.ResetForTests();
            _window = new ConnectionWindow("teardown panel");
            _window.Show();
        }

        [TearDown]
        public void TearDown()
        {
            DeferredControlDisposal.ResetForTests();
            _window?.Dispose();
            _window = null;
        }

        [Test]
        public void TheTeardownDropsWhatItHasJustDisposed()
        {
            var protocol = HostAProtocol();
            var control = protocol.HostedControl;

            protocol.Close();
            Pump();

            Assume.That(control.IsDisposed, Is.True, "The control was parked, not disposed.");
            Assert.Multiple(() =>
            {
                Assert.That(protocol.HostedControl, Is.Null,
                            "The protocol still points at its disposed hosted control.");
                Assert.That(protocol.InterfaceControl, Is.Null,
                            "The protocol still points at its disposed InterfaceControl, and so at "
                            + "the whole control tree and ConnectionInfo behind it.");
            });
        }

        [Test]
        public void TheSessionLabelStillReadsAfterTheTeardownHasDroppedItsReferences()
        {
            // SessionLabel is derived from the InterfaceControl's Info and cached on first read.
            // TearDown reads it in stage 1, so nulling the field at the end is safe - but only
            // because of that caching, which is worth a test rather than a comment.
            var protocol = HostAProtocol();

            protocol.Close();
            Pump();

            Assert.That(protocol.LabelForTests, Does.Contain("server1"),
                        "The session label was lost with the InterfaceControl, so every log line "
                        + "after the teardown names the type instead of the host.");
        }

        [Test]
        public void ReplacingTheReconnectGroupDisposesTheOneItReplaces()
        {
            // RDPEvent_OnDisconnected builds one of these on every disconnect, each with a running
            // 200 ms animation timer, and only the successful-reconnect path disposed one. A
            // flapping server therefore stacked live timers on the hosted control.
            var protocol = HostAProtocol();
            var first = new ReconnectGroup();
            var second = new ReconnectGroup();

            protocol.Replace(first);
            protocol.Replace(second);

            Assert.Multiple(() =>
            {
                Assert.That(first.IsDisposed, Is.True,
                            "The replaced reconnect group was left alive with its timer running.");
                Assert.That(second.IsDisposed, Is.False, "The new one was disposed instead.");
                Assert.That(protocol.CurrentReconnectGroup, Is.SameAs(second));
            });

            protocol.Close();
            Pump();
        }

        [Test]
        public void TheTeardownDisposesAReconnectGroupThatIsStillUp()
        {
            // A Session closed while its reconnect countdown is on screen: the group is a child of
            // the hosted control, so disposing the control does reach it - but on the parked path
            // that dispose is up to two minutes away, and on a Session that never settles the
            // timer keeps ticking until then.
            var protocol = HostAProtocol();
            var group = new ReconnectGroup();
            protocol.Replace(group);

            protocol.Close();
            Pump();

            Assert.Multiple(() =>
            {
                Assert.That(group.IsDisposed, Is.True,
                            "The reconnect group outlived the teardown.");
                Assert.That(protocol.CurrentReconnectGroup, Is.Null,
                            "The protocol still points at the disposed reconnect group.");
            });
        }

        private QuietProtocol HostAProtocol()
        {
            var connectionInfo = new ConnectionInfo { Name = "server1" };
            var tabPage = _window.AddConnectionTab(connectionInfo);
            Assert.That(tabPage, Is.Not.Null, "AddConnectionTab returned no tab page.");

            var protocol = new QuietProtocol();
            protocol.InterfaceControl = new InterfaceControl(tabPage, protocol, connectionInfo);
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
