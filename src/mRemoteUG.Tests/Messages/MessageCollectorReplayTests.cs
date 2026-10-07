#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using mRemoteUG.Messages;
using NUnit.Framework;

namespace mRemoteUG.Tests.Messages
{
    /// <summary>
    /// The message writers are not built until frmMain_Load, so everything reported during startup
    /// arrived before there was anywhere to put it. It was collected and then never looked at
    /// again, which made the log begin part-way through startup no matter how the user had
    /// configured it.
    /// </summary>
    [TestFixture]
    public class MessageCollectorReplayTests
    {
        // Rebuilt by [SetUp] before each test, so null! rather than an initializer here;
        // see ADR-0022 on when that is the right answer.
        private MessageCollector _collector = null!;
        private List<string> _written = null!;
        private NotifyCollectionChangedEventHandler _handler = null!;

        [SetUp]
        public void Setup()
        {
            _collector = new MessageCollector();
            _written = new List<string>();
            _handler = (o, args) =>
            {
                if (args.NewItems == null) return;
                foreach (IMessage message in args.NewItems)
                    lock (_written) _written.Add(message.Text);
            };
        }

        [Test]
        public void MessagesCollectedBeforeTheWritersExistAreReplayed()
        {
            _collector.AddMessage(MessageClass.InformationMsg, "before one");
            _collector.AddMessage(MessageClass.InformationMsg, "before two");

            _collector.SubscribeAndReplay(_handler);

            Assert.That(_written, Is.EqualTo(new[] { "before one", "before two" }));
        }

        [Test]
        public void AMessageAddedAfterSubscribingIsWrittenExactlyOnce()
        {
            _collector.AddMessage(MessageClass.InformationMsg, "before");
            _collector.SubscribeAndReplay(_handler);

            _collector.AddMessage(MessageClass.InformationMsg, "after");

            Assert.That(_written, Is.EqualTo(new[] { "before", "after" }));
        }

        [Test]
        public void SubscribingWithAnEmptyBacklogRaisesNothing()
        {
            _collector.SubscribeAndReplay(_handler);

            Assert.That(_written, Is.Empty);
        }

        [Test]
        public void SubscribingWithoutAHandlerIsRejected()
        {
            // The null is the case under test; ! says so rather than widening the parameter.
            Assert.That(() => _collector.SubscribeAndReplay(null!), Throws.ArgumentNullException);
        }

        /// <summary>
        /// Protocol callbacks report from their own threads, so a message can be added at the exact
        /// moment the writers are being wired up. Snapshotting the backlog and subscribing happen
        /// under one lock so that such a message is either in the backlog or delivered by the
        /// event, but never neither.
        /// </summary>
        [Test]
        public void AMessageAddedFromAnotherThreadWhileSubscribingIsNotLost()
        {
            for (var iteration = 0; iteration < 200; iteration++)
            {
                var collector = new MessageCollector();
                var seen = new List<string>();
                var gate = new object();
                var ready = new ManualResetEventSlim(false);

                collector.AddMessage(MessageClass.InformationMsg, "existing");

                var adder = Task.Run(() =>
                {
                    ready.Wait();
                    collector.AddMessage(MessageClass.InformationMsg, "racing");
                });

                ready.Set();
                collector.SubscribeAndReplay((o, args) =>
                {
                    if (args.NewItems == null) return;
                    foreach (IMessage message in args.NewItems)
                        lock (gate) seen.Add(message.Text);
                });

                adder.Wait();

                // The racing message may arrive in the backlog or through the event, but it must
                // arrive, and it must not arrive twice.
                lock (gate)
                {
                    Assert.That(seen.Count(t => t == "existing"), Is.EqualTo(1),
                                $"iteration {iteration}: the existing message was delivered {seen.Count(t => t == "existing")} times.");
                    Assert.That(seen.Count(t => t == "racing"), Is.EqualTo(1),
                                $"iteration {iteration}: the racing message was delivered {seen.Count(t => t == "racing")} times.");
                }
            }
        }
    }
}
