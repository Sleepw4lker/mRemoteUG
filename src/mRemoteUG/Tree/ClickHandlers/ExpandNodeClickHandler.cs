using System;
using mRemoteUG.Connection;
using mRemoteUG.Container;
using mRemoteUG.UI.Controls;


namespace mRemoteUG.Tree
{
    public class ExpandNodeClickHandler : ITreeNodeClickHandler<ConnectionInfo>
    {
        private readonly IConnectionTree _connectionTree;

        public ExpandNodeClickHandler(IConnectionTree connectionTree)
        {
            if (connectionTree == null)
                throw new ArgumentNullException(nameof(connectionTree));

            _connectionTree = connectionTree;
        }

        public void Execute(ConnectionInfo clickedNode)
        {
            var clickedNodeAsContainer = clickedNode as ContainerInfo;
            if (clickedNodeAsContainer == null) return;
            _connectionTree.ToggleExpansion(clickedNodeAsContainer);
        }
    }
}