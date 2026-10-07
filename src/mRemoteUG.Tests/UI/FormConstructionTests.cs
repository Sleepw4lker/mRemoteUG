using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using mRemoteUG.UI.Forms;
using NUnit.Framework;

namespace mRemoteUG.Tests.UI
{
    /// <summary>
    /// Constructs every Form and UserControl in mRemoteUG and forces its window handle.
    /// </summary>
    /// <remarks>
    /// This runs InitializeComponent and every .resx load in the application, which is the part
    /// of the .NET 10 migration that fails silently rather than loudly: a renamed manifest
    /// resource, a broken ResXFileRef path, or a resource type that no longer deserialises
    /// produces a MissingManifestResourceException the first time a window opens, and nothing
    /// earlier in the build says so.
    ///
    /// It matters here because the application cannot be run on the development machine, so
    /// without this the first evidence would come from a machine that can only report back a log.
    /// </remarks>
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class FormConstructionTests
    {
        /// <summary>
        /// Types excluded deliberately, so that adding one is a decision rather than an accident.
        /// </summary>
        private static readonly Dictionary<Type, string> Excluded = new Dictionary<Type, string>
        {
            [typeof(FrmMain)] = "Singleton that builds the entire application shell, loads " +
                                "connections and starts the layout; constructing it here would " +
                                "run most of the program rather than test a form."
        };

        private static IEnumerable<Type> ConstructibleControls()
        {
            return typeof(FrmMain).Assembly
                                  .GetTypes()
                                  .Where(t => typeof(Control).IsAssignableFrom(t))
                                  .Where(t => !t.IsAbstract && !t.IsGenericTypeDefinition)
                                  .Where(t => t.GetConstructor(BindingFlags.Public | BindingFlags.Instance,
                                                               null, Type.EmptyTypes, null) != null)
                                  .Where(t => !Excluded.ContainsKey(t))
                                  .OrderBy(t => t.FullName);
        }

        [TestCaseSource(nameof(ConstructibleControls))]
        public void ControlCanBeConstructedAndItsHandleCreated(Type controlType)
        {
            Control control = null;
            try
            {
                control = (Control)Activator.CreateInstance(controlType);

                // Forcing the handle runs the resource loading that InitializeComponent defers.
                control.CreateControl();
                var _ = control.Handle;
            }
            catch (Exception ex)
            {
                // TargetInvocationException hides the useful message behind reflection.
                var actual = ex is TargetInvocationException tie && tie.InnerException != null
                    ? tie.InnerException
                    : ex;
                Assert.Fail($"{controlType.FullName} could not be constructed: " +
                            $"{actual.GetType().Name}: {actual.Message}{Environment.NewLine}{actual.StackTrace}");
            }
            finally
            {
                control?.Dispose();
            }
        }

        [Test]
        public void TheExclusionListStaysHonest()
        {
            // A stale exclusion would quietly shrink this suite's coverage.
            foreach (var excluded in Excluded.Keys)
                Assert.That(typeof(FrmMain).Assembly.GetTypes(), Does.Contain(excluded),
                            $"{excluded.FullName} is excluded but no longer exists.");
        }

        [Test]
        public void TheSuiteActuallyCoversSomething()
        {
            // Guards against a reflection filter that silently matches nothing.
            Assert.That(ConstructibleControls().Count(), Is.GreaterThan(10));
        }
    }
}
