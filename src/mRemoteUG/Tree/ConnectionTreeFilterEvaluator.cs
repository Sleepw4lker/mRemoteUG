using System.Collections.Generic;
using mRemoteUG.Connection;
using mRemoteUG.Container;

namespace mRemoteUG.Tree
{
    /// <summary>
    /// Works out which nodes a filter leaves visible. A node survives if it matches the
    /// filter itself or if any of its descendants does, so a match is never orphaned
    /// from the folders it lives in.
    /// </summary>
    public static class ConnectionTreeFilterEvaluator
    {
        public static HashSet<ConnectionInfo> VisibleNodes(IEnumerable<ConnectionInfo> roots,
                                                          IConnectionTreeNodeFilter filter)
        {
            var visible = new HashSet<ConnectionInfo>();
            if (roots == null)
                return visible;

            foreach (var root in roots)
                Evaluate(root, filter, visible);

            return visible;
        }

        /// <returns>True if <paramref name="node"/> or any descendant is visible.</returns>
        private static bool Evaluate(ConnectionInfo node,
                                     IConnectionTreeNodeFilter filter,
                                     ISet<ConnectionInfo> visible)
        {
            if (node == null)
                return false;

            var anyDescendantVisible = false;
            if (node is ContainerInfo container)
            {
                // evaluate every child, so the whole visible subtree is collected
                foreach (var child in container.Children)
                {
                    if (Evaluate(child, filter, visible))
                        anyDescendantVisible = true;
                }
            }

            var selfMatches = filter == null || filter.Filter(node);
            if (!selfMatches && !anyDescendantVisible)
                return false;

            visible.Add(node);
            return true;
        }
    }
}
