using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using mRemoteUG.Connection;
using mRemoteUG.UI;
using mRemoteUG.UI.Window;
using NUnit.Framework;

namespace mRemoteUG.Tests.UI
{
    /// <summary>
    /// The property grid's toolbar is the framework's, not this application's, and the buttons on
    /// it are reached by index.
    /// </summary>
    /// <remarks>
    /// A <see cref="PropertyGrid"/> builds its own toolbar and fills it with bitmaps embedded in
    /// System.Windows.Forms.dll. <c>ConfigWindow</c> reaches into that strip twice - once to hide
    /// Property Pages, once to replace the artwork - and both do it by position, because the only
    /// other handle is <c>Text</c>, which follows the OS UI culture rather than this application's.
    /// <para>
    /// So the shape of that strip is an assumption about a control this fork does not own. These
    /// pin it: if a future WinForms reorders or resizes it, this fails here rather than putting
    /// the wrong mark on the wrong button in front of a user.
    /// </para>
    /// </remarks>
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class PropertyGridToolbarTests
    {
        private static ToolStrip FrameworkStripOf(ConfigWindow window) =>
            DpiScaling.ToolStripsOf(window).First(t => t.GetType().Name == "PropertyGridToolStrip");

        /// <summary>
        /// Shows the window, because everything here depends on Config_Load having run.
        /// </summary>
        /// <remarks>
        /// AddToolStripItems - which hides Property Pages, restyles the sort buttons and merges
        /// this application's own buttons in - is called from the Load handler. Load does not run
        /// for a form that has only had its handle created, so a test written against
        /// CreateControl passes or fails for the wrong reasons: every item reports Visible=false
        /// because the strip was never shown.
        /// </remarks>
        private static ConfigWindow Shown()
        {
            var window = new ConfigWindow();
            window.Show();
            return window;
        }

        /// <summary>
        /// The strip is the framework's five items with this application's six merged onto the end.
        /// </summary>
        /// <remarks>
        /// ConfigWindow records the count before merging and indexes off it, so the split is what
        /// matters rather than the total: the framework's Categorized, Alphabetical, NoSort,
        /// separator and Property Pages occupy 0-4, and everything this application adds comes
        /// after. If the framework ever contributes a different number, those indexes move and
        /// the Property Pages line hides the wrong button.
        /// </remarks>
        [Test]
        public void TheStripIsTheFrameworksFiveItemsPlusThisApplicationsSix()
        {
            using (var window = Shown())
            {
                var strip = FrameworkStripOf(window);

                Assert.That(strip.Items.Count, Is.EqualTo(5 + 6),
                            "the framework toolbar changed shape; ConfigWindow indexes into it");

                // The first three are the framework's sort buttons - a ToolStripButton each, not
                // the ToolStripButtons this application merges in.
                Assert.Multiple(() =>
                {
                    foreach (var index in new[] { 0, 1, 2 })
                        Assert.That(strip.Items[index].GetType().Name,
                                    Is.EqualTo("PropertyGridToolStripButton"),
                                    $"item {index} is no longer one of the framework's own buttons");

                    Assert.That(strip.Items[3], Is.InstanceOf<ToolStripSeparator>());
                });
            }
        }

        /// <summary>
        /// The three sort buttons are where this code thinks they are.
        /// </summary>
        /// <remarks>
        /// Identified by behaviour rather than by text, which is what makes this robust against
        /// the framework's own localisation: setting <c>PropertySort</c> checks exactly one of
        /// them, and which one is the definition of what that button is.
        /// </remarks>
        [TestCase(0, PropertySort.Categorized)]
        [TestCase(1, PropertySort.Alphabetical)]
        [TestCase(2, PropertySort.NoSort)]
        public void TheSortButtonsAreInTheOrderThisCodeAssumes(int index, PropertySort sort)
        {
            using (var window = Shown())
            {
                var grid = DpiScaling.DescendantsOf(window).OfType<PropertyGrid>().First();
                var strip = FrameworkStripOf(window);

                grid.PropertySort = sort;

                Assert.That(((ToolStripButton)strip.Items[index]).Checked, Is.True,
                            $"item {index} is not the {sort} button any more");
            }
        }

        [Test]
        public void EverySortButtonCarriesThisApplicationsArtwork()
        {
            using (var window = Shown())
            {
                var strip = FrameworkStripOf(window);

                Assert.Multiple(() =>
                {
                    foreach (var index in new[] { 0, 1, 2 })
                        Assert.That(Glyphs.IsCached(strip.Items[index].Image), Is.True,
                                    $"item {index} is still drawing a framework bitmap, so it does " +
                                    "not match the rest of the toolbar and cannot be re-picked on a " +
                                    "DPI change");
                });
            }
        }

        /// <summary>
        /// The NoSort button can be told apart from the one beside it.
        /// </summary>
        /// <remarks>
        /// Stock WinForms gives NoSort the same bitmap as Alphabetical and no tooltip at all, so
        /// two adjacent buttons look identical and one of them cannot be identified by hovering.
        /// Both halves of that are fixed in ConfigWindow, and both are worth pinning.
        /// </remarks>
        [Test]
        public void TheNoSortButtonIsDistinguishableFromAlphabetical()
        {
            using (var window = Shown())
            {
                var strip = FrameworkStripOf(window);

                Assert.Multiple(() =>
                {
                    Assert.That(strip.Items[2].Image, Is.Not.SameAs(strip.Items[1].Image),
                                "NoSort and Alphabetical are drawing the same image");
                    Assert.That(strip.Items[2].ToolTipText, Is.Not.Empty,
                                "NoSort has no tooltip, so it cannot be identified by hovering");
                });
            }
        }

        [Test]
        public void ThePropertyPagesButtonIsStillHidden()
        {
            using (var window = Shown())
            {
                Assert.That(FrameworkStripOf(window).Items[4].Visible, Is.False);
            }
        }

        /// <summary>
        /// The two buttons whose image is assigned while the window is running are drawn from
        /// the frame the strip asks for, not from the 16 pixel one.
        /// </summary>
        /// <remarks>
        /// The four mode buttons are given their image once, in InitializeComponent, so
        /// <see cref="DpiScaling.ApplyImageScaling"/> re-picks them on the way up and they are
        /// right at any scale. These two are not: the connection icon is reassigned on every
        /// selection change and on every pick from the icon menu, and the host status mark on
        /// every selection change and every ping reply - all long after that pass has run. Each
        /// of those assignments took the 16 pixel frame, so above 100% the strip drew a 16 into
        /// a larger rectangle and two soft buttons sat beside four sharp ones.
        /// </remarks>
        [Test]
        public void TheConnectionIconIsDrawnFromTheFrameTheStripAsksFor()
        {
            using (var window = Shown())
            {
                var strip = FrameworkStripOf(window);
                DpiScaling.ApplyImageScaling(strip, Dpi192);

                window.SelectedTreeNode = new ConnectionInfo { Icon = ConnectionIcon.DefaultIconName };

                var expected = DpiScaling.Scale(16, Dpi192);
                Assert.That(ItemTitled(strip, mRemoteUG.Language.strButtonIcon).Image.Size,
                            Is.EqualTo(new Size(expected, expected)),
                            "the connection icon is still the 16 pixel frame, so it is stretched");
            }
        }

        [Test]
        public void TheHostStatusMarkIsDrawnFromTheFrameTheStripAsksFor()
        {
            using (var window = Shown())
            {
                var strip = FrameworkStripOf(window);
                DpiScaling.ApplyImageScaling(strip, Dpi192);

                window.SelectedTreeNode = new ConnectionInfo { Hostname = "localhost" };

                var expected = DpiScaling.Scale(16, Dpi192);
                Assert.That(ItemTitled(strip, mRemoteUG.Language.strStatus).Image.Size,
                            Is.EqualTo(new Size(expected, expected)),
                            "the host status mark is still the 16 pixel frame, so it is stretched");
            }
        }

        /// <summary>
        /// One of this application's own merged buttons, by the text it was given.
        /// </summary>
        /// <remarks>
        /// Text rather than index, which is the opposite of what the tests above do and for the
        /// opposite reason: these are this fork's own buttons carrying this fork's own strings,
        /// so the text is stable here in a way the framework's is not.
        /// </remarks>
        private static ToolStripItem ItemTitled(ToolStrip strip, string text) =>
            strip.Items.Cast<ToolStripItem>().First(item => item.Text == text);

        private const int Dpi192 = 192;
    }
}
