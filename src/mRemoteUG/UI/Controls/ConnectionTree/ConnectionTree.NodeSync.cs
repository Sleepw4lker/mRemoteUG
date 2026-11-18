using System.Collections.Specialized;

using mRemoteUG.Connection;
using mRemoteUG.Container;
using mRemoteUG.Tree;

namespace mRemoteUG.UI.Controls;

/// <summary>
/// Keeps the TreeNode tree in step with the connection model. ObjectListView did
/// this itself through CanExpandGetter/ChildrenGetter; a TreeView holds real nodes,
/// so the tree is built eagerly and then patched in place as the model changes.
/// </summary>
public partial class ConnectionTree
{
    /// <summary>
    /// Model to node, and the authoritative answer to "is this model shown".
    /// ConnectionInfo does not override Equals, so reference identity is used.
    /// </summary>
    private readonly Dictionary<ConnectionInfo, TreeNode> _nodeMap =
        new Dictionary<ConnectionInfo, TreeNode>();

    private bool _suppressExpansionSync;
    private ConnectionInfo[]? _explicitExpandedObjects;

    #region Building

    private TreeNode CreateNode(ConnectionInfo model)
    {
        var node = new TreeNode { Tag = model };
        ApplyNodeState(node, model);
        _nodeMap[model] = node;
        return node;
    }

    /// <summary>
    /// Refreshes everything about a node that the model can change.
    /// </summary>
    private void ApplyNodeState(TreeNode node, ConnectionInfo model)
    {
        node.Text = model.Name;
        string imageKey = _statusImageList.GetKey(model);
        node.ImageKey = imageKey;
        node.SelectedImageKey = imageKey;
        node.ToolTipText = Settings.Default.ShowDescriptionTooltipsInTree ? model.Description : "";
    }

    /// <summary>
    /// Builds a node and its descendants. When <paramref name="visible"/> is given,
    /// nodes outside that set are skipped.
    /// </summary>
    private TreeNode BuildSubtree(ConnectionInfo model, ICollection<ConnectionInfo>? visible)
    {
        TreeNode node = CreateNode(model);

        if (model is ContainerInfo container)
        {
            foreach (ConnectionInfo child in container.Children)
            {
                if (visible != null && !visible.Contains(child))
                {
                    continue;
                }

                node.Nodes.Add(BuildSubtree(child, visible));
            }
        }

        return node;
    }

    private void RemoveSubtree(ConnectionInfo model)
    {
        if (!_nodeMap.TryGetValue(model, out TreeNode? node))
        {
            return;
        }

        ForgetSubtree(model);
        node.Remove();
    }

    private void ForgetSubtree(ConnectionInfo model)
    {
        _nodeMap.Remove(model);
        if (!(model is ContainerInfo container))
        {
            return;
        }

        foreach (ConnectionInfo child in container.Children)
        {
            ForgetSubtree(child);
        }
    }

    /// <summary>
    /// Rebuilds one container's children in place, used for sorts and for the PuTTY
    /// subtree, which has no collection events of its own.
    /// </summary>
    private void RebuildChildren(ContainerInfo parent)
    {
        if (parent == null || !_nodeMap.TryGetValue(parent, out TreeNode? parentNode))
        {
            return;
        }

        HashSet<ConnectionInfo>? visible = CurrentVisibleSet();

        BeginUpdate();
        _suppressExpansionSync = true;
        try
        {
            foreach (ConnectionInfo child in parent.Children)
            {
                ForgetSubtree(child);
            }

            parentNode.Nodes.Clear();

            foreach (ConnectionInfo child in parent.Children)
            {
                if (visible != null && !visible.Contains(child))
                {
                    continue;
                }

                parentNode.Nodes.Add(BuildSubtree(child, visible));
            }

            ApplyExpansion(visible);
        }
        finally
        {
            _suppressExpansionSync = false;
            EndUpdate();
        }

        // the selected node may have been one of the children just cleared
        ApplyMenuRulesToSelection();
    }

    /// <summary>
    /// Rebuilds the whole tree, optionally restoring selection and scroll position.
    /// </summary>
    private void RebuildAll(bool preserveState)
    {
        if (ConnectionTreeModel == null)
        {
            return;
        }

        ConnectionInfo? selected = preserveState ? SelectedConnection : null;
        ConnectionInfo? topModel = preserveState ? TopNode?.Tag as ConnectionInfo : null;
        HashSet<ConnectionInfo>? visible = CurrentVisibleSet();

        BeginUpdate();
        _suppressExpansionSync = true;
        try
        {
            Nodes.Clear();
            _nodeMap.Clear();

            foreach (ContainerInfo root in ConnectionTreeModel.RootNodes)
            {
                if (visible != null && !visible.Contains(root))
                {
                    continue;
                }

                Nodes.Add(BuildSubtree(root, visible));
            }

            ApplyExpansion(visible);
        }
        finally
        {
            _suppressExpansionSync = false;
            EndUpdate();
        }

        if (selected != null && _nodeMap.TryGetValue(selected, out TreeNode? selectedNode))
        {
            SelectedNode = selectedNode;
        }

        ApplyMenuRulesToSelection();

        // after the selection, which scrolls on its own
        if (topModel != null && _nodeMap.TryGetValue(topModel, out TreeNode? top))
        {
            TopNode = top;
        }
    }

    /// <summary>
    /// Applies expansion after a rebuild: an explicit request first, otherwise the
    /// model. While filtering, ancestors of matches are forced open so the results
    /// are actually reachable.
    /// </summary>
    private void ApplyExpansion(ICollection<ConnectionInfo>? visible)
    {
        ConnectionInfo[]? explicitlyExpanded = _explicitExpandedObjects;
        _explicitExpandedObjects = null;

        foreach (KeyValuePair<ConnectionInfo, TreeNode> pair in _nodeMap)
        {
            if (!(pair.Key is ContainerInfo container))
            {
                continue;
            }

            bool expand;
            if (explicitlyExpanded != null)
            {
                expand = explicitlyExpanded.Contains(container);
            }
            else
            {
                expand = container.IsExpanded;
            }

            if (visible != null && pair.Value.Nodes.Count > 0)
            {
                expand = true;
            }

            if (expand)
            {
                pair.Value.Expand();
            }
            else
            {
                pair.Value.Collapse();
            }
        }
    }

    #endregion

    #region Model events

    private void HandleCollectionChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        InvokeIfRequired(() => HandleCollectionChangedCore(sender, args));
    }

    private void HandleCollectionChangedCore(object? sender, NotifyCollectionChangedEventArgs args)
    {
        // Under a filter, one change can alter which ancestors stay visible, so the
        // whole filtered tree is recomputed instead of patched.
        if (IsFiltering)
        {
            UpdateFiltering();
            return;
        }

        if (sender is ConnectionTreeModel)
        {
            RebuildAll(true);
            return;
        }

        if (!(sender is ContainerInfo parent) || !_nodeMap.TryGetValue(parent, out TreeNode? parentNode))
        {
            return;
        }

        switch (args.Action)
        {
            case NotifyCollectionChangedAction.Add:
                InsertChildren(parent, parentNode, args);
                break;
            case NotifyCollectionChangedAction.Remove:
                RemoveChildren(args);
                break;
            case NotifyCollectionChangedAction.Move:
                MoveChild(parent, parentNode, args);
                break;
            default:
                RebuildChildren(parent);
                break;
        }
    }

    private void InsertChildren(ContainerInfo parent, TreeNode parentNode, NotifyCollectionChangedEventArgs args)
    {
        if (args.NewItems == null)
        {
            return;
        }

        BeginUpdate();
        _suppressExpansionSync = true;
        try
        {
            foreach (ConnectionInfo item in args.NewItems.OfType<ConnectionInfo>())
            {
                if (_nodeMap.ContainsKey(item))
                {
                    continue;
                }

                // AddChildAt/RemoveChild raise their events without an index
                int index = parent.Children.IndexOf(item);
                TreeNode node = BuildSubtree(item, null);
                if (index < 0 || index > parentNode.Nodes.Count)
                {
                    parentNode.Nodes.Add(node);
                }
                else
                {
                    parentNode.Nodes.Insert(index, node);
                }

                ApplyExpansionToSubtree(item);
            }
        }
        finally
        {
            _suppressExpansionSync = false;
            EndUpdate();
        }
    }

    private void RemoveChildren(NotifyCollectionChangedEventArgs args)
    {
        if (args.OldItems == null)
        {
            return;
        }

        BeginUpdate();
        try
        {
            foreach (ConnectionInfo item in args.OldItems.OfType<ConnectionInfo>())
            {
                RemoveSubtree(item);
            }
        }
        finally
        {
            EndUpdate();
        }
    }

    private void MoveChild(ContainerInfo parent, TreeNode parentNode, NotifyCollectionChangedEventArgs args)
    {
        ConnectionInfo? moved = args.NewItems?.OfType<ConnectionInfo>().FirstOrDefault();
        if (moved == null)
        {
            return;
        }

        bool wasSelected = ReferenceEquals(SelectedConnection, moved);

        BeginUpdate();
        _suppressExpansionSync = true;
        try
        {
            RemoveSubtree(moved);

            int index = args.NewStartingIndex >= 0 ? args.NewStartingIndex : parent.Children.IndexOf(moved);
            TreeNode node = BuildSubtree(moved, null);
            if (index < 0 || index > parentNode.Nodes.Count)
            {
                parentNode.Nodes.Add(node);
            }
            else
            {
                parentNode.Nodes.Insert(index, node);
            }

            ApplyExpansionToSubtree(moved);
        }
        finally
        {
            _suppressExpansionSync = false;
            EndUpdate();
        }

        // rebuilding the subtree drops the selection, which a drag-reorder should keep
        if (wasSelected)
        {
            SelectObject(moved);
        }
    }

    /// <summary>
    /// Restores IsExpanded across a freshly built subtree.
    /// </summary>
    private void ApplyExpansionToSubtree(ConnectionInfo model)
    {
        if (!(model is ContainerInfo container))
        {
            return;
        }

        if (_nodeMap.TryGetValue(model, out TreeNode? node))
        {
            if (container.IsExpanded)
            {
                node.Expand();
            }
            else
            {
                node.Collapse();
            }
        }

        foreach (ConnectionInfo child in container.Children)
        {
            ApplyExpansionToSubtree(child);
        }
    }

    #endregion
}
