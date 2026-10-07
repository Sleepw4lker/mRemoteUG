using System;
using System.Drawing;

namespace mRemoteUG.Connection.Protocol.RDP
{
    /// <summary>
    /// Turns a local display DPI and panel size into arguments
    /// <c>IMsRdpClient9.UpdateSessionDisplaySettings</c> will actually act on.
    /// </summary>
    /// <remarks>
    /// The constraints below are not this application's; they come from [MS-RDPEDISP], which
    /// defines what the display-control channel does with a monitor layout. They matter because
    /// **every one of them fails silently**: a value out of range is ignored, the call still
    /// returns S_OK, and the session simply does not change. There is nothing to catch and nothing
    /// to log except what was sent, which is why this is a separate, testable class rather than
    /// three expressions inside the resize path.
    /// </remarks>
    public static class RdpDisplayScale
    {
        /// <summary>The DPI at which a display is scaled 100%.</summary>
        public const int DefaultDpi = 96;

        public const uint MinDesktopScaleFactor = 100;
        public const uint MaxDesktopScaleFactor = 500;

        /// <summary>Smallest session dimension the protocol accepts.</summary>
        public const int MinSessionDimension = 200;

        /// <summary>Largest session dimension the protocol accepts.</summary>
        public const int MaxSessionDimension = 8192;

        /// <summary>
        /// The remote desktop's scale factor, as a percentage, for a local display DPI.
        /// </summary>
        /// <remarks>
        /// Ignored by the server if outside 100-500, so it is clamped rather than passed through.
        /// </remarks>
        public static uint DesktopScaleFactor(int dpi)
        {
            if (dpi <= 0)
                return MinDesktopScaleFactor;

            var percent = (uint)Math.Round(dpi * 100.0 / DefaultDpi, MidpointRounding.AwayFromZero);
            return Math.Min(Math.Max(percent, MinDesktopScaleFactor), MaxDesktopScaleFactor);
        }

        /// <summary>
        /// The remote device scale factor for a local display DPI.
        /// </summary>
        /// <remarks>
        /// The protocol accepts exactly three values - 100, 140 and 180 - and ignores the desktop
        /// scale factor too if this one is not one of them, so a "close enough" value is not close
        /// enough. [MS-RDPEDISP] gives no mapping from a DPI, so this picks the nearest of the
        /// three: 100% stays 100, 125% and 150% become 140, 175% and above become 180.
        /// </remarks>
        public static uint DeviceScaleFactor(int dpi)
        {
            var percent = (int)DesktopScaleFactor(dpi);

            var nearest = 100u;
            var smallestGap = Math.Abs(percent - 100);

            foreach (var candidate in new[] { 140, 180 })
            {
                var gap = Math.Abs(percent - candidate);
                if (gap >= smallestGap)
                    continue;

                smallestGap = gap;
                nearest = (uint)candidate;
            }

            return nearest;
        }

        /// <summary>
        /// Adjusts a panel size to something the protocol will accept, if it can.
        /// </summary>
        /// <remarks>
        /// The width must not be odd - an odd width makes the server ignore the whole layout - so
        /// it is rounded down rather than up, which keeps the session inside the panel. False means
        /// no adjustment would help: the panel is smaller than 200 pixels or larger than 8192, the
        /// latter being reachable on a 4K display now that sizes are physical pixels rather than
        /// virtualised ones.
        /// </remarks>
        public static bool TryNormaliseSessionSize(Size requested, out Size normalised)
        {
            var width = requested.Width - (requested.Width % 2);
            normalised = new Size(width, requested.Height);

            return width >= MinSessionDimension && width <= MaxSessionDimension &&
                   requested.Height >= MinSessionDimension && requested.Height <= MaxSessionDimension;
        }
    }
}
