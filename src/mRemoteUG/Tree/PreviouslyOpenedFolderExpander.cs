using mRemoteUG.Container;
using mRemoteUG.Tree.Root;
using mRemoteUG.UI.Controls;


namespace mRemoteUG.Tree;

public class PreviouslyOpenedFolderExpander : IConnectionTreePostSetupAction
{
    public void Execute(IConnectionTree connectionTree)
    {
        RootNodeInfo rootNode = connectionTree.GetRootConnectionNode();
        IEnumerable<ContainerInfo> containerList = ConnectionTreeModel.GetRecursiveChildList(rootNode)
            .OfType<ContainerInfo>();
        IEnumerable<ContainerInfo> previouslyExpandedNodes = containerList.Where(container => container.IsExpanded);
        connectionTree.ExpandedObjects = previouslyExpandedNodes;
        connectionTree.InvokeRebuildAll(true);
    }
}
