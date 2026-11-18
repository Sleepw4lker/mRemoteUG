using System.ComponentModel;

using mRemoteUG.Connection;

using NUnit.Framework;


namespace mRemoteUG.Tests.Connection;

public class ConnectionInfoComparerTests
{
    private readonly ConnectionInfo _con1 = new() { Name = "a" };
    private readonly ConnectionInfo _con2 = new() { Name = "b" };

    [Test]
    public void SortAscendingOnName()
    {
        var comparer = new ConnectionInfoComparer<string>(node => node.Name)
        {
            SortDirection = ListSortDirection.Ascending
        };
        int compareReturn = comparer.Compare(_con1, _con2);
        Assert.That(compareReturn, Is.Negative);
    }

    [Test]
    public void SortDescendingOnName()
    {
        var comparer = new ConnectionInfoComparer<string>(node => node.Name)
        {
            SortDirection = ListSortDirection.Descending
        };
        int compareReturn = comparer.Compare(_con1, _con2);
        Assert.That(compareReturn, Is.Positive);
    }

    [TestCase(ListSortDirection.Ascending)]
    [TestCase(ListSortDirection.Descending)]
    public void NullsSortFirstWithoutReachingTheSortExpression(ListSortDirection direction)
    {
        var comparer = new ConnectionInfoComparer<string>(node => node.Name) { SortDirection = direction };

        Assert.Multiple(() =>
        {
            Assert.That(comparer.Compare(null, _con1), Is.Negative);
            Assert.That(comparer.Compare(_con1, null), Is.Positive);
            Assert.That(comparer.Compare(null, null), Is.Zero);
        });
    }
}
