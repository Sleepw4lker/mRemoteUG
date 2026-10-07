using mRemoteUG.App.Info;
using NUnit.Framework;

namespace mRemoteUG.Tests.App
{
    /// <summary>
    /// The About dialog is the only place in the running application that says whose work this
    /// is. These assert the parts of it that must not quietly disappear.
    /// </summary>
    /// <remarks>
    /// Names are asserted rather than the whole string, so the wording stays free to change. A
    /// rename or a copyright edit that drops one of them fails here instead of shipping.
    /// </remarks>
    [TestFixture]
    public class AboutTextTests
    {
        [Test]
        public void TheContentSaysThisIsAForkAndWhoMaintainsIt()
        {
            Assert.Multiple(() =>
            {
                Assert.That(AboutText.Content, Does.Contain("fork of mRemoteNG"));
                Assert.That(AboutText.Content, Does.Contain("Uwe Gradenegger"));
            });
        }

        [Test]
        public void TheContentCarriesTheVersion()
        {
            Assert.That(AboutText.Content, Does.Contain(GeneralAppInfo.ApplicationVersion));
        }

        /// <summary>
        /// The copyright reaches the dialog from the assembly attribute, so this also covers the
        /// attribute still naming the people the code came from.
        /// </summary>
        [TestCase("mRemoteNG Dev Team")]
        [TestCase("Riley McArdle")]
        [TestCase("Felix Deimel")]
        public void TheContentStillCreditsTheOriginalAuthors(string author)
        {
            Assert.That(AboutText.Content, Does.Contain(author));
        }

        [Test]
        public void TheCreditsPointAtTheFileThatListsEveryone()
        {
            Assert.That(AboutText.Credits, Does.Contain("Credits.txt"));
        }

        [TestCase("PuTTY")]
        [TestCase("Serilog")]
        [TestCase("Fluent")]
        public void TheCreditsNameTheThirdPartyWorkThatShipsWithIt(string component)
        {
            Assert.That(AboutText.Credits, Does.Contain(component));
        }
    }
}
