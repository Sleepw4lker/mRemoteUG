using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using mRemoteUG.UI.Forms;
using mRemoteUG.UI.Forms.OptionsPages;
using mRemoteUG.UI.Window;
using NUnit.Framework;

namespace mRemoteUG.Tests.UI
{
    /// <summary>
    /// Guards the parts of HiDPI support that a build cannot see and a screenshot would only show
    /// on the machine that took it.
    /// </summary>
    /// <remarks>
    /// The failure mode these cover is silence. A container that declares no auto-scale baseline
    /// does not throw, it just never scales; a fixed pixel cap on a toolbar does not throw, it just
    /// clips its own contents above 100%. Both shipped in this application for years, and neither
    /// is visible at 96 DPI, which is what a developer's machine most often is.
    /// </remarks>
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class DpiScalingTests
    {
        /// <summary>
        /// Types excluded deliberately, so that adding one is a decision rather than an accident.
        /// </summary>
        private static readonly Dictionary<Type, string> Excluded = new Dictionary<Type, string>
        {
            [typeof(FrmMain)] = "Singleton that builds the entire application shell; constructing " +
                                "it here would run most of the program rather than test a form.",
            [typeof(BaseWindow)] = "A base class, never shown on its own. It deliberately declares " +
                                   "no baseline because its derived windows do not share a font - " +
                                   "see the remarks on its constructor."
        };

        private static IEnumerable<Type> Containers()
        {
            return typeof(FrmMain).Assembly
                                  .GetTypes()
                                  .Where(t => typeof(ContainerControl).IsAssignableFrom(t))
                                  .Where(t => !t.IsAbstract && !t.IsGenericTypeDefinition)
                                  .Where(t => t.GetConstructor(BindingFlags.Public | BindingFlags.Instance,
                                                               null, Type.EmptyTypes, null) != null)
                                  .Where(t => !Excluded.ContainsKey(t))
                                  .OrderBy(t => t.FullName);
        }

        /// <summary>
        /// A container with no mode or no baseline scales by nothing at all.
        /// </summary>
        /// <remarks>
        /// The baseline has to be read before the handle exists. <c>PerformAutoScale</c> overwrites
        /// <c>AutoScaleDimensions</c> with <c>CurrentAutoScaleDimensions</c> as soon as it has
        /// scaled, after which every container reports a factor of exactly 1.000 whatever its
        /// designer said - so a test that constructs first and asks afterwards can only ever pass.
        /// </remarks>
        [TestCaseSource(nameof(Containers))]
        public void ContainerDeclaresAnAutoScaleBaseline(Type containerType)
        {
            ContainerControl container = null;
            try
            {
                container = (ContainerControl)Activator.CreateInstance(containerType);

                Assert.That(container.AutoScaleMode,
                            Is.Not.EqualTo(AutoScaleMode.None).And.Not.EqualTo(AutoScaleMode.Inherit),
                            $"{containerType.FullName} does not auto-scale: a Form left at " +
                            "AutoScaleMode.Inherit keeps its designer pixel sizes at every DPI.");

                Assert.That(container.AutoScaleDimensions, Is.Not.EqualTo(SizeF.Empty),
                            $"{containerType.FullName} declares AutoScaleMode.{container.AutoScaleMode} " +
                            "but no AutoScaleDimensions, which makes the mode a no-op: WinForms " +
                            "fills the baseline in from the current metric and scales by 1.0.");
            }
            finally
            {
                container?.Dispose();
            }
        }

        /// <summary>
        /// Scaling a container has to actually move its contents.
        /// </summary>
        /// <remarks>
        /// Proves the declared baseline is wired up rather than merely present: a container whose
        /// children are all docked or auto-sized would satisfy the test above and still be pinned
        /// to 96 DPI measurements in the places that matter.
        /// </remarks>
        [TestCaseSource(nameof(Containers))]
        public void ScalingAContainerGrowsIt(Type containerType)
        {
            ContainerControl container = null;
            try
            {
                container = (ContainerControl)Activator.CreateInstance(containerType);
                container.CreateControl();

                var before = container.ClientSize;
                if (before.Width == 0 || before.Height == 0)
                    Assert.Ignore($"{containerType.Name} has no client area to scale.");

                container.Scale(new SizeF(2f, 2f));
                var after = container.ClientSize;

                Assert.That(after.Width, Is.GreaterThan(before.Width),
                            $"{containerType.FullName} did not get wider when scaled 2x.");
                Assert.That(after.Height, Is.GreaterThan(before.Height),
                            $"{containerType.FullName} did not get taller when scaled 2x.");
            }
            finally
            {
                container?.Dispose();
            }
        }

        /// <summary>
        /// Every ToolStrip in the assembly, which is not reachable by walking the containers above:
        /// a ToolStrip is not a ContainerControl, and the only thing that hosts one here is FrmMain.
        /// </summary>
        private static IEnumerable<Type> ToolStrips()
        {
            return typeof(FrmMain).Assembly
                                  .GetTypes()
                                  .Where(t => typeof(ToolStrip).IsAssignableFrom(t))
                                  .Where(t => !t.IsAbstract && !t.IsGenericTypeDefinition)
                                  .Where(t => t.GetConstructor(BindingFlags.Public | BindingFlags.Instance,
                                                               null, Type.EmptyTypes, null) != null)
                                  .OrderBy(t => t.FullName);
        }

        /// <summary>
        /// A fixed pixel height cap on a ToolStrip clips its own contents above 100%.
        /// </summary>
        /// <remarks>
        /// This is the QuickConnect toolbar's old <c>MaximumSize = (0, 25)</c>. MaximumSize is not
        /// touched by the auto-scale pass, so the bar stayed 25 pixels tall while the combo box and
        /// buttons inside it grew. Verified to fail by putting the cap back.
        /// </remarks>
        [TestCaseSource(nameof(ToolStrips))]
        public void NoToolStripKeepsAFixedHeightCap(Type toolStripType)
        {
            ToolStrip toolStrip = null;
            try
            {
                toolStrip = (ToolStrip)Activator.CreateInstance(toolStripType);

                Assert.That(toolStrip.MaximumSize.Height, Is.Zero,
                            $"{toolStripType.FullName} caps its height at " +
                            $"{toolStrip.MaximumSize.Height} pixels, which is a 96 DPI measurement " +
                            "that nothing rescales.");
            }
            finally
            {
                toolStrip?.Dispose();
            }
        }

        /// <summary>
        /// A glyph opted out of the strip's image scaling stays 16 pixels at every DPI.
        /// </summary>
        /// <remarks>
        /// <c>ToolStripItemImageScaling.None</c> takes one item's image out of
        /// <see cref="ToolStrip.ImageScalingSize"/> altogether, which is the one thing
        /// <see cref="DpiScaling.ApplyImageScaling"/> cannot reach. The Quick Connect bar had it on
        /// its connections button, so it drew a 16 pixel icon at 200% while the Connect button
        /// beside it drew the same artwork at 32. There is still no reason to opt out: artwork is
        /// picked at the exact size now, so <c>SizeToFit</c> into an equal rectangle is an identity
        /// transform, and opting out only takes the item out of the re-pick as well.
        /// <para>
        /// Only the top-level items are walked. Whether a nested drop-down scales correctly is a
        /// known open defect that ADR-0010 says explicitly not to go near, and asserting on it here
        /// would invite exactly that.
        /// </para>
        /// </remarks>
        [TestCaseSource(nameof(ToolStrips))]
        public void NoToolStripItemOptsOutOfGlyphScaling(Type toolStripType)
        {
            ToolStrip toolStrip = null;
            try
            {
                toolStrip = (ToolStrip)Activator.CreateInstance(toolStripType);

                foreach (ToolStripItem item in toolStrip.Items)
                {
                    if (item.Image == null)
                        continue;

                    Assert.That(item.ImageScaling, Is.Not.EqualTo(ToolStripItemImageScaling.None),
                                $"{toolStripType.FullName}.{item.Name} pins its glyph with " +
                                "ImageScaling.None, so it stays 16 pixels while the rest of the " +
                                "strip follows ImageScalingSize.");
                }
            }
            finally
            {
                toolStrip?.Dispose();
            }
        }

        /// <summary>
        /// A text box shorter than its own font cannot show its text.
        /// </summary>
        /// <remarks>
        /// The connection tree's search box was pinned to 14 pixels, a height that fits Segoe UI
        /// 8.25pt at 96 DPI and nothing above it. Font metrics do not grow linearly with DPI - the
        /// same font measures 13 pixels tall at 96 and 19 at 120 - so scaling that literal is not
        /// enough by itself; the font's own preferred height has to be the floor.
        /// <para>
        /// The window has to be shown, not merely constructed: the sizing happens in the Load
        /// handler, and Load does not run for a form that only had its handle created.
        /// </para>
        /// </remarks>
        [Test]
        public void TheConnectionTreeSearchBoxFitsItsOwnFont()
        {
            using (var window = new ConnectionTreeWindow())
            {
                window.Show();
                try
                {
                    var searchBox = Descendants(window)
                                    .OfType<System.Windows.Forms.TextBox>()
                                    .SingleOrDefault(t => t.Name == "txtSearch");

                    Assert.That(searchBox, Is.Not.Null, "txtSearch has been renamed or removed.");

                    // The row is what is checked, not a MinimumSize on the box. The box is docked
                    // to fill the row and a borderless single-line TextBox pins its own height to
                    // its font, so the box can no longer be too short - but the row around it can,
                    // and then the box is simply clipped by it. That is the value this window now
                    // computes, and so the one that can be wrong.
                    Assert.That(window.SearchRow.Height, Is.GreaterThanOrEqualTo(searchBox.PreferredHeight),
                                $"the search row is {window.SearchRow.Height} pixels tall but the " +
                                $"box in it needs {searchBox.PreferredHeight} for its font.");
                    Assert.That(searchBox.Height, Is.GreaterThanOrEqualTo(searchBox.PreferredHeight),
                                $"the search box is {searchBox.Height} pixels tall but needs " +
                                $"{searchBox.PreferredHeight} for its font.");
                }
                finally
                {
                    window.Close();
                }
            }
        }

        /// <summary>
        /// The options tab strip takes its height from its font, which is what makes it follow the
        /// DPI.
        /// </summary>
        /// <remarks>
        /// <see cref="TabControl.ItemSize"/> defaults to empty, which means "measure from the
        /// font", and the font is what the auto-scale pass already scales. Assigning it - to make
        /// the tabs taller, or all the same width - turns it into a 96-DPI pixel literal that
        /// nothing rescales, which is the bug the page list it replaced used to have in
        /// <see cref="ColumnHeader.Width"/>. This is what notices.
        /// </remarks>
        [Test]
        public void TheOptionsTabStripIsMeasuredFromItsFont()
        {
            using (var options = new OptionsForm())
            {
                options.Show();
                try
                {
                    var tabs = Descendants(options).OfType<TabControl>().Single();
                    var stripBefore = tabs.GetTabRect(0).Height;
                    var pageBefore = tabs.DisplayRectangle.Height;

                    using (var larger = new Font(tabs.Font.FontFamily, tabs.Font.SizeInPoints * 2))
                    {
                        tabs.Font = larger;

                        Assert.That(tabs.GetTabRect(0).Height, Is.GreaterThan(stripBefore),
                                    "the tab strip did not grow with its font, so it will not grow " +
                                    "with the DPI either - ItemSize has probably been pinned.");
                        Assert.That(tabs.DisplayRectangle.Height, Is.LessThan(pageBefore),
                                    "the strip grew but the page area did not give the height back.");
                    }
                }
                finally
                {
                    options.Close();
                }
            }
        }

        /// <summary>
        /// Every options page fits inside the tab that hosts it.
        /// </summary>
        /// <remarks>
        /// The pages are absolutely positioned and were laid out against a 610x489 panel. A
        /// horizontal tab strip spends around 28 pixels of height that the vertical page list it
        /// replaced did not, and the slack absorbs it - the deepest page, Notifications, ends at
        /// 387 - but nothing enforces that. This is what catches a page that grows, a ClientSize
        /// that shrinks, or a tab strip that starts taking more room than it did.
        /// </remarks>
        [Test]
        public void EveryOptionsPageFitsInsideItsTab()
        {
            using (var options = new OptionsForm())
            {
                options.Show();
                try
                {
                    var tabs = Descendants(options).OfType<TabControl>().Single();
                    Assert.That(tabs.TabPages.Count, Is.EqualTo(8),
                                "the options pages did not all load.");

                    // The tab control is measured, not the TabPage: a page that has never been
                    // selected is never laid out, so its own ClientSize is still the designer's
                    // and comparing against it would pass however badly the content fit.
                    var available = tabs.DisplayRectangle.Size;

                    foreach (TabPage tab in tabs.TabPages)
                    {
                        var page = tab.Controls.Cast<Control>().Single();
                        var content = page.Controls.Cast<Control>().ToList();
                        if (content.Count == 0)
                            continue;

                        var bottom = content.Max(c => c.Bounds.Bottom);
                        var right = content.Max(c => c.Bounds.Right);

                        Assert.That(bottom, Is.LessThanOrEqualTo(available.Height),
                                    $"{tab.Text} needs {bottom} pixels of height but its tab " +
                                    $"offers {available.Height}.");
                        Assert.That(right, Is.LessThanOrEqualTo(available.Width),
                                    $"{tab.Text} needs {right} pixels of width but its tab " +
                                    $"offers {available.Width}.");
                    }
                }
                finally
                {
                    options.Close();
                }
            }
        }

        /// <summary>
        /// Every options page scales by the same amount.
        /// </summary>
        /// <remarks>
        /// The eight pages are the same vintage on the same 610x489 designer canvas, so they should
        /// end up the same size. AppearancePage did not: it declared a 7x15 auto-scale baseline
        /// where its siblings declare 6x13, so it alone never scaled, and its AutoSize controls grew
        /// into the whitespace its unscaled Locations had reserved until three pairs of them were
        /// touching at a 0-pixel gap. A declared baseline is overwritten with the current one as
        /// soon as the scale pass runs, so the mismatch cannot be read back directly - the size it
        /// produces is the only symptom left to assert on.
        /// </remarks>
        [Test]
        public void EveryOptionsPageScalesByTheSameAmount()
        {
            var types = typeof(FrmMain).Assembly.GetTypes()
                                       .Where(t => typeof(OptionsPage).IsAssignableFrom(t) &&
                                                   t != typeof(OptionsPage) && !t.IsAbstract)
                                       .OrderBy(t => t.Name)
                                       .ToList();

            Assert.That(types, Has.Count.EqualTo(8), "the options pages were not all found.");

            var measured = new List<string>();
            foreach (var type in types)
                using (var page = (Control)Activator.CreateInstance(type))
                    measured.Add($"{type.Name} {page.ClientSize.Width}x{page.ClientSize.Height}");

            Assert.That(measured.Select(m => m.Split(' ')[1]).Distinct().Count(), Is.EqualTo(1),
                        "the options pages did not all scale to the same size, so one of them " +
                        "declares a different AutoScaleDimensions from the rest: " +
                        string.Join(", ", measured));
        }

        /// <summary>
        /// No two controls on the export dialog overlap each other.
        /// </summary>
        /// <remarks>
        /// ExportForm declared a 7x15 auto-scale baseline against literals that are 6x13
        /// throughout - three buttons at the canonical 75x23, labels at 13, check boxes and radio
        /// buttons at 17, a combo box at 21 - so it never scaled while its twelve AutoSize
        /// controls grew to fit the ambient font. They grew into each other: lblFileFormat ended
        /// up a pixel inside cboFileFormat, and the labels that show the selected folder and
        /// connection overlapped the radio buttons above them by two.
        /// <para>
        /// Degenerate bounds are skipped. Those two labels are empty until the dialog is actually
        /// used, and Rectangle.IntersectsWith reports a zero-width rectangle as intersecting
        /// anything it shares a row with, which would make this assert things that are not true.
        /// </para>
        /// </remarks>
        [Test]
        public void ExportFormControlsDoNotOverlap()
        {
            using (var form = new ExportForm())
            {
                form.Show();
                try
                {
                    foreach (var container in Descendants(form).Concat(new Control[] { form }))
                    {
                        var siblings = container.Controls.Cast<Control>()
                                                .Where(c => c.Width > 0 && c.Height > 0)
                                                .ToList();

                        for (var i = 0; i < siblings.Count; i++)
                        for (var j = i + 1; j < siblings.Count; j++)
                            Assert.That(siblings[i].Bounds.IntersectsWith(siblings[j].Bounds), Is.False,
                                        $"{siblings[i].Name} at {siblings[i].Bounds} overlaps " +
                                        $"{siblings[j].Name} at {siblings[j].Bounds}, inside " +
                                        $"{container.GetType().Name} {container.Name}.");
                    }
                }
                finally
                {
                    form.Close();
                }
            }
        }

        /// <summary>
        /// The panel chooser's buttons fit the dialog and do not overlap.
        /// </summary>
        /// <remarks>
        /// The dialog had an OK button and a New button but no Cancel, so Esc could not dismiss it
        /// and the title bar was the only way out. Fitting a third button in meant moving New to
        /// the bottom left and sliding OK across, all inside the existing 245x107 canvas.
        /// <para>
        /// Constructed but deliberately not shown. Load calls AddAvailablePanels, which walks
        /// Runtime.WindowList - null outside a running application, so showing this form in a test
        /// throws on the UI thread and WinForms answers with a modal error dialog. Nothing here
        /// needs a layout pass anyway: no control on this form is AutoSize, so the auto-scale that
        /// runs during construction is the whole story, and the bounds match a shown form exactly.
        /// </para>
        /// </remarks>
        [Test]
        public void ThePanelChooserButtonsFitWithoutOverlapping()
        {
            using (var form = new frmChoosePanel())
            {
                Assert.That(form.CancelButton, Is.Not.Null,
                            "the dialog has no CancelButton, so Esc cannot dismiss it.");

                var buttons = form.Controls.OfType<Button>().OrderBy(b => b.Bounds.Left).ToList();
                Assert.That(buttons, Has.Count.EqualTo(3), "expected New, OK and Cancel.");

                foreach (var button in buttons)
                {
                    Assert.That(button.Bounds.Right, Is.LessThanOrEqualTo(form.ClientSize.Width),
                                $"{button.Name} runs past the right edge of a {form.ClientSize.Width} " +
                                "pixel dialog.");
                    Assert.That(button.Bounds.Bottom, Is.LessThanOrEqualTo(form.ClientSize.Height),
                                $"{button.Name} runs past the bottom edge.");
                }

                for (var i = 0; i < buttons.Count; i++)
                for (var j = i + 1; j < buttons.Count; j++)
                    Assert.That(buttons[i].Bounds.IntersectsWith(buttons[j].Bounds), Is.False,
                                $"{buttons[i].Name} at {buttons[i].Bounds} overlaps " +
                                $"{buttons[j].Name} at {buttons[j].Bounds}.");
            }
        }

        [Test]
        public void TheExclusionListStaysHonest()
        {
            foreach (var excluded in Excluded.Keys)
                Assert.That(typeof(FrmMain).Assembly.GetTypes(), Does.Contain(excluded),
                            $"{excluded.FullName} is excluded but no longer exists.");
        }

        [Test]
        public void TheSuiteActuallyCoversSomething()
        {
            Assert.That(Containers().Count(), Is.GreaterThan(10));
        }

        private static IEnumerable<Control> Descendants(Control root)
        {
            foreach (Control child in root.Controls)
            {
                yield return child;
                foreach (var descendant in Descendants(child))
                    yield return descendant;
            }
        }
    }
}
