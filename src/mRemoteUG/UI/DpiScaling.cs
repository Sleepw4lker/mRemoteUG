using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace mRemoteUG.UI
{
    /// <summary>
    /// One definition of "this many pixels at 96 DPI, rendered at that DPI".
    /// </summary>
    /// <remarks>
    /// WinForms scales anything assigned inside <c>InitializeComponent</c> by itself, so most pixel
    /// literals in this application need nothing. What needs this helper is the rest: sizes
    /// assigned after the auto-scale pass has run, geometry computed at run time, constants used by
    /// owner-draw code, and <see cref="ImageList.ImageSize"/>.
    /// <para>
    /// Prefer <see cref="Control.LogicalToDeviceUnits(int)"/> when a control is in hand - it is the
    /// framework's own version of this and is kept in step with the DPI the control is currently
    /// being scaled for. These overloads exist for the cases where there is no control, or where
    /// the DPI has to come from somewhere other than the control being sized.
    /// </para>
    /// </remarks>
    public static class DpiScaling
    {
        /// <summary>The DPI every pixel literal in this codebase is expressed in.</summary>
        public const int DefaultDpi = 96;

        public static int Scale(int logical, int dpi) =>
            (int)Math.Round(logical * dpi / (double)DefaultDpi, MidpointRounding.AwayFromZero);

        public static float Scale(float logical, int dpi) => logical * dpi / (float)DefaultDpi;

        public static Size Scale(Size logical, int dpi) =>
            new Size(Scale(logical.Width, dpi), Scale(logical.Height, dpi));

        public static Padding Scale(Padding logical, int dpi) =>
            new Padding(Scale(logical.Left, dpi), Scale(logical.Top, dpi),
                        Scale(logical.Right, dpi), Scale(logical.Bottom, dpi));

        /// <summary>
        /// The DPI a control is being rendered at, or <see cref="DefaultDpi"/> when there is none.
        /// </summary>
        /// <summary>
        /// Resizes an image list for a DPI, carrying the images it holds across.
        /// </summary>
        /// <remarks>
        /// Assigning <see cref="ImageList.ImageSize"/> <em>empties the list</em> once its native
        /// handle exists - measured, two images in and none out. Before a handle is created the
        /// images survive, which is why this looked correct: at startup nothing has been realized
        /// yet, so the icons were there. The first DPI change after a window is on screen wiped
        /// them, and every node fell back to a blank image.
        /// <para>
        /// So the images are copied out first and put back afterwards - and the collection is
        /// cleared explicitly in between. Keys do <em>not</em> survive that round trip on their
        /// own: assigning <c>ImageSize</c> empties the native list while leaving the parallel key
        /// table behind it untouched, so the re-added entries sit behind stale duplicates and
        /// every lookup past the image count resolves to the wrong image or to nothing. Measured,
        /// two images in: <c>Count</c> 2 and <c>Keys</c> 4. That is what inverted the connection
        /// status badges - the icons are held in plain/overlaid pairs and the shift was odd, so
        /// every key landed on the other member of its own pair. This overload stretches what it
        /// carries across, which was all that was available while every glyph had one 16x16 frame.
        /// The overload below re-resolves from the keys instead and is what callers should use.
        /// </para>
        /// <para>
        /// A <see cref="TreeView"/> or <see cref="ListView"/> only re-measures its rows when the
        /// <c>ImageList</c> property is assigned, so callers must re-assign it afterwards rather
        /// than relying on the list they already handed over.
        /// </para>
        /// </remarks>
        public static void ResizeForDpi(ImageList imageList, int logicalSize, int dpi)
        {
            if (imageList == null)
                return;

            var scaled = Scale(logicalSize, dpi);
            if (imageList.ImageSize.Width == scaled && imageList.ImageSize.Height == scaled)
                return;

            var keys = new List<string>();
            var images = new List<Image>();
            for (var i = 0; i < imageList.Images.Count; i++)
            {
                keys.Add(imageList.Images.Keys[i]);
                images.Add(imageList.Images[i]);
            }

            // Clear before assigning, not after. The assignment empties the native list but not
            // the key table beside it, and Clear is the only call that empties both.
            imageList.Images.Clear();
            imageList.ImageSize = new Size(scaled, scaled);

            for (var i = 0; i < keys.Count; i++)
                imageList.Images.Add(keys[i], images[i]);

            // Images[i] hands back a fresh Bitmap on every read, so each of these is a copy this
            // method owns and Add has already taken its own copy of. Leaving them to the garbage
            // collector leaked a bitmap per icon per DPI change.
            foreach (var image in images)
                image.Dispose();
        }

        /// <summary>
        /// Resizes an image list for a DPI by re-resolving its contents at the new size.
        /// </summary>
        /// <remarks>
        /// The overload above carries the images across and lets them stretch, which was all that
        /// was available while every glyph had one 16x16 frame. This one asks
        /// <paramref name="resolve"/> for each key instead, so the list is rebuilt out of artwork
        /// drawn for the size rather than out of its own previous contents.
        /// <para>
        /// That also takes the sharpest edge off the bug documented on the other overload. Keys
        /// are the input here rather than something recovered from a round trip, so the images and
        /// the key table cannot come back out of step - which is what put the connected badge on
        /// the disconnected hosts. The <c>Images.Clear()</c> before the <c>ImageSize</c>
        /// assignment is still required and is still here: without it the native list empties and
        /// the key table does not.
        /// </para>
        /// <para>
        /// Nothing here is disposed, and <paramref name="resolve"/> must therefore hand back
        /// images that outlive the call - cached ones. Disposing on this side was tried and is
        /// not safe: an <c>ImageList</c> does not copy what it is given until its native handle
        /// exists, and assigning <c>ImageSize</c> destroys that handle, so the window in which a
        /// just-added image may be released is narrower than it looks. Ownership stays with the
        /// resolver, which is the only side that knows whether it made the image or borrowed it.
        /// </para>
        /// </remarks>
        public static void ResizeForDpi(ImageList imageList, int logicalSize, int dpi,
                                        Func<string, int, Image> resolve)
        {
            if (imageList == null || resolve == null)
                return;

            var scaled = Scale(logicalSize, dpi);
            var keys = new List<string>();
            for (var i = 0; i < imageList.Images.Count; i++)
                keys.Add(imageList.Images.Keys[i]);

            // Clear before assigning, not after - the assignment empties the native list but not
            // the key table beside it, and Clear is the only call that empties both.
            imageList.Images.Clear();
            imageList.ImageSize = new Size(scaled, scaled);

            foreach (var key in keys)
            {
                var image = resolve(key, scaled);
                if (image == null)
                    continue;

                imageList.Images.Add(key, image);
            }
        }

        /// <summary>
        /// Sets the size a ToolStrip and the menus hanging off it draw their glyphs at.
        /// </summary>
        /// <remarks>
        /// Two measured facts make this one assignment the whole fix, and each is the opposite of
        /// what it looks like. Setting <c>ImageScalingSize</c> propagates to the drop-downs an item
        /// owns - both those already built and any built later - so there is nothing to walk. But
        /// the strip's own rescale during a DPI change does <em>not</em> propagate: a drop-down was
        /// measured sitting at 16x16 while its owner had already moved to 32x32, which is the File
        /// menu keeping 200%-sized glyphs after the window was dragged to a 100% monitor.
        /// <para>
        /// So the assignment has to be made by hand even on a strip the framework has already
        /// rescaled. Setting the size it would have chosen anyway costs nothing.
        /// </para>
        /// </remarks>
        public static void ApplyImageScaling(ToolStrip toolStrip, int dpi)
        {
            if (toolStrip == null)
                return;

            var scaled = Scale(LogicalGlyphSize, dpi);
            toolStrip.ImageScalingSize = new Size(scaled, scaled);

            // ImageScalingSize only says what rectangle to draw into; the Image on each item is
            // still whatever was assigned when the menu was built, which is the 16 pixel frame.
            // Picking a larger frame is a second, separate pass - and it is the whole reason the
            // artwork has more than one.
            foreach (var item in ItemsOf(toolStrip))
            {
                if (Glyphs.TryRepick(item.Image, scaled, out var repicked))
                    item.Image = repicked;
            }
        }

        /// <summary>
        /// Every item on a strip, including those in drop-downs that have already been built.
        /// </summary>
        /// <remarks>
        /// <see cref="ToolStripDropDownItem.HasDropDownItems"/> is checked rather than the item
        /// count, because <em>reading</em> <c>DropDownItems</c> creates the drop-down. Walking
        /// without that guard would build every submenu in the application during the first DPI
        /// pass, including the ones <c>ConnectionsTreeToMenuItemsConverter</c> fills on demand.
        /// <para>
        /// Note what this deliberately does not do: it never assigns to a nested drop-down's own
        /// <see cref="ToolStrip.ImageScalingSize"/>. ADR-0010 records that doing so was much worse
        /// at every scale and says not to retry it. Assigning an item's
        /// <see cref="ToolStripItem.Image"/> is a different thing and leaves that property alone.
        /// </para>
        /// </remarks>
        private static IEnumerable<ToolStripItem> ItemsOf(ToolStrip toolStrip)
        {
            var stack = new Stack<ToolStripItemCollection>();
            stack.Push(toolStrip.Items);

            while (stack.Count > 0)
            {
                foreach (ToolStripItem item in stack.Pop())
                {
                    yield return item;

                    if (item is ToolStripDropDownItem parent && parent.HasDropDownItems)
                        stack.Push(parent.DropDownItems);
                }
            }
        }

        /// <summary>The size of every menu and toolbar glyph this application ships.</summary>
        private const int LogicalGlyphSize = 16;

        /// <summary>
        /// Every ToolStrip a window owns, wherever it keeps it.
        /// </summary>
        /// <remarks>
        /// Walking the control tree alone finds only the docked strips. A
        /// <see cref="ContextMenuStrip"/> is never a child control: some hang off the
        /// <see cref="Control.ContextMenuStrip"/> property and others live only in a designer
        /// field and are shown by hand, so all three routes are followed.
        /// </remarks>
        /// <summary>
        /// Brings every ToolStrip under a window into line with a DPI change.
        /// </summary>
        /// <remarks>
        /// Both of the things done here are ones a DPI change does not do by itself.
        /// <see cref="ApplyImageScaling"/> covers the glyphs; the font is the other half, and the
        /// easier one to miss. A ToolStrip does <em>not</em> inherit its parent's font - measured,
        /// a form went from 9pt to 18pt and a Label on it followed while the MenuStrip stayed at
        /// 9pt with nothing of its own set, because ToolStrip reads a process-wide default rather
        /// than its parent. Menu text therefore kept its old size through every DPI change.
        /// <para>
        /// The font is assigned rather than copied. A control never disposes a font it was handed,
        /// so sharing the window's own instance leaves nothing to clean up, and it ties the menus
        /// to whatever the rest of the window settled on rather than to a size computed here.
        /// </para>
        /// </remarks>
        public static void FollowDpiChange(Control root, int dpi, Font font)
        {
            if (root == null)
                return;

            foreach (var strip in ToolStripsOf(root))
            {
                ApplyImageScaling(strip, dpi);

                if (font != null)
                    strip.Font = font;
            }
        }

        /// <summary>Every control under this one, at any depth.</summary>
        public static IEnumerable<Control> DescendantsOf(Control root)
        {
            foreach (Control child in root.Controls)
            {
                yield return child;
                foreach (var descendant in DescendantsOf(child))
                    yield return descendant;
            }
        }

        public static IEnumerable<ToolStrip> ToolStripsOf(Control root)
        {
            var seen = new HashSet<ToolStrip>();
            var stack = new Stack<Control>();
            stack.Push(root);

            while (stack.Count > 0)
            {
                var control = stack.Pop();

                if (control is ToolStrip self && seen.Add(self))
                    yield return self;

                if (control.ContextMenuStrip != null && seen.Add(control.ContextMenuStrip))
                    yield return control.ContextMenuStrip;

                foreach (var field in control.GetType()
                                             .GetFields(BindingFlags.Instance | BindingFlags.Public |
                                                        BindingFlags.NonPublic | BindingFlags.FlattenHierarchy)
                                             .Where(f => typeof(ToolStrip).IsAssignableFrom(f.FieldType)))
                {
                    if (field.GetValue(control) is ToolStrip strip && seen.Add(strip))
                        yield return strip;
                }

                foreach (Control child in control.Controls)
                    stack.Push(child);
            }
        }
    }
}
