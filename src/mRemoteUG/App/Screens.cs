using mRemoteUG.UI.Forms;

namespace mRemoteUG.App;

public static class Screens
{
    public static void SendFormToScreen(Screen screen)
    {
        FrmMain frmMain = FrmMain.Default;
        bool wasMax = false;

        if (frmMain.WindowState == FormWindowState.Maximized)
        {
            wasMax = true;
            frmMain.WindowState = FormWindowState.Normal;
        }

        frmMain.Location = screen.Bounds.Location;

        if (wasMax)
        {
            frmMain.WindowState = FormWindowState.Maximized;
        }
    }
}
