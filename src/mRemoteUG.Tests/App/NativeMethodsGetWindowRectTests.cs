using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using mRemoteUG.App;
using NUnit.Framework;

namespace mRemoteUG.Tests.App
{
    /// <summary>
    /// Pins that <see cref="NativeMethods.GetWindowRect"/> marshals correctly against a real
    /// window, added alongside the PuTTY-undock-resize investigation: the readback it backs is
    /// only meaningful if the P/Invoke itself reports the window's true screen bounds.
    /// </summary>
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class NativeMethodsGetWindowRectTests
    {
        [Test]
        public void ReportsTheFormsActualScreenBounds()
        {
            using var form = new Form
            {
                StartPosition = FormStartPosition.Manual,
                Bounds = new Rectangle(37, 41, 321, 219),
                ShowInTaskbar = false
            };
            form.Show();
            Application.DoEvents();

            var succeeded = NativeMethods.GetWindowRect(form.Handle, out var rect);

            Assert.That(succeeded, Is.True, "GetWindowRect failed for a real, shown window.");
            Assert.That(new Rectangle(rect.left, rect.top, rect.right - rect.left, rect.bottom - rect.top),
                Is.EqualTo(form.Bounds));
        }
    }
}
