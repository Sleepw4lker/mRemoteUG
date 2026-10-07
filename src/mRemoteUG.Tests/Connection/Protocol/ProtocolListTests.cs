using System.Collections;
using System.Collections.Specialized;
using mRemoteUG.Connection.Protocol;
using NUnit.Framework;


namespace mRemoteUG.Tests.Connection.Protocol
{
    public class ProtocolListTests
    {
        // ProtocolBase has no abstract members; a minimal subclass avoids
        // RdpProtocol's dependency on the FrmMain singleton (STA/WinForms-only).
        private class DummyProtocol : ProtocolBase
        {
        }

        private ProtocolList _protocolList;
        private ProtocolBase _protocol1;
        private ProtocolBase _protocol2;
        private ProtocolBase _protocol3;


        [SetUp]
        public void Setup()
        {
            _protocolList = new ProtocolList();
            _protocol1 = new DummyProtocol();
            _protocol2 = new DummyProtocol();
            _protocol3 = new DummyProtocol();
        }

        [TearDown]
        public void Teardown()
        {
            _protocolList = null;
            _protocol1 = null;
            _protocol2 = null;
            _protocol3 = null;
        }

        [Test]
        public void EmptyWhenInitialized()
        {
            Assert.That(_protocolList.Count == 0);
        }

        [Test]
        public void AddAddsObjectToList()
        {
            _protocolList.Add(_protocol1);
            Assert.That(_protocolList[0] == _protocol1);
        }

        [Test]
        public void CountUpdatesToReflectCurrentList()
        {
            var protArray = new[] { _protocol1, _protocol2, _protocol3 };
            foreach (var prot in protArray)
                _protocolList.Add(prot);
            Assert.That(_protocolList.Count == protArray.Length);
        }

        [Test]
        public void RemoveRemovesObjectFromList()
        {
            _protocolList.Add(_protocol1);
            _protocolList.Remove(_protocol1);
            Assert.That(_protocolList.Count == 0);
        }

        [Test]
        public void ClearResetsList()
        {
            var protArray = new[] { _protocol1, _protocol2, _protocol3 };
            foreach (var prot in protArray)
                _protocolList.Add(prot);
            _protocolList.Clear();
            Assert.That(_protocolList.Count == 0);
        }

        [Test]
        public void IntIndexerReturnsCorrectObject()
        {
            var protArray = new[] { _protocol1, _protocol2, _protocol3 };
            foreach (var prot in protArray)
                _protocolList.Add(prot);
            Assert.That(_protocolList[1], Is.EqualTo(protArray[1]));
        }

        [Test]
        public void RemovingNonexistantObjectFromListDoesNothing()
        {
            Assert.DoesNotThrow(()=> _protocolList.Remove(_protocol1));
        }

        [Test]
        public void AddRaisesCollectionChangedEvent()
        {
            var eventWasCalled = false;
            _protocolList.CollectionChanged += (sender, args) => eventWasCalled = true;
            _protocolList.Add(_protocol1);
            Assert.That(eventWasCalled);
        }

        [Test]
        public void AddCollectionChangedEventContainsAddedObject()
        {
            IList nodeListFromEvent = new ArrayList();
            _protocolList.CollectionChanged += (sender, args) => nodeListFromEvent = args.NewItems;
            _protocolList.Add(_protocol1);
            Assert.That(nodeListFromEvent, Is.EquivalentTo(new[] {_protocol1}));
        }

        [Test]
        public void RemoveCollectionChangedEventContainsRemovedObject()
        {
            IList nodeListFromEvent = new ArrayList();
            _protocolList.Add(_protocol1);
            _protocolList.CollectionChanged += (sender, args) => nodeListFromEvent = args.OldItems;
            _protocolList.Remove(_protocol1);
            Assert.That(nodeListFromEvent, Is.EquivalentTo(new[] { _protocol1 }));
        }

        [Test]
        public void AttemptingToRemoveNonexistantObjectDoesNotRaiseCollectionChangedEvent()
        {
            var eventWasCalled = false;
            _protocolList.CollectionChanged += (sender, args) => eventWasCalled = true;
            _protocolList.Remove(_protocol1);
            Assert.That(eventWasCalled == false);
        }

        [Test]
        public void ClearRaisesCollectionChangedEvent()
        {
            var eventWasCalled = false;
            _protocolList.Add(_protocol1);
            _protocolList.CollectionChanged += (sender, args) => eventWasCalled = true;
            _protocolList.Clear();
            Assert.That(eventWasCalled);
        }

        [Test]
        public void ClearDoesntRaiseCollectionChangedEventWhenNoObjectsRemoved()
        {
            var eventWasCalled = false;
            _protocolList.CollectionChanged += (sender, args) => eventWasCalled = true;
            _protocolList.Clear();
            Assert.That(eventWasCalled == false);
        }

        [Test]
        public void AddCollectionChangedEventHasCorrectAction()
        {
            NotifyCollectionChangedAction collectionChangedAction = NotifyCollectionChangedAction.Move;
            _protocolList.CollectionChanged += (sender, args) => collectionChangedAction = args.Action;
            _protocolList.Add(_protocol1);
            Assert.That(collectionChangedAction, Is.EqualTo(NotifyCollectionChangedAction.Add));
        }

        [Test]
        public void RemoveCollectionChangedEventHasCorrectAction()
        {
            NotifyCollectionChangedAction collectionChangedAction = NotifyCollectionChangedAction.Move;
            _protocolList.Add(_protocol1);
            _protocolList.CollectionChanged += (sender, args) => collectionChangedAction = args.Action;
            _protocolList.Remove(_protocol1);
            Assert.That(collectionChangedAction, Is.EqualTo(NotifyCollectionChangedAction.Remove));
        }

        [Test]
        public void ClearCollectionChangedEventHasCorrectAction()
        {
            NotifyCollectionChangedAction collectionChangedAction = NotifyCollectionChangedAction.Move;
            _protocolList.Add(_protocol1);
            _protocolList.CollectionChanged += (sender, args) => collectionChangedAction = args.Action;
            _protocolList.Clear();
            Assert.That(collectionChangedAction, Is.EqualTo(NotifyCollectionChangedAction.Reset));
        }
    }
}