using System.Windows.Forms;
using mRemoteUG.Connection;
using mRemoteUG.Tree;
using NUnit.Framework;

namespace mRemoteUG.Tests.Tree
{
    /// <summary>
    /// The drag payload must survive being handed back as a plain <see cref="DataObject"/> rather
    /// than as the original instance. That is the shape the receiving side sees once WinForms has
    /// round-tripped the payload through OLE, and it is the case that used to fall back on
    /// BinaryFormatter.
    /// </summary>
    [TestFixture]
    public class ConnectionInfoDataObjectTests
    {
        private ConnectionInfo _connection;

        [SetUp]
        public void Setup()
        {
            _connection = new ConnectionInfo { Name = "server1" };
        }

        /// <summary>
        /// Reads the token with the typed API. DataObject.GetData(string) is obsolete from
        /// .NET 10 onward, so the tests exercise the same route production code takes.
        /// </summary>
        private static string GetToken(ConnectionInfoDataObject payload)
        {
            Assert.That(payload.TryGetData(ConnectionInfoDataObject.Format, out string token), Is.True);
            return token;
        }

        [Test]
        public void RecognisesItsOwnInstance()
        {
            var payload = new ConnectionInfoDataObject(_connection);
            try
            {
                Assert.That(ConnectionInfoDataObject.TryGetConnections(payload, out var models), Is.True);
                Assert.That(models, Is.EqualTo(new[] { _connection }));
            }
            finally { payload.Release(); }
        }

        [Test]
        public void ResolvesConnectionsFromAPlainDataObjectCarryingOnlyTheToken()
        {
            var payload = new ConnectionInfoDataObject(_connection);
            try
            {
                // What the drop target sees: only the format and its string token survived.
                var roundTripped = new DataObject();
                roundTripped.SetData(ConnectionInfoDataObject.Format,
                                     GetToken(payload));

                Assert.That(ConnectionInfoDataObject.TryGetConnections(roundTripped, out var models), Is.True);
                Assert.That(models, Is.EqualTo(new[] { _connection }));
            }
            finally { payload.Release(); }
        }

        [Test]
        public void StoresOnlyAStringOnTheDataObject()
        {
            // Anything richer than a string would be serialised when the payload crosses the OLE
            // boundary, which is exactly what must not happen.
            var payload = new ConnectionInfoDataObject(_connection);
            try
            {
                Assert.That(GetToken(payload), Is.InstanceOf<string>());
            }
            finally { payload.Release(); }
        }

        [Test]
        public void DoesNotResolveOnceTheDragHasFinished()
        {
            var payload = new ConnectionInfoDataObject(_connection);
            var token = GetToken(payload);
            payload.Release();

            var stale = new DataObject();
            stale.SetData(ConnectionInfoDataObject.Format, token);

            Assert.That(ConnectionInfoDataObject.TryGetConnections(stale, out var models), Is.False);
            Assert.That(models, Is.Null);
        }

        [Test]
        public void ReturnsFalseForAnUnrelatedDataObject()
        {
            var unrelated = new DataObject();
            unrelated.SetData(DataFormats.Text, "not a connection");

            Assert.That(ConnectionInfoDataObject.TryGetConnections(unrelated, out var models), Is.False);
            Assert.That(models, Is.Null);
        }

        [Test]
        public void ReturnsFalseForNull()
        {
            Assert.That(ConnectionInfoDataObject.TryGetConnections(null, out var models), Is.False);
            Assert.That(models, Is.Null);
        }

        [Test]
        public void IgnoresNullConnections()
        {
            var payload = new ConnectionInfoDataObject(null, _connection, null);
            try
            {
                Assert.That(ConnectionInfoDataObject.TryGetConnections(payload, out var models), Is.True);
                Assert.That(models, Is.EqualTo(new[] { _connection }));
            }
            finally { payload.Release(); }
        }
    }
}
