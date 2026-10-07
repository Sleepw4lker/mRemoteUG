using System;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using mRemoteUG.UI.Window;
using NUnit.Framework;

namespace mRemoteUG.Tests.UI.Window
{
    /// <summary>
    /// The notifications window derives one font from its own and must not free it while it is
    /// still on screen.
    /// </summary>
    /// <remarks>
    /// A derived font opts its control out of inheriting, so it has to be rebuilt whenever the font
    /// it came from changes - on a DPI change, among other things. Rebuilding it means releasing
    /// the one before, and that is where this goes wrong: Control.Font keeps the instance it
    /// already holds when the new one compares equal to it, so the obvious "assign the new one,
    /// dispose the old one" leaves the label holding a font that has just been freed. Measured: the
    /// next read of that font throws ArgumentException, "Parameter is not valid".
    /// </remarks>
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class ErrorAndInfoWindowFontTests
    {
        [Test]
        public void RebuildingTheDerivedFontTwiceLeavesTheLabelUsable()
        {
            using (var window = new ErrorAndInfoWindow())
            {
                var rebuild = typeof(ErrorAndInfoWindow)
                              .GetMethod("ApplyDerivedFonts", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(rebuild, Is.Not.Null, "ApplyDerivedFonts has been renamed or removed");

                // Twice, with nothing changed in between, which is the case that bites: the second
                // derived font compares equal to the first, so the label keeps the first.
                rebuild.Invoke(window, null);
                rebuild.Invoke(window, null);

                Assert.DoesNotThrow(() =>
                {
                    var height = window.lblMsgDate.Font.Height;
                    Assert.That(height, Is.GreaterThan(0));
                }, "the label is holding a font that has been disposed out from under it");
            }
        }
    }
}
