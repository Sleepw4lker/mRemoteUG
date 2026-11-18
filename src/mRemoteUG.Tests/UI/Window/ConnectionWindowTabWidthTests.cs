using System.Diagnostics.CodeAnalysis;

using mRemoteUG.Connection;
using mRemoteUG.UI;
using mRemoteUG.UI.Forms;
using mRemoteUG.UI.Window;

using NUnit.Framework;

namespace mRemoteUG.Tests.UI.Window;

/// <summary>
/// Each connection Tab is as wide as its own icon, title and close button, the title is cut at a
/// maximum width with the full title as a tooltip, and a DPI change re-measures every Tab (#261).
/// </summary>
/// <remarks>
/// A DPI change is driven the way the other connection window tests drive it: the host's font
/// moves to what that DPI gives a 9pt font, and the window is told the DPI as a number, through
/// either of the two routes it has. That needs no per-monitor awareness, so this runs on the CI
/// runner. Every width is read back from the native control through <c>GetTabRect</c>, never from
/// the code that asked for it.
/// </remarks>
[TestFixture]
[Apartment(ApartmentState.STA)]
[SuppressMessage("Design", "CA1001",
    Justification = "[TearDown] disposes the hosts and fonts after every test. "
        + "NUnit would dispose an IDisposable fixture once, after its last test.")]
public class ConnectionWindowTabWidthTests
{
    private const string Ellipsis = "…";
    private const string ShortTitle = "dc1";
    private const string MediumTitle = "server1.example.org";
    private const string LongTitle =
        "a-very-long-host-name-that-will-not-fit-in-one-tab.subdomain.example.org (CONTOSO\\administrator)";

    private readonly List<Form> _hosts = [];
    private readonly List<Font> _fonts = [];

    [TearDown]
    public void TearDown()
    {
        foreach (Form host in _hosts)
        {
            host.Dispose();
        }

        foreach (Font font in _fonts)
        {
            font.Dispose();
        }

        _hosts.Clear();
        _fonts.Clear();
    }

    [Test]
    public void TabsWithDifferentTitlesHaveDifferentWidths()
    {
        ConnectionWindow window = OpenWindow(96);
        ConnectionTabPage shortTab = AddTab(window, ShortTitle);
        ConnectionTabPage mediumTab = AddTab(window, MediumTitle);

        Assert.That(WidthOf(window, mediumTab), Is.GreaterThan(WidthOf(window, shortTab)),
                    "The Tabs share one width, so a short title gets the room of the longest one.");
    }

    [Test]
    public void OpeningAndClosingALongTitledTabLeavesTheOthersAlone()
    {
        ConnectionWindow window = OpenWindow(96);
        ConnectionTabPage shortTab = AddTab(window, ShortTitle);
        int before = WidthOf(window, shortTab);

        ConnectionTabPage longTab = AddTab(window, LongTitle);
        int whileOpen = WidthOf(window, shortTab);

        // What CloseTab does once a Session's teardown reaches the Tab.
        window.TabController.TabPages.Remove(longTab);
        longTab.Dispose();
        Pump();
        int afterClose = WidthOf(window, shortTab);

        Assert.Multiple(() =>
        {
            Assert.That(whileOpen, Is.EqualTo(before), "Opening a long-titled Tab widened the short one.");
            Assert.That(afterClose, Is.EqualTo(before), "The short Tab kept the closed long Tab's width.");
        });
    }

    [Test]
    public void ATitleLongerThanTheMaximumIsCutAndGetsTheFullTitleAsATooltip()
    {
        ConnectionWindow window = OpenWindow(96);
        ConnectionTabPage tab = AddTab(window, LongTitle);
        int maximum = DpiScaling.Scale(ConnectionWindow.LogicalMaxTitleWidth, 96);

        Assert.Multiple(() =>
        {
            Assert.That(tab.Title, Is.EqualTo(LongTitle), "The Tab no longer knows its full title.");
            Assert.That(tab.DisplayTitle, Does.EndWith(Ellipsis));
            Assert.That(LongTitle, Does.StartWith(tab.DisplayTitle[..^1].TrimEnd()),
                        "The shortened title is not the start of the full one.");
            Assert.That(TextWidth(tab.DisplayTitle, window.TabController.Font), Is.LessThanOrEqualTo(maximum),
                        "The shown title is wider than the maximum.");
            Assert.That(tab.ToolTipText, Is.EqualTo(LongTitle));
            Assert.That(window.TabController.ShowToolTips, Is.True, "The strip does not show tooltips at all.");
        });
    }

    [Test]
    public void ATitleThatFitsIsShownWholeAndHasNoTooltip()
    {
        ConnectionWindow window = OpenWindow(96);
        ConnectionTabPage tab = AddTab(window, MediumTitle);

        Assert.Multiple(() =>
        {
            Assert.That(tab.DisplayTitle, Is.EqualTo(MediumTitle));
            Assert.That(tab.Text, Is.EqualTo(MediumTitle));
            Assert.That(tab.ToolTipText, Is.Empty);
        });
    }

    /// <summary>
    /// The native control measures <see cref="TabPage.Text"/>, which keeps the ampersand doubled;
    /// the strip draws the title itself and shows it once.
    /// </summary>
    [Test]
    public void AnAmpersandIsDoubledForTheNativeControlAndShownOnce()
    {
        ConnectionWindow window = OpenWindow(96);
        ConnectionTabPage tab = AddTab(window, "R&D");

        Assert.Multiple(() =>
        {
            Assert.That(tab.Title, Is.EqualTo("R&D"));
            Assert.That(tab.DisplayTitle, Is.EqualTo("R&D"));
            Assert.That(tab.Text, Is.EqualTo("R&&D"));
        });
    }

    /// <summary>
    /// What Rename Tab does with the dialog's answer: reads the full title in, writes the full
    /// title back.
    /// </summary>
    [Test]
    public void RenamingToALongTitleAndBackMovesTheWidthAndTheTooltip()
    {
        ConnectionWindow window = OpenWindow(96);
        ConnectionTabPage tab = AddTab(window, ShortTitle);
        int shortWidth = WidthOf(window, tab);

        window.RetitleTab(tab, LongTitle);
        Pump();
        int longWidth = WidthOf(window, tab);
        string longTooltip = tab.ToolTipText;
        string prefill = ConnectionWindow.TabTitleOf(tab);

        window.RetitleTab(tab, "R&D");
        Pump();

        Assert.Multiple(() =>
        {
            Assert.That(longWidth, Is.GreaterThan(shortWidth), "Renaming to a long title did not widen the Tab.");
            Assert.That(longTooltip, Is.EqualTo(LongTitle), "A Tab renamed to a cut title has no tooltip.");
            Assert.That(prefill, Is.EqualTo(LongTitle), "The rename dialog would be prefilled with the cut title.");
            Assert.That(WidthOf(window, tab), Is.LessThan(longWidth), "Renaming back to a short title kept the width.");
            Assert.That(tab.ToolTipText, Is.Empty, "A Tab renamed to a title that fits kept its tooltip.");
            Assert.That(ConnectionWindow.TabTitleOf(tab), Is.EqualTo("R&D"),
                        "The ampersand was doubled into the title the rename dialog shows.");
        });
    }

    /// <summary>
    /// comctl32 measures a Tab's text itself and adds the padding; the owner-draw lays out an icon,
    /// the text drawn with <see cref="TextRenderer"/> and a close button inside what it gets. The
    /// two must agree: narrower clips the title, wider leaves a gap.
    /// </summary>
    [TestCase(96)]
    [TestCase(144)]
    [TestCase(192)]
    public void TheNativeTabHoldsTheIconTheDrawnTitleAndTheCloseButtonWithoutAGap(int dpi)
    {
        ConnectionWindow window = OpenWindow(dpi);
        foreach (string title in new[] { MediumTitle, "Windows Server 2025 jump host", LongTitle })
        {
            AddTab(window, title);
        }

        int padding = DpiScaling.Scale(ConnectionWindow.LogicalTabContentPadding, dpi);
        int icon = DpiScaling.Scale(ConnectionWindow.LogicalIconSize, dpi);
        int close = DpiScaling.Scale(ConnectionWindow.LogicalCloseButtonSize, dpi);
        Assert.Multiple(() =>
        {
            foreach (ConnectionTabPage tab in window.TabController.TabPages.OfType<ConnectionTabPage>())
            {
                int text = TextWidth(tab.DisplayTitle, window.TabController.Font);
                int needed = padding + icon + padding + text + padding + close + padding;
                int width = WidthOf(window, tab);
                Assert.That(width, Is.GreaterThanOrEqualTo(needed),
                            $"'{tab.DisplayTitle}' at {dpi} DPI: the Tab is {width} px, its content needs {needed}.");
                // The padding is symmetric and sized for the wider side, so the narrower side has
                // the difference between the icon and the close button to spare - nothing more.
                Assert.That(width - needed, Is.LessThanOrEqualTo(Math.Abs(icon - close)),
                            $"'{tab.DisplayTitle}' at {dpi} DPI: the Tab is {width} px for {needed} px of content.");
            }
        });
    }

    /// <summary>
    /// The padding changes with every DPI change, and <see cref="TabControl.Padding"/> recreates the
    /// strip's window when assigned once it exists - with every session's hosted control inside it.
    /// </summary>
    [Test]
    public void ADpiChangeDoesNotRecreateTheTabStripsWindow()
    {
        ConnectionWindow window = OpenWindow(144);
        ConnectionTabPage tab = AddTab(window, MediumTitle);
        IntPtr before = window.TabController.Handle;
        int widthBefore = WidthOf(window, tab);

        window.RefreshTabMetrics(192);
        Pump();

        Assert.Multiple(() =>
        {
            Assert.That(window.TabController.Handle, Is.EqualTo(before), "The tab strip's window was recreated.");
            Assert.That(WidthOf(window, tab), Is.GreaterThan(widthBefore), "The padding did not reach the Tab.");
        });
    }

    [Test]
    public void TabsThatDoNotFitStayOnOneRow()
    {
        ConnectionWindow window = OpenWindow(96);
        for (int i = 0; i < 30; i++)
        {
            AddTab(window, $"{MediumTitle}-{i}");
        }

        TabControl tabs = window.TabController;
        Assert.Multiple(() =>
        {
            Assert.That(Enumerable.Range(0, tabs.TabCount).Sum(i => tabs.GetTabRect(i).Width),
                        Is.GreaterThan(tabs.Width), "The test did not open enough Tabs to overflow the strip.");
            Assert.That(tabs.Multiline, Is.False);
            Assert.That(tabs.RowCount, Is.EqualTo(1), "The Tabs wrapped into more rows instead of scrolling.");
        });
    }

    public enum Route
    {
        /// <summary>The connection window's own handling: <c>RescaleConstantsForDpi</c>.</summary>
        WindowsOwnHook,

        /// <summary>The main window's posted follow-up, <c>FrmMain.FollowDpiChangeIn</c>.</summary>
        MainWindowFollowUp,
    }

    /// <summary>
    /// After a DPI change through either route alone, every Tab is the Tab a window opened at the
    /// new DPI gives the same title, and a title that fitted and now does not is cut, or the
    /// reverse.
    /// </summary>
    /// <param name="route">The one route the change is driven through.</param>
    /// <param name="fromDpi">The DPI the Tabs are opened at.</param>
    /// <param name="toDpi">The DPI the window moves to.</param>
    /// <param name="fontScale">
    /// The text scale on top of the DPI at the end of the move. 1 is a plain dock or undock; any
    /// other value makes the text grow or shrink faster than the maximum width, which is what
    /// moves a title across the cut: the test then asserts that one did. At 1 the text and the
    /// maximum scale together, and a title stays on its side of the cut.
    /// </param>
    [TestCase(Route.WindowsOwnHook, 144, 192, 1f)]
    [TestCase(Route.WindowsOwnHook, 192, 144, 1f)]
    [TestCase(Route.MainWindowFollowUp, 144, 192, 1f)]
    [TestCase(Route.MainWindowFollowUp, 192, 144, 1f)]
    [TestCase(Route.WindowsOwnHook, 144, 192, 1.25f)]
    [TestCase(Route.WindowsOwnHook, 192, 144, 0.8f)]
    [TestCase(Route.MainWindowFollowUp, 144, 192, 1.25f)]
    [TestCase(Route.MainWindowFollowUp, 192, 144, 0.8f)]
    public void ADpiChangeRemeasuresEveryTabAsIfItWereOpenedThere(Route route, int fromDpi, int toDpi, float fontScale)
    {
        // A title that just fits at whichever end has less room for it.
        Font fromFont = FontAt(fromDpi, 1f);
        Font toFont = FontAt(toDpi, fontScale);
        int fromMaximum = DpiScaling.Scale(ConnectionWindow.LogicalMaxTitleWidth, fromDpi);
        int toMaximum = DpiScaling.Scale(ConnectionWindow.LogicalMaxTitleWidth, toDpi);
        bool tighterAfter = TextWidth(LongTitle, toFont) / (float)toMaximum
                            > TextWidth(LongTitle, fromFont) / (float)fromMaximum;
        string borderline = tighterAfter
                                ? LongestPrefixThatFits(LongTitle, fromFont, fromMaximum)
                                : LongestPrefixThatFits(LongTitle, toFont, toMaximum);
        string[] titles = [ShortTitle, MediumTitle, borderline, LongTitle];

        ConnectionWindow moved = route == Route.WindowsOwnHook
                                     ? new OwnDpiHookWindow()
                                     : new ConnectionWindow("moved");
        Form host = Embed(moved, fromFont);
        if (moved is OwnDpiHookWindow ownHook)
        {
            ownHook.HearDpiChange(96, fromDpi);
        }
        else
        {
            moved.RefreshTabMetrics(fromDpi);
        }

        foreach (string title in titles)
        {
            AddTab(moved, title);
        }

        bool borderlineCutBefore = Tab(moved, borderline).DisplayTitle != borderline;

        if (moved is OwnDpiHookWindow hook)
        {
            // In the order a real move takes: the child hook runs part-way through the change,
            // with the font of the monitor being left; the new font arrives afterwards.
            hook.HearDpiChange(fromDpi, toDpi);
            host.Font = toFont;
            Pump();
        }
        else
        {
            host.Font = toFont;
            Pump();
            FrmMain.FollowDpiChangeIn(host, toDpi);
            Pump();
        }

        var fresh = new ConnectionWindow("fresh");
        Embed(fresh, toFont);
        fresh.RefreshTabMetrics(toDpi);
        foreach (string title in titles)
        {
            AddTab(fresh, title);
        }

        bool crossesTheCut = Math.Abs(fontScale - 1f) > 0.01f;
        Assert.Multiple(() =>
        {
            if (crossesTheCut)
            {
                Assert.That(borderlineCutBefore, Is.EqualTo(!tighterAfter),
                            "The borderline title did not start on the side of the cut the test meant.");
                Assert.That(Tab(moved, borderline).DisplayTitle != borderline, Is.EqualTo(tighterAfter),
                            "The borderline title is not on the other side of the cut after the change.");
            }

            foreach (string title in titles)
            {
                ConnectionTabPage movedTab = Tab(moved, title);
                ConnectionTabPage freshTab = Tab(fresh, title);
                Assert.That(RectOf(moved, movedTab).Size, Is.EqualTo(RectOf(fresh, freshTab).Size),
                            $"'{title}' after {fromDpi} -> {toDpi} DPI through {route}: not the size a Tab opened " +
                            "at the new DPI gets.");
                Assert.That(movedTab.DisplayTitle, Is.EqualTo(freshTab.DisplayTitle),
                            $"'{title}' after {fromDpi} -> {toDpi} DPI through {route}: shortened for the old DPI.");
                Assert.That(movedTab.ToolTipText, Is.EqualTo(freshTab.ToolTipText),
                            $"'{title}' after {fromDpi} -> {toDpi} DPI through {route}: the tooltip did not follow.");
            }
        });
    }

    /// <summary>Exposes the window's own DPI hook, which only Windows calls.</summary>
    private sealed class OwnDpiHookWindow : ConnectionWindow
    {
        public OwnDpiHookWindow()
            : base("own hook")
        {
        }

        public void HearDpiChange(int oldDpi, int newDpi) => RescaleConstantsForDpi(oldDpi, newDpi);
    }

    private ConnectionWindow OpenWindow(int dpi)
    {
        var window = new ConnectionWindow("tab width");
        Embed(window, FontAt(dpi, 1f));
        window.RefreshTabMetrics(dpi);
        return window;
    }

    /// <summary>Embeds a connection window the way MainLayout does, in a host with this font.</summary>
    private Form Embed(ConnectionWindow window, Font font)
    {
        var host = new Form
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-32000, -32000),
            ClientSize = new Size(1600, 600),
            AutoScaleMode = AutoScaleMode.None,
            Font = font,
            Opacity = 0,
            ShowInTaskbar = false,
        };
        _hosts.Add(host);
        window.TopLevel = false;
        window.FormBorderStyle = FormBorderStyle.None;
        window.Dock = DockStyle.Fill;
        host.Controls.Add(window);
        host.Show();
        window.Show();
        Pump();
        return host;
    }

    /// <summary>The font a 9pt UI font is at this DPI, in a process that is itself at 96.</summary>
    private Font FontAt(int dpi, float scale)
    {
        var font = new Font("Segoe UI", 9f * dpi / 96f * scale);
        _fonts.Add(font);
        return font;
    }

    private static ConnectionTabPage AddTab(ConnectionWindow window, string title)
    {
        var info = new ConnectionInfo { Name = title, Hostname = "127.0.0.2", Icon = "Windows" };
        var tab = (ConnectionTabPage)(window.AddConnectionTab(info)
                                      ?? throw new AssertionException("AddConnectionTab returned no tab page."));
        Pump();
        return tab;
    }

    private static ConnectionTabPage Tab(ConnectionWindow window, string title) =>
        window.TabController.TabPages.OfType<ConnectionTabPage>().Single(tab => tab.Title == title);

    private static Rectangle RectOf(ConnectionWindow window, TabPage tab) =>
        window.TabController.GetTabRect(window.TabController.TabPages.IndexOf(tab));

    private static int WidthOf(ConnectionWindow window, TabPage tab) => RectOf(window, tab).Width;

    private static int TextWidth(string text, Font font) =>
        TextRenderer.MeasureText(text, font, Size.Empty,
                                 TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix
                                 | TextFormatFlags.SingleLine).Width;

    private static string LongestPrefixThatFits(string text, Font font, int maximum)
    {
        int length = text.Length;
        while (length > 1 && TextWidth(text[..length], font) > maximum)
        {
            length--;
        }

        return text[..length];
    }

    private static void Pump()
    {
        for (int i = 0; i < 5; i++)
        {
            Application.DoEvents();
        }
    }
}
