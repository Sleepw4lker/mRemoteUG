using System.IO;
using mRemoteUG.Tools;
using NUnit.Framework;

namespace mRemoteUG.Tests.Tools
{
    public class PuttyPathProviderTests
    {
        private string _originalCustomPuttyPath;
        private string _existingFile;

        [SetUp]
        public void Setup()
        {
            _originalCustomPuttyPath = mRemoteUG.Settings.Default.CustomPuttyPath;
            _existingFile = Path.GetTempFileName();
        }

        [TearDown]
        public void Teardown()
        {
            mRemoteUG.Settings.Default.CustomPuttyPath = _originalCustomPuttyPath;
            if (File.Exists(_existingFile))
                File.Delete(_existingFile);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void AnUnsetPathIsNotConfigured(string configured)
        {
            mRemoteUG.Settings.Default.CustomPuttyPath = configured;

            Assert.That(PuttyPathProvider.ConfiguredPath, Is.Empty);
        }

        [Test]
        public void TheConfiguredPathIsTrimmed()
        {
            mRemoteUG.Settings.Default.CustomPuttyPath = $"  {_existingFile}  ";

            Assert.That(PuttyPathProvider.ConfiguredPath, Is.EqualTo(_existingFile));
        }

        [Test]
        public void AnExistingConfiguredFileIsUsable()
        {
            mRemoteUG.Settings.Default.CustomPuttyPath = _existingFile;

            Assert.Multiple(() =>
            {
                Assert.That(PuttyPathProvider.IsUsable, Is.True);
                Assert.That(PuttyPathProvider.GetUnusableReason(), Is.Null);
                Assert.That(PuttyPathProvider.ResolvedPath, Is.EqualTo(_existingFile));
            });
        }

        [Test]
        public void AMissingConfiguredFileReportsWhyItCannotBeUsed()
        {
            var missing = Path.Combine(Path.GetTempPath(), "definitely-not-here-putty.exe");
            mRemoteUG.Settings.Default.CustomPuttyPath = missing;

            Assert.Multiple(() =>
            {
                Assert.That(PuttyPathProvider.IsUsable, Is.False);
                Assert.That(PuttyPathProvider.GetUnusableReason(), Does.Contain(missing));
            });
        }

        [Test]
        public void AMalformedPathIsRejectedRatherThanThrowing()
        {
            // Path.GetDirectoryName throws on invalid characters; callers must not see that.
            mRemoteUG.Settings.Default.CustomPuttyPath = "C:\\|not|a|path";

            Assert.Multiple(() =>
            {
                Assert.That(PuttyPathProvider.IsUsable, Is.False);
                Assert.That(PuttyPathProvider.ResolvedDirectory, Is.Null);
            });
        }

        [Test]
        public void ResolvedDirectoryIsTheFolderHoldingTheExecutable()
        {
            mRemoteUG.Settings.Default.CustomPuttyPath = _existingFile;

            Assert.That(PuttyPathProvider.ResolvedDirectory, Is.EqualTo(Path.GetDirectoryName(_existingFile)));
        }

        [Test]
        public void ResolvedDirectoryIsNullWhenNoPuttyIsAvailable()
        {
            mRemoteUG.Settings.Default.CustomPuttyPath = "";

            // Discovery may legitimately find a PuTTY on this machine; only assert the
            // contract that a directory is returned exactly when a path was resolved.
            var resolved = PuttyPathProvider.ResolvedPath;
            if (resolved.Length == 0)
                Assert.That(PuttyPathProvider.ResolvedDirectory, Is.Null);
            else
                Assert.That(PuttyPathProvider.ResolvedDirectory, Is.Not.Null);
        }
    }
}
