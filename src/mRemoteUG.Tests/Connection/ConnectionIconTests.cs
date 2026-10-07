using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using mRemoteUG.App;
using mRemoteUG.Connection;
using NUnit.Framework;


namespace mRemoteUG.Tests.Connection
{
    /// <summary>
    /// The connection icons live in the assembly manifest, not in a folder beside the exe.
    /// Nothing about that arrangement fails at build time, so it is pinned here.
    /// </summary>
    [TestFixture]
    public class ConnectionIconTests
    {
        [Test]
        public void IconsAreEmbeddedInTheAssembly()
        {
            Assert.That(ConnectionIcon.Icons, Is.Not.Empty);
        }

        [Test]
        public void IconsIncludeTheDefaultEveryNewConnectionAsksFor()
        {
            Assert.That(ConnectionIcon.Icons, Contains.Item(mRemoteUG.Settings.Default.ConDefaultIcon));
        }

        [TestCase("Windows")]
        [TestCase("Remote Desktop")]
        [TestCase("Domain Controller")]
        public void NamesTheIconPickerOffersResolve(string iconName)
        {
            // Names with spaces are the ones a manifest-name change would break first.
            Assert.That(ConnectionIcon.Icons, Contains.Item(iconName));
            Assert.That(ConnectionIcon.FromString(iconName), Is.Not.Null);
        }

        /// <summary>
        /// A name an older build wrote still resolves, without being offered again.
        /// </summary>
        /// <remarks>
        /// These two properties used to be one assertion, and curating the set is what separated
        /// them: "Anti Virus" is no longer something a user can pick, but a connections file that
        /// names it must still draw something. Asserting only that it resolves would let it creep
        /// back into the picker; asserting only that it is absent would let the healing rot.
        /// </remarks>
        [TestCase("Anti Virus")]
        [TestCase("Terminal Server")]
        [TestCase("Fax")]
        public void NamesCarriedInExistingConnectionFilesStillResolveWithoutBeingOffered(string iconName)
        {
            Assert.That(ConnectionIcon.Icons, Does.Not.Contain(iconName),
                        "a healed name is being offered in the picker as well");
            Assert.That(ConnectionIcon.FromString(iconName), Is.Not.Null,
                        "a connection saved with this name would lose its icon");
        }

        [Test]
        public void EveryLegacyNameHealsToSomethingThatIsOffered()
        {
            Assert.Multiple(() =>
            {
                foreach (var pair in ConnectionIcon.LegacyNames)
                {
                    Assert.That(ConnectionIcon.Icons, Contains.Item(pair.Value),
                                $"\"{pair.Key}\" heals to \"{pair.Value}\", which is not in the set");
                    Assert.That(ConnectionIcon.Heal(pair.Key), Is.EqualTo(pair.Value));
                }
            });
        }

        [Test]
        public void NoLegacyNameIsAlsoOfferedInItsOwnRight()
        {
            // One that was both would shadow itself: the table would rewrite a name that already
            // resolves, and the picker would offer a name that silently becomes another.
            foreach (var legacy in ConnectionIcon.LegacyNames.Keys)
                Assert.That(ConnectionIcon.Icons, Does.Not.Contain(legacy));
        }

        /// <summary>
        /// Every name the set carried before it was curated still resolves.
        /// </summary>
        /// <remarks>
        /// The list is written out rather than read from anywhere, because the thing it guards
        /// against is the set changing. It is what stops curation from being data loss for
        /// somebody whose connections file predates it.
        /// </remarks>
        [Test]
        public void EveryNameFromBeforeTheCurationStillDrawsSomething()
        {
            Assert.Multiple(() =>
            {
                foreach (var iconName in NamesShippedBeforeTheSetWasCurated)
                    Assert.That(ConnectionIcon.FromString(iconName), Is.Not.Null,
                                $"a connection saved as \"{iconName}\" would lose its icon");
            });
        }

        private static readonly string[] NamesShippedBeforeTheSetWasCurated =
        {
            "Anti Virus", "Backup", "Build Server", "Database", "Domain Controller", "ESX",
            "Fax", "File Server", "Finance", "Firewall", "Linux", "Log", "Mail Server", "PuTTY",
            "Remote Desktop", "Router", "SSH", "SharePoint", "Switch", "Tel", "Telnet",
            "Terminal Server", "Test Server", "Virtual Machine", "Web Server", "WiFi", "Windows",
            "Workstation", "mRemote", "mRemoteUG"
        };

        [Test]
        public void EveryListedIconResolves()
        {
            // The icon picker builds a menu item per name with no fallback of its own.
            var unresolved = ConnectionIcon.Icons.Where(n => ConnectionIcon.FromString(n) == null);
            Assert.That(unresolved, Is.Empty);
        }

        [Test]
        public void IconsAreSortedAndUnique()
        {
            Assert.That(ConnectionIcon.Icons, Is.Unique);
            Assert.That(ConnectionIcon.Icons, Is.Ordered.Using<string>(System.StringComparer.CurrentCultureIgnoreCase));
        }

        /// <summary>
        /// Asking for a larger icon returns a larger icon.
        /// </summary>
        /// <remarks>
        /// This asserted only non-null for as long as the artwork had a single 16x16 frame, so it
        /// passed against a size that had never once been produced - the size argument was
        /// documented at the time as changing nothing. It does now.
        /// </remarks>
        [Test]
        public void AnIconRequestedAtALargerSizeIsThatSize()
        {
            Assert.That(ConnectionIcon.FromString("Windows", 32).Size,
                        Is.EqualTo(new System.Drawing.Size(32, 32)));
        }

        [Test]
        public void ATabIconResolvesForTheOneCallerThatNeedsAnIcon()
        {
            Assert.That(ConnectionIcon.TabIconFor("Windows", 16), Is.Not.Null);
            Assert.That(ConnectionIcon.TabIconFor("Windows", 16),
                        Is.SameAs(ConnectionIcon.TabIconFor("Windows", 16)),
                        "each call made a new HICON, which Icon.FromHandle will never free");
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("no such icon")]
        public void UnresolvableNamesReturnNullRatherThanThrowing(string iconName)
        {
            Assert.That(ConnectionIcon.FromString(iconName), Is.Null);
        }

        [Test]
        public void ANameCarryingPathSeparatorsCannotReachTheFileSystem()
        {
            // Icon values come out of a connections file, which is not necessarily trusted.
            Assert.That(ConnectionIcon.FromString(@"..\..\mRemoteUG"), Is.Null);
        }

        [Test]
        public void TheBuildOutputShipsNoLooseIconsFolder()
        {
            // CopyToOutputDirectory never deletes, and the installer harvests this folder
            // wholesale - so a stale copy here would silently ship in the MSI.
            Assert.That(Directory.Exists(Path.Combine(BuildOutputPath(), "Icons")), Is.False);
        }


        /// <summary>
        /// A stored default icon this build does not carry falls back to the one it ships.
        /// </summary>
        /// <remarks>
        /// ConDefaultIcon is user-scoped, and startup calls Settings.Upgrade, which carries the
        /// previous version's value forward from its own user.config. Every profile that ever ran a
        /// build from before the product was renamed therefore still asks for an icon named after
        /// the old product - which is no longer embedded, so every new connection was given a name
        /// that resolves to nothing and --selftest failed on it. The name is resolved rather than
        /// trusted so such a profile heals itself instead of needing the setting cleared by hand.
        /// </remarks>
        [Test]
        public void AStoredDefaultIconThatIsNotEmbeddedFallsBackToTheShippedOne()
        {
            var stored = Settings.Default.ConDefaultIcon;
            try
            {
                Settings.Default.ConDefaultIcon = "mRemoteNG";

                Assert.That(ConnectionIcon.FromString(ConnectionIcon.DefaultIconName), Is.Not.Null,
                            $"the default icon resolved to \"{ConnectionIcon.DefaultIconName}\", " +
                            "which is not embedded");
            }
            finally
            {
                Settings.Default.ConDefaultIcon = stored;
            }
        }

        /// <summary>A stored icon this build does carry is left alone.</summary>
        [Test]
        public void AStoredDefaultIconThatIsEmbeddedIsUsedAsIs()
        {
            var stored = Settings.Default.ConDefaultIcon;
            try
            {
                var embedded = ConnectionIcon.Icons.First();
                Settings.Default.ConDefaultIcon = embedded;

                Assert.That(ConnectionIcon.DefaultIconName, Is.EqualTo(embedded));
            }
            finally
            {
                Settings.Default.ConDefaultIcon = stored;
            }
        }

        private static string BuildOutputPath([CallerFilePath] string sourceFilePath = "")
        {
            const string debugOrRelease =
#if DEBUG
                "Debug";
#else
                "Release";
#endif
            var here = Path.GetDirectoryName(sourceFilePath);
            return Path.GetFullPath(Path.Combine(here, "..", "..", "mRemoteUG", "bin", debugOrRelease));
        }
    }
}
