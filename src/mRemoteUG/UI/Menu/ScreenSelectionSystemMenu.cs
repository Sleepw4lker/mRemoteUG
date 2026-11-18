using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;

using mRemoteUG.App;
using mRemoteUG.Messages;
using mRemoteUG.Tools;

namespace mRemoteUG.UI.Menu;

// This class creates new menu items to menu that appears when you right click the top of the app
// (where the window title is)
public class ScreenSelectionSystemMenu
{
    private readonly SystemMenu _systemMenu;
    private readonly int[] _sysMenSubItems = new int[51];

    public ScreenSelectionSystemMenu(IWin32Window boundControl)
    {
        _systemMenu = new SystemMenu(boundControl.Handle);
    }

    public Screen? GetScreenById(int id)
    {
        for (int i = 0; i <= _sysMenSubItems.Length - 1; i++)
        {
            if (_sysMenSubItems[i] != id)
            {
                continue;
            }

            return Screen.AllScreens[i];
        }
        return null;
    }

    public void OnDisplayChanged(object? sender, EventArgs e)
    {
        ResetScreenList();
        BuildScreenList();
    }

    private void ResetScreenList()
    {
        _systemMenu.Reset();
    }

    public void BuildScreenList()
    {
        nint popMen = SystemMenu.CreatePopupMenuItem();

        for (int i = 0; i <= Screen.AllScreens.Length - 1; i++)
        {
            _sysMenSubItems[i] = 200 + i;
            SystemMenu.AppendMenuItem(
                popMen,
                SystemMenu.MenuItemOptions.MF_STRING,
                new IntPtr(_sysMenSubItems[i]),
                Language.strScreen + " " + Convert.ToString(i + 1, CultureInfo.CurrentCulture));
        }

        bool popupInserted = SystemMenu.InsertMenuItem(
            _systemMenu.SystemMenuHandle,
            0,
            SystemMenu.MenuItemOptions.MF_POPUP | SystemMenu.MenuItemOptions.MF_BYPOSITION,
            popMen,
            Language.strSendTo);
        int popupError = Marshal.GetLastPInvokeError();
        bool separatorInserted = SystemMenu.InsertMenuItem(
            _systemMenu.SystemMenuHandle,
            1,
            SystemMenu.MenuItemOptions.MF_BYPOSITION | SystemMenu.MenuItemOptions.MF_SEPARATOR,
            IntPtr.Zero,
            null);
        int separatorError = Marshal.GetLastPInvokeError();

        ReportFailure(popupInserted, "Send to", popupError);
        ReportFailure(separatorInserted, "Send to separator", separatorError);
    }

    private static void ReportFailure(bool inserted, string what, int error)
    {
        if (inserted)
        {
            return;
        }

        Runtime.MessageCollector.AddMessage(
            MessageClass.WarningMsg,
            $"The {what} entry could not be added to the title-bar menu: {new Win32Exception(error).Message} " +
            $"(Win32 error {error}).");
    }
}
