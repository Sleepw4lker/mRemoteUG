using System;
using System.Collections;
using System.Drawing;
using System.Windows.Forms;
using mRemoteUG.App;
using mRemoteUG.Connection;
using mRemoteUG.Container;
using mRemoteUG.Tree;

namespace mRemoteUG.UI.Controls
{
    /// <summary>
    /// Drag and drop. ObjectListView supplied a SimpleDropSink that could drop between
    /// rows; TreeView offers only item-level drag events, so the above/on/below decision
    /// and its feedback are done here. The drop rules themselves are unchanged and still
    /// live in ConnectionTreeDragAndDropHandler.
    /// </summary>
    public partial class ConnectionTree
    {
        private const int HoverExpandDelayMs = 500;
        private const int AutoScrollMarginPx = 20;

        private ConnectionTreeDropSink _dropSink;
        private Timer _hoverTimer;
        private TreeNode _hoverNode;
        private IList _dragSources;
        private bool _suppressNativeToggle;

        /// <summary>
        /// Raised while a drag hovers, so the handler can say whether the drop is legal.
        /// </summary>
        public event EventHandler<ModelDropEventArgs> ModelCanDrop;

        /// <summary>
        /// Raised when a drag is released over the tree.
        /// </summary>
        public event EventHandler<ModelDropEventArgs> ModelDropped;

        private void SetupDragDrop()
        {
            AllowDrop = true;
            _dropSink = new ConnectionTreeDropSink(this);

            _hoverTimer = new Timer { Interval = HoverExpandDelayMs };
            _hoverTimer.Tick += HoverTimer_Tick;

            ModelCanDrop += _dragAndDropHandler.HandleEvent_ModelCanDrop;
            ModelDropped += _dragAndDropHandler.HandleEvent_ModelDropped;

            ItemDrag += OnItemDrag;
            DragEnter += OnDragEnter;
            DragOver += OnDragOver;
            DragDrop += OnDragDrop;
            DragLeave += OnDragLeave;
            QueryContinueDrag += OnQueryContinueDrag;
        }

        private void DisposeDragDrop()
        {
            if (_hoverTimer == null)
                return;

            _hoverTimer.Tick -= HoverTimer_Tick;
            _hoverTimer.Dispose();
            _hoverTimer = null;
        }

        private void OnItemDrag(object? sender, ItemDragEventArgs e)
        {
            if (!(e.Item is TreeNode node) || !(node.Tag is ConnectionInfo model))
                return;

            _dragSources = new[] { model };
            var payload = new ConnectionInfoDataObject(model);
            try
            {
                DoDragDrop(payload, DragDropEffects.Move);
            }
            finally
            {
                payload.Release();
                _dragSources = null;
                ClearDropFeedback();
            }
        }

        private void OnDragEnter(object? sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.None;
            _dropSink.Reset();
        }

        private void OnDragOver(object? sender, DragEventArgs e)
        {
            var sources = ResolveDragSources(e.Data);
            if (sources == null)
            {
                e.Effect = DragDropEffects.None;
                return;
            }

            var point = PointToClient(new Point(e.X, e.Y));
            var node = NodeAt(point);
            var location = LocationFor(node, point);

            var args = RaiseCanDrop(sources, node, location);
            e.Effect = args.Effect;

            if (_dropSink.Update(node, location, args.InfoMessage))
                _dropSink.Redraw();

            RestartHoverTimer(node);
            AutoScroll(point);
        }

        private void OnDragDrop(object? sender, DragEventArgs e)
        {
            var sources = ResolveDragSources(e.Data);
            ClearDropFeedback();
            if (sources == null)
                return;

            var point = PointToClient(new Point(e.X, e.Y));
            var node = NodeAt(point);
            if (node == null)
                return;

            var args = new ModelDropEventArgs
            {
                TargetModel = node.Tag,
                SourceModels = sources,
                DropTargetLocation = LocationFor(node, point),
                DropSink = _dropSink
            };

            ModelDropped?.Invoke(this, args);
        }

        private void OnDragLeave(object? sender, EventArgs e)
        {
            ClearDropFeedback();
        }

        private void OnQueryContinueDrag(object? sender, QueryContinueDragEventArgs e)
        {
            if (e.Action != DragAction.Continue)
                ClearDropFeedback();
        }

        private ModelDropEventArgs RaiseCanDrop(IList sources, TreeNode node, DropTargetLocation location)
        {
            var args = new ModelDropEventArgs
            {
                TargetModel = node?.Tag,
                SourceModels = sources,
                DropTargetLocation = node == null ? DropTargetLocation.None : location,
                DropSink = _dropSink,
                Effect = DragDropEffects.None
            };

            if (node != null)
                ModelCanDrop?.Invoke(this, args);

            return args;
        }

        private IList ResolveDragSources(IDataObject data)
        {
            if (ConnectionInfoDataObject.TryGetConnections(data, out var models))
                return (IList)models;

            // a drag that started in this control, where the payload never left the process
            return _dragSources;
        }

        /// <summary>
        /// Hit test across the whole row. GetNodeAt is x-sensitive, so a fixed small x
        /// gives the same whole-row behaviour ObjectListView had.
        /// </summary>
        private TreeNode NodeAt(Point point)
        {
            return GetNodeAt(2, point.Y);
        }

        private DropTargetLocation LocationFor(TreeNode node, Point point)
        {
            if (node == null)
                return DropTargetLocation.None;

            var rowBounds = new Rectangle(0, node.Bounds.Top, ClientSize.Width, node.Bounds.Height);
            return TreeNodeDropLocationCalculator.Locate(rowBounds, point.Y);
        }

        private void ClearDropFeedback()
        {
            _hoverTimer?.Stop();
            _hoverNode = null;
            if (_dropSink == null)
                return;

            var wasShowing = _dropSink.IsShowing;
            _dropSink.Reset();
            if (wasShowing)
            {
                Invalidate();
                Update();
            }
        }

        private void RestartHoverTimer(TreeNode node)
        {
            if (ReferenceEquals(node, _hoverNode))
                return;

            _hoverNode = node;
            _hoverTimer.Stop();
            if (node != null && node.Nodes.Count > 0 && !node.IsExpanded)
                _hoverTimer.Start();
        }

        private void HoverTimer_Tick(object? sender, EventArgs e)
        {
            _hoverTimer.Stop();
            if (_hoverNode == null || _hoverNode.IsExpanded)
                return;

            // expand through the model so IsExpanded stays authoritative
            if (_hoverNode.Tag is ContainerInfo container)
                Expand(container);
            else
                _hoverNode.Expand();
        }

        /// <summary>
        /// Scrolls a row at a time near the edges. SimpleDropSink did this; TreeView
        /// does not.
        /// </summary>
        private void AutoScroll(Point point)
        {
            if (point.Y < AutoScrollMarginPx)
            {
                var previous = TopNode?.PrevVisibleNode;
                if (previous != null)
                    TopNode = previous;
            }
            else if (point.Y > ClientSize.Height - AutoScrollMarginPx)
            {
                var next = TopNode?.NextVisibleNode;
                if (next != null)
                    TopNode = next;
            }
        }

        private const int WM_LBUTTONDBLCLK = 0x0203;

        protected override void WndProc(ref Message m)
        {
            // A double-click natively toggles expansion, which would cancel out
            // ExpandNodeClickHandler and leave the node where it started.
            if (m.Msg == WM_LBUTTONDBLCLK)
            {
                _suppressNativeToggle = true;
                try
                {
                    base.WndProc(ref m);
                }
                finally
                {
                    _suppressNativeToggle = false;
                }
                return;
            }

            base.WndProc(ref m);
        }

        protected override void OnBeforeExpand(TreeViewCancelEventArgs e)
        {
            if (_suppressNativeToggle)
            {
                e.Cancel = true;
                return;
            }
            base.OnBeforeExpand(e);
        }

        protected override void OnBeforeCollapse(TreeViewCancelEventArgs e)
        {
            if (_suppressNativeToggle)
            {
                e.Cancel = true;
                return;
            }
            base.OnBeforeCollapse(e);
        }

        private const int TVM_SETEXTENDEDSTYLE = 0x1100 + 44;
        private const int TVS_EX_DOUBLEBUFFER = 0x0004;

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            // The native extended style is used rather than DoubleBuffered, which
            // interferes with drag and insertion-line rendering.
            NativeMethods.SendMessage(Handle, TVM_SETEXTENDEDSTYLE,
                (IntPtr)TVS_EX_DOUBLEBUFFER, (IntPtr)TVS_EX_DOUBLEBUFFER);
        }
    }
}
