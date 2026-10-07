using System.IO;
using mRemoteUG.App;
using mRemoteUG.Connection;
using mRemoteUG.Container;
// Aliased rather than imported: the product assembly has a Resources class in namespace
// mRemoteUG, and a member of an enclosing namespace is found before a file-level using, so
// inside mRemoteUG.Tests the bare name Resources binds to that one instead of this one.
using Fixtures = mRemoteUG.Tests.Properties.Resources;
using mRemoteUG.Tests.TestHelpers;
using NUnit.Framework;

namespace mRemoteUG.Tests.App
{
	public class ImportTests
	{
		[Test]
		public void ErrorHandlerCalledWhenUnsupportedFileExtensionFound()
		{
			using (FileTestHelpers.DisposableTempFile(out var file, ".blah"))
			{
				var conService = new ConnectionsService();
				var container = new ContainerInfo();
				var exceptionOccurred = false;

				Import.HeadlessFileImport(new []{file}, container, conService, s => exceptionOccurred = true);

				Assert.That(exceptionOccurred);
			}
		}

		[Test]
		public void AnErrorInOneFileDoNotPreventOtherFilesFromProcessing()
		{
			using (FileTestHelpers.DisposableTempFile(out var badFile, ".blah"))
			using (FileTestHelpers.DisposableTempFile(out var xmlFile, ".xml"))
			{
				File.AppendAllText(xmlFile, Fixtures.confCons_v2_9);
				var conService = new ConnectionsService();
				var container = new ContainerInfo();
				var exceptionCount = 0;

				Import.HeadlessFileImport(new[] { badFile, xmlFile }, container, conService, s => exceptionCount++);

				Assert.That(exceptionCount, Is.EqualTo(1));
				Assert.That(container.Children, Has.One.Items);
			}
		}
	}
}
