using mRemoteUG.Connection;


namespace mRemoteUG.Tree;

public class NodeSearcher
{
    private readonly ConnectionTreeModel _connectionTreeModel;

    private List<ConnectionInfo> Matches { get; set; } = new List<ConnectionInfo>();
    public ConnectionInfo? CurrentMatch { get; private set; }


    public NodeSearcher(ConnectionTreeModel connectionTreeModel)
    {
        _connectionTreeModel = connectionTreeModel;
    }

    public IEnumerable<ConnectionInfo> SearchByName(string searchText)
    {
        ResetMatches();
        if (searchText == "")
        {
            return Matches;
        }

        IReadOnlyList<ConnectionInfo> nodes = _connectionTreeModel.GetRecursiveChildList();
        // See ConnectionTreeSearchTextFilter: OrdinalIgnoreCase rather than lower-casing
        // every field of every node, which allocated three strings per node and folded
        // I to a dotless i under a Turkish culture.
        foreach (ConnectionInfo node in nodes)
        {
            if (node.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                node.Description.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                node.Hostname.Contains(searchText, StringComparison.OrdinalIgnoreCase))
            {
                Matches.Add(node);
            }
        }
        if (Matches.Count > 0)
        {
            CurrentMatch = Matches.First();
        }

        return Matches;
    }

    public ConnectionInfo? NextMatch()
    {
        int currentMatchIndex = CurrentMatchIndex();
        if (!CurrentMatchIsTheLastMatchInTheList())
        {
            CurrentMatch = Matches[currentMatchIndex + 1];
        }

        return CurrentMatch;
    }

    private bool CurrentMatchIsTheLastMatchInTheList()
    {
        int currentMatchIndex = CurrentMatchIndex();
        return currentMatchIndex >= Matches.Count - 1;
    }

    public ConnectionInfo? PreviousMatch()
    {
        int currentMatchIndex = CurrentMatchIndex();
        if (!CurrentMatchIsTheFirstMatchInTheList())
        {
            CurrentMatch = Matches[currentMatchIndex - 1];
        }

        return CurrentMatch;
    }

    private bool CurrentMatchIsTheFirstMatchInTheList()
    {
        int currentMatchIndex = CurrentMatchIndex();
        return currentMatchIndex <= 0;
    }

    // No current match is "not in the list", which is what IndexOf answers for it too.
    private int CurrentMatchIndex()
    {
        return CurrentMatch == null ? -1 : Matches.IndexOf(CurrentMatch);
    }

    private void ResetMatches()
    {
        Matches = new List<ConnectionInfo>();
        CurrentMatch = null;
    }
}
