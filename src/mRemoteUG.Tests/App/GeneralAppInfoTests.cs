using mRemoteUG.App.Info;
using NUnit.Framework;

namespace mRemoteUG.Tests.App
{
    /// <summary>
    /// The two shapes of the version string, and the rule that separates them.
    /// </summary>
    /// <remarks>
    /// The version reaches the application as the assembly's informational version, which since
    /// the move to git-derived versioning carries build metadata after a '+' - the commit the
    /// build came from, and whether the tree was dirty. That is exactly what a log read off
    /// another machine needs and exactly what the About dialog should not show, so there are two
    /// accessors and this fixture pins which is which.
    /// </remarks>
    [TestFixture]
    public class GeneralAppInfoTests
    {
        [TestCase("2.1.1.45+g1a2b3c4d", ExpectedResult = "2.1.1.45")]
        [TestCase("2.1.1.45+g1a2b3c4d.dirty", ExpectedResult = "2.1.1.45")]
        [TestCase("2.1.0.0", ExpectedResult = "2.1.0.0")]
        [TestCase("", ExpectedResult = "")]
        public string TrimBuildMetadataCutsAtTheFirstPlus(string version)
        {
            return GeneralAppInfo.TrimBuildMetadata(version);
        }

        /// <summary>
        /// A '+' with nothing before it would otherwise leave the dialog saying "Version ".
        /// </summary>
        [Test]
        public void TrimBuildMetadataLeavesNothingWhenThereIsNothingBeforeThePlus()
        {
            Assert.That(GeneralAppInfo.TrimBuildMetadata("+g1a2b3c4d"), Is.Empty);
        }

        [Test]
        public void TrimBuildMetadataToleratesNull()
        {
            Assert.That(GeneralAppInfo.TrimBuildMetadata(null), Is.Null);
        }

        /// <summary>
        /// The dialog gets the trimmed one.
        /// </summary>
        [Test]
        public void TheApplicationVersionCarriesNoBuildMetadata()
        {
            Assert.That(GeneralAppInfo.ApplicationVersion, Does.Not.Contain("+"));
        }

        /// <summary>
        /// The log gets the whole thing. This is the half that has to survive, because it is the
        /// only way to tell which commit a binary on another machine was built from.
        /// </summary>
        [Test]
        public void TheFullApplicationVersionNamesTheCommit()
        {
            Assert.That(GeneralAppInfo.ApplicationVersionFull, Does.Contain("+g"),
                        "the informational version no longer carries the commit it was built " +
                        "from, so a log read off another machine cannot identify the build");
        }

        [Test]
        public void TheTrimmedVersionIsThePrefixOfTheFullOne()
        {
            Assert.That(GeneralAppInfo.ApplicationVersionFull,
                        Does.StartWith(GeneralAppInfo.ApplicationVersion));
        }
    }
}
