using System.Drawing;
using mRemoteUG.Connection.Protocol.RDP;
using NUnit.Framework;

namespace mRemoteUG.Tests.Connection.Protocol
{
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
            Assert.That(RdpDisplayScale.TryNormaliseSessionSize(new Size(1921, 1080), out var size),
                        Is.True);
            Assert.That(size, Is.EqualTo(new Size(1920, 1080)));
        }

        [Test]
        public void RoundingDownNeverGrowsTheSessionBeyondItsPanel()
        {
            for (var width = RdpDisplayScale.MinSessionDimension; width < 4000; width++)
            {
                RdpDisplayScale.TryNormaliseSessionSize(new Size(width, 1080), out var size);
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

        [TestCase(200, 200)]
        [TestCase(1920, 1080)]
        [TestCase(3840, 2160)]
        [TestCase(8192, 8192)]
        public void OrdinaryPanelSizesAreAccepted(int width, int height)
        {
            Assert.That(RdpDisplayScale.TryNormaliseSessionSize(new Size(width, height), out var size),
                        Is.True);
            Assert.That(size, Is.EqualTo(new Size(width, height)));
        }
    }
}
