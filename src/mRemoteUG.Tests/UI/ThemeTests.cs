using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using mRemoteUG.UI;
using mRemoteUG.UI.Forms;
using mRemoteUG.UI.Window;
using NUnit.Framework;

namespace mRemoteUG.Tests.UI
{
    /// <summary>
    /// Guards the two things that decide whether the application follows the Windows colour theme.
    /// </summary>
    /// <remarks>
    /// Almost all of the theming is the framework's work: <c>Application.SetColorMode</c> re-points
    /// the whole <see cref="SystemColors"/> table, and a colour taken from it is a handle that is
    /// resolved to an ARGB value on every read, so anything painted with one follows for free -
    /// even a value already assigned to a control or cached in a field.
    /// <para>
    /// That leaves exactly two ways to opt out, and both are silent. A control that assigns a
    /// literal colour freezes at whatever was typed; a ToolStrip pinned to the Professional
    /// renderer keeps a light-grey menu, because that renderer has no dark variant to fall back on
    /// and only the system one does. Neither shows up at build time, and neither is visible on a
    /// machine running the light theme - which is what a developer's machine usually is.
    /// </para>
    /// <para>
    /// These run in whatever mode the test process starts in, which is why they assert on what was
    /// <em>written into the source</em> rather than on resolved colours. The resolved colours are
    /// measured by <c>mRemoteUG.exe --selftest --dark</c>, which can set the mode before any
    /// control exists; a test process cannot, because NUnit has already created controls.
    /// </para>
    /// </remarks>
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class ThemeTests
    {
        /// <summary>
        /// Colours a control may hold that are not system colours, each with the reason.
        /// </summary>
        /// <remarks>
        /// Every entry here is a colour that will not change when the theme does, so each one has
        /// to be worth that. Keeping the list explicit is what makes adding to it a decision.
        /// </remarks>
        private static readonly Dictionary<Color, string> AllowedLiteralColors = new Dictionary<Color, string>
        {
            [Color.Transparent] = "Not a colour: the parent's shows through, so it follows the theme."
        };

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
        /// No control may paint with a colour of its own.
        /// </summary>
        /// <remarks>
        /// <see cref="Color.IsSystemColor"/> is the right predicate and
        /// <see cref="Color.IsKnownColor"/> is not: <c>Color.White</c> is a known colour, so a test
        /// written against <c>IsKnownColor</c> passes for exactly the assignments this is meant to
        /// catch. Two of those shipped in this application - a white panel on the reconnect group,
        /// and four severity backgrounds on the notifications pane.
        /// </remarks>
        [TestCaseSource(nameof(Containers))]
        public void NoControlPaintsWithAColourOfItsOwn(Type containerType)
        {
            ContainerControl container = null;
            try
            {
                container = (ContainerControl)Activator.CreateInstance(containerType);

                foreach (var control in new Control[] { container }.Concat(Descendants(container)))
                {
                    AssertFollowsTheTheme(containerType, control, control.BackColor, nameof(Control.BackColor));
                    AssertFollowsTheTheme(containerType, control, control.ForeColor, nameof(Control.ForeColor));
                }
            }
            finally
            {
                container?.Dispose();
            }
        }

        private static void AssertFollowsTheTheme(Type containerType, Control control, Color color, string property)
        {
            if (color.IsSystemColor || AllowedLiteralColors.ContainsKey(color))
                return;

            Assert.Fail($"{containerType.Name}.{control.Name} ({control.GetType().Name}) sets " +
                        $"{property} to {color}, which is not a system colour and so will stay " +
                        "the same when the user switches between the light and dark themes. Use a " +
                        "SystemColors value, or add it to AllowedLiteralColors with the reason.");
        }

        /// <summary>
        /// Every ToolStrip a container owns, however it happens to hold it.
        /// </summary>
        /// <remarks>
        /// Walking <see cref="Control.Controls"/> is not enough and quietly finds almost nothing.
        /// A <see cref="ContextMenuStrip"/> is never a child control: some are reached through the
        /// <see cref="Control.ContextMenuStrip"/> property, and others - the connection tab menu,
        /// for one - are held only in a designer field and shown by hand. A first version of this
        /// test walked the control tree alone and passed happily with the defect it was written to
        /// catch put back, which is the whole reason the sweep looks like this.
        /// </remarks>
        private static IEnumerable<ToolStrip> ToolStripsOf(Control container)
        {
            var seen = new HashSet<ToolStrip>();

            foreach (var control in new[] { container }.Concat(Descendants(container)))
            {
                if (control is ToolStrip self && seen.Add(self))
                    yield return self;

                if (control.ContextMenuStrip != null && seen.Add(control.ContextMenuStrip))
                    yield return control.ContextMenuStrip;

                var fields = control.GetType()
                                    .GetFields(BindingFlags.Instance | BindingFlags.Public |
                                               BindingFlags.NonPublic | BindingFlags.FlattenHierarchy)
                                    .Where(f => typeof(ToolStrip).IsAssignableFrom(f.FieldType));

                foreach (var field in fields)
                {
                    if (field.GetValue(control) is ToolStrip strip && seen.Add(strip))
                        yield return strip;
                }
            }
        }

        /// <summary>
        /// No ToolStrip may pin itself to the Professional renderer.
        /// </summary>
        /// <remarks>
        /// Only <c>ToolStripSystemRenderer</c> substitutes a dark renderer when the application is
        /// in dark mode; <c>ToolStripProfessionalRenderer</c> has no such branch and keeps painting
        /// its light palette whatever the theme. Six controls in this application pinned themselves
        /// to it.
        /// <para>
        /// Note that leaving <see cref="ToolStrip.RenderMode"/> alone is not enough on its own -
        /// its default is <c>ManagerRenderMode</c>, and the manager's own default is Professional.
        /// <c>ProgramRoot.ConfigureApplication</c> sets the manager to System; this test guards the
        /// other half, that nothing overrides it locally.
        /// </para>
        /// <para>
        /// One ToolStrip is out of reach here: the notification area menu is built by
        /// <c>NotificationAreaIcon</c>, which is not a control and puts a live icon in the tray
        /// when constructed. <c>--selftest</c> covers it by reporting the renderer every strip
        /// actually resolves to at run time.
        /// </para>
        /// </remarks>
        [TestCaseSource(nameof(Containers))]
        public void NoToolStripPinsTheProfessionalRenderer(Type containerType)
        {
            ContainerControl container = null;
            try
            {
                container = (ContainerControl)Activator.CreateInstance(containerType);

                foreach (var strip in ToolStripsOf(container))
                {
                    Assert.That(strip.RenderMode, Is.Not.EqualTo(ToolStripRenderMode.Professional),
                                $"{containerType.Name}.{strip.Name} pins the Professional renderer, " +
                                "which has no dark variant, so it stays light when the rest of the " +
                                "application goes dark.");
                }
            }
            finally
            {
                container?.Dispose();
            }
        }

        /// <summary>
        /// The sweep above actually finds the menus, rather than passing because it found none.
        /// </summary>
        /// <remarks>
        /// A per-container test that silently sweeps an empty collection is indistinguishable from
        /// one that passes, which is exactly how the first version of it went wrong. This pins the
        /// three that are held in ways the control tree does not reach, so that a change to how
        /// they are stored fails here rather than turning the guard above into a no-op.
        /// <para>
        /// Two are out of reach and stay that way: the connection tab menu, because
        /// <c>ConnectionWindow</c> takes a connection in its constructor and so is not swept at
        /// all, and the notification area menu, because <c>NotificationAreaIcon</c> is not a
        /// control and puts a live icon in the tray when constructed. <c>--selftest</c> covers
        /// both by reporting the renderer every strip resolves to at run time.
        /// </para>
        /// </remarks>
        [TestCase(typeof(mRemoteUG.UI.Window.ErrorAndInfoWindow), "cMenMC")]
        [TestCase(typeof(mRemoteUG.UI.Window.ConnectionTreeWindow), "cMenTree")]
        public void TheToolStripSweepReachesMenusOutsideTheControlTree(Type containerType, string stripName)
        {
            ContainerControl container = null;
            try
            {
                container = (ContainerControl)Activator.CreateInstance(containerType);

                Assert.That(ToolStripsOf(container).Select(s => s.Name), Does.Contain(stripName),
                            $"{containerType.Name}.{stripName} is no longer found by the sweep, so " +
                            "NoToolStripPinsTheProfessionalRenderer is not checking it any more.");
            }
            finally
            {
                container?.Dispose();
            }
        }

        /// <summary>
        /// Every <see cref="UiTheme"/> maps to the colour mode it names.
        /// </summary>
        /// <remarks>
        /// The two enums are deliberately separate - the persisted one is ours so that a rename in
        /// the framework cannot reach <c>user.config</c>, and so that light mode is not written
        /// there as the word "Classic". This is the seam between them, and it is the one place a
        /// mapping mistake would silently give the user a theme they did not ask for.
        /// </remarks>
        [TestCase(UiTheme.System, SystemColorMode.System)]
        [TestCase(UiTheme.Light, SystemColorMode.Classic)]
        [TestCase(UiTheme.Dark, SystemColorMode.Dark)]
        public void EachThemeMapsToItsColourMode(UiTheme theme, SystemColorMode expected)
        {
            Assert.That(theme.ToColorMode(), Is.EqualTo(expected));
        }

        /// <summary>
        /// An unrecognised saved value follows the system rather than throwing.
        /// </summary>
        /// <remarks>
        /// user.config is a text file a user can edit, and a value written by a later version has
        /// to land somewhere. Following the system is both the default and the least surprising.
        /// </remarks>
        [Test]
        public void AnUnknownThemeFollowsTheSystem()
        {
            Assert.That(((UiTheme)999).ToColorMode(), Is.EqualTo(SystemColorMode.System));
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
