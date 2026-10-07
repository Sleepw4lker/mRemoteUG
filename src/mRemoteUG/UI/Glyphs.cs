#nullable enable
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.CompilerServices;

namespace mRemoteUG.UI
{
    /// <summary>
    /// The artwork this application ships, by name and device size.
    /// </summary>
    /// <remarks>
    /// Every glyph exists as a PNG at each size on <see cref="Ladder"/>, generated from
    /// <c>src/mRemoteUG/Resources/icon-manifest.json</c> by <c>Tools/IconGen</c> and embedded.
    /// Asking for a size picks a frame rather than stretching one, which is the thing the old
    /// 16x16-only artwork could not do at any scale above 100%.
    /// <para>
    /// Most glyphs ship <em>colourless</em> - white RGB with the shape carried entirely in the
    /// alpha channel - and are tinted here to <see cref="TintColor"/>. The ones that carry meaning
    /// in their colour (an error is red, a warning is amber) are listed in
    /// <see cref="Resources.FixedColourGlyphs"/> and are used exactly as generated.
    /// </para>
    /// <para>
    /// Connection icons are stored and named the same way and go through the same cache by way of
    /// <see cref="FromStem"/>. That is what lets one DPI pass re-pick an icon-picker menu item and
    /// a File menu item with the same code, neither side knowing about the other.
    /// </para>
    /// </remarks>
    internal static class Glyphs
    {
        /// <summary>The size every glyph is authored at, at 96 DPI.</summary>
        public const int LogicalSize = 16;

        /// <summary>
        /// The device sizes artwork exists at. A request snaps <em>up</em> to one of these.
        /// </summary>
        /// <remarks>
        /// These are what <c>DpiScaling.Scale(16, dpi)</c> produces at the scale factors Windows
        /// offers: 16 at 100%, 20 at 125%, 24 at 150%, 28 at 175%, 32 at 200%, 40 at 250%, 48 at
        /// 300% and 64 at 400%. 225% and 350% land between and take the next one up.
        /// </remarks>
        public static readonly int[] Ladder = { 16, 20, 24, 28, 32, 40, 48, 64 };

        /// <summary>
        /// The colour a tintable glyph is drawn in.
        /// </summary>
        /// <remarks>
        /// Taken from <see cref="SystemColors"/>, so it is whatever <c>Application.SetColorMode</c>
        /// settled on at startup - which is once, by design (ADR-0009), so a tint taken lazily on
        /// first use can never be stale.
        /// <para>
        /// Artwork lands on two different backgrounds: <c>Control</c> for menus and toolbars,
        /// <c>Window</c> for the connection tree and the notifications list, whose foregrounds are
        /// <c>ControlText</c> and <c>WindowText</c>. Those are <em>not</em> the same value -
        /// measured in dark mode on Windows 11, ControlText is pure white and WindowText is
        /// FFF0F0F0. One tint is used anyway, because a gap of 15/255 on one channel is not
        /// visible and two tinted copies of every glyph is a real cost. <c>--selftest</c> prints
        /// both and the size of the gap, so a theme that ever opens it up properly shows up in
        /// the log rather than as a washed-out glyph on one surface.
        /// </para>
        /// </remarks>
        public static Color TintColor => SystemColors.ControlText;

        /// <summary>Every glyph name the manifest declares.</summary>
        public static IReadOnlyList<string> Names => Resources.GlyphNames;

        internal const string GlyphPrefix = "mRemoteUG.Glyphs.";

        private static readonly Dictionary<(string Stem, int Size), Bitmap> Cache = new();

        /// <summary>What an image was loaded from, so it can be loaded again at another size.</summary>
        private sealed class Source
        {
            public Source(string stem, bool tint)
            {
                Stem = stem;
                Tint = tint;
            }

            public string Stem { get; }
            public bool Tint { get; }
        }

        private static readonly ConditionalWeakTable<Image, Source> SourcesByImage = new();

        /// <summary>The ladder size a request of this many device pixels resolves to.</summary>
        public static int LadderSizeFor(int devicePixels)
        {
            foreach (var size in Ladder)
                if (size >= devicePixels)
                    return size;

            // Past the top of the ladder there is nothing larger to pick, so the largest is it.
            return Ladder[Ladder.Length - 1];
        }

        /// <summary>
        /// A glyph at the ladder size covering <paramref name="devicePixels"/>.
        /// </summary>
        /// <remarks>
        /// The returned bitmap is <strong>shared and must not be disposed</strong>. This is the
        /// one place the old <c>Resources.Foo</c> contract changed: that handed back a fresh copy
        /// on every read, so a caller could own it. Disposing one of these empties the cache for
        /// every other caller, and the failure surfaces later, somewhere else, first as a
        /// collapse in drawing speed and then as <c>ArgumentException: Parameter is not valid</c>.
        /// </remarks>
        public static Bitmap Get(string name, int devicePixels) =>
            FromStem(GlyphPrefix + name, !IsFixedColour(name), devicePixels);

        /// <summary>
        /// Any embedded artwork of this shape, by its resource name without size or suffix.
        /// </summary>
        public static Bitmap FromStem(string stem, bool tint, int devicePixels)
        {
            var size = LadderSizeFor(devicePixels);

            lock (Cache)
            {
                if (Cache.TryGetValue((stem, size), out var cached))
                    return cached;

                var bitmap = Load(stem, size, tint);
                Cache[(stem, size)] = bitmap;
                SourcesByImage.AddOrUpdate(bitmap, new Source(stem, tint));
                return bitmap;
            }
        }

        /// <summary>
        /// The same artwork at another size, for an image this cache handed out.
        /// </summary>
        /// <remarks>
        /// Keyed by reference, which is what lets a DPI change re-pick a
        /// <see cref="System.Windows.Forms.ToolStripItem"/>'s image without every call site having
        /// to name its artwork a second time. It works only because <see cref="FromStem"/> hands
        /// back one shared instance per stem and size.
        /// <para>
        /// This deliberately cannot work through an <c>ImageList</c>:
        /// <c>ImageList.Images[i]</c> returns a fresh <see cref="Bitmap"/> on every read, so
        /// reference identity does not survive one. Image lists are rebuilt from their keys
        /// instead.
        /// </para>
        /// </remarks>
        public static bool TryRepick(Image? image, int devicePixels, out Bitmap repicked)
        {
            if (image is not null && SourcesByImage.TryGetValue(image, out var source))
            {
                repicked = FromStem(source.Stem, source.Tint, devicePixels);
                return true;
            }

            repicked = null!;
            return false;
        }

        /// <summary>Whether this image came out of the cache, and so must not be disposed.</summary>
        public static bool IsCached(Image? image) =>
            image is not null && SourcesByImage.TryGetValue(image, out _);

        private static Bitmap Load(string stem, int size, bool tint)
        {
            var resource = $"{stem}.{size}.png";

            using (var stream = typeof(Glyphs).Assembly.GetManifestResourceStream(resource))
            {
                if (stream == null)
                    throw new InvalidOperationException(
                        $"There is no artwork embedded at {resource}. " +
                        "Add it to icon-manifest.json and run Tools/IconGen.");

                // Copied out of the manifest stream: a Bitmap built straight on a stream keeps
                // that stream open for its whole life.
                using (var raw = new Bitmap(stream))
                {
                    var copy = new Bitmap(raw.Width, raw.Height, PixelFormat.Format32bppArgb);
                    using (var graphics = Graphics.FromImage(copy))
                        graphics.DrawImageUnscaled(raw, 0, 0);

                    if (tint)
                        Tint(copy, TintColor);

                    return copy;
                }
            }
        }

        private static bool IsFixedColour(string name) =>
            Array.IndexOf(Resources.FixedColourGlyphs, name) >= 0;

        /// <summary>
        /// Replaces the colour of every pixel while leaving its transparency alone.
        /// </summary>
        /// <remarks>
        /// Done with <see cref="Bitmap.LockBits(Rectangle,ImageLockMode,PixelFormat)"/> rather than
        /// a <c>ColorMatrix</c> through <c>DrawImage</c>. GDI+ applies its own colour adjustment
        /// on that path, so the result is close but not bit-exact, and a mask that is only
        /// approximately the tint colour cannot be asserted on. This is exact, and it is what lets
        /// <c>GlyphTests</c> say that a mask ships colourless and comes back tinted.
        /// </remarks>
        private static void Tint(Bitmap bitmap, Color color)
        {
            var area = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            var data = bitmap.LockBits(area, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);

            try
            {
                var bytes = Math.Abs(data.Stride) * bitmap.Height;
                var pixels = new byte[bytes];
                System.Runtime.InteropServices.Marshal.Copy(data.Scan0, pixels, 0, bytes);

                // Format32bppArgb is B, G, R, A in memory order on little-endian.
                for (var i = 0; i < bytes; i += 4)
                {
                    if (pixels[i + 3] == 0)
                        continue;

                    pixels[i + 0] = color.B;
                    pixels[i + 1] = color.G;
                    pixels[i + 2] = color.R;
                }

                System.Runtime.InteropServices.Marshal.Copy(pixels, 0, data.Scan0, bytes);
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }

        /// <summary>
        /// The raw embedded bytes for a glyph, before tinting.
        /// </summary>
        /// <remarks>
        /// Exists for the tests, which have to be able to say what ships rather than what is
        /// served: <see cref="Get"/> tints on the way out, so reading a mask back through it
        /// would only ever prove that tinting works.
        /// </remarks>
        internal static byte[] RawBytes(string name, int size)
        {
            using (var stream = typeof(Glyphs).Assembly.GetManifestResourceStream($"{GlyphPrefix}{name}.{size}.png"))
            {
                if (stream == null)
                    throw new InvalidOperationException($"No embedded glyph {name} at {size}.");

                using (var memory = new MemoryStream())
                {
                    stream.CopyTo(memory);
                    return memory.ToArray();
                }
            }
        }
    }
}
