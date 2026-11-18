using System.Runtime.InteropServices;

using mRemoteUG.App;

namespace mRemoteUG.Connection.Protocol;

/// <summary>
/// Turns a stock PuTTY window into a chromeless, maximised window inside a panel, gives it the
/// keyboard, and reads back whether it stayed that way.
/// </summary>
/// <remarks>
/// Shared by <see cref="PuttyBase"/> and <c>SelfTest</c>, so the sequence the self-test
/// proves against a real PuTTY is the sequence a session runs.
/// </remarks>
public static class HostedPuttyWindow
{
    /// <summary>
    /// Polls <paramref name="probe"/> until it returns a window handle or the timeout passes.
    /// </summary>
    /// <remarks>
    /// TickCount64, not TickCount: the 32-bit counter wraps after about 24.9 days of uptime,
    /// and a wrapped deadline either fell straight through or waited out the full wrap.
    /// Sleep(10), not Sleep(0): the latter yields only to a ready thread of the same priority
    /// on the same core, so the loop burned a core while PuTTY started. It does not pump,
    /// unlike RdpProtocol.PumpUntil; nothing here needs to stay responsive.
    /// </remarks>
    public static IntPtr WaitForHandle(Func<IntPtr> probe, TimeSpan timeout)
    {
        long deadline = Environment.TickCount64 + (long)timeout.TotalMilliseconds;
        nint handle = probe();
        while (handle == IntPtr.Zero && Environment.TickCount64 < deadline)
        {
            Thread.Sleep(10);
            handle = probe();
        }

        return handle;
    }

    /// <summary>
    /// Makes <paramref name="hwnd"/> a chromeless, maximised window inside <paramref name="parent"/>.
    /// </summary>
    /// <remarks>
    /// Stripping WS_CAPTION|WS_THICKFRAME|WS_BORDER and applying it with SWP_FRAMECHANGED does
    /// not work on PuTTY, and 890b245 shipped exactly that. PuTTY's WM_SIZE handler
    /// (windows/window.c) calls clear_full_screen() on every SIZE_RESTORED, which puts
    /// WS_CAPTION|WS_BORDER|WS_THICKFRAME straight back - and the frame change that applies a
    /// strip shrinks the client area, which is a SIZE_RESTORED. The strip undoes itself before
    /// it returns, and so does every MoveWindow after it.
    /// <para>
    /// A maximised window only ever receives SIZE_MAXIMIZED, which takes a branch that never
    /// touches the style. Zoomed and captionless is also PuTTY's own definition of full-screen
    /// (is_full_screen()), in which it sizes the terminal grid from whatever client area it is
    /// given, centres it, and refuses server-side requests to resize the window. That is what
    /// an embedded terminal wants, so the ShowWindow(SW_MAXIMIZE) below is the fix, not a
    /// leftover. Do not remove it, and do not put a SetWindowPos(SWP_FRAMECHANGED) before it.
    /// </para>
    /// <para>
    /// WS_CHILD is deliberately NOT set, against the SetParent documentation's advice, because
    /// a child window can never be the foreground window and keyboard input goes to the
    /// foreground thread's focus window: with WS_CHILD, SetForegroundWindow activated the
    /// main form instead and PuTTY never saw a keystroke or a WM_SETFOCUS (its cursor stayed
    /// hollow). Left top-level with a parent, which is how this application hosted PuTTY
    /// before 890b245, it activates on its own when clicked and Focus() can make it
    /// foreground.
    /// </para>
    /// <para>
    /// SetParent itself is where the WM_SIZE that undoes the strip can come from - measured,
    /// not assumed, with a 50-run loopback harness outside this repo: 2 of 50 runs
    /// read back a window that was both zoomed (the ShowWindow(SW_MAXIMIZE) below had already
    /// run) and captioned, style 0x15EF0000 against the clean 0x152B0000 - exactly "before"
    /// OR WS_MAXIMIZE. SIZE_MAXIMIZED never touches the style, so the only way to end up
    /// zoomed with a caption is a SIZE_RESTORED landing on PuTTY's thread somewhere between
    /// the strip above and the maximize below, most likely from SetParent recomputing the
    /// window's position against its new parent. Retrying the strip and the maximize once,
    /// after reading the result back, closes that window: SetParent runs exactly once, so a
    /// retry that repeats only the strip and the maximize is not exposed to the same race
    /// again - confirmed by rerunning the harness with the retry in place for 150 iterations
    /// with no caption surviving it.
    /// </para>
    /// </remarks>
    public static void Adopt(IntPtr hwnd, IntPtr parent)
    {
        if (hwnd == IntPtr.Zero)
        {
            throw new ArgumentException("There is no window to adopt.", nameof(hwnd));
        }

        if (parent == IntPtr.Zero)
        {
            throw new ArgumentException("There is no panel to adopt the window into.", nameof(parent));
        }

        StripChromeStyles(hwnd);

        NativeMethods.SetParent(hwnd, parent);
        // GetAncestor, not GetParent: the latter reports the owner for a window that is not WS_CHILD,
        // and this one is deliberately not.
        if (NativeMethods.GetAncestor(hwnd, NativeMethods.GA_PARENT) != parent)
        {
            throw new InvalidOperationException(
                $"SetParent did not take: the window's parent is 0x" +
                $"{NativeMethods.GetAncestor(hwnd, NativeMethods.GA_PARENT).ToInt64():X}, " +
                $"not 0x{parent.ToInt64():X}. The two windows may differ in integrity level.");
        }

        _ = NativeMethods.ShowWindow(hwnd, (int)NativeMethods.SW_MAXIMIZE);

        // A SIZE_RESTORED that lands on PuTTY's thread during SetParent puts the caption back
        // (see remarks) after the strip above already ran but before this ShowWindow. One
        // retry - strip, then maximize again, neither of which is the step the race comes
        // from - has been enough every time it has been exercised.
        if (Describe(hwnd).HasCaption)
        {
            StripChromeStyles(hwnd);
            _ = NativeMethods.ShowWindow(hwnd, (int)NativeMethods.SW_MAXIMIZE);
        }
    }

    /// <summary>
    /// Strips the caption, thick frame and both 3D edges PuTTY starts with. No
    /// SWP_FRAMECHANGED: that would itself be a client-size change, which is a SIZE_RESTORED,
    /// which is the exact failure this class exists to avoid (see <see cref="Adopt"/>).
    /// </summary>
    private static void StripChromeStyles(IntPtr hwnd)
    {
        long style = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_STYLE).ToInt64();
        style &= ~(long)(NativeMethods.WS_CAPTION | NativeMethods.WS_THICKFRAME | NativeMethods.WS_BORDER);
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_STYLE, new IntPtr(style));

        // WS_EX_CLIENTEDGE is PuTTY's "sunken edge" option, a 2px 3D border that the style strip
        // above does not remove; WS_EX_WINDOWEDGE is the raised edge Windows adds to any framed window.
        long exStyle = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE).ToInt64();
        exStyle &= ~(long)(NativeMethods.WS_EX_CLIENTEDGE | NativeMethods.WS_EX_WINDOWEDGE);
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(exStyle));
    }

    /// <summary>
    /// Gives the hosted window the keyboard.
    /// </summary>
    /// <remarks>
    /// SetForegroundWindow, not SetFocus: focus is per thread, and keystrokes are delivered to
    /// the focus window of the thread that owns the foreground window. Only making PuTTY's own
    /// window foreground puts its thread in that position, which is why the window must not be
    /// WS_CHILD (see <see cref="Adopt"/>).
    /// </remarks>
    public static void Focus(IntPtr hwnd)
    {
        NativeMethods.SetForegroundWindow(hwnd);
    }

    /// <summary>
    /// Tells the hosted window that it is now shown at <paramref name="dpi"/>, as Windows would
    /// tell a top-level window.
    /// </summary>
    /// <returns>Null when the window took the message, otherwise why it did not.</returns>
    /// <remarks>
    /// Windows sends WM_DPICHANGED to top-level windows only. A reparented PuTTY is not one, so
    /// it never hears of a dock or an undock and keeps the font size of the monitor it started
    /// on until it is restarted (#262). PuTTY 0.85 handles the message itself, in
    /// windows/window.c: it takes the new DPI from wParam, copies the RECT lParam points at,
    /// rebuilds its fonts, and - maximised, as <see cref="Adopt"/> leaves it - keeps its window
    /// and fits the terminal's rows and columns to it. The RECT is used only when PuTTY is not
    /// maximised, as where to move to, so this passes where the window already is.
    /// <para>
    /// Sending a pointer to another process is safe here only because user32 marshals
    /// WM_DPICHANGED's RECT - measured, not assumed, against a real PuTTY 0.85 (see
    /// docs/platform-findings.md, PuTTY hosting): a non-maximised PuTTY moved to exactly the
    /// rectangle sent from this process; an lParam that is not a valid pointer here failed in
    /// the sender and left PuTTY untouched; and PostMessage refuses the message with
    /// ERROR_MESSAGE_SYNC_ONLY, which user32 reserves for messages whose parameters it copies.
    /// </para>
    /// <para>
    /// SendMessageTimeout with SMTO_ABORTIFHUNG, not SendMessage: this runs on the UI thread,
    /// and a hung PuTTY must not take the desktop with it (ADR-0005). It cannot be posted.
    /// </para>
    /// </remarks>
    public static string? TellDpi(IntPtr hwnd, int dpi)
    {
        if (!NativeMethods.GetWindowRect(hwnd, out NativeMethods.RECT where))
        {
            return $"GetWindowRect failed (error {Marshal.GetLastPInvokeError()})";
        }

        IntPtr wParam = new((dpi << 16) | (dpi & 0xFFFF));
        IntPtr sent = NativeMethods.SendMessageTimeout(
            hwnd, NativeMethods.WM_DPICHANGED, wParam, ref where,
            NativeMethods.SMTO_ABORTIFHUNG, TellDpiTimeoutMs, out _);
        return sent != IntPtr.Zero
            ? null
            : $"SendMessageTimeout failed or timed out (error {Marshal.GetLastPInvokeError()})";
    }

    /// <summary>
    /// How long the UI thread waits for PuTTY to rebuild its fonts. It took 1-2 ms in the
    /// probe; a PuTTY that takes longer than this is treated as hung.
    /// </summary>
    private const uint TellDpiTimeoutMs = 1000;

    /// <summary>
    /// True when a keystroke would reach <paramref name="hwnd"/> right now: its thread owns the
    /// foreground window and has that window as its focus.
    /// </summary>
    public static bool HasKeyboardFocus(IntPtr hwnd)
    {
        if (NativeMethods.GetForegroundWindow() != hwnd)
        {
            return false;
        }

        uint thread = NativeMethods.GetWindowThreadProcessId(hwnd, out _);
        var info = new NativeMethods.GUITHREADINFO();
        info.cbSize = Marshal.SizeOf<NativeMethods.GUITHREADINFO>();
        return NativeMethods.GetGUIThreadInfo(thread, ref info) && info.hwndFocus == hwnd;
    }

    /// <summary>
    /// Reads back the state <see cref="Adopt"/> is supposed to leave a window in.
    /// </summary>
    public static HostedWindowState Describe(IntPtr hwnd)
    {
        return new HostedWindowState(
            NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_STYLE).ToInt64() & 0xFFFFFFFFL,
            NativeMethods.GetAncestor(hwnd, NativeMethods.GA_PARENT),
            NativeMethods.IsZoomed(hwnd));
    }
}

/// <summary>
/// What a hosted window looks like to Windows: the assertion the self-test makes and the
/// line the session log carries.
/// </summary>
public readonly record struct HostedWindowState(long Style, IntPtr Parent, bool IsZoomed)
{
    /// <summary>True when PuTTY has (re)instated its title bar.</summary>
    public bool HasCaption => (Style & NativeMethods.WS_CAPTION) == NativeMethods.WS_CAPTION;

    /// <summary>Must stay false: a child window cannot take the keyboard. See Adopt.</summary>
    public bool IsChild => (Style & NativeMethods.WS_CHILD) != 0;

    public override string ToString()
    {
        return $"style 0x{Style:X8}, caption {(HasCaption ? "present" : "absent")}, " +
               $"{(IsChild ? "child" : "top-level")}, {(IsZoomed ? "zoomed" : "not zoomed")}, " +
               $"parent 0x{Parent.ToInt64():X}";
    }
}
