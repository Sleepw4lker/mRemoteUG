using System.Runtime.InteropServices;

using mRemoteUG.App;
using mRemoteUG.Tools;
using mRemoteUG.UI.Menu;

using NUnit.Framework;

namespace mRemoteUG.Tests.Tools;

/// <summary>
/// <see cref="SystemMenu"/> holds the window's system menu as a borrowed handle: the window owns the
/// menu and destroys it itself, so nothing here may release it.
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
public class SystemMenuTests
{
    [Test]
    public void HeldHandleIsAValidMenuAndStaysOneAfterTheOwnerIsDropped()
    {
        using var form = new Form { ShowInTaskbar = false };
        IntPtr formMenu = NativeMethods.GetSystemMenu(form.Handle, false);
        var menu = new SystemMenu(form.Handle);
        var screens = new ScreenSelectionSystemMenu(form);
        screens.BuildScreenList();
        screens = null;
        GC.Collect();
        GC.WaitForPendingFinalizers();

        Assert.Multiple(() =>
        {
            Assert.That(NativeMethods.IsMenu(menu.SystemMenuHandle), Is.True, "SystemMenu holds no valid menu.");
            Assert.That(menu.SystemMenuHandle, Is.EqualTo(formMenu));
            Assert.That(NativeMethods.IsMenu(formMenu), Is.True, "The form's system menu was destroyed.");
        });
    }

    [Test]
    public void SendToPopupIsInTheSystemMenuExactlyOnceAfterBuildResetBuild()
    {
        using var form = new Form { ShowInTaskbar = false };
        var screens = new ScreenSelectionSystemMenu(form);

        screens.BuildScreenList();
        screens.OnDisplayChanged(null, EventArgs.Empty);
        IntPtr current = NativeMethods.GetSystemMenu(form.Handle, false);

        Assert.Multiple(() =>
        {
            Assert.That(CountPopups(current, Language.strSendTo), Is.EqualTo(1),
                "The Send to popup is not in the system menu exactly once.");
            Assert.That(NativeMethods.IsMenu(current), Is.True);
        });
    }

    [Test]
    public void ResetKeepsAValidHandleToTheRestoredMenu()
    {
        using var form = new Form { ShowInTaskbar = false };
        var menu = new SystemMenu(form.Handle);

        menu.Reset();

        Assert.That(NativeMethods.IsMenu(menu.SystemMenuHandle), Is.True,
            "Reset left SystemMenu without a valid menu handle.");
    }

    private static int CountPopups(IntPtr menu, string text)
    {
        int count = 0;
        for (int i = 0; i < GetMenuItemCount(menu); i++)
        {
            char[] buffer = new char[256];
            int length = GetMenuString(menu, i, buffer, buffer.Length, MfByPosition);
            if (GetSubMenu(menu, i) != IntPtr.Zero && new string(buffer, 0, length) == text)
            {
                count++;
            }
        }

        return count;
    }

    private const int MfByPosition = 0x400;

    [DllImport("user32.dll")]
    private static extern int GetMenuItemCount(IntPtr hMenu);

    [DllImport("user32.dll")]
    private static extern IntPtr GetSubMenu(IntPtr hMenu, int nPos);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetMenuString(IntPtr hMenu, int uIDItem, char[] lpString, int nMaxCount,
        int uFlag);
}
