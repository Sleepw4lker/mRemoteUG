using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using mRemoteUG.UI;
using NUnit.Framework;

namespace mRemoteUG.Tests.UI
{
    /// <summary>
    /// Rebuilding a drop-down has to dispose what it is replacing.
    /// </summary>
    /// <remarks>
    /// The View menu's connection-panel list is rebuilt on every DropDownOpening, and each entry
    /// carries a bitmap made on the spot from the panel's window icon. Neither
    /// <c>DropDownItems.Clear()</c> nor <c>ToolStripItem.Dispose()</c> touches that bitmap, so the
    /// GDI+ handles accumulated for as long as the application ran.
    /// </remarks>
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class ToolStripItemsTests
    {
        [Test]
        public void ClearingDisposesEveryItemAndTheImageItOwns()
        {
            using var owner = new ToolStripMenuItem("panels");
            var firstImage = new Bitmap(16, 16);
            var secondImage = new Bitmap(16, 16);
            var first = new ToolStripMenuItem("one", firstImage);
            var second = new ToolStripMenuItem("two", secondImage);
            owner.DropDownItems.Add(first);
            owner.DropDownItems.Add(second);

            ToolStripItems.ClearAndDisposeOwned(owner.DropDownItems);

            Assert.Multiple(() =>
            {
                Assert.That(owner.DropDownItems, Is.Empty, "The collection was not emptied.");
                Assert.That(first.IsDisposed, Is.True, "The first item survived.");
                Assert.That(second.IsDisposed, Is.True,
                            "The second item survived - disposing an item removes it from the "
                            + "collection, so iterating the live collection skips every other one.");
                Assert.That(() => firstImage.Width, Throws.TypeOf<System.ArgumentException>(),
                            "The first item's image was not disposed.");
                Assert.That(() => secondImage.Width, Throws.TypeOf<System.ArgumentException>(),
                            "The second item's image was not disposed.");
            });
        }

        [Test]
        public void AnItemWithNoImageIsStillDisposed()
        {
            using var owner = new ToolStripMenuItem("panels");
            var plain = new ToolStripMenuItem("no image");
            owner.DropDownItems.Add(plain);

            ToolStripItems.ClearAndDisposeOwned(owner.DropDownItems);

            Assert.That(plain.IsDisposed, Is.True);
        }

        [Test]
        public void AnEmptyOrMissingCollectionIsNotAnError()
        {
            using var owner = new ToolStripMenuItem("panels");

            Assert.Multiple(() =>
            {
                Assert.That(() => ToolStripItems.ClearAndDisposeOwned(owner.DropDownItems), Throws.Nothing);
                Assert.That(() => ToolStripItems.ClearAndDisposeOwned(null), Throws.Nothing);
            });
        }
    }
}
