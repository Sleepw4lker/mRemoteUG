using System.Diagnostics.CodeAnalysis;

using mRemoteUG.App;
// ReSharper disable MemberCanBeMadeStatic.Global

namespace mRemoteUG.Tools;

// Holds the window's system menu as a borrowed handle: the window owns the menu and destroys it
// together with itself, so nothing here releases it.
public sealed class SystemMenu
{
    [Flags]
    [SuppressMessage("Naming", "CA1707",
        Justification = "Names mirror the Win32 API: each member is the MF_ or WM_ constant it carries.")]
    [SuppressMessage("Design", "CA1069",
        Justification = "Names mirror the Win32 API: MF_STRING and MF_BYCOMMAND are both 0 there.")]
    public enum MenuItemOptions
    {
        MF_STRING = NativeMethods.MF_STRING,
        MF_SEPARATOR = NativeMethods.MF_SEPARATOR,
        MF_BYCOMMAND = NativeMethods.MF_BYCOMMAND,
        MF_BYPOSITION = NativeMethods.MF_BYPOSITION,
        MF_POPUP = NativeMethods.MF_POPUP,
        WM_SYSCOMMAND = NativeMethods.WM_SYSCOMMAND
    }

    internal IntPtr SystemMenuHandle;
    private readonly IntPtr _formHandle;

    public SystemMenu(IntPtr handle)
    {
        _formHandle = handle;
        SystemMenuHandle = NativeMethods.GetSystemMenu(_formHandle, false);
    }

    public void Reset()
    {
        // Reverting returns NULL by contract, so the restored menu is read again.
        NativeMethods.GetSystemMenu(_formHandle, true);
        SystemMenuHandle = NativeMethods.GetSystemMenu(_formHandle, false);
    }

    public static void AppendMenuItem(IntPtr parentMenu, MenuItemOptions flags, IntPtr id, string text)
    {
        NativeMethods.AppendMenu(parentMenu, (int)flags, id, text);
    }

    public static IntPtr CreatePopupMenuItem()
    {
        return NativeMethods.CreatePopupMenu();
    }

    public static bool InsertMenuItem(IntPtr sysMenu, int position, MenuItemOptions flags, IntPtr subMenu, string? text)
    {
        return NativeMethods.InsertMenu(sysMenu, position, (int)flags, subMenu, text);
    }

}
