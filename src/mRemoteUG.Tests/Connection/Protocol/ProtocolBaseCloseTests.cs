using System;
using System.Collections.Generic;
using System.Windows.Forms;
using mRemoteUG.Connection;
using mRemoteUG.Connection.Protocol;
using mRemoteUG.UI;
using NUnit.Framework;

namespace mRemoteUG.Tests.Connection.Protocol
{
    /// <summary>
    /// Guards the teardown ordering that fixes the multi-second freeze on RDP session close.
    /// </summary>
    /// <remarks>
    /// The ActiveX release itself can't be exercised here, so what's pinned instead is the
    /// ordering it depends on: the hosted control must be disposed while it is still parented and
    /// its handle is alive, which means before the Closed event removes the tab page out from
    /// under it. Get that order wrong and the OCX is released outside its own apartment, which is
    /// what made logging off hang the UI.
    /// </remarks>
    [TestFixture]
    public class ProtocolBaseCloseTests
    {
        private const string CleanedUp = "protocol resources cleaned up";
        private const string ControlDisposed = "hosted control disposed";
        private const string ClosedRaised = "closed event raised";
        private const string InterfaceDisposed = "interface control disposed";

        /// <summary>Stands in for the real UI thread: everything runs inline, on this thread.</summary>
        private sealed class ImmediateUiThreadInvoker : IUiThreadInvoker
        {
            public int PostCount { get; private set; }

            public bool OnUiThread => true;

            public void Post(Action action)
            {
                PostCount++;
                action();
            }

            public void Send(Action action) => action();
        }

        private sealed class RecordingProtocol : ProtocolBase
        {
            public readonly List<string> Events;

            public RecordingProtocol(Control hostedControl, List<string> sharedLog = null, string tag = "")
            {
                Events = sharedLog ?? new List<string>();
                Tag = tag;
                Control = hostedControl;
                hostedControl.Disposed += (s, e) => Events.Add(Tag + ControlDisposed);
            }

            public string Tag { get; }

            public int CleanupCallCount { get; private set; }

            /// <summary>Runs inside the teardown, to simulate re-entrancy from a pumped message.</summary>
            public Action DuringCleanup { get; set; }

            protected override void CleanupProtocolResources()
            {
                CleanupCallCount++;
                Events.Add(Tag + CleanedUp);
                DuringCleanup?.Invoke();
            }
        }

        private Panel _tabPageStandIn;
        private Panel _hostedControl;
        private RecordingProtocol _sut;
        private ImmediateUiThreadInvoker _invoker;

        [SetUp]
        public void Setup()
        {
            _tabPageStandIn = new Panel();
            _hostedControl = new Panel();

            _sut = new RecordingProtocol(_hostedControl)
            {
                InterfaceControl = null
            };

            var interfaceControl = new InterfaceControl(_tabPageStandIn, _sut, new ConnectionInfo());
            interfaceControl.Disposed += (s, e) => _sut.Events.Add(InterfaceDisposed);
            _sut.InterfaceControl = interfaceControl;

            _invoker = new ImmediateUiThreadInvoker();
            _sut.UiThreadInvoker = _invoker;

            _sut.Closed += sender => _sut.Events.Add(ClosedRaised);
        }

        [TearDown]
        public void Teardown()
        {
            _tabPageStandIn?.Dispose();
            _hostedControl?.Dispose();
        }

        [Test]
        public void DisposesTheHostedControlBeforeRaisingClosed()
        {
            _sut.Close();

            Assert.That(_sut.Events.IndexOf(ControlDisposed),
                Is.LessThan(_sut.Events.IndexOf(ClosedRaised)),
                "The hosted control must be disposed before Closed removes the tab page, or the "
                + "ActiveX is released outside its own apartment.");
        }

        [Test]
        public void ReleasesProtocolResourcesBeforeDisposingTheHostedControl()
        {
            _sut.Close();

            Assert.That(_sut.Events.IndexOf(CleanedUp),
                Is.LessThan(_sut.Events.IndexOf(ControlDisposed)));
        }

        [Test]
        public void DisposesTheInterfaceControlAfterRaisingClosed()
        {
            _sut.Close();

            Assert.That(_sut.Events.IndexOf(ClosedRaised),
                Is.LessThan(_sut.Events.IndexOf(InterfaceDisposed)),
                "Closed handlers locate the tab via InterfaceControl.Parent, so the interface "
                + "control has to outlive the event.");
        }

        [Test]
        public void RunsTheFullTeardownInOrder()
        {
            _sut.Close();

            Assert.That(_sut.Events,
                Is.EqualTo(new[] { CleanedUp, ControlDisposed, ClosedRaised, InterfaceDisposed }));
        }

        [Test]
        public void ClosingTwiceTearsDownOnce()
        {
            _sut.Close();
            _sut.Close();

            Assert.Multiple(() =>
            {
                Assert.That(_sut.CleanupCallCount, Is.EqualTo(1));
                Assert.That(_sut.Events.FindAll(e => e == ClosedRaised), Has.Count.EqualTo(1));
            });
        }

        [Test]
        public void RunsInlineWhenAlreadyOnTheUiThread()
        {
            _sut.Close();

            Assert.That(_invoker.PostCount, Is.Zero,
                "Callers that are about to destroy the window need a completed teardown, not a "
                + "posted callback the dying message loop would drop.");
        }

        [Test]
        public void MarshalsToTheUiThreadWhenCalledFromElsewhere()
        {
            var offThreadInvoker = new OffUiThreadInvoker();
            _sut.UiThreadInvoker = offThreadInvoker;

            _sut.Close();

            Assert.That(offThreadInvoker.PostCount, Is.EqualTo(1));
        }

        [Test]
        public void DoesNotNestOneTeardownInsideAnother()
        {
            // Releasing an ActiveX control pumps messages, so another tab's posted teardown can be
            // dispatched mid-teardown. That must not interleave.
            var log = new List<string>();
            var firstControl = new Panel();
            var secondControl = new Panel();

            var second = new RecordingProtocol(secondControl, log, "second:")
            {
                UiThreadInvoker = new ImmediateUiThreadInvoker()
            };

            var first = new RecordingProtocol(firstControl, log, "first:")
            {
                UiThreadInvoker = new ImmediateUiThreadInvoker()
            };

            first.Closed += sender => log.Add("first:" + ClosedRaised);
            second.Closed += sender => log.Add("second:" + ClosedRaised);

            // The nested close arrives while the first is still tearing down.
            first.DuringCleanup = () => second.Close();

            try
            {
                first.Close();

                Assert.That(log, Does.Not.Contain("second:" + CleanedUp),
                    "The second teardown ran inside the first. A deferred teardown now waits for an "
                    + "idle turn of the message loop, so that it cannot start inside another "
                    + "session's mstscax call.");

                // Standing in for the idle turn, which no message loop is running here to provide.
                ProtocolBase.DrainQueuedTeardownsNow();

                var firstFinished = log.IndexOf("first:" + ClosedRaised);
                var secondStarted = log.IndexOf("second:" + CleanedUp);

                Assert.Multiple(() =>
                {
                    Assert.That(secondStarted, Is.GreaterThan(firstFinished),
                        "The second teardown must wait for the first to finish, not run inside it.");
                    Assert.That(log, Does.Contain("second:" + ControlDisposed),
                        "The deferred teardown still has to run.");
                });
            }
            finally
            {
                firstControl.Dispose();
                secondControl.Dispose();
            }
        }

        private sealed class OffUiThreadInvoker : IUiThreadInvoker
        {
            public int PostCount { get; private set; }

            public bool OnUiThread => false;

            public void Post(Action action)
            {
                PostCount++;
                action();
            }

            public void Send(Action action) => action();
        }
    }
}
