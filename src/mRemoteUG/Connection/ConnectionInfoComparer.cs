using System.ComponentModel;


namespace mRemoteUG.Connection;

public class ConnectionInfoComparer<TProperty> : IComparer<ConnectionInfo> where TProperty : IComparable<TProperty>
{
    private readonly Func<ConnectionInfo, TProperty> _sortExpression;
    public ListSortDirection SortDirection { get; set; } = ListSortDirection.Ascending;

    public ConnectionInfoComparer(Func<ConnectionInfo, TProperty> sortExpression)
    {
        _sortExpression = sortExpression;
    }

    public int Compare(ConnectionInfo? x, ConnectionInfo? y)
    {
        // IComparer<T> allows nulls; order them first, as Comparer<T>.Default does, rather than
        // hand them to a sort expression that would dereference them.
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x == null)
        {
            return -1;
        }

        if (y == null)
        {
            return 1;
        }

        return SortDirection == ListSortDirection.Ascending ? CompareAscending(x, y) : CompareDescending(x, y);
    }

    private int CompareAscending(ConnectionInfo x, ConnectionInfo y)
    {
        return _sortExpression(x).CompareTo(_sortExpression(y));
    }

    private int CompareDescending(ConnectionInfo x, ConnectionInfo y)
    {
        return _sortExpression(y).CompareTo(_sortExpression(x));
    }
}
