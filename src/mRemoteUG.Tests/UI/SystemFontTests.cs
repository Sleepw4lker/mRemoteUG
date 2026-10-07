using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using mRemoteUG.UI.Forms;
using mRemoteUG.UI.Window;
using NUnit.Framework;

namespace mRemoteUG.Tests.UI
{
    /// <summary>
    /// Guards that the user interface uses the font Windows was asked for, and survives a
    /// bigger one.
    /// </summary>
    /// <remarks>
    /// The application used to pin itself to "Segoe UI 8.25pt" in twenty-one places - the
    /// Windows 7 default, two thirds of a point smaller than Windows 10 and 11 use, and
    /// completely deaf to a user who has chosen a larger UI font. Deleting those assignments is
    /// only half the job: nothing stops the next one being added, and nothing checked that the
    /// layouts actually cope once the font is not the one they were drawn with.
    /// </remarks>
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class SystemFontTests
    {
        private static readonly Dictionary<Type, string> Excluded = new Dictionary<Type, string>
        {
            [typeof(FrmMain)] = "Singleton that builds the entire application shell; constructing " +
                                "it here would run most of the program rather than test a form.",
            [typeof(BaseWindow)] = "A base class, never shown on its own."
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

        private static IEnumerable<Control> Descendants(Control root)
        {
            foreach (Control child in root.Controls)
            {
                yield return child;
                foreach (var descendant in Descendants(child))
                    yield return descendant;
            }
        }

        /// <summary>
        /// No container may pin its own font: it has to be the one Windows chose.
        /// </summary>
        /// <remarks>
        /// This is the guard that keeps "no hardcoded fonts" true. A reintroduced
        /// <c>new Font("Segoe UI", ...)</c> fails here at run time rather than having to be
        /// spotted in review.
        /// </remarks>
        [TestCaseSource(nameof(Containers))]
        public void ContainerUsesTheSystemFont(Type containerType)
        {
            ContainerControl container = null;
            try
            {
                container = (ContainerControl)Activator.CreateInstance(containerType);

                // Compared against Control.DefaultFont, and on size as well as family. The
                // family alone proves nothing: the font this application used to pin was
                // "Segoe UI 8.25pt", whose family is the system family on every Windows this
                // runs on - only the size gave it away.
                Assert.That(container.Font.Name, Is.EqualTo(Control.DefaultFont.Name),
                            $"{containerType.FullName} pins the font family " +
                            $"\"{container.Font.Name}\" instead of using the system one.");

                Assert.That(container.Font.SizeInPoints,
                            Is.EqualTo(Control.DefaultFont.SizeInPoints).Within(0.01f),
                            $"{containerType.FullName} pins its font at " +
                            $"{container.Font.SizeInPoints}pt instead of the system " +
                            $"{Control.DefaultFont.SizeInPoints}pt.");
            }
            finally
            {
                container?.Dispose();
            }
        }

        /// <summary>
        /// Every control uses the system font family; only style and size may differ.
        /// </summary>
        /// <remarks>
        /// Nothing in the application needs a face other than the one Windows reports any more.
        /// The last exception was the About window's credits and changelog viewers, which were
        /// monospaced so their columns lined up, and they went when that window was replaced by
        /// a task dialog. If a monospaced surface is ever needed again this is the right place to
        /// widen deliberately - <c>FontFamily.GenericMonospace</c> being the nearest thing to a
        /// system token for one - rather than to hardcode a face.
        /// </remarks>
        [TestCaseSource(nameof(Containers))]
        public void NoControlIntroducesAnotherFontFamily(Type containerType)
        {
            ContainerControl container = null;
            try
            {
                container = (ContainerControl)Activator.CreateInstance(containerType);

                foreach (var control in Descendants(container))
                {
                    Assert.That(control.Font.Name, Is.EqualTo(Control.DefaultFont.Name),
                                $"{containerType.Name}.{control.Name} uses " +
                                $"\"{control.Font.Name}\" rather than the system font family.");
                }
            }
            finally
            {
                container?.Dispose();
            }
        }

        /// <summary>
        /// A font derived from the window's own has to keep following it.
        /// </summary>
        /// <remarks>
        /// Setting a control's font explicitly opts it out of inheriting its parent's, so the two
        /// windows that need a larger, bolder or monospaced face have to rebuild those faces
        /// whenever the face they were derived from changes. Without that they stay at whatever
        /// size they were first built with while everything around them moves.
        /// <para>
        /// Note what this test does *not* do: it cannot check that a layout copes with a larger
        /// system font by assigning one here. A font assigned after construction does not re-run
        /// the auto-scale pass - <c>CurrentAutoScaleDimensions</c> is already cached, so the
        /// factor comes out 1.0 and only auto-sizing controls move. The only way to see a layout
        /// built for a different font is to start the process with one, which is what
        /// <c>mRemoteUG.exe --selftest --largefont</c> does.
        /// </para>
        /// </remarks>
        /// <summary>
        /// Each derived font, described as what makes it different from the window's own.
        /// </summary>
        /// <remarks>
        /// Asserting only that the size tracks the window would prove nothing: a control with no
        /// font of its own inherits the window's and tracks it for free, so a test like that
        /// passes just as happily when the derivation has been deleted altogether. What
        /// distinguishes a derived font is the difference - the ratio, the style, the family.
        /// </remarks>
        private static IEnumerable<TestCaseData> DerivedFonts()
        {
            // Only one derived font is left in the application. The About window used to carry
            // three more - two heading sizes and a monospaced face for its credits and changelog
            // viewers - and went when that window was replaced by a task dialog.
            yield return new TestCaseData(typeof(ErrorAndInfoWindow), "lblMsgDate", 1f,
                                          FontStyle.Italic, false).SetName("Notifications_Date_IsItalic");
        }

        [TestCaseSource(nameof(DerivedFonts))]
        public void DerivedFontsKeepTheirRelationshipToTheWindowFont(Type windowType, string controlName,
                                                                     float expectedRatio, FontStyle expectedStyle,
                                                                     bool monospaced)
        {
            using (var window = (Form)Activator.CreateInstance(windowType))
            {
                window.CreateControl();
                var control = Descendants(window).Single(c => c.Name == controlName);

                void Check(string when)
                {
                    Assert.That(control.Font.SizeInPoints / window.Font.SizeInPoints,
                                Is.EqualTo(expectedRatio).Within(0.02f),
                                $"{windowType.Name}.{controlName} is " +
                                $"{control.Font.SizeInPoints}pt against a window font of " +
                                $"{window.Font.SizeInPoints}pt {when}.");

                    Assert.That(control.Font.Style, Is.EqualTo(expectedStyle), $"style {when}");

                    if (monospaced)
                        Assert.That(control.Font.Name, Is.EqualTo(FontFamily.GenericMonospace.Name),
                                    $"family {when}");
                    else
                        Assert.That(control.Font.Name, Is.EqualTo(window.Font.Name), $"family {when}");
                }

                Check("as built");

                // Changing the window font must rebuild it, not leave it behind.
                using (var larger = new Font(window.Font.FontFamily, window.Font.SizeInPoints * 1.5f))
                {
                    window.Font = larger;
                    Check("after the window font changed");
                }
            }
        }

        [Test]
        public void TheExclusionListStaysHonest()
        {
            foreach (var excluded in Excluded.Keys)
                Assert.That(typeof(FrmMain).Assembly.GetTypes(), Does.Contain(excluded),
                            $"{excluded.FullName} is excluded but no longer exists.");
        }
    }
}
