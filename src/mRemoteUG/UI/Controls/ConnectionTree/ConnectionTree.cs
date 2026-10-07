using mRemoteUG.App;
using mRemoteUG.Config.Putty;
using mRemoteUG.Connection;
using mRemoteUG.Container;
using mRemoteUG.Tree;
using mRemoteUG.Tree.Root;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
// ReSharper disable ArrangeAccessorOwnerBody

namespace mRemoteUG.UI.Controls
{
    public partial class ConnectionTree : TreeView, IConnectionTree
    {
        private readonly ConnectionTreeDragAndDropHandler _dragAndDropHandler = new ConnectionTreeDragAndDropHandler();
        private readonly PuttySessionsManager _puttySessionsManager = PuttySessionsManager.Instance;
	    private readonly StatusImageList _statusImageList = new StatusImageList();
        private readonly ConnectionTreeSearchTextFilter _connectionTreeSearchTextFilter = new ConnectionTreeSearchTextFilter();
        private bool _nodeInEditMode;
        private bool _allowEdit;
        private ConnectionContextMenu _contextMenu;
        private ConnectionTreeModel _connectionTreeModel;

        public ConnectionInfo SelectedConnection => SelectedNode?.Tag as ConnectionInfo;

        public NodeSearcher NodeSearcher { get; private set; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IConfirm<ConnectionInfo> NodeDeletionConfirmer { get; set; } = new AlwaysConfirmYes();

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IEnumerable<IConnectionTreeDelegate> PostSetupActions { get; set; } = new IConnectionTreeDelegate[0];

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ITreeNodeClickHandler<ConnectionInfo> DoubleClickHandler { get; set; } = new TreeNodeCompositeClickHandler();

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ITreeNodeClickHandler<ConnectionInfo> SingleClickHandler { get; set; } = new TreeNodeCompositeClickHandler();

        /// <summary>
        /// The model object behind the selected node, if any.
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ConnectionInfo SelectedObject
        {
            get { return SelectedConnection; }
            set { SelectObject(value); }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ConnectionTreeModel ConnectionTreeModel
        {
            get { return _connectionTreeModel; }
            set
            {
                if (_connectionTreeModel == value)
                    return;

                UnregisterModelUpdateHandlers(_connectionTreeModel);
                _connectionTreeModel = value;
                PopulateTreeView(value);
            }
        }

        public ConnectionTree()
        {
            InitializeComponent();
            SetupConnectionTreeView();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                components?.Dispose();
                _statusImageList?.Dispose();
                DisposeDragDrop();
            }
            base.Dispose(disposing);
        }


        #region ConnectionTree Setup
        /// <summary>The node indent at 96 DPI, which is what the designer used to hardcode.</summary>
        private const int LogicalIndent = 19;

        /// <summary>
        /// Keeps the node icons and the indent in step with the DPI the tree is being drawn at.
        /// </summary>
        /// <remarks>
        /// Re-assigning <see cref="TreeView.ImageList"/> is the part that matters for the icons: a
        /// TreeView re-measures its rows only when the property is set, not when the list it
        /// already holds changes size.
        /// <para>
        /// <see cref="TreeView.ItemHeight"/> is deliberately never set. Left alone it is derived
        /// from the font and the image list - measured at 18 for a 9pt font, 34 for 18pt, and 32
        /// when a 32px image list is attached - so it follows a DPI change for free. Set once, it
        /// sticks: the designer pinned it to 18 and rows stayed 18 pixels tall while the font grew
        /// to 36, which is what made the connection list look unscaled at 200%.
        /// </para>
        /// <para>
        /// <see cref="TreeView.Indent"/> is the opposite case and has to be done by hand. It is
        /// derived from nothing, and measured it does not move for either a font change or a DPI
        /// change.
        /// </para>
        /// </remarks>
        protected override void RescaleConstantsForDpi(int deviceDpiOld, int deviceDpiNew)
        {
            base.RescaleConstantsForDpi(deviceDpiOld, deviceDpiNew);
            ApplyDpiMetrics(deviceDpiNew);
        }

        private void ApplyDpiMetrics(int dpi)
        {
            _statusImageList.RescaleForDpi(dpi);
            ImageList = _statusImageList.ImageList;
            Indent = DpiScaling.Scale(LogicalIndent, dpi);
        }

        private void SetupConnectionTreeView()
        {
            ApplyDpiMetrics(DeviceDpi);
            _contextMenu = new ConnectionContextMenu(this);
            ContextMenuStrip = _contextMenu;
            SetupDragDrop();
            SetEventHandlers();
        }

        private void SetEventHandlers()
        {
            AfterCollapse += (sender, args) => SyncExpansionToModel(args.Node, false);
            AfterExpand += (sender, args) => SyncExpansionToModel(args.Node, true);
            AfterSelect += tvConnections_AfterSelect;
            NodeMouseClick += OnNodeMouseClick;
            NodeMouseDoubleClick += OnNodeMouseDoubleClick;
            BeforeLabelEdit += OnBeforeLabelEdit;
            AfterLabelEdit += OnAfterLabelEdit;
        }

        /// <summary>
        /// Mirrors a user-driven expand or collapse back onto the model, which is where
        /// expansion state is persisted. Suppressed while the tree rebuilds itself, so a
        /// transient search cannot pollute the saved connections file.
        /// </summary>
        private void SyncExpansionToModel(TreeNode node, bool expanded)
        {
            if (_suppressExpansionSync)
                return;

            if (node?.Tag is ContainerInfo container)
                container.IsExpanded = expanded;
        }

        private void PopulateTreeView(ConnectionTreeModel newModel)
        {
            RegisterModelUpdateHandlers(newModel);
            NodeSearcher = new NodeSearcher(newModel);
            RebuildAll(false);
            ExecutePostSetupActions();
        }

        private void RegisterModelUpdateHandlers(ConnectionTreeModel newModel)
        {
            _puttySessionsManager.PuttySessionsCollectionChanged += OnPuttySessionsCollectionChanged;
            newModel.CollectionChanged += HandleCollectionChanged;
            newModel.PropertyChanged += HandleCollectionPropertyChanged;
        }

        private void UnregisterModelUpdateHandlers(ConnectionTreeModel oldConnectionTreeModel)
        {
            _puttySessionsManager.PuttySessionsCollectionChanged -= OnPuttySessionsCollectionChanged;

            if (oldConnectionTreeModel == null)
                return;

            oldConnectionTreeModel.CollectionChanged -= HandleCollectionChanged;
            oldConnectionTreeModel.PropertyChanged -= HandleCollectionPropertyChanged;
        }

        private void OnPuttySessionsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs args)
        {
            // Raised on the UI thread since ADR-0033 marshalled the PuTTY Profile refresh there,
            // but still guarded: AddSessions is also called directly when connections load.
            InvokeIfRequired(() =>
            {
                foreach (var puttyRoot in GetRootPuttyNodes().ToList())
                    RebuildChildren(puttyRoot);
            });
        }

        private void HandleCollectionPropertyChanged(object? sender, PropertyChangedEventArgs propertyChangedEventArgs)
        {
            var property = propertyChangedEventArgs.PropertyName;
            if (property != nameof(ConnectionInfo.Name)
                && property != nameof(ConnectionInfo.OpenConnections)
                && property != nameof(ConnectionInfo.Icon))
            {
                return;
            }

            if (!(sender is ConnectionInfo senderAsConnectionInfo))
                return;

            InvokeIfRequired(() =>
            {
                if (!_nodeMap.TryGetValue(senderAsConnectionInfo, out var node))
                    return;

                ApplyNodeState(node, senderAsConnectionInfo);

                // a rename can change what the active filter matches
                if (property == nameof(ConnectionInfo.Name) && IsFiltering)
                    UpdateFiltering();
            });
        }

        private void ExecutePostSetupActions()
        {
            foreach (var action in PostSetupActions)
            {
                action.Execute(this);
            }
        }
        #endregion

        #region ConnectionTree Behavior
        public RootNodeInfo GetRootConnectionNode()
        {
            return (RootNodeInfo)ConnectionTreeModel.RootNodes.First(item => item is RootNodeInfo);
        }

        public void Invoke(Action action)
        {
            Invoke((Delegate)action);
        }

        /// <summary>
        /// Marshals to the UI thread only when there is a handle to marshal to, so the
        /// same code path works in tests where the control is never shown.
        /// </summary>
        private void InvokeIfRequired(Action action)
        {
            if (IsHandleCreated && InvokeRequired)
                Invoke((Delegate)action);
            else
                action();
        }

        public void InvokeExpand(object model)
        {
            InvokeIfRequired(() => Expand(model));
        }

        public void InvokeRebuildAll(bool preserveState)
        {
            InvokeIfRequired(() => RebuildAll(preserveState));
        }

        public IEnumerable<RootPuttySessionsNodeInfo> GetRootPuttyNodes()
        {
            return ConnectionTreeModel?.RootNodes.OfType<RootPuttySessionsNodeInfo>()
                   ?? Enumerable.Empty<RootPuttySessionsNodeInfo>();
        }

        public void SelectObject(ConnectionInfo model, bool ensureVisible = false)
        {
            if (model == null)
            {
                SelectedNode = null;
                return;
            }

            if (!_nodeMap.TryGetValue(model, out var node))
                return;

            SelectedNode = node;
            if (ensureVisible)
                EnsureModelVisible(model);
        }

        public void EnsureModelVisible(ConnectionInfo model)
        {
            if (model == null || !IsHandleCreated)
                return;

            if (_nodeMap.TryGetValue(model, out var node))
                node.EnsureVisible();
        }

        public void Expand(object model)
        {
            SetExpansion(model, true);
        }

        public void Collapse(object model)
        {
            SetExpansion(model, false);
        }

        public void ToggleExpansion(object model)
        {
            if (!(model is ConnectionInfo connectionInfo) || !_nodeMap.TryGetValue(connectionInfo, out var node))
                return;

            SetExpansion(model, !node.IsExpanded);
        }

        private void SetExpansion(object model, bool expanded)
        {
            if (!(model is ConnectionInfo connectionInfo) || !_nodeMap.TryGetValue(connectionInfo, out var node))
                return;

            // AfterExpand/AfterCollapse do not fire without a handle, so the model side is
            // written explicitly rather than through the event
            if (expanded)
                node.Expand();
            else
                node.Collapse();

            if (connectionInfo is ContainerInfo container)
                container.IsExpanded = expanded;
        }

        /// <summary>
        /// Expands every folder. Hides TreeView.ExpandAll, which is not virtual, so the
        /// model is kept in step.
        /// </summary>
        public new void ExpandAll()
        {
            base.ExpandAll();
            SyncAllExpansionToModel(true);
        }

        public new void CollapseAll()
        {
            base.CollapseAll();
            SyncAllExpansionToModel(false);
        }

        private void SyncAllExpansionToModel(bool expanded)
        {
            foreach (var container in _nodeMap.Keys.OfType<ContainerInfo>().ToList())
                container.IsExpanded = expanded;
        }

        /// <summary>
        /// Folders the caller wants expanded on the next rebuild; reading it reports what
        /// is expanded now.
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IEnumerable ExpandedObjects
        {
            get
            {
                return _nodeMap.Where(pair => pair.Value.IsExpanded)
                               .Select(pair => pair.Key)
                               .ToArray();
            }
            set
            {
                _explicitExpandedObjects = value?.Cast<ConnectionInfo>().ToArray();
            }
        }

        public void AddConnection()
        {
            try
            {
                AddNode(new ConnectionInfo());
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("UI.Window.Tree.AddConnection() failed.", ex);
            }
        }

        public void AddFolder()
        {
            try
            {
                AddNode(new ContainerInfo());
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage(Language.strErrorAddFolderFailed, ex);
            }
        }

        private void AddNode(ConnectionInfo newNode)
        {
            if (SelectedConnection?.GetTreeNodeType() == TreeNodeType.PuttyRoot || SelectedConnection?.GetTreeNodeType() == TreeNodeType.PuttySession)
                return;

            // the new node will survive filtering if filtering is active
            _connectionTreeSearchTextFilter.SpecialInclusionList.Add(newNode);

            // use root node if no node is selected
            ConnectionInfo parentNode = SelectedConnection ?? GetRootConnectionNode();
            DefaultConnectionInfo.Instance.SaveTo(newNode);
            DefaultConnectionInheritance.Instance.SaveTo(newNode.Inheritance);
            var selectedContainer = parentNode as ContainerInfo;
            var parent = selectedContainer ?? parentNode?.Parent;
            newNode.SetParent(parent);
            Expand(parent);
            SelectObject(newNode, true);
            BeginRenamingSelectedNode();
        }

        public void DuplicateSelectedNode()
        {
            if (SelectedConnection == null)
                return;

            var selectedNodeType = SelectedConnection.GetTreeNodeType();
            if (selectedNodeType != TreeNodeType.Connection && selectedNodeType != TreeNodeType.Container)
                return;

            var newNode = SelectedConnection.Clone();
            SelectedConnection.Parent.AddChildBelow(newNode, SelectedConnection);
            newNode.Parent.SetChildBelow(newNode, SelectedConnection);
        }

        public void RenameSelectedNode()
        {
            BeginRenamingSelectedNode();
        }

        private void BeginRenamingSelectedNode()
        {
            // label editing needs a created handle; in tests there is none
            if (SelectedNode == null || !IsHandleCreated)
                return;

            _allowEdit = true;
            SelectedNode.BeginEdit();
        }

        public void DeleteSelectedNode()
        {
            if (SelectedConnection is RootNodeInfo || SelectedConnection is PuttySessionInfo) return;
            if (!NodeDeletionConfirmer.Confirm(SelectedConnection)) return;
            ConnectionTreeModel.DeleteNode(SelectedConnection);
        }

        public void SortRecursive(ConnectionInfo sortTarget, ListSortDirection sortDirection)
        {
            if (sortTarget == null)
                sortTarget = GetRootConnectionNode();

            Runtime.ConnectionsService.BeginBatchingSaves();

            var sortTargetAsContainer = sortTarget as ContainerInfo;
            if (sortTargetAsContainer != null)
                sortTargetAsContainer.SortRecursive(sortDirection);
            else
                SelectedConnection.Parent.SortRecursive(sortDirection);

            Runtime.ConnectionsService.EndBatchingSaves();
        }

        private void tvConnections_AfterSelect(object? sender, TreeViewEventArgs e)
        {
            try
            {
                AppWindows.ConfigForm.SelectedTreeNode = SelectedConnection;
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("tvConnections_AfterSelect (UI.Window.ConnectionTreeWindow) failed", ex);
            }
        }

        private void OnNodeMouseDoubleClick(object? sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            if (!(e.Node?.Tag is ConnectionInfo clickedNode)) return;
            DoubleClickHandler.Execute(clickedNode);
        }

        private void OnNodeMouseClick(object? sender, TreeNodeMouseClickEventArgs e)
        {
            // TreeView does not select on right-click, so the context menu would otherwise
            // act on whatever was selected before
            if (e.Button == MouseButtons.Right)
            {
                SelectedNode = e.Node;
                return;
            }

            if (e.Button != MouseButtons.Left) return;
            if (!(e.Node?.Tag is ConnectionInfo clickedNode)) return;
            SingleClickHandler.Execute(clickedNode);
        }

        private void OnBeforeLabelEdit(object? sender, NodeLabelEditEventArgs e)
        {
            if (_nodeInEditMode)
                return;

            if (!_allowEdit || SelectedConnection is PuttySessionInfo || SelectedConnection is RootPuttySessionsNodeInfo)
            {
                e.CancelEdit = true;
                return;
            }

            _nodeInEditMode = true;
            _contextMenu.DisableShortcutKeys();
        }

        private void OnAfterLabelEdit(object? sender, NodeLabelEditEventArgs e)
        {
            if (!_nodeInEditMode)
                return;

            try
            {
                // an empty label would blank the node text while RenameNode rejects it
                if (string.IsNullOrEmpty(e.Label))
                {
                    e.CancelEdit = true;
                    return;
                }

                ConnectionTreeModel.RenameNode(SelectedConnection, e.Label);
                // ensures that if we are filtering and a new item is added that does not
                // match the filter, it will be filtered out
                _connectionTreeSearchTextFilter.SpecialInclusionList.Clear();
                UpdateFiltering();
                AppWindows.ConfigForm.SelectedTreeNode = SelectedConnection;
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("tvConnections_AfterLabelEdit (UI.Window.ConnectionTreeWindow) failed", ex);
            }
            finally
            {
                _contextMenu.EnableShortcutKeys();
                _nodeInEditMode = false;
                _allowEdit = false;
            }
        }
        #endregion
    }
}
