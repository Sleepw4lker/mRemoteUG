using System;
using System.Windows.Forms;
using mRemoteUG.App;

namespace mRemoteUG.UI
{
    /// <summary>
    /// Emptying a <see cref="ToolStripItemCollection"/> that owns its images.
    /// </summary>
    /// <remarks>
    /// <see cref="ToolStripItemCollection.Clear"/> disposes nothing, and disposing a
    /// <see cref="ToolStripItem"/> does not reach its <see cref="ToolStripItem.Image"/> either. So a
    /// drop-down rebuilt from scratch every time it opens leaks one item and one GDI+ bitmap per
    /// entry per open unless the old contents are disposed first.
    /// <para>
    /// <strong>Only for a collection whose images it owns.</strong> Most menus here take their
    /// artwork from the <see cref="Glyphs"/> cache, which hands the same
    /// <see cref="System.Drawing.Bitmap"/> to every caller and expects nobody to dispose it;
    /// pointing this at one of those would blank that glyph everywhere it appears. The collection
    /// that qualifies is the View menu's connection-panel list, whose images come from a fresh
    /// <c>Icon.ToBitmap()</c> per item.
    /// </para>
    /// </remarks>
    internal static class ToolStripItems
    {
        /// <summary>
        /// Empties <paramref name="items"/>, disposing each item and the image it owns.
        /// </summary>
        /// <remarks>
        /// Order matters, and not for the obvious reason. Disposing an item removes it from its
        /// owning collection, so the collection has to be snapshotted before anything is disposed or
        /// the loop skips every other entry. But the dispose also has to happen <em>while</em> the
        /// item still has an owner: measured on .NET 10,
        /// <see cref="ToolStripItem.IsDisposed"/> is only set when
        /// <c>Dispose</c> finds an <see cref="ToolStripItem.Owner"/>, so an item cleared first and
        /// disposed afterwards is disposed but never says so - which makes a test of this look like
        /// a bug in the helper rather than in the order.
        /// </remarks>
        internal static void ClearAndDisposeOwned(ToolStripItemCollection items)
        {
            if (items == null)
                return;

            var doomed = new ToolStripItem[items.Count];
            items.CopyTo(doomed, 0);

            foreach (var item in doomed)
            {
                try
                {
                    var image = item.Image;
                    item.Image = null;
                    image?.Dispose();

                    // Takes the item out of `items` as well.
                    item.Dispose();
                }
                catch (Exception ex)
                {
                    Runtime.MessageCollector?.AddExceptionMessage(
                        "Couldn't dispose a menu item (UI.ToolStripItems)", ex);
                }
            }

            // Anything that declined to remove itself.
            items.Clear();
        }
    }
}
