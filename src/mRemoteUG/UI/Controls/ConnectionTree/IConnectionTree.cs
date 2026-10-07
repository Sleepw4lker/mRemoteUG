using System.Collections;
using mRemoteUG.Connection;
using mRemoteUG.Tree;
using mRemoteUG.Tree.Root;


namespace mRemoteUG.UI.Controls
{
    public interface IConnectionTree
    {
        ConnectionTreeModel ConnectionTreeModel { get; set; }

        ConnectionInfo SelectedConnection { get; }

        IEnumerable ExpandedObjects { get; set; }

        RootNodeInfo GetRootConnectionNode();

        void InvokeExpand(object model);

        void InvokeRebuildAll(bool preserveState);

        void ToggleExpansion(object model);
    }
}