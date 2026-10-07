using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using mRemoteUG.App;
using mRemoteUG.Connection;
using mRemoteUG.Container;
using mRemoteUG.Tree.Root;


namespace mRemoteUG.UI.Controls
{
    public class StatusImageList : IDisposable
    {
        /// <summary>The icon size at 96 DPI. Every glyph this application ships is this size.</summary>
        private const int LogicalIconSize = 16;

        private int _dpi = DpiScaling.DefaultDpi;

        public ImageList ImageList { get; }

        public StatusImageList()
        {
            ImageList = new ImageList
            {
                ColorDepth = ColorDepth.Depth32Bit,
                ImageSize = new Size(LogicalIconSize, LogicalIconSize),
                TransparentColor = Color.Transparent
            };

            FillImageList(ImageList);
        }

        /// <summary>
        /// The icon size in device pixels at the DPI this list was last scaled for.
        /// </summary>
        private int ScaledIconSize => DpiScaling.Scale(LogicalIconSize, _dpi);

        /// <summary>
        /// Resizes the icon slots for a new DPI.
        /// </summary>
        /// <remarks>
        /// Every image is re-resolved at the new size rather than stretched, which is what the
        /// artwork gaining a ladder of frames bought. Each key is enough to rebuild its own
        /// image: the node keys name glyphs, and a connection key carries the icon name and
        /// whether it is connected.
        /// <para>
        /// The caller must re-assign the owning control's <c>ImageList</c> property afterwards.
        /// A TreeView re-measures its row height only on that assignment, not when the list it
        /// already holds changes size.
        /// </para>
        /// </remarks>
        public void RescaleForDpi(int dpi)
        {
            if (_dpi == dpi)
                return;

            _dpi = dpi;
            DpiScaling.ResizeForDpi(ImageList, LogicalIconSize, dpi, Resolve);
        }

        /// <summary>
        /// Rebuilds one entry of the list from its key, at a size.
        /// </summary>
        /// <remarks>
        /// The keys are the only thing that survives a resize, so they have to be enough to
        /// redraw from - which is why the connection keys encode the icon name and the connected
        /// state rather than being opaque handles.
        /// </remarks>
        /// <summary>A badged icon this list owns and releases, built once per key and size.</summary>
        private Bitmap Composite(string key, int size, Image plain)
        {
            if (_composited.TryGetValue((key, size), out var cached))
                return cached;

            var badged = Badged(plain, size);
            _composited[(key, size)] = badged;
            return badged;
        }

        private Image Resolve(string key, int size)
        {
            // DpiScaling.ResizeForDpi disposes nothing, so whatever is handed back has to outlive
            // the call. Glyphs and connection icons are already shared cache instances; a badged
            // one is not, so Composite owns it and Dispose releases it. Without that the list
            // leaked a bitmap and its GDI handle per connection icon on every DPI change.
            return Build(key, size);
        }

        private readonly Dictionary<(string Key, int Size), Bitmap> _composited = new Dictionary<(string, int), Bitmap>();

        private Image Build(string key, int size)
        {
            if (key == "Root" || key == "Folder" || key == "PuttySessions")
                return Glyphs.Get(key, size);

            if (key == DefaultConnectionIcon)
                return Glyphs.Get("RDP", size);
            if (key == DefaultConnectionIconConnected)
                return Composite(key, size, Glyphs.Get("RDP", size));

            if (key.StartsWith(ConnectionKeyPrefix, StringComparison.Ordinal))
            {
                var body = key.Substring(ConnectionKeyPrefix.Length);
                var connected = body.EndsWith(ConnectedSuffix, StringComparison.Ordinal);
                var icon = body.Substring(0, body.Length -
                                             (connected ? ConnectedSuffix.Length : DefaultSuffix.Length));

                var plain = ConnectionIcon.FromString(icon, size);
                if (plain == null)
                    return null;

                // Shared and cached, so the unbadged case hands the cache instance straight back
                // and Resolve will not take ownership of it.
                return connected ? Composite(key, size, plain) : plain;
            }

            return null;
        }

        public string GetKey(ConnectionInfo connectionInfo)
        {
            if (connectionInfo == null) return "";
            if (connectionInfo is RootPuttySessionsNodeInfo) return "PuttySessions";
            if (connectionInfo is RootNodeInfo) return "Root";
            if (connectionInfo is ContainerInfo) return "Folder";
            
            return GetConnectionIcon(connectionInfo);
        }

        // The key has to be enough to rebuild the image from on a DPI change, so it carries the
        // icon name and the connected state rather than being an opaque handle. Resolve reads it
        // back apart again.
        private const string ConnectionKeyPrefix = "Connection_";
        private const string ConnectedSuffix = "_Play";
        private const string DefaultSuffix = "_Default";

        private static string BuildConnectionIconName(string icon, bool connected)
        {
            return ConnectionKeyPrefix + icon + (connected ? ConnectedSuffix : DefaultSuffix);
        }

        // A TreeNode with an empty ImageKey falls back to the control's default image,
        // i.e. the root icon, so the fallback has to name a real image. Connections land
        // here when their stored Icon names something this build does not carry: an older
        // connections file, or an icon a user had dropped in the Icons folder back when
        // that folder shipped.
        private const string DefaultConnectionIcon = "Connection_Default";
        private const string DefaultConnectionIconConnected = "Connection_Default_Play";

        private static string DefaultIconFor(bool connected)
        {
            return connected ? DefaultConnectionIconConnected : DefaultConnectionIcon;
        }

        private string GetConnectionIcon(ConnectionInfo connection)
        {
            var connected = connection.OpenConnections.Count > 0;

            if (string.IsNullOrEmpty(connection.Icon))
            {
                return DefaultIconFor(connected);
            }

            var name = BuildConnectionIconName(connection.Icon, connected);
            if (!ImageList.Images.ContainsKey(name))
            {
                var plain = ConnectionIcon.FromString(connection.Icon, ScaledIconSize);
                if (plain == null)
                {
                    return DefaultIconFor(connected);
                }

                // Neither is disposed. The plain one is the shared cache instance, and the badged
                // one is cached here and released in Dispose - the list may not have a handle yet
                // at this point, and before it does it keeps the reference rather than copying.
                ImageList.Images.Add(BuildConnectionIconName(connection.Icon, false), plain);
                ImageList.Images.Add(BuildConnectionIconName(connection.Icon, true),
                                     Composite(name, ScaledIconSize, plain));

            }
            return name;
        }

        /// <summary>
        /// A connection icon with the connected badge composited onto it.
        /// </summary>
        /// <remarks>
        /// The badge is drawn over the full frame, not at its own size. While every image in the
        /// application was 16x16 those were the same thing; once icons come in eight sizes they
        /// are not, and drawing a 16 pixel badge onto a 32 pixel icon put it at quarter size in
        /// the top-left corner. The badge artwork is generated already inset into its own corner
        /// (see icon-manifest.json), so this stays a straight 1:1 blit.
        /// </remarks>
        private static Bitmap Badged(Image background, int size)
        {
            var badge = Glyphs.Get("ConnectedOverlay", size);

            var result = new Bitmap(background, size, size);
            using (var gr = Graphics.FromImage(result))
            {
                gr.DrawImage(badge, new Rectangle(0, 0, size, size));
            }
            return result;
        }

        private static void FillImageList(ImageList imageList)
        {
            try
            {
                imageList.Images.Add("Root", Resources.Root);
                imageList.Images.Add("Folder", Resources.Folder);
                imageList.Images.Add("PuttySessions", Resources.PuttySessions);
                imageList.Images.Add(DefaultConnectionIcon, Resources.RDP);
                // Deliberately not disposed, and this is the one place that matters. An
                // ImageList does not copy what it is given until its native handle exists -
                // before that it keeps the reference and reads it during CreateHandle - and this
                // runs from the constructor, before anything has realized it. Disposing here
                // crashes the process later, from the handle access rather than from here, with
                // "Parameter is not valid".
                imageList.Images.Add(DefaultConnectionIconConnected, Badged(Resources.RDP, LogicalIconSize));
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage($"Unable to fill the image list of type {nameof(StatusImageList)}", ex);
            }
        }

        public void Dispose()
        {
            foreach (var image in _composited.Values)
                image.Dispose();
            _composited.Clear();

            ImageList?.Dispose();
        }
    }
}