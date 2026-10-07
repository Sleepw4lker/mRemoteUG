using System;
using System.Collections.Generic;
using System.Linq;
using mRemoteUG.Connection;


namespace mRemoteUG.Tree
{
	public class NodeSearcher
    {
        private readonly ConnectionTreeModel _connectionTreeModel;

        private List<ConnectionInfo> Matches { get; set; }
        public ConnectionInfo CurrentMatch { get; private set; }


        public NodeSearcher(ConnectionTreeModel connectionTreeModel)
        {
            _connectionTreeModel = connectionTreeModel;
        }

        public IEnumerable<ConnectionInfo> SearchByName(string searchText)
        {
            ResetMatches();
            if (searchText == "") return Matches;
            var nodes = _connectionTreeModel.GetRecursiveChildList();
            // See ConnectionTreeSearchTextFilter: OrdinalIgnoreCase rather than lower-casing
            // every field of every node, which allocated three strings per node and folded
            // I to a dotless i under a Turkish culture.
            foreach (var node in nodes)
            {
                if (node.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                    node.Description.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                    node.Hostname.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                    Matches.Add(node);
            }
            if (Matches.Count > 0)
                CurrentMatch = Matches.First();
            return Matches;
        }

        public ConnectionInfo NextMatch()
        {
            var currentMatchIndex = Matches.IndexOf(CurrentMatch);
            if (!CurrentMatchIsTheLastMatchInTheList())
                CurrentMatch = Matches[currentMatchIndex + 1];
            return CurrentMatch;
        }

        private bool CurrentMatchIsTheLastMatchInTheList()
        {
            var currentMatchIndex = Matches.IndexOf(CurrentMatch);
            return currentMatchIndex >= Matches.Count - 1;
        }

        public ConnectionInfo PreviousMatch()
        {
            var currentMatchIndex = Matches.IndexOf(CurrentMatch);
            if (!CurrentMatchIsTheFirstMatchInTheList())
                CurrentMatch = Matches[currentMatchIndex - 1];
            return CurrentMatch;
        }

        private bool CurrentMatchIsTheFirstMatchInTheList()
        {
            var currentMatchIndex = Matches.IndexOf(CurrentMatch);
            return currentMatchIndex <= 0;
        }

        private void ResetMatches()
        {
            Matches = new List<ConnectionInfo>();
            CurrentMatch = null;
        }
    }
}