using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using mRemoteUG.Connection;
using mRemoteUG.Connection.Protocol;
using mRemoteUG.UI;
using mRemoteUG.UI.Controls;
using mRemoteUG.UI.Window;
using NUnit.Framework;

namespace mRemoteUG.Tests.UI
{
    /// <summary>
    /// Guards what happens when a window is dragged to a monitor at a different scale.
    /// </summary>
    /// <remarks>
    /// This is the half of HiDPI that a first pass missed. Starting the application at 200% looked
    /// right, so the work was called done; but the metrics that the framework fixes at construction
    /// and never revisits only go wrong once the DPI actually changes, and all three of the
    /// reported symptoms - a connection list that stayed compact, menu glyphs that kept their old
    /// size, and node icons that vanished outright - were that one situation.
    /// <para>
    /// Everything asserted here was measured first, because none of it is guessable: a TreeView
    /// derives <c>ItemHeight</c> from its font and image list but only while nothing has set it;
    /// <c>Indent</c> is derived from nothing at all; and <c>ImageList.ImageSize</c> empties the
    /// collection once a native handle exists.
    /// </para>
    /// </remarks>
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class DpiChangeTests
    {
        private const int Dpi96 = 96;
        private const int Dpi192 = 192;

        /// <summary>
        /// Drives the same entry point Windows uses for a DPI change on an existing window.
        /// </summary>
        /// <remarks>
        /// <c>RescaleConstantsForDpi</c> is protected, and reaching it by reflection is the only
        /// way to make a DPI change happen on demand: the real one needs a second monitor at a
        /// different scale and a hand to drag the window between them.
        /// </remarks>
        private static void ChangeDpi(Control control, int from, int to)
        {
            var method = typeof(Control).GetMethod("RescaleConstantsForDpi",
                                                   BindingFlags.Instance | BindingFlags.NonPublic);
            method.Invoke(control, new object[] { from, to });
        }

        /// <summary>
        /// Resizing an image list keeps the images in it.
        /// </summary>
        /// <remarks>
        /// Measured: assigning <c>ImageSize</c> to a list whose handle exists leaves it empty - two
        /// images in, none out. Before a handle exists they survive, which is exactly why this was
        /// missed. At startup nothing is realized yet and the icons were fine; the first DPI change
        /// after a window was on screen wiped them and every node drew blank.
        /// </remarks>
        [Test]
        public void ResizingAnImageListKeepsItsImages()
        {
            using (var list = new ImageList
            {
                ColorDepth = ColorDepth.Depth32Bit,
                ImageSize = new Size(16, 16),
                TransparentColor = Color.Transparent
            })
            {
                list.Images.Add("first", new Bitmap(16, 16));
                list.Images.Add("second", new Bitmap(16, 16));

                // The handle is what makes the assignment destructive, so it has to exist here or
                // this test passes against the bug it is written for.
                Assert.That(list.Handle, Is.Not.EqualTo(IntPtr.Zero));

                DpiScaling.ResizeForDpi(list, 16, Dpi192);

                Assert.That(list.ImageSize, Is.EqualTo(new Size(32, 32)));
                Assert.That(list.Images.Count, Is.EqualTo(2), "the images were lost in the resize");
                Assert.That(list.Images.IndexOfKey("first"), Is.EqualTo(0));
                Assert.That(list.Images.IndexOfKey("second"), Is.EqualTo(1));
                // Everything above passes against the defect this test was extended for. Keys
                // that existed before the resize still resolve, because the collection keeps a
                // parallel key table that assigning ImageSize does not clear, and the stale
                // entries sit at the indices the re-added images landed on. Measured on the bug:
                // Count 2, Keys 4.
                Assert.That(list.Images.Keys.Count, Is.EqualTo(list.Images.Count),
                            "the key table and the image array disagree in length, so lookups " +
                            "past the image count resolve to the wrong image or to nothing");

                // A key added after the resize is the case the application actually hits: a
                // connection icon is built lazily the first time a node asks for it, so every
                // one of them is added to a list that has already been rescaled.
                list.Images.Add("third", new Bitmap(16, 16));
                Assert.That(list.Images.IndexOfKey("third"), Is.EqualTo(2),
                            "a key added after the resize is invisible to IndexOfKey");
                Assert.That(list.Images.ContainsKey("third"), Is.True);
            }
        }

        /// <summary>
        /// The connection tree's rows, indent and icons all follow a DPI change.
        /// </summary>
        /// <remarks>
        /// The three together are the "connection list does not scale" report. Row height and icon
        /// size fail independently of each other, so all three are asserted rather than one
        /// standing in for the rest.
        /// </remarks>
        [Test]
        public void TheConnectionTreeFollowsADpiChange()
        {
            using (var window = new ConnectionTreeWindow())
            {
                window.CreateControl();
                var tree = window.ConnectionTree;
                tree.CreateControl();
                var _ = tree.ImageList.Handle;

                var indentBefore = tree.Indent;
                var itemHeightBefore = tree.ItemHeight;
                var iconsBefore = tree.ImageList.Images.Count;
                Assert.That(iconsBefore, Is.GreaterThan(0), "no icons to begin with");

                ChangeDpi(tree, tree.DeviceDpi, Dpi192);

                Assert.That(tree.ImageList.Images.Count, Is.EqualTo(iconsBefore),
                            "the node icons were lost when the DPI changed");
                Assert.That(tree.ImageList.ImageSize.Width, Is.EqualTo(32),
                            "the node icons did not grow with the DPI");
                Assert.That(tree.Indent, Is.GreaterThan(indentBefore),
                            "the node indent did not grow with the DPI");
                Assert.That(tree.ItemHeight, Is.GreaterThanOrEqualTo(itemHeightBefore),
                            "the row height did not follow the DPI");
            }
        }

        /// <summary>
        /// Nothing may pin the tree's row height, which is what stops it following the font.
        /// </summary>
        /// <remarks>
        /// Measured: a TreeView left alone reports ItemHeight 18 for a 9pt font and 34 for 18pt,
        /// and 32 when a 32-pixel image list is attached - it follows both for free. Set it once
        /// and it never moves again, which is the whole defect: the designer pinned it to 18 and
        /// rows stayed 18 pixels tall while the font grew to 36 at 200%.
        /// </remarks>
        [Test]
        public void TheTreeRowHeightIsLeftToTheFramework()
        {
            using (var window = new ConnectionTreeWindow())
            {
                var tree = window.ConnectionTree;
                tree.CreateControl();

                var before = tree.ItemHeight;
                using (var larger = new Font(tree.Font.FontFamily, tree.Font.SizeInPoints * 2))
                {
                    tree.Font = larger;
                    Assert.That(tree.ItemHeight, Is.GreaterThan(before),
                                "ItemHeight did not follow the font, so something has set it " +
                                "explicitly - which pins it for the life of the control.");
                }
            }
        }

        /// <summary>
        /// Setting a strip's glyph size carries to the menus hanging off it.
        /// </summary>
        /// <remarks>
        /// This pins a framework behaviour the fix leans on rather than a defect of ours, and it is
        /// worth pinning because the two halves of it point opposite ways. Assigning
        /// <c>ImageScalingSize</c> propagates, so nothing has to walk the menu tree; but the
        /// strip's <em>own</em> rescale during a DPI change does not, and a drop-down was measured
        /// sitting at 16x16 while its owner had already moved to 32x32. That asymmetry is why the
        /// assignment is made by hand on every DPI change instead of trusting the framework.
        /// </remarks>
        [Test]
        public void SettingAStripsGlyphSizeCarriesToItsMenus()
        {
            using (var strip = new MenuStrip())
            {
                var parent = new ToolStripMenuItem("parent");
                parent.DropDownItems.Add(new ToolStripMenuItem("child", new Bitmap(16, 16)));
                strip.Items.Add(parent);

                DpiScaling.ApplyImageScaling(strip, Dpi192);

                Assert.That(strip.ImageScalingSize, Is.EqualTo(new Size(32, 32)));
                Assert.That(parent.DropDown.ImageScalingSize, Is.EqualTo(new Size(32, 32)),
                            "the drop-down kept the old glyph size");
            }
        }

        /// <summary>
        /// The Quick Connect entry field grows when the DPI does.
        /// </summary>
        /// <remarks>
        /// The field was authored as <c>Size = (200, 25)</c> on the ToolStripItem, and that is a
        /// worse defect than it looks. A <c>ToolStripItem</c> is not a <c>Control</c>, so the
        /// auto-scale pass never sees its Size - the same fact already recorded here for
        /// <c>ColumnHeader.Width</c>. Worse, assigning the item's Size pushes the width onto the
        /// hosted ComboBox and <c>ToolStripControlHost.GetPreferredSize</c> reads it straight back,
        /// so the value sustains itself: measured 200 physical pixels at 96 DPI and still 200 at
        /// 192, while the font inside it doubled.
        /// </remarks>
        [Test]
        public void TheQuickConnectEntryFieldGrowsWithTheDpi()
        {
            using (var form = new Form())
            using (var strip = new QuickConnectToolStrip())
            {
                form.Controls.Add(strip);
                form.CreateControl();
                var combo = EntryFieldOf(strip);
                var before = combo.Width;

                ChangeDpi(strip, Dpi96, Dpi192);

                Assert.That(combo.Width, Is.GreaterThan(before * 3 / 2),
                            $"the entry field measured {combo.Width} pixels at 192 DPI against " +
                            $"{before} at 96 - the font doubled and the field did not follow.");
            }
        }

        /// <summary>
        /// The entry field follows the font even when no DPI change happens.
        /// </summary>
        /// <remarks>
        /// This is the half a DPI-scaled literal cannot cover, and the reason the width is measured
        /// rather than merely scaled. A user who has set a larger UI font, or a run under
        /// <c>--selftest --largefont</c>, changes the font with the DPI staying at 96; scaling a
        /// literal by the DPI answers the same 200 it always did, and the text no longer fits.
        /// </remarks>
        [Test]
        public void TheQuickConnectEntryFieldFollowsItsFont()
        {
            using (var form = new Form())
            using (var strip = new QuickConnectToolStrip())
            {
                form.Controls.Add(strip);
                form.CreateControl();
                var combo = EntryFieldOf(strip);
                var before = combo.Width;

                using (var doubled = new Font(strip.Font.FontFamily, strip.Font.SizeInPoints * 2))
                {
                    strip.Font = doubled;

                    Assert.That(combo.Width, Is.GreaterThan(before),
                                $"the entry field stayed {combo.Width} pixels wide while its font " +
                                "doubled, so it is scaled by the DPI alone and a larger system " +
                                "font still clips.");
                }
            }
        }

        /// <summary>
        /// The width is recomputed from scratch every time, never multiplied.
        /// </summary>
        /// <remarks>
        /// The guard against the mistake that sank the first attempt at the nested drop-down fix
        /// recorded in ADR-0010: a value derived from state that has already been scaled, assigned
        /// back into something that then scales it again. Here every term is read afresh from the
        /// current font and the DPI passed in, so running the pass twice - or letting the framework
        /// run its own scaling in between - has to leave the same number.
        /// <para>
        /// That is also what makes it unnecessary to know whether <c>PerformAutoScale</c> touches
        /// <c>ToolStripItem.Size</c>, which cannot be measured on a single-monitor machine. This
        /// test asserts the invariant instead of the framework's behaviour, so it cannot go stale.
        /// </para>
        /// </remarks>
        [Test]
        public void TheQuickConnectEntryFieldIsRecomputedNotScaled()
        {
            using (var form = new Form())
            using (var strip = new QuickConnectToolStrip())
            {
                form.Controls.Add(strip);
                form.CreateControl();
                var combo = EntryFieldOf(strip);

                ChangeDpi(strip, Dpi96, Dpi192);
                var once = combo.Width;

                ChangeDpi(strip, Dpi96, Dpi192);

                Assert.That(combo.Width, Is.EqualTo(once),
                            $"a second pass at the same DPI took the entry field from {once} to " +
                            $"{combo.Width} pixels, so the width is being multiplied rather than " +
                            "recomputed.");

                strip.Scale(new SizeF(2f, 2f));
                ChangeDpi(strip, Dpi96, Dpi192);

                Assert.That(combo.Width, Is.EqualTo(once),
                            $"an auto-scale pass over the strip left the entry field at " +
                            $"{combo.Width} instead of the {once} its font and DPI ask for.");
            }
        }

        /// <summary>
        /// Every item on the bar measures itself, which is what makes the deleted literals dead.
        /// </summary>
        /// <remarks>
        /// <c>Initialize</c> used to carry a pixel Size for the label and both buttons. They were
        /// inert, because <c>ToolStripItem.AutoSize</c> defaults to true and preferred size wins,
        /// and they were deleted rather than scaled. This is the test that says so out loud: if
        /// AutoSize is ever switched off, those sizes stop being dead and have to come back scaled.
        /// </remarks>
        [Test]
        public void EveryQuickConnectItemMeasuresItself()
        {
            using (var strip = new QuickConnectToolStrip())
            {
                foreach (ToolStripItem item in strip.Items)
                    Assert.That(item.AutoSize, Is.True,
                                $"{item.Name} has AutoSize off, so a pixel size for it is no " +
                                "longer dead and has to be scaled for the DPI.");
            }
        }

        /// <summary>
        /// The text in the entry field follows the strip's font, which it does not do by itself.
        /// </summary>
        /// <remarks>
        /// Measured while fixing the width, and the more surprising half of it. A
        /// <see cref="ToolStripComboBox"/> is a <c>ToolStripControlHost</c>, and
        /// <c>ToolStripControlHost.Font</c> *is* the hosted control's font rather than something
        /// resolved from the owner - so assigning the strip a font moves the labels and buttons and
        /// leaves the combo box behind. The strip went to 18pt with the hosted ComboBox still at
        /// 9pt. <c>DpiScaling.FollowDpiChange</c> therefore grew the menu text on every DPI change
        /// and never the quick connect field's, so the bar has to hand the font down by name.
        /// <para>
        /// This also makes <c>QuickConnectComboBox</c>'s own <c>ItemHeight</c> hook fire, which is
        /// what keeps the dropped-down rows in step with the text.
        /// </para>
        /// </remarks>
        [Test]
        public void TheQuickConnectEntryTextFollowsTheStripsFont()
        {
            using (var form = new Form())
            using (var strip = new QuickConnectToolStrip())
            {
                form.Controls.Add(strip);
                form.CreateControl();
                var combo = EntryFieldOf(strip);

                using (var doubled = new Font(strip.Font.FontFamily, strip.Font.SizeInPoints * 2))
                {
                    strip.Font = doubled;

                    Assert.That(combo.ComboBox.Font.SizeInPoints, Is.EqualTo(doubled.SizeInPoints),
                                $"the entry field is drawing at {combo.ComboBox.Font.SizeInPoints}pt " +
                                $"while the strip around it is at {doubled.SizeInPoints}pt.");
                    Assert.That(combo.ComboBox.ItemHeight, Is.EqualTo(combo.ComboBox.Font.Height),
                                "the dropped-down rows no longer match the text they hold.");
                }
            }
        }

        /// <summary>The Quick Connect bar's entry field, reached the way the bar exposes it.</summary>
        private static QuickConnectComboBox EntryFieldOf(QuickConnectToolStrip strip) =>
            strip.Items.OfType<QuickConnectComboBox>().Single();

        /// <summary>
        /// A ToolStrip does not inherit its parent's font, which is why menu text went stale.
        /// </summary>
        /// <remarks>
        /// This pins the framework behaviour the fix exists for. Every other control on a form
        /// follows when the form's font changes; a ToolStrip reads a process-wide default instead
        /// and stays where it was, so the menu bar kept its 200% text after the window moved to a
        /// 100% monitor. If this ever starts failing, the framework has changed and the assignment
        /// in <see cref="DpiScaling.FollowDpiChange"/> can go.
        /// </remarks>
        [Test]
        public void AToolStripDoesNotInheritTheParentFont()
        {
            using (var form = new Form())
            using (var strip = new MenuStrip())
            using (var label = new Label())
            {
                form.Controls.Add(strip);
                form.Controls.Add(label);
                form.CreateControl();

                using (var doubled = new Font(form.Font.FontFamily, form.Font.SizeInPoints * 2))
                {
                    form.Font = doubled;

                    Assert.That(label.Font.SizeInPoints, Is.EqualTo(doubled.SizeInPoints),
                                "a Label should follow its parent");
                    Assert.That(strip.Font.SizeInPoints, Is.Not.EqualTo(doubled.SizeInPoints),
                                "a ToolStrip apparently follows its parent now, so the explicit " +
                                "font assignment on a DPI change is no longer needed");
                }
            }
        }

        /// <summary>
        /// The follow-up pass gives every strip in a window the font and glyph size it should have.
        /// </summary>
        /// <remarks>
        /// Built on a stand-in rather than on the real main window, which is a singleton whose
        /// construction starts most of the application. The shape is what matters: a docked strip,
        /// a context menu that is not a child of anything, and a menu hanging off an item.
        /// </remarks>
        [Test]
        public void TheFollowUpPassReachesEveryStripInTheWindow()
        {
            using (var form = new Form())
            using (var strip = new MenuStrip { Name = "docked" })
            using (var panel = new Panel())
            using (var context = new ContextMenuStrip { Name = "context" })
            {
                var withMenu = new ToolStripMenuItem("parent");
                withMenu.DropDownItems.Add(new ToolStripMenuItem("child", new Bitmap(16, 16)));
                strip.Items.Add(withMenu);

                panel.ContextMenuStrip = context;
                form.Controls.Add(strip);
                form.Controls.Add(panel);
                form.CreateControl();

                using (var target = new Font(form.Font.FontFamily, form.Font.SizeInPoints * 2))
                {
                    DpiScaling.FollowDpiChange(form, Dpi192, target);

                    Assert.That(strip.Font.SizeInPoints, Is.EqualTo(target.SizeInPoints),
                                "the docked toolbar kept its old font");
                    Assert.That(context.Font.SizeInPoints, Is.EqualTo(target.SizeInPoints),
                                "the context menu kept its old font");
                    Assert.That(strip.ImageScalingSize, Is.EqualTo(new Size(32, 32)));
                    Assert.That(withMenu.DropDown.ImageScalingSize, Is.EqualTo(new Size(32, 32)));
                }
            }
        }

        /// <summary>
        /// The sweep finds a menu held only in a field.
        /// </summary>
        /// <remarks>
        /// A context menu is never a child control. Most in this application are reachable through
        /// <see cref="Control.ContextMenuStrip"/>, but the connection tab menu is held only in a
        /// designer field and shown by hand - and that window takes a connection in its constructor,
        /// so it cannot be built here to prove the point. Hence a stand-in with the same shape: a
        /// control holding a strip in a field and nowhere else.
        /// </remarks>
        [Test]
        public void TheSweepReachesAMenuHeldOnlyInAField()
        {
            using (var host = new FieldOnlyMenuHost())
            {
                Assert.That(DpiScaling.ToolStripsOf(host).Select(s => s.Name),
                            Does.Contain("heldInAField"));
            }
        }

        private sealed class FieldOnlyMenuHost : UserControl
        {
            // referenced by the sweep through reflection, not by name
#pragma warning disable CS0414
            private readonly ContextMenuStrip _menu = new ContextMenuStrip { Name = "heldInAField" };
#pragma warning restore CS0414
        }

        /// <summary>
        /// The sweep finds both the docked toolbar and the context menu of a real window.
        /// </summary>
        /// <remarks>
        /// A per-window sweep that silently returns nothing is indistinguishable from one that
        /// passes, so this pins that the real thing is actually found.
        /// </remarks>
        [Test]
        public void TheToolStripSweepReachesARealWindowsMenus()
        {
            using (var window = new ConnectionTreeWindow())
            {
                var found = DpiScaling.ToolStripsOf(window).Select(s => s.Name).ToList();

                Assert.That(found, Does.Contain("msMain"), "the toolbar was not found");
                Assert.That(found, Does.Contain("cMenTree"), "the context menu was not found");
            }
        }

        /// <summary>
        /// The green badge marks the connections that are open, at every scale.
        /// </summary>
        /// <remarks>
        /// Reported inverted on a scaled monitor: the badge sat on the hosts that were <em>not</em>
        /// connected. The selection in StatusImageList was never wrong - the image list underneath
        /// it was, and only above 100%, because the rescale is skipped at 96 DPI.
        /// <para>
        /// A connection with an icon of its own is what makes this bite. Those icons are built the
        /// first time a node asks for one, so they are added to a list that has already been
        /// rescaled, which is exactly the case a stale key table hides. The two built-in defaults
        /// are added before any rescale and resolve correctly even against the defect, so a test
        /// written over them passes for the wrong reason.
        /// </para>
        /// </remarks>
        [Test]
        public void TheConnectedBadgeMarksTheOpenConnectionAfterARescale()
        {
            using (var statusImages = new StatusImageList())
            {
                // Realized first: before a handle exists the assignment is harmless, which is why
                // this never showed at startup.
                var _ = statusImages.ImageList.Handle;
                statusImages.RescaleForDpi(Dpi192);

                var icon = ConnectionIcon.Icons.First();
                var idle = new ConnectionInfo { Icon = icon };
                var open = new ConnectionInfo { Icon = icon };
                open.OpenConnections.Add(new DummyProtocol());

                var idleKey = statusImages.GetKey(idle);
                var openKey = statusImages.GetKey(open);
                var images = statusImages.ImageList.Images;

                Assert.That(idleKey, Is.Not.EqualTo(openKey), "the two states asked for one image");
                Assert.That(images.IndexOfKey(idleKey), Is.GreaterThanOrEqualTo(0),
                            "the unconnected icon does not resolve, so the node draws the root glyph");
                Assert.That(images.IndexOfKey(openKey), Is.GreaterThanOrEqualTo(0),
                            "the connected icon does not resolve, so the node draws the root glyph");
                Assert.That(images.IndexOfKey(idleKey), Is.Not.EqualTo(images.IndexOfKey(openKey)),
                            "both states resolved to the same image");

                // Which of the two carries the badge, rather than merely that they differ - the
                // defect swapped them, so a difference alone holds just as well against it. The
                // badge is inset into a corner of the base icon, so the connected image has
                // green where the other does not.
                Assert.That(GreenPixels(images[images.IndexOfKey(openKey)]),
                            Is.GreaterThan(GreenPixels(images[images.IndexOfKey(idleKey)])),
                            "the badge is on the unconnected icon");
            }
        }

        /// <summary>
        /// A rescaled image list holds images of the new size, not stretched old ones.
        /// </summary>
        /// <remarks>
        /// ImageList.ImageSize alone has always reported the new size - it is the property that
        /// was assigned. What it could not say is whether anything behind it changed, and while
        /// every glyph had one 16x16 frame the honest answer was no. This asserts the images.
        /// </remarks>
        [Test]
        public void ARescaledImageListHoldsImagesDrawnForTheNewSize()
        {
            using (var statusImages = new StatusImageList())
            {
                var _ = statusImages.ImageList.Handle;
                statusImages.RescaleForDpi(Dpi192);

                var expected = DpiScaling.Scale(16, Dpi192);
                Assert.That(statusImages.ImageList.ImageSize.Width, Is.EqualTo(expected));

                var images = statusImages.ImageList.Images;
                Assume.That(images.Count, Is.GreaterThan(0));

                for (var i = 0; i < images.Count; i++)
                    Assert.That(images[i].Size, Is.EqualTo(new Size(expected, expected)),
                                $"the image at {i} is not the size the list was rescaled to");
            }
        }

        /// <summary>
        /// A DPI change re-picks the frame each toolbar glyph is drawn from.
        /// </summary>
        /// <remarks>
        /// ToolStrip.ImageScalingSize only says what rectangle the image is drawn into; the Image
        /// itself is whatever was assigned when the menu was built, which is the 16 pixel frame.
        /// Before the artwork had a ladder there was nothing else to assign, so the strip reported
        /// a correct ImageScalingSize while every glyph on it was a stretched 16.
        /// </remarks>
        [Test]
        public void ADpiChangeRepicksTheFrameAToolbarGlyphIsDrawnFrom()
        {
            using (var strip = new ToolStrip())
            {
                var item = new ToolStripMenuItem { Image = mRemoteUG.Resources.Folder };
                strip.Items.Add(item);
                Assume.That(item.Image.Size, Is.EqualTo(new Size(16, 16)));

                DpiScaling.ApplyImageScaling(strip, Dpi192);

                var expected = DpiScaling.Scale(16, Dpi192);
                Assert.That(strip.ImageScalingSize.Width, Is.EqualTo(expected));
                Assert.That(item.Image.Size, Is.EqualTo(new Size(expected, expected)),
                            "the glyph is still the 16 pixel frame, so it is being stretched");
            }
        }

        /// <summary>An image that is not a glyph is left alone by the re-pick.</summary>
        [Test]
        public void ADpiChangeLeavesAnImageItDidNotProvideAlone()
        {
            using (var strip = new ToolStrip())
            using (var stranger = new Bitmap(16, 16))
            {
                var item = new ToolStripMenuItem { Image = stranger };
                strip.Items.Add(item);

                DpiScaling.ApplyImageScaling(strip, Dpi192);

                Assert.That(item.Image, Is.SameAs(stranger));
            }
        }

        /// <summary>Pixels the connected overlay contributes: opaque, and green-dominant.</summary>
        private static int GreenPixels(Image image)
        {
            using (var bitmap = new Bitmap(image))
            {
                var count = 0;
                for (var x = 0; x < bitmap.Width; x++)
                for (var y = 0; y < bitmap.Height; y++)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    if (pixel.A > 128 && pixel.G > 96 && pixel.G > pixel.R + 24 && pixel.G > pixel.B + 24)
                        count++;
                }

                return count;
            }
        }

        private class DummyProtocol : ProtocolBase
        {
        }
    }
}
