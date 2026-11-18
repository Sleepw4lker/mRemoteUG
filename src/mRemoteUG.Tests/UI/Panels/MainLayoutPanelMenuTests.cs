using mRemoteUG.App;
using mRemoteUG.UI.Window;

using NUnit.Framework;

namespace mRemoteUG.Tests.UI.Panels;

/// <summary>
/// A right-click on a panel's tab in the documents strip opens that panel's menu (Rename).
/// </summary>
/// <remarks>
/// The panel menu is handed to the panel with <c>OwnContextMenu</c>, as <c>PanelAdder</c> does,
/// and the right-click is a real <c>WM_RBUTTONDOWN</c>/<c>WM_RBUTTONUP</c> pair sent to the
/// documents strip.
/// </remarks>
[TestFixture]
[Apartment(ApartmentState.STA)]
public class MainLayoutPanelMenuTests
{
    private const int WM_RBUTTONDOWN = 0x0204;
    private const int WM_RBUTTONUP = 0x0205;
    private const int MK_RBUTTON = 0x0002;

    [Test]
    public void RightClickingAPanelTabOpensThatPanelsMenu()
    {
        using var harness = new MainLayoutHarness();
        using var first = new ConnectionWindow("first panel");
        using var second = new ConnectionWindow("second panel");
        int firstOpenings = 0;
        int secondOpenings = 0;
        first.OwnContextMenu(MenuCounting(() => firstOpenings++));
        second.OwnContextMenu(MenuCounting(() => secondOpenings++));
        harness.Layout.AddDocument(first);
        harness.Layout.AddDocument(second);
        harness.Form.Show();

        Rectangle secondTab = harness.Tabs.GetTabRect(1);
        Assume.That(secondTab.Width, Is.GreaterThan(0), "The documents strip shows no tabs.");
        var point = new Point(secondTab.Left + secondTab.Width / 2, secondTab.Top + secondTab.Height / 2);
        IntPtr lParam = (IntPtr)((point.Y << 16) | (point.X & 0xFFFF));
        NativeMethods.SendMessage(harness.Tabs.Handle, WM_RBUTTONDOWN, (IntPtr)MK_RBUTTON, lParam);
        NativeMethods.SendMessage(harness.Tabs.Handle, WM_RBUTTONUP, IntPtr.Zero, lParam);

        Assert.Multiple(() =>
        {
            Assert.That(secondOpenings, Is.EqualTo(1), "The right-clicked panel's menu did not open.");
            Assert.That(firstOpenings, Is.Zero, "The other panel's menu opened.");
        });
    }

    private static ContextMenuStrip MenuCounting(Action opened)
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem("Rename"));
        menu.Opening += (sender, e) =>
        {
            opened();
            e.Cancel = true;
        };
        return menu;
    }
}
