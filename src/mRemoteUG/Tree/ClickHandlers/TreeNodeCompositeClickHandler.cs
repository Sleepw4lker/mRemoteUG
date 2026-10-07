using System;
using System.Collections.Generic;
using mRemoteUG.Connection;


namespace mRemoteUG.Tree
{
    public class TreeNodeCompositeClickHandler : ITreeNodeClickHandler<ConnectionInfo>
    {
        public IEnumerable<ITreeNodeClickHandler<ConnectionInfo>> ClickHandlers { get; set; } = new ITreeNodeClickHandler<ConnectionInfo>[0];

        public void Execute(ConnectionInfo clickedNode)
        {
            if (clickedNode == null)
                throw new ArgumentNullException(nameof(clickedNode));
            foreach (var handler in ClickHandlers)
            {
                handler.Execute(clickedNode);
            }
        }
    }
}