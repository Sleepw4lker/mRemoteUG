using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;
using mRemoteUG.Connection.Protocol.RDP;
using MSTSCLib;
using NUnit.Framework;

namespace mRemoteUG.Tests.Connection.Protocol
{
    /// <summary>
    /// Pins the RDP control chain: what it will create, in what order, and that whatever it
    /// creates can actually be driven through the interface <see cref="RdpProtocol"/> uses.
    /// </summary>
    /// <remarks>
    /// The chain used to exist twice - here and hand-copied into <c>SelfTest.CheckRdpControl</c> -
    /// with nothing keeping them in step. Now there is one walk, and this is the test of it.
    /// <para>
    /// These tests activate a real ActiveX control, so they need STA and careful disposal: a live
    /// RDP control still alive at process exit takes the whole test run down with it (the same
    /// reason <c>RdpProtocolSessionShutdownTests</c> resets <c>DeferredControlDisposal</c>).
    /// </para>
    /// </remarks>
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class RdpClientCandidatesTests
    {
        private Form _host;

        [SetUp]
        public void Setup()
        {
            _host = new Form { Width = 640, Height = 480, ShowInTaskbar = false };
            _host.Show();
        }

        [TearDown]
        public void TearDown()
        {
            _host?.Dispose();
            _host = null;
        }

        [Test]
        public void TheFloorIsV11()
        {
            // The application is Windows 11 only, and Windows 11 always registers v11. Anything
            // older would only be selectable on an OS this build does not support.
            Assert.That(RdpClientCandidates.NewestFirst, Has.Length.EqualTo(2));

            var names = new List<string>();
            foreach (var factory in RdpClientCandidates.NewestFirst)
            {
                var control = factory();
                names.Add(control.GetType().Name);
                control.Dispose();
            }

            Assert.Multiple(() =>
            {
                Assert.That(names[0], Is.EqualTo("AxMsRdpClient12NotSafeForScripting"), "newest must be tried first.");
                Assert.That(names[1], Is.EqualTo("AxMsRdpClient11NotSafeForScripting"), "v11 is the floor.");
                Assert.That(names, Has.None.Contains("Client8").And.None.Contains("Client9").And.None.Contains("Client10"));
            });
        }

        [Test]
        public void EveryCandidateIsAccountedForInTheAttemptList()
        {
            // The attempt list is what a remote tester reads back, so "v12 unavailable, v11
            // created" has to be visible rather than inferred from silence. It stops at the first
            // success, so the count is bounded by the chain length and at least one.
            var attempts = new List<string>();
            var control = RdpClientCandidates.CreateNewest(_host, "test", DockStyle.Fill, attempts);

            try
            {
                Assert.That(control, Is.Not.Null, "no RDP ActiveX control could be created on this machine.");
                Assert.Multiple(() =>
                {
                    Assert.That(attempts, Is.Not.Empty);
                    Assert.That(attempts, Has.Count.LessThanOrEqualTo(RdpClientCandidates.NewestFirst.Length));
                    Assert.That(attempts[0], Does.StartWith("AxMsRdpClient12NotSafeForScripting"),
                                "the newest candidate must be reported whether or not it worked.");
                    Assert.That(attempts[attempts.Count - 1], Does.EndWith(": created"));
                });
            }
            finally
            {
                Dispose(control);
            }
        }

        [Test]
        public void TheNewestAvailableControlCanBeDrivenThroughIMsRdpClient10()
        {
            // This is the QueryInterface that RdpProtocol.Initialize performs on every connection.
            // Proving it here means it is answered at `dotnet test` time on the dev machine,
            // rather than only on a machine the binaries were copied to.
            var attempts = new List<string>();
            var control = RdpClientCandidates.CreateNewest(_host, "test", DockStyle.Fill, attempts);

            try
            {
                Assert.That(control, Is.Not.Null, "no RDP ActiveX control could be created on this machine.");

                var ocx = control.GetOcx();
                MsRdpClient11NotSafeForScripting client = null;
                Assert.DoesNotThrow(() => client = (MsRdpClient11NotSafeForScripting)ocx,
                                    $"{control.GetType().Name} would not answer QueryInterface(IID_IMsRdpClient10).");

                Assert.Multiple(() =>
                {
                    Assert.That(client, Is.InstanceOf<IMsRdpClient10>());
                    Assert.That(Version.TryParse(client.Version, out _), Is.True,
                                $"mstscax reported an unparseable version: '{client.Version}'.");
                });
            }
            finally
            {
                Dispose(control);
            }
        }

        private void Dispose(Control control)
        {
            if (control == null) return;
            _host.Controls.Remove(control);
            control.Dispose();
        }
    }
}
