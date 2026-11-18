using mRemoteUG.Config.Settings;

using NUnit.Framework;

namespace mRemoteUG.Tests.UI.Forms;

/// <summary>
/// The arithmetic behind restoring the main window: the saved size and toolbar offset are
/// physical pixels at the DPI they were saved at, and the window has to end up on a screen.
/// </summary>
/// <remarks>
/// The same rules <see cref="MainLayoutSettings"/> applies to the left column width, and the
/// same caveat: settings written before the DPI was recorded have it at 0 and are taken at
/// face value.
/// </remarks>
[TestFixture]
public class MainFormPlacementTests
{
    private static readonly Point s_anyLocation = new Point(10, 20);

    [TestCase(1600, 1000, 96, 96, 1600, 1000, TestName = "Same DPI: unchanged")]
    [TestCase(1600, 1000, 192, 96, 800, 500, TestName = "Saved at 200%, reopened at 100%: halved")]
    [TestCase(800, 500, 96, 192, 1600, 1000, TestName = "Saved at 100%, reopened at 200%: doubled")]
    [TestCase(1000, 700, 96, 144, 1500, 1050, TestName = "Saved at 100%, reopened at 150%")]
    [TestCase(100, 200, 144, 96, 67, 133, TestName = "Rounded, not truncated")]
    [TestCase(1600, 1000, 96, 0, 1600, 1000, TestName = "Monitor DPI unknown: unchanged")]
    [TestCase(1600, 1000, 96, -1, 1600, 1000, TestName = "Monitor DPI invalid: unchanged")]
    public void ASavedSizeIsRescaledToTheMonitorItReopensOn(
        int width, int height, int savedDpi, int targetDpi, int expectedWidth, int expectedHeight)
    {
        Size rescaled = MainFormPlacement.RescaleSavedSize(new Size(width, height), s_anyLocation, savedDpi,
                                                           _ => targetDpi);

        Assert.That(rescaled, Is.EqualTo(new Size(expectedWidth, expectedHeight)));
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void ASizeWithNoRecordedDpiIsTakenAtFaceValueWithoutAskingTheMonitor(int savedDpi)
    {
        bool asked = false;

        Size rescaled = MainFormPlacement.RescaleSavedSize(new Size(1600, 1000), s_anyLocation, savedDpi,
                                                           _ =>
                                                           {
                                                               asked = true;
                                                               return 192;
                                                           });

        Assert.Multiple(() =>
        {
            Assert.That(rescaled, Is.EqualTo(new Size(1600, 1000)));
            Assert.That(asked, Is.False);
        });
    }

    /// <summary>
    /// The monitor that counts is the one at the saved location, where the window reopens.
    /// </summary>
    [Test]
    public void TheMonitorAskedIsTheOneAtTheSavedLocation()
    {
        var savedLocation = new Point(-1900, 300);
        Point? asked = null;

        MainFormPlacement.RescaleSavedSize(new Size(1600, 1000), savedLocation, 96,
                                           point =>
                                           {
                                               asked = point;
                                               return 192;
                                           });

        Assert.That(asked, Is.EqualTo(savedLocation));
    }

    [TestCase(600, 40, 192, 96, 300, 20, TestName = "Saved at 200%, now 100%: halved")]
    [TestCase(300, 20, 96, 192, 600, 40, TestName = "Saved at 100%, now 200%: doubled")]
    [TestCase(300, 20, 96, 96, 300, 20, TestName = "Same DPI: unchanged")]
    [TestCase(300, 20, 0, 192, 300, 20, TestName = "No recorded DPI: unchanged")]
    [TestCase(300, 20, -1, 192, 300, 20, TestName = "Invalid recorded DPI: unchanged")]
    [TestCase(100, 200, 144, 96, 67, 133, TestName = "Toolbar offset rounded, not truncated")]
    public void AToolbarOffsetIsRescaledToTheCurrentDpi(
        int x, int y, int savedDpi, int currentDpi, int expectedX, int expectedY)
    {
        Point rescaled = MainFormPlacement.RescaleSavedToolbarLocation(new Point(x, y), savedDpi, currentDpi);

        Assert.That(rescaled, Is.EqualTo(new Point(expectedX, expectedY)));
    }

    /// <summary>
    /// The one call into Windows: every monitor has an effective DPI of at least 96, so 0 here
    /// would mean the lookup failed.
    /// </summary>
    [Test]
    public void ThePrimaryMonitorReportsItsDpi()
    {
        Screen primary = Screen.PrimaryScreen
                         ?? throw new InconclusiveException("No primary screen in this session.");

        int dpi = MainFormPlacement.DpiForPoint(primary.Bounds.Location);

        Assert.That(dpi, Is.GreaterThanOrEqualTo(96));
    }

    private static readonly Rectangle s_fullHd = new Rectangle(0, 0, 1920, 1080);

    // A window of 800x600; at least 300 across and 150 down of it has to stay on the screen.
    [TestCase(100, 100, 100, 100, TestName = "On screen: stays")]
    [TestCase(-2000, 100, -500, 100, TestName = "Off the left: pulled back 300 in")]
    [TestCase(-500, 100, -500, 100, TestName = "Exactly 300 showing on the left: stays")]
    [TestCase(-501, 100, -500, 100, TestName = "299 showing on the left: pulled back")]
    [TestCase(3000, 100, 1620, 100, TestName = "Off the right: pulled back 300 in")]
    [TestCase(1620, 100, 1620, 100, TestName = "Exactly 300 showing on the right: stays")]
    [TestCase(1621, 100, 1620, 100, TestName = "299 showing on the right: pulled back")]
    [TestCase(100, -2000, 100, -450, TestName = "Off the top: pulled back 150 in")]
    [TestCase(100, -450, 100, -450, TestName = "Exactly 150 showing at the top: stays")]
    [TestCase(100, -451, 100, -450, TestName = "149 showing at the top: pulled back")]
    [TestCase(100, 2000, 100, 930, TestName = "Off the bottom: pulled back 150 in")]
    [TestCase(100, 930, 100, 930, TestName = "Exactly 150 showing at the bottom: stays")]
    [TestCase(100, 931, 100, 930, TestName = "149 showing at the bottom: pulled back")]
    [TestCase(-5000, -5000, -500, -450, TestName = "Off a corner: pulled back on both axes")]
    [TestCase(5000, 5000, 1620, 930, TestName = "Off the opposite corner: pulled back on both axes")]
    public void AWindowIsKeptOnScreen(int x, int y, int expectedX, int expectedY)
    {
        Point location = MainFormPlacement.KeepOnScreen(new Rectangle(x, y, 800, 600), s_fullHd);

        Assert.That(location, Is.EqualTo(new Point(expectedX, expectedY)));
    }

    /// <summary>
    /// A second monitor sits anywhere on the desktop, so the limits are its edges, not 0.
    /// </summary>
    [TestCase(0, 0, 1420, 0, TestName = "Left of a monitor to the right of the primary")]
    [TestCase(5000, 0, 4180, 0, TestName = "Right of a monitor to the right of the primary")]
    [TestCase(2000, -2000, 2000, -650, TestName = "Above a monitor raised above the primary")]
    [TestCase(2000, 2000, 2000, 1090, TestName = "Below a monitor raised above the primary")]
    public void AWindowIsKeptOnTheScreenItIsOn(int x, int y, int expectedX, int expectedY)
    {
        var secondMonitor = new Rectangle(1920, -200, 2560, 1440);

        Point location = MainFormPlacement.KeepOnScreen(new Rectangle(x, y, 800, 600), secondMonitor);

        Assert.That(location, Is.EqualTo(new Point(expectedX, expectedY)));
    }

    [Test]
    public void KeepOnScreenLeavesTheSizeToTheCaller()
    {
        var bounds = new Rectangle(-2000, -2000, 1000, 700);

        Point location = MainFormPlacement.KeepOnScreen(bounds, s_fullHd);

        Assert.That(location, Is.EqualTo(new Point(300 - 1000, 150 - 700)));
    }
}
