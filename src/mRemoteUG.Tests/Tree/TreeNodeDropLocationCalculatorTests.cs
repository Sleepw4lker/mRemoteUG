using System.Drawing;
using mRemoteUG.Tree;
using NUnit.Framework;

namespace mRemoteUG.Tests.Tree
{
    [TestFixture]
    public class TreeNodeDropLocationCalculatorTests
    {
        // a 30px row starting at y=100: above < 110, onto 110..119, below >= 120
        private static readonly Rectangle Row = new Rectangle(0, 100, 200, 30);

        [TestCase(100, DropTargetLocation.AboveItem)]
        [TestCase(109, DropTargetLocation.AboveItem)]
        [TestCase(110, DropTargetLocation.Item)]
        [TestCase(119, DropTargetLocation.Item)]
        [TestCase(120, DropTargetLocation.BelowItem)]
        [TestCase(129, DropTargetLocation.BelowItem)]
        public void ThirdsDecideTheDropLocation(int pointY, DropTargetLocation expected)
        {
            Assert.That(TreeNodeDropLocationCalculator.Locate(Row, pointY), Is.EqualTo(expected));
        }

        [Test]
        public void AZeroHeightRowDropsOntoTheItem()
        {
            var row = new Rectangle(0, 100, 200, 0);
            Assert.That(TreeNodeDropLocationCalculator.Locate(row, 100), Is.EqualTo(DropTargetLocation.Item));
        }

        [Test]
        public void AVeryShortRowStillDistinguishesAboveFromBelow()
        {
            // height 2 floors to a zero-size third, which would otherwise swallow the edges
            var row = new Rectangle(0, 100, 200, 2);
            Assert.That(TreeNodeDropLocationCalculator.Locate(row, 100), Is.EqualTo(DropTargetLocation.AboveItem));
            Assert.That(TreeNodeDropLocationCalculator.Locate(row, 101), Is.EqualTo(DropTargetLocation.BelowItem));
        }
    }
}
