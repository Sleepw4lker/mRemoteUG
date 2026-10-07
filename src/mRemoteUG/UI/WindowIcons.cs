#nullable enable
using System;
using System.Collections.Generic;
using System.Drawing;

namespace mRemoteUG.UI
{
    /// <summary>
    /// The multi-frame icons Windows draws on a title bar, in Alt-Tab and on the taskbar.
    /// </summary>
    /// <remarks>
    /// These are the third shape of artwork this application carries, and the one that behaves
    /// least like the other two. A glyph is picked at a size by this code and tinted to the
    /// theme; a window icon is handed to Windows whole, and <em>Windows</em> picks the frame -
    /// a different one for the caption than for the taskbar, at whatever the shell's own scaling
    /// is rather than the window's.
    /// <para>
    /// So they stay <c>.ico</c>, they carry every frame from 16 to 256, and they are never
    /// tinted: the caption they are drawn on is painted by the shell, not by this application,
    /// and a glyph tinted to <c>ControlText</c> would vanish into it. They carry the accent blue
    /// instead, which is legible on a light and a dark caption alike.
    /// </para>
    /// </remarks>
    internal static class WindowIcons
    {
        private const string ResourcePrefix = "mRemoteUG.WindowIcons.";

        private static readonly Dictionary<string, Icon> Cache = new(StringComparer.Ordinal);

        /// <summary>
        /// A window icon by name, with every frame it was generated with.
        /// </summary>
        /// <remarks>
        /// Cached and shared, and like a glyph it must not be disposed - a <see cref="Form"/>
        /// does not dispose the icon it is given, and these outlive every window that shows one.
        /// The size is deliberately not a parameter: handing Windows the whole file is the point,
        /// and <c>new Icon(stream, w, h)</c> would throw away every frame but one.
        /// </remarks>
        public static Icon Get(string name)
        {
            lock (Cache)
            {
                if (Cache.TryGetValue(name, out var cached))
                    return cached;

                var resource = ResourcePrefix + name + ".ico";
                using (var stream = typeof(WindowIcons).Assembly.GetManifestResourceStream(resource))
                {
                    if (stream == null)
                        throw new InvalidOperationException(
                            $"The window icon \"{name}\" is not embedded (looked for {resource}).");

                    // Icon(Stream) reads the whole file, so closing the stream here is safe and
                    // every frame survives.
                    var icon = new Icon(stream);
                    Cache[name] = icon;
                    return icon;
                }
            }
        }

        /// <summary>Every window icon name that is embedded.</summary>
        internal static IEnumerable<string> Names =>
            Array.ConvertAll(
                Array.FindAll(typeof(WindowIcons).Assembly.GetManifestResourceNames(),
                              n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal) &&
                                   n.EndsWith(".ico", StringComparison.Ordinal)),
                n => n.Substring(ResourcePrefix.Length, n.Length - ResourcePrefix.Length - 4));
    }
}
