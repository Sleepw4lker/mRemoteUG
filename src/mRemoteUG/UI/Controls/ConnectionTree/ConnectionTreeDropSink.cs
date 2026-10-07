using System.Drawing;
using System.Windows.Forms;
using mRemoteUG.Tree;

namespace mRemoteUG.UI.Controls
{
    /// <summary>
    /// Draws drag feedback over a TreeView: a box around the target node for a drop
    /// onto it, or an insertion line for a drop between nodes. TreeView has no
    /// owner-draw by default and switching DrawMode mid-drag recreates the handle, so
    /// the feedback is painted directly and erased by invalidating.
    /// </summary>
    internal class ConnectionTreeDropSink : IDropFeedback
    {
        // 96 DPI measurements. This paints directly rather than through a control's own drawing,
        // so nothing scales them for us; they are scaled against the tree's current DPI at the
        // point of use, which also keeps them right after the tree moves to another monitor.
        private const int LogicalLineHeight = 2;
        private const int LogicalEndTickHeight = 6;
        private const int LogicalInfoMessageGap = 8;

        private readonly TreeView _tree;

        private int LineHeight => UI.DpiScaling.Scale(LogicalLineHeight, _tree.DeviceDpi);
        private int EndTickHeight => UI.DpiScaling.Scale(LogicalEndTickHeight, _tree.DeviceDpi);
        private int InfoMessageGap => UI.DpiScaling.Scale(LogicalInfoMessageGap, _tree.DeviceDpi);

        private TreeNode _targetNode;
        private DropTargetLocation _location = DropTargetLocation.None;
        private string _infoMessage;

        public bool EnableFeedback { get; set; } = true;
        public Color FeedbackColor { get; set; } = Color.Green;

        public ConnectionTreeDropSink(TreeView tree)
        {
            _tree = tree;
        }

        public bool IsShowing => _location != DropTargetLocation.None;

        /// <returns>True if the target or location actually moved.</returns>
        public bool Update(TreeNode node, DropTargetLocation location, string infoMessage)
        {
            if (ReferenceEquals(node, _targetNode) && location == _location && infoMessage == _infoMessage)
                return false;

            _targetNode = node;
            _location = location;
            _infoMessage = infoMessage;
            return true;
        }

        public void Reset()
        {
            _targetNode = null;
            _location = DropTargetLocation.None;
            _infoMessage = null;
            EnableFeedback = true;
        }

        /// <summary>
        /// Erases the previous feedback and paints the current one. Call after Update
        /// reports a change.
        /// </summary>
        public void Redraw()
        {
            _tree.Invalidate();
            _tree.Update();

            if (_targetNode == null || _location == DropTargetLocation.None)
                return;

            if (!EnableFeedback && string.IsNullOrEmpty(_infoMessage))
                return;

            using (var g = _tree.CreateGraphics())
            {
                var row = RowBounds(_targetNode);

                if (EnableFeedback)
                {
                    using (var pen = new Pen(FeedbackColor, LineHeight))
                    {
                        if (_location == DropTargetLocation.Item)
                            DrawItemBox(g, pen, row);
                        else
                            DrawInsertionLine(g, pen, row);
                    }
                }

                if (!string.IsNullOrEmpty(_infoMessage))
                    DrawInfoMessage(g, row);
            }
        }

        private Rectangle RowBounds(TreeNode node)
        {
            // TreeNode.Bounds covers the label only; feedback spans the whole row
            return new Rectangle(0, node.Bounds.Top, _tree.ClientSize.Width - 1, node.Bounds.Height);
        }

        private static void DrawItemBox(Graphics g, Pen pen, Rectangle row)
        {
            g.DrawRectangle(pen, row.Left + 1, row.Top + 1, row.Width - 2, row.Height - 2);
        }

        private void DrawInsertionLine(Graphics g, Pen pen, Rectangle row)
        {
            var y = _location == DropTargetLocation.AboveItem ? row.Top : row.Bottom - LineHeight;
            var left = _targetNode.Bounds.Left;
            var right = row.Right;

            g.DrawLine(pen, left, y, right, y);
            // end ticks, so the line reads as an insertion point rather than a border
            g.DrawLine(pen, left, y - EndTickHeight / 2, left, y + EndTickHeight / 2);
            g.DrawLine(pen, right, y - EndTickHeight / 2, right, y + EndTickHeight / 2);
        }

        private void DrawInfoMessage(Graphics g, Rectangle row)
        {
            var size = g.MeasureString(_infoMessage, _tree.Font);
            float x = _targetNode.Bounds.Right + InfoMessageGap;
            if (x + size.Width > row.Right)
                x = System.Math.Max(0f, row.Right - size.Width);

            var box = new RectangleF(x, row.Top, size.Width, size.Height);

            using (var background = new SolidBrush(Color.FromArgb(220, SystemColors.Info)))
            using (var text = new SolidBrush(SystemColors.InfoText))
            {
                g.FillRectangle(background, box);
                g.DrawString(_infoMessage, _tree.Font, text, box);
            }
        }
    }
}
