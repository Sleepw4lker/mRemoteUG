using mRemoteUG.App;

namespace mRemoteUG.Config.Settings;

/// <summary>
/// The arithmetic <see cref="SettingsLoader"/> uses to put the main window back where it was
/// left: the DPI rescaling of the saved size and toolbar offset, and the correction that keeps
/// the window on screen.
/// </summary>
/// <remarks>
/// Kept apart from <see cref="SettingsLoader"/> only so the tests can reach it: the loader needs
/// a constructed <c>FrmMain</c>, this needs nothing but its arguments.
/// </remarks>
internal static class MainFormPlacement
{
    /// <summary>How much of the window has to stay on screen horizontally.</summary>
    internal const int MinVisibleHorizontal = 300;

    /// <summary>How much of the window has to stay on screen vertically.</summary>
    internal const int MinVisibleVertical = 150;

    /// <summary>
    /// Converts a window size saved at one DPI into the equivalent size at the DPI of the
    /// monitor the window is about to reopen on.
    /// </summary>
    /// <remarks>
    /// A saved size is physical pixels, so on its own it means nothing: 1600x1000 fills a
    /// quarter of a 4K panel at 100% and all of it at 200%. Before per-monitor awareness the
    /// question did not arise, because Windows virtualised every size to 96 DPI.
    /// <para>
    /// The location is deliberately not scaled - it is a desktop coordinate, not a measurement,
    /// and the caller already clamps it back on screen. Settings written before this DPI was
    /// recorded have <c>MainFormDpi</c> at 0 and are taken at face value, which is what they
    /// meant when they were written; the monitor is then not asked at all.
    /// </para>
    /// </remarks>
    internal static Size RescaleSavedSize(Size savedSize, Point savedLocation, int savedDpi,
                                          Func<Point, int> dpiForPoint)
    {
        if (savedDpi <= 0)
        {
            return savedSize;
        }

        int targetDpi = dpiForPoint(savedLocation);
        if (targetDpi <= 0 || targetDpi == savedDpi)
        {
            return savedSize;
        }

        return new Size((int)Math.Round(savedSize.Width * targetDpi / (double)savedDpi),
                        (int)Math.Round(savedSize.Height * targetDpi / (double)savedDpi));
    }

    /// <summary>
    /// Converts a toolbar offset saved at one DPI into the equivalent offset at the current one.
    /// </summary>
    /// <remarks>
    /// Unlike the window location this really is a measurement - an offset within the main
    /// window's ToolStripPanel - so it scales. It shares <c>MainFormDpi</c> because both are
    /// written by the same save.
    /// </remarks>
    internal static Point RescaleSavedToolbarLocation(Point saved, int savedDpi, int currentDpi)
    {
        if (savedDpi <= 0 || savedDpi == currentDpi)
        {
            return saved;
        }

        return new Point((int)Math.Round(saved.X * currentDpi / (double)savedDpi),
                         (int)Math.Round(saved.Y * currentDpi / (double)savedDpi));
    }

    /// <summary>
    /// The effective DPI of the monitor nearest a desktop point, or 0 if Windows will not say.
    /// </summary>
    internal static int DpiForPoint(Point point)
    {
        nint monitor = NativeMethods.MonitorFromPoint(point, NativeMethods.MONITOR_DEFAULTTONEAREST);
        if (monitor == IntPtr.Zero)
        {
            return 0;
        }

        return NativeMethods.GetDpiForMonitor(monitor, NativeMethods.MonitorDpiType.Effective,
                                              out uint dpiX, out _) == 0
            ? (int)dpiX
            : 0;
    }

    /// <summary>
    /// Where the window has to go so that at least <see cref="MinVisibleHorizontal"/> by
    /// <see cref="MinVisibleVertical"/> pixels of it stay on <paramref name="screenBounds"/>.
    /// </summary>
    internal static Point KeepOnScreen(Rectangle windowBounds, Rectangle screenBounds)
    {
        Rectangle newBounds = windowBounds;

        if (newBounds.Right < screenBounds.Left + MinVisibleHorizontal)
        {
            newBounds.X = screenBounds.Left + MinVisibleHorizontal - newBounds.Width;
        }

        if (newBounds.Left > screenBounds.Right - MinVisibleHorizontal)
        {
            newBounds.X = screenBounds.Right - MinVisibleHorizontal;
        }

        if (newBounds.Bottom < screenBounds.Top + MinVisibleVertical)
        {
            newBounds.Y = screenBounds.Top + MinVisibleVertical - newBounds.Height;
        }

        if (newBounds.Top > screenBounds.Bottom - MinVisibleVertical)
        {
            newBounds.Y = screenBounds.Bottom - MinVisibleVertical;
        }

        return newBounds.Location;
    }
}
