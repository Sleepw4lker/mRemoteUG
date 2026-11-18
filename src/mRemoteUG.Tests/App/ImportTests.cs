using mRemoteUG.App;
using mRemoteUG.Connection;
using mRemoteUG.Container;
using mRemoteUG.Messages;
using mRemoteUG.Tests.TestHelpers;

using NUnit.Framework;
// Aliased rather than imported: the product assembly has a Resources class in namespace
// mRemoteUG, and a member of an enclosing namespace is found before a file-level using, so
// inside mRemoteUG.Tests the bare name Resources binds to that one instead of this one.
using Fixtures = mRemoteUG.Tests.Properties.Resources;

namespace mRemoteUG.Tests.App;

public class ImportTests
{
    [Test]
    public void ErrorHandlerCalledWhenUnsupportedFileExtensionFound()
    {
        using (FileTestHelpers.DisposableTempFile(out string file, ".blah"))
        {
            var conService = new ConnectionsService();
            var container = new ContainerInfo();
            bool exceptionOccurred = false;

            Import.HeadlessFileImport(new[] { file }, container, conService, s => exceptionOccurred = true);

            Assert.That(exceptionOccurred);
        }
    }

    [Test]
    public void AnErrorInOneFileDoNotPreventOtherFilesFromProcessing()
    {
        using (FileTestHelpers.DisposableTempFile(out string badFile, ".blah"))
        using (FileTestHelpers.DisposableTempFile(out string xmlFile, ".xml"))
        {
            File.AppendAllText(xmlFile, Fixtures.confCons_v2_9);
            var conService = new ConnectionsService();
            var container = new ContainerInfo();
            int exceptionCount = 0;

            Import.HeadlessFileImport(new[] { badFile, xmlFile }, container, conService, s => exceptionCount++);

            Assert.That(exceptionCount, Is.EqualTo(1));
            Assert.That(container.Children, Has.One.Items);
        }
    }

    /// <summary>
    /// A file another program holds open is reported as a failed import, and the log names the
    /// reason it could not be read rather than an unsupported old format.
    /// </summary>
    [Test]
    public void ALockedFileIsReportedAsAFailedImportWithTheRealCause()
    {
        using (FileTestHelpers.DisposableTempFile(out string xmlFile, ".xml"))
        {
            File.AppendAllText(xmlFile, Fixtures.confCons_v2_9);
            var conService = new ConnectionsService();
            var container = new ContainerInfo();
            var failedFiles = new List<string>();
            using var refusals = new RefusalRecorder();
            using var recorder = new MessageRecorder();

            using (new FileStream(xmlFile, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                // Read from the system rather than written here: the text is localized.
                string expectedCause = Assert.Throws<IOException>(() => File.ReadAllText(xmlFile))!.Message;

                Import.HeadlessFileImport(new[] { xmlFile }, container, conService, failedFiles.Add);

                Assert.Multiple(() =>
                {
                    Assert.That(failedFiles, Is.EqualTo(new[] { xmlFile }));
                    Assert.That(container.Children, Is.Empty);
                    Assert.That(refusals.Headings, Is.Empty);
                    Assert.That(recorder.Matching(MessageClass.ErrorMsg, expectedCause), Has.Count.EqualTo(1));
                });
            }
        }
    }
}
