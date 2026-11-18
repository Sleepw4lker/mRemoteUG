using System.Diagnostics.CodeAnalysis;

using mRemoteUG.Connection;
using mRemoteUG.UI;
using mRemoteUG.UI.Window;

using NUnit.Framework;

namespace mRemoteUG.Tests.UI.Window;

/// <summary>
/// A Tab's icon is a frame of the size it is drawn at, before and after a DPI change.
/// </summary>
/// <remarks>
/// The owner-drawn strip draws the icon into a rectangle re-derived for every DPI, and
/// <c>Graphics.DrawIcon</c> stretches whatever frame it is handed into it - the same stretched
/// glyph ADR-0010's 2026-09-25 amendment fixed on the toolbars. A frame is crisp only when it was
/// picked at the size it is drawn. <c>RefreshTabMetrics</c> takes the DPI as a number, so this
/// needs no per-monitor awareness and runs on the CI runner.
/// </remarks>
[TestFixture]
[Apartment(ApartmentState.STA)]
[SuppressMessage("Design", "CA1001",
    Justification = "[TearDown] disposes _window after every test. "
        + "NUnit would dispose an IDisposable fixture once, after its last test.")]
public class ConnectionWindowTabIconTests
{
    private ConnectionWindow _window = null!;

    [TearDown]
    public void TearDown()
    {
        _window?.Dispose();
    }

    [TestCase(96, 192)]   // to a 200% monitor
    [TestCase(192, 96)]   // back to 100%
    [TestCase(144, 120)]  // 150% to 125%
    public void TheTabIconIsAFrameOfTheSizeItIsDrawnAt(int fromDpi, int toDpi)
    {
        _window = new ConnectionWindow("tab icon");
        _window.RefreshTabMetrics(fromDpi);

        var info = new ConnectionInfo { Name = "server1", Hostname = "127.0.0.2", Icon = "Windows" };
        var page = (ConnectionTabPage)(_window.AddConnectionTab(info)
                                       ?? throw new AssertionException("AddConnectionTab returned no tab page."));
        int openedAt = page.Icon?.Width ?? 0;

        _window.RefreshTabMetrics(toDpi);
        int afterChange = page.Icon?.Width ?? 0;

        Assert.Multiple(() =>
        {
            Assert.That(openedAt, Is.EqualTo(DpiScaling.Scale(16, fromDpi)),
                        $"A Tab opened at {fromDpi} DPI got a {openedAt} pixel icon frame.");
            Assert.That(afterChange, Is.EqualTo(DpiScaling.Scale(16, toDpi)),
                        $"After a change to {toDpi} DPI the Tab icon is still a {afterChange} pixel frame, " +
                        "stretched into the new rectangle.");
        });
    }
}
