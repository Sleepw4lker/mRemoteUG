using System.Linq;
using mRemoteUG.Container;
using mRemoteUG.UI.Controls;


namespace mRemoteUG.Tree
{
    public class PreviouslyOpenedFolderExpander : IConnectionTreeDelegate
    {
        public void Execute(IConnectionTree connectionTree)
        {
            var rootNode = connectionTree.GetRootConnectionNode();
            var containerList = connectionTree.ConnectionTreeModel.GetRecursiveChildList(rootNode).OfType<ContainerInfo>();
            var previouslyExpandedNodes = containerList.Where(container => container.IsExpanded);
            connectionTree.ExpandedObjects = previouslyExpandedNodes;
            connectionTree.InvokeRebuildAll(true);
        }
    }
}