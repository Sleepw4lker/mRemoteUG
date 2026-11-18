using mRemoteUG.Tree.Root;
using mRemoteUG.UI.Controls;


namespace mRemoteUG.Tree;

public class RootNodeExpander : IConnectionTreePostSetupAction
{
    public void Execute(IConnectionTree connectionTree)
    {
        RootNodeInfo rootConnectionNode = connectionTree.GetRootConnectionNode();
        connectionTree.InvokeExpand(rootConnectionNode);
    }
}
