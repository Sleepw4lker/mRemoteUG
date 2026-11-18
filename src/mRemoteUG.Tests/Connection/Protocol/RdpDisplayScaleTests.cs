using mRemoteUG.Connection.Protocol.RDP;

using NUnit.Framework;

namespace mRemoteUG.Tests.Connection.Protocol;

/// <summary>
/// Pins the arguments this fork sends to <c>UpdateSessionDisplaySettings</c>.
/// </summary>
/// <remarks>
/// Worth testing on its own precisely because the protocol cannot tell us when we get it
/// wrong: [MS-RDPEDISP] says an out-of-range value is *ignored*, so a bad scale factor returns
/// S_OK and quietly does nothing. Nothing at run time distinguishes "the session rescaled" from
/// "the session ignored us", and this cannot be exercised on the development machine at all,
/// which has no route to an RDP host.
/// </remarks>
[TestFixture]
public class RdpDisplayScaleTests
{
    [TestCase(96, 100u)]   // 100%
    [TestCase(120, 125u)]  // 125%
    [TestCase(144, 150u)]  // 150%
    [TestCase(168, 175u)]  // 175%
    [TestCase(192, 200u)]  // 200%
    [TestCase(288, 300u)]  // 300%
    public void DesktopScaleFactorIsTheDpiAsAPercentage(int dpi, uint expected)
    {
        Assert.That(RdpDisplayScale.DesktopScaleFactor(dpi), Is.EqualTo(expected));
    }

    [TestCase(48)]    // below the minimum the protocol accepts
    [TestCase(96)]
    [TestCase(960)]   // 1000%, above the maximum
    [TestCase(0)]
    [TestCase(-1)]
    public void DesktopScaleFactorStaysInsideTheAcceptedRange(int dpi)
    {
        Assert.That(RdpDisplayScale.DesktopScaleFactor(dpi),
                    Is.InRange(RdpDisplayScale.MinDesktopScaleFactor,
                               RdpDisplayScale.MaxDesktopScaleFactor));
    }

    [TestCase(96, 100u)]   // 100% -> exactly 100
    [TestCase(120, 140u)]  // 125% is nearer 140 than 100
    [TestCase(144, 140u)]  // 150% is nearer 140 than 180
    [TestCase(168, 180u)]  // 175% is nearer 180
    [TestCase(192, 180u)]  // 200% -> 180, the largest there is
    public void DeviceScaleFactorPicksTheNearestPermittedValue(int dpi, uint expected)
    {
        Assert.That(RdpDisplayScale.DeviceScaleFactor(dpi), Is.EqualTo(expected));
    }

    /// <summary>
    /// The protocol permits exactly three device scale factors, and ignores the desktop scale
    /// factor as well when given a fourth.
    /// </summary>
    [TestCase(48)]
    [TestCase(96)]
    [TestCase(120)]
    [TestCase(144)]
    [TestCase(168)]
    [TestCase(192)]
    [TestCase(240)]
    [TestCase(384)]
    [TestCase(960)]
    public void DeviceScaleFactorIsAlwaysOneOfTheThreeLegalValues(int dpi)
    {
        Assert.That(RdpDisplayScale.DeviceScaleFactor(dpi), Is.AnyOf(100u, 140u, 180u));
    }

    [Test]
    public void AnOddWidthIsRoundedDownBecauseTheProtocolRejectsOne()
    {
        Assert.That(RdpDisplayScale.TryNormaliseSessionSize(new Size(1921, 1080), out Size size),
                    Is.True);
        Assert.That(size, Is.EqualTo(new Size(1920, 1080)));
    }

    [Test]
    public void RoundingDownNeverGrowsTheSessionBeyondItsPanel()
    {
        for (int width = RdpDisplayScale.MinSessionDimension; width < 4000; width++)
        {
            RdpDisplayScale.TryNormaliseSessionSize(new Size(width, 1080), out Size size);
            Assert.That(size.Width, Is.LessThanOrEqualTo(width),
                        $"a panel {width} wide was given a session {size.Width} wide");
            Assert.That(size.Width % 2, Is.Zero, $"width {size.Width} is odd");
        }
    }

    [TestCase(199, 1080)]  // narrower than the protocol allows
    [TestCase(1920, 199)]  // shorter than the protocol allows
    [TestCase(8194, 1080)] // wider than the protocol allows - reachable on a 4K panel now
    [TestCase(1920, 8193)]
    public void ASizeTheProtocolCannotExpressIsRejectedRatherThanSentAnyway(int width, int height)
    {
        Assert.That(RdpDisplayScale.TryNormaliseSessionSize(new Size(width, height), out _),
                    Is.False);
    }

    /// <summary>
    /// Replays a dock from a 150% panel to a 200% monitor, as a <c>--verbose</c> log recorded it.
    /// </summary>
    /// <remarks>
    /// The resize had already asked for 4364x2254 at 200% when the DPI-change pass ran, but the
    /// control's <c>DesktopWidth</c>/<c>DesktopHeight</c> still reported 3604x1940, a request
    /// two behind. Sending that took the session back to the stale size and left a grey border
    /// round it in the Tab.
    /// </remarks>
    [Test]
    public void ARescaleAfterAResizeAtTheNewScaleSendsNothing()
    {
        bool send = RdpDisplayScale.TryChooseRescaleSize(
            lastRequestedSize: new Size(4365, 2254),
            lastRequestedScale: (200u, 180u),
            reportedDesktopSize: new Size(3604, 1940),
            dpi: 192,
            out _);

        Assert.That(send, Is.False);
    }

    /// <summary>
    /// The size a session was last asked for wins over what the control reports, which lags.
    /// </summary>
    [Test]
    public void ARescaleUsesTheLastRequestedSizeNotTheReportedOne()
    {
        bool send = RdpDisplayScale.TryChooseRescaleSize(
            lastRequestedSize: new Size(4365, 2254),
            lastRequestedScale: (150u, 140u),
            reportedDesktopSize: new Size(3604, 1940),
            dpi: 192,
            out Size size);

        Assert.That(send, Is.True);
        Assert.That(size, Is.EqualTo(new Size(4365, 2254)));
    }

    /// <summary>
    /// A fixed-resolution session dragged to a monitor of a different DPI keeps its size and
    /// only changes scale.
    /// </summary>
    [Test]
    public void AFixedResolutionSessionKeepsItsSizeAndTakesTheNewScale()
    {
        bool send = RdpDisplayScale.TryChooseRescaleSize(
            lastRequestedSize: new Size(1920, 1080),
            lastRequestedScale: (150u, 140u),
            reportedDesktopSize: new Size(1920, 1080),
            dpi: 192,
            out Size size);

        Assert.That(send, Is.True);
        Assert.That(size, Is.EqualTo(new Size(1920, 1080)));
    }

    /// <summary>
    /// With nothing asked for yet, the reported size is the only one there is.
    /// </summary>
    [Test]
    public void WithNoRequestYetTheReportedSizeIsUsed()
    {
        bool send = RdpDisplayScale.TryChooseRescaleSize(
            lastRequestedSize: Size.Empty,
            lastRequestedScale: default,
            reportedDesktopSize: new Size(1684, 1015),
            dpi: 192,
            out Size size);

        Assert.That(send, Is.True);
        Assert.That(size, Is.EqualTo(new Size(1684, 1015)));
    }

    /// <summary>
    /// <c>E_UNEXPECTED</c> from <c>UpdateSessionDisplaySettings</c> means "not now", not "never".
    /// </summary>
    /// <remarks>
    /// Measured against a current Windows 11 host: it comes back right after every connect and
    /// every automatic reconnect, before the display channel is open. Read as "this server is too
    /// old", it switched the session to resizing by reconnecting, and each of those reconnects
    /// dropped the session again - more than thirty times, until the host locked the session.
    /// </remarks>
    [Test]
    public void ARefusalWithEUnexpectedIsForNow()
    {
        Assert.That(RdpDisplayScale.IsRefusalForNow(unchecked((int)0x8000FFFF)), Is.True);
    }

    [TestCase(unchecked((int)0x80004001))] // E_NOTIMPL
    [TestCase(unchecked((int)0x80070057))] // E_INVALIDARG
    [TestCase(unchecked((int)0x80004005))] // E_FAIL
    public void OtherRefusalsAreNotForNow(int hresult)
    {
        Assert.That(RdpDisplayScale.IsRefusalForNow(hresult), Is.False);
    }

    [TestCase(200, 200)]
    [TestCase(1920, 1080)]
    [TestCase(3840, 2160)]
    [TestCase(8192, 8192)]
    public void OrdinaryPanelSizesAreAccepted(int width, int height)
    {
        Assert.That(RdpDisplayScale.TryNormaliseSessionSize(new Size(width, height), out Size size),
                    Is.True);
        Assert.That(size, Is.EqualTo(new Size(width, height)));
    }
}
