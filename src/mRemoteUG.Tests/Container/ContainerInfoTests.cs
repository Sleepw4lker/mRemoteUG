using System.Collections.Specialized;
using System.ComponentModel;

using mRemoteUG.Connection;
using mRemoteUG.Container;

using NUnit.Framework;


namespace mRemoteUG.Tests.Container;

public class ContainerInfoTests
{
    // Rebuilt by [SetUp] before each test, so null! rather than an initializer here;
    // see ADR-0022 on when that is the right answer.
    private ContainerInfo _containerInfo = null!;
    private ConnectionInfo _con1 = null!;
    private ConnectionInfo _con2 = null!;
    private ConnectionInfo _con3 = null!;

    [SetUp]
    public void Setup()
    {
        _containerInfo = new ContainerInfo();
        _con1 = new ConnectionInfo { Name = "a" };
        _con2 = new ConnectionInfo { Name = "b" };
        _con3 = new ConnectionInfo { Name = "c" };
    }

    [Test]
    public void AddSetsParentPropertyOnTheChild()
    {
        _containerInfo.AddChild(_con1);
        Assert.That(_con1.Parent, Is.EqualTo(_containerInfo));
    }

    [Test]
    public void AddAddsChildToChildrenList()
    {
        _containerInfo.AddChild(_con1);
        Assert.That(_containerInfo.Children, Does.Contain(_con1));
    }

    [Test]
    public void AddRangeAddsAllItems()
    {
        ConnectionInfo[] collection = new[] { _con1, _con2, _con3 };
        _containerInfo.AddChildRange(collection);
        Assert.That(_containerInfo.Children, Is.EquivalentTo(collection));
    }

    [Test]
    public void RemoveUnsetsParentPropertyOnChild()
    {
        _containerInfo.AddChild(_con1);
        _containerInfo.RemoveChild(_con1);
        Assert.That(_con1.Parent, Is.Not.EqualTo(_containerInfo));
    }

    [Test]
    public void RemoveRemovesChildFromChildrenList()
    {
        _containerInfo.AddChild(_con1);
        _containerInfo.RemoveChild(_con1);
        Assert.That(_containerInfo.Children, Does.Not.Contains(_con1));
    }

    [Test]
    public void RemoveRangeRemovesAllIndicatedItems()
    {
        ConnectionInfo[] collection = new[] { _con1, _con2, new ContainerInfo() };
        _containerInfo.AddChildRange(collection);
        _containerInfo.RemoveChildRange(collection);
        Assert.That(
            _containerInfo.Children,
            Does.Not.Contains(collection[0]).And.Not.Contains(collection[1]).And.Not.Contains(collection[2]));
    }

    [Test]
    public void RemoveRangeDoesNotRemoveUntargetedMembers()
    {
        ConnectionInfo[] collection = new[] { _con1, _con2, new ContainerInfo() };
        _containerInfo.AddChildRange(collection);
        _containerInfo.AddChild(_con3);
        _containerInfo.RemoveChildRange(collection);
        Assert.That(_containerInfo.Children, Does.Contain(_con3));
    }

    [Test]
    public void AddingChildTriggersCollectionChangedEvent()
    {
        bool wasCalled = false;
        _containerInfo.CollectionChanged += (sender, args) => wasCalled = true;
        _containerInfo.AddChild(_con1);
        Assert.That(wasCalled, Is.True);
    }

    [Test]
    public void RemovingChildTriggersCollectionChangedEvent()
    {
        bool wasCalled = false;
        _containerInfo.AddChild(_con1);
        _containerInfo.CollectionChanged += (sender, args) => wasCalled = true;
        _containerInfo.RemoveChild(_con1);
        Assert.That(wasCalled, Is.True);
    }

    [Test]
    public void ChangingChildPropertyTriggersPropertyChangedEvent()
    {
        bool wasCalled = false;
        _containerInfo.AddChild(_con1);
        _containerInfo.PropertyChanged += (sender, args) => wasCalled = true;
        _con1.Name = "somethinghere";
        Assert.That(wasCalled, Is.True);
    }

    [Test]
    public void ChangingSubChildPropertyTriggersPropertyChangedEvent()
    {
        bool wasCalled = false;
        var container2 = new ContainerInfo();
        _containerInfo.AddChild(container2);
        container2.AddChild(_con1);
        _containerInfo.PropertyChanged += (sender, args) => wasCalled = true;
        _con1.Name = "somethinghere";
        Assert.That(wasCalled, Is.True);
    }

    [Test]
    public void SetChildPositionPutsChildInCorrectPosition()
    {
        _containerInfo.AddChild(_con1);
        _containerInfo.AddChild(_con2);
        _containerInfo.AddChild(_con3);
        _containerInfo.SetChildPosition(_con2, 2);
        Assert.That(_containerInfo.Children.IndexOf(_con2), Is.EqualTo(2));
    }

    [Test]
    public void SettingChildPositionAboveArrayBoundsPutsItAtEndOfList()
    {
        _containerInfo.AddChild(_con1);
        _containerInfo.AddChild(_con2);
        _containerInfo.AddChild(_con3);
        int finalIndex = _containerInfo.Children.Count - 1;
        _containerInfo.SetChildPosition(_con2, 5);
        Assert.That(_containerInfo.Children.IndexOf(_con2), Is.EqualTo(finalIndex));
    }

    [Test]
    public void SettingChildPositionBelowArrayBoundsDoesNotMoveTheChild()
    {
        _containerInfo.AddChild(_con1);
        _containerInfo.AddChild(_con2);
        _containerInfo.AddChild(_con3);
        int originalIndex = _containerInfo.Children.IndexOf(_con2);
        _containerInfo.SetChildPosition(_con2, -1);
        Assert.That(_containerInfo.Children.IndexOf(_con2), Is.EqualTo(originalIndex));
    }

    [Test]
    public void SetChildAbovePutsChildInCorrectPosition()
    {
        _containerInfo.AddChild(_con1);
        _containerInfo.AddChild(_con2);
        _containerInfo.AddChild(_con3);
        int referenceChildIndexBeforeMove = _containerInfo.Children.IndexOf(_con2);
        _containerInfo.SetChildAbove(_con3, _con2);
        int targetsNewIndex = _containerInfo.Children.IndexOf(_con3);
        Assert.That(targetsNewIndex, Is.EqualTo(referenceChildIndexBeforeMove));
    }

    [Test]
    public void SetChildBelowPutsChildInCorrectPosition()
    {
        _containerInfo.AddChild(_con1);
        _containerInfo.AddChild(_con2);
        _containerInfo.AddChild(_con3);
        int referenceChildIndexBeforeMove = _containerInfo.Children.IndexOf(_con1);
        _containerInfo.SetChildBelow(_con3, _con1);
        int targetsNewIndex = _containerInfo.Children.IndexOf(_con3);
        Assert.That(targetsNewIndex, Is.EqualTo(referenceChildIndexBeforeMove + 1));
    }

    [Test]
    public void PromoteChildMovesTargetUpOne()
    {
        _containerInfo.AddChild(_con1);
        _containerInfo.AddChild(_con2);
        _containerInfo.AddChild(_con3);
        int targetsIndexBeforeMove = _containerInfo.Children.IndexOf(_con3);
        _containerInfo.PromoteChild(_con3);
        int targetsNewIndex = _containerInfo.Children.IndexOf(_con3);
        Assert.That(targetsNewIndex, Is.EqualTo(targetsIndexBeforeMove - 1));
    }

    [Test]
    public void PromoteChildDoesNothingWhenAlreadyAtTopOfList()
    {
        _containerInfo.AddChild(_con1);
        _containerInfo.AddChild(_con2);
        _containerInfo.AddChild(_con3);
        int targetsIndexBeforeMove = _containerInfo.Children.IndexOf(_con1);
        _containerInfo.PromoteChild(_con1);
        int targetsNewIndex = _containerInfo.Children.IndexOf(_con1);
        Assert.That(targetsNewIndex, Is.EqualTo(targetsIndexBeforeMove));
    }

    [Test]
    public void DemoteChildMovesTargetDownOne()
    {
        _containerInfo.AddChild(_con1);
        _containerInfo.AddChild(_con2);
        _containerInfo.AddChild(_con3);
        int targetsIndexBeforeMove = _containerInfo.Children.IndexOf(_con1);
        _containerInfo.DemoteChild(_con1);
        int targetsNewIndex = _containerInfo.Children.IndexOf(_con1);
        Assert.That(targetsNewIndex, Is.EqualTo(targetsIndexBeforeMove + 1));
    }

    [Test]
    public void DemoteChildDoesNothingWhenAlreadyAtTopOfList()
    {
        _containerInfo.AddChild(_con1);
        _containerInfo.AddChild(_con2);
        _containerInfo.AddChild(_con3);
        int targetsIndexBeforeMove = _containerInfo.Children.IndexOf(_con3);
        _containerInfo.DemoteChild(_con3);
        int targetsNewIndex = _containerInfo.Children.IndexOf(_con3);
        Assert.That(targetsNewIndex, Is.EqualTo(targetsIndexBeforeMove));
    }

    [Test]
    public void WhenChildAlreadyPresentAddChildAtDoesNothing()
    {
        _containerInfo.AddChild(_con1);
        _containerInfo.AddChild(_con2);
        _containerInfo.AddChild(_con3);
        int indexBeforeAttemptedMove = _containerInfo.Children.IndexOf(_con1);
        _containerInfo.AddChildAt(_con1, 2);
        int indexAfterAttemptedMove = _containerInfo.Children.IndexOf(_con1);
        Assert.That(indexAfterAttemptedMove, Is.EqualTo(indexBeforeAttemptedMove));
    }

    [Test]
    public void RemoveChildDoesNothingIfChildNotInList()
    {
        _containerInfo.AddChild(_con1);
        List<ConnectionInfo> childListBeforeRemoval = _containerInfo.Children;
        _containerInfo.RemoveChild(_con2);
        List<ConnectionInfo> childListAfterRemoval = _containerInfo.Children;
        Assert.That(childListAfterRemoval, Is.EquivalentTo(childListBeforeRemoval));
    }

    [Test]
    public void ClonedContainerHasNewConstantId()
    {
        ConnectionInfo clone = _containerInfo.Clone();
        Assert.That(clone.ConstantID, Is.Not.EqualTo(_containerInfo.ConstantID));
    }

    [Test]
    public void ClonedContainerDoesNotHaveParentSet()
    {
        _containerInfo.SetParent(new ContainerInfo());
        ConnectionInfo clone = _containerInfo.Clone();
        Assert.That(clone.Parent, Is.Null);
    }

    [Test]
    public void ClonedContainerContainsClonedChildren()
    {
        _containerInfo.AddChild(_con1);
        _containerInfo.AddChild(_con2);
        _containerInfo.AddChild(_con3);
        var clone = _containerInfo.Clone() as ContainerInfo;
        IEnumerable<string>? clonedChildNames = clone?.Children.Select((node) => node.Name);
        IEnumerable<string> originalChildNames = _containerInfo.Children.Select((node) => node.Name);
        Assert.That(clonedChildNames, Is.EquivalentTo(originalChildNames));
    }

    [Test]
    public void HasChildrenReturnsFalseForNoChildren()
    {
        bool hasChildren = _containerInfo.HasChildren();
        Assert.That(hasChildren, Is.False);
    }

    [Test]
    public void HasChildrenReturnsTrueWhenChildrenPresent()
    {
        _containerInfo.AddChild(_con1);
        bool hasChildren = _containerInfo.HasChildren();
        Assert.That(hasChildren, Is.True);
    }

    [Test]
    public void AddChildAbovePutsNewChildInCorrectLocation()
    {
        _containerInfo.AddChild(_con1);
        int referenceChildIndexBeforeInsertion = _containerInfo.Children.IndexOf(_con1);
        _containerInfo.AddChildAbove(_con2, _con1);
        int newChildIndex = _containerInfo.Children.IndexOf(_con2);
        Assert.That(newChildIndex, Is.EqualTo(referenceChildIndexBeforeInsertion));
    }

    [Test]
    public void AddChildAbovePutsNewChildAtEndOfListIfReferenceChildNotInList()
    {
        _containerInfo.AddChild(_con1);
        _containerInfo.AddChildAbove(_con2, _con3);
        int newChildIndex = _containerInfo.Children.IndexOf(_con2);
        int lastIndex = _containerInfo.Children.Count - 1;
        Assert.That(newChildIndex, Is.EqualTo(lastIndex));
    }

    [Test]
    public void AddChildBelowPutsNewChildInCorrectLocation()
    {
        _containerInfo.AddChild(_con1);
        _containerInfo.AddChild(_con2);
        int referenceChildIndexBeforeInsertion = _containerInfo.Children.IndexOf(_con1);
        _containerInfo.AddChildBelow(_con3, _con1);
        int newChildIndex = _containerInfo.Children.IndexOf(_con3);
        Assert.That(newChildIndex, Is.EqualTo(referenceChildIndexBeforeInsertion + 1));
    }

    [Test]
    public void AddChildBelowPutsNewChildAtEndOfListIfReferenceChildNotInList()
    {
        _containerInfo.AddChild(_con1);
        _containerInfo.AddChildBelow(_con2, _con3);
        int newChildIndex = _containerInfo.Children.IndexOf(_con2);
        int lastIndex = _containerInfo.Children.Count - 1;
        Assert.That(newChildIndex, Is.EqualTo(lastIndex));
    }

    [Test]
    public void SortAscendingSortsCorrectlyByName()
    {
        _containerInfo.AddChild(_con2);
        _containerInfo.AddChild(_con1);
        _containerInfo.AddChild(_con3);
        _containerInfo.Sort();
        ConnectionInfo[] orderAfterSort = _containerInfo.Children.ToArray();
        Assert.That(orderAfterSort, Is.Ordered.Ascending.By(nameof(ConnectionInfo.Name)));
    }

    [Test]
    public void SortDescendingSortsCorrectlyByName()
    {
        _containerInfo.AddChild(_con2);
        _containerInfo.AddChild(_con1);
        _containerInfo.AddChild(_con3);
        _containerInfo.Sort(ListSortDirection.Descending);
        ConnectionInfo[] orderAfterSort = _containerInfo.Children.ToArray();
        Assert.That(orderAfterSort, Is.Ordered.Descending.By(nameof(ConnectionInfo.Name)));
    }

    [Test]
    public void SortOnConstantIdAscendingSortsCorrectly()
    {
        _containerInfo.AddChild(_con2);
        _containerInfo.AddChild(_con1);
        _containerInfo.AddChild(_con3);
        _containerInfo.SortOn(node => node.ConstantID);
        ConnectionInfo[] orderAfterSort = _containerInfo.Children.ToArray();
        Assert.That(orderAfterSort, Is.Ordered.Ascending.By(nameof(ConnectionInfo.ConstantID)));
    }

    [Test]
    public void SortOnConstantIdDescendingSortsCorrectly()
    {
        _containerInfo.AddChild(_con2);
        _containerInfo.AddChild(_con1);
        _containerInfo.AddChild(_con3);
        _containerInfo.SortOn(node => node.ConstantID, ListSortDirection.Descending);
        ConnectionInfo[] orderAfterSort = _containerInfo.Children.ToArray();
        Assert.That(orderAfterSort, Is.Ordered.Descending.By(nameof(ConnectionInfo.ConstantID)));
    }

    [Test]
    public void SortAscendingRecursiveSortsGrandchildrenCorrectlyByName()
    {
        var childContainer = new ContainerInfo();
        childContainer.AddChild(_con2);
        childContainer.AddChild(_con1);
        childContainer.AddChild(_con3);
        _containerInfo.AddChild(childContainer);
        _containerInfo.SortRecursive();
        ConnectionInfo[] grandchildOrderAfterSort = childContainer.Children.ToArray();
        Assert.That(grandchildOrderAfterSort, Is.Ordered.Ascending.By(nameof(ConnectionInfo.Name)));
    }

    [Test]
    public void SortDescendingRecursiveSortsGrandchildrenCorrectlyByName()
    {
        var childContainer = new ContainerInfo();
        childContainer.AddChild(_con2);
        childContainer.AddChild(_con1);
        childContainer.AddChild(_con3);
        _containerInfo.AddChild(childContainer);
        _containerInfo.SortRecursive(ListSortDirection.Descending);
        ConnectionInfo[] grandchildOrderAfterSort = _containerInfo.Children.ToArray();
        Assert.That(grandchildOrderAfterSort, Is.Ordered.Descending.By(nameof(ConnectionInfo.Name)));
    }

    [Test]
    public void SortOnRecursiveConstantIdAscendingSortsGrandchildrenCorrectly()
    {
        var childContainer = new ContainerInfo();
        childContainer.AddChild(_con2);
        childContainer.AddChild(_con1);
        childContainer.AddChild(_con3);
        _containerInfo.AddChild(childContainer);
        _containerInfo.SortOnRecursive(node => node.ConstantID);
        ConnectionInfo[] grandchildOrderAfterSort = _containerInfo.Children.ToArray();
        Assert.That(grandchildOrderAfterSort, Is.Ordered.Ascending.By(nameof(ConnectionInfo.ConstantID)));
    }

    [Test]
    public void SortOnRecursiveConstantIdDescendingSortsGrandchildrenCorrectly()
    {
        var childContainer = new ContainerInfo();
        childContainer.AddChild(_con2);
        childContainer.AddChild(_con1);
        childContainer.AddChild(_con3);
        _containerInfo.AddChild(childContainer);
        _containerInfo.SortOnRecursive(node => node.ConstantID, ListSortDirection.Descending);
        ConnectionInfo[] grandchildOrderAfterSort = _containerInfo.Children.ToArray();
        Assert.That(grandchildOrderAfterSort, Is.Ordered.Descending.By(nameof(ConnectionInfo.ConstantID)));
    }

    [Test]
    public void SetChildPositionMovesAChildToTheTop()
    {
        _containerInfo.AddChildRange(new[] { _con1, _con2, _con3 });
        _containerInfo.SetChildPosition(_con3, 0);
        Assert.That(_containerInfo.Children, Is.EqualTo(new[] { _con3, _con1, _con2 }));
    }

    [Test]
    public void SetChildPositionRaisesAMoveEventWithBothIndices()
    {
        _containerInfo.AddChildRange(new[] { _con1, _con2, _con3 });
        var events = new List<NotifyCollectionChangedEventArgs>();
        _containerInfo.CollectionChanged += (sender, args) => events.Add(args);

        _containerInfo.SetChildPosition(_con3, 0);

        Assert.That(events, Has.Count.EqualTo(1));
        Assert.That(events[0].Action, Is.EqualTo(NotifyCollectionChangedAction.Move));
        Assert.That(events[0].OldStartingIndex, Is.EqualTo(2));
        Assert.That(events[0].NewStartingIndex, Is.EqualTo(0));
        Assert.That(events[0].NewItems, Is.EqualTo(new[] { _con3 }));
    }

    [Test]
    public void SetChildPositionToTheSameIndexChangesNothing()
    {
        _containerInfo.AddChildRange(new[] { _con1, _con2, _con3 });
        bool wasCalled = false;
        _containerInfo.CollectionChanged += (sender, args) => wasCalled = true;

        _containerInfo.SetChildPosition(_con2, 1);

        Assert.That(_containerInfo.Children, Is.EqualTo(new[] { _con1, _con2, _con3 }));
        Assert.That(wasCalled, Is.False);
    }

    [Test]
    public void SetChildPositionOfANodeThatIsNotAChildChangesNothing()
    {
        _containerInfo.AddChildRange(new[] { _con1, _con2 });
        bool wasCalled = false;
        _containerInfo.CollectionChanged += (sender, args) => wasCalled = true;

        _containerInfo.SetChildPosition(_con3, 0);

        Assert.That(_containerInfo.Children, Is.EqualTo(new[] { _con1, _con2 }));
        Assert.That(wasCalled, Is.False);
    }

    [Test]
    public void SetChildAboveMovesAChildDownToJustAboveTheReference()
    {
        _containerInfo.AddChildRange(new[] { _con1, _con2, _con3 });
        _containerInfo.SetChildAbove(_con1, _con3);
        Assert.That(_containerInfo.Children, Is.EqualTo(new[] { _con2, _con1, _con3 }));
    }

    [Test]
    public void SetChildBelowMovesAChildUpToJustBelowTheReference()
    {
        _containerInfo.AddChildRange(new[] { _con1, _con2, _con3 });
        _containerInfo.SetChildBelow(_con3, _con1);
        Assert.That(_containerInfo.Children, Is.EqualTo(new[] { _con1, _con3, _con2 }));
    }

    [Test]
    public void SetChildAboveItselfDoesNotMoveTheChild()
    {
        _containerInfo.AddChildRange(new[] { _con1, _con2, _con3 });
        _containerInfo.SetChildAbove(_con2, _con2);
        Assert.That(_containerInfo.Children, Is.EqualTo(new[] { _con1, _con2, _con3 }));
    }

    [Test]
    public void SetChildBelowItselfDoesNotMoveTheChild()
    {
        _containerInfo.AddChildRange(new[] { _con1, _con2, _con3 });
        _containerInfo.SetChildBelow(_con2, _con2);
        Assert.That(_containerInfo.Children, Is.EqualTo(new[] { _con1, _con2, _con3 }));
    }

    [Test]
    public void RemovingANodeThatIsNotAChildRaisesNoEventAndKeepsItsParent()
    {
        var otherParent = new ContainerInfo();
        otherParent.AddChild(_con2);
        _containerInfo.AddChild(_con1);
        bool wasCalled = false;
        _containerInfo.CollectionChanged += (sender, args) => wasCalled = true;

        _containerInfo.RemoveChild(_con2);

        Assert.That(wasCalled, Is.False);
        Assert.That(_con2.Parent, Is.SameAs(otherParent));
    }

    [Test]
    public void ARemovedChildsPropertyChangesNoLongerReachItsOldParent()
    {
        _containerInfo.AddChild(_con1);
        _containerInfo.RemoveChild(_con1);
        bool wasCalled = false;
        _containerInfo.PropertyChanged += (sender, args) => wasCalled = true;

        _con1.Name = "changed after removal";

        Assert.That(wasCalled, Is.False);
    }

    [Test]
    public void ARemovedFoldersCollectionChangesNoLongerReachItsOldParent()
    {
        var folder = new ContainerInfo();
        _containerInfo.AddChild(folder);
        _containerInfo.RemoveChild(folder);
        bool wasCalled = false;
        _containerInfo.CollectionChanged += (sender, args) => wasCalled = true;

        folder.AddChild(_con1);

        Assert.That(wasCalled, Is.False);
    }

    [Test]
    public void SortRaisesAResetEvent()
    {
        _containerInfo.AddChildRange(new[] { _con2, _con1 });
        var actions = new List<NotifyCollectionChangedAction>();
        _containerInfo.CollectionChanged += (sender, args) => actions.Add(args.Action);

        _containerInfo.Sort();

        Assert.That(actions, Is.EqualTo(new[] { NotifyCollectionChangedAction.Reset }));
    }

    [Test]
    public void ACloneKeepsTheFoldersOwnProperties()
    {
        _containerInfo.Name = "folder";
        _containerInfo.Hostname = "host.example";
        _containerInfo.Description = "a description";

        ConnectionInfo clone = _containerInfo.Clone();

        Assert.That(clone.Name, Is.EqualTo("folder"));
        Assert.That(clone.Hostname, Is.EqualTo("host.example"));
        Assert.That(clone.Description, Is.EqualTo("a description"));
    }

    [Test]
    public void GetRecursiveChildListIncludesEveryLevelBelow()
    {
        var child = new ContainerInfo { Name = "child" };
        var grandchild = new ContainerInfo { Name = "grandchild" };
        _containerInfo.AddChild(child);
        child.AddChild(grandchild);
        child.AddChild(_con1);
        grandchild.AddChild(_con2);
        _containerInfo.AddChild(_con3);

        Assert.That(_containerInfo.GetRecursiveChildList(),
                    Is.EqualTo(new ConnectionInfo[] { child, grandchild, _con2, _con1, _con3 }));
    }
}
