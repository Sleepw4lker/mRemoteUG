using System.Security;

using mRemoteUG.App;
using mRemoteUG.Config.Import;
using mRemoteUG.Config.Serializers.Xml;
using mRemoteUG.Connection;
using mRemoteUG.Container;
using mRemoteUG.Messages;
using mRemoteUG.Security;
using mRemoteUG.Security.SymmetricEncryption;
using mRemoteUG.Tests.TestHelpers;
using mRemoteUG.Tree;
using mRemoteUG.Tree.Root;

using NUnit.Framework;

namespace mRemoteUG.Tests.Config.Import;

public class MRemoteNGXmlImporterTests
{
    private const string FilePassword = "the file's own password";
    private const string ConnectionPassword = "con1's password";

    private static readonly string[] s_folderCon1Arguments = { "folder", "con1" };

    // Rebuilt for every test in Setup: each test imports from a directory of its own, and
    // _requestor records the prompts of one test only.
    private string _tempDirectory = null!;
    private string _fileName = null!;
    private string _protectedFileName = null!;
    private FakePasswordRequestor _requestor = null!;

    [SetUp]
    public void Setup()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "mRemoteUG.Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
        _fileName = Path.Combine(_tempDirectory, "Imported hosts.xml");
        File.WriteAllText(_fileName, SerializedFile());
        _protectedFileName = Path.Combine(_tempDirectory, "Protected hosts.xml");
        File.WriteAllText(_protectedFileName, SerializedFile(FilePassword));
        _requestor = new FakePasswordRequestor(answer: null);
    }

    [TearDown]
    public void Teardown()
    {
        FileTestHelpers.DeleteDirectory(_tempDirectory);
    }

    [Test]
    public void TheFileIsImportedAsOneContainerNamedAfterIt()
    {
        var destination = new ContainerInfo { Name = "destination" };

        new MRemoteNGXmlImporter(_requestor.Request).Import(_fileName, destination);

        Assert.Multiple(() =>
        {
            Assert.That(destination.Children, Has.Count.EqualTo(1));
            Assert.That(destination.Children.FirstOrDefault()?.Name, Is.EqualTo("Imported hosts"));
        });
    }

    [Test]
    public void TheImportedContainerHoldsTheFilesTopLevelNodes()
    {
        var destination = new ContainerInfo { Name = "destination" };

        new MRemoteNGXmlImporter(_requestor.Request).Import(_fileName, destination);

        var imported = destination.Children.FirstOrDefault() as ContainerInfo;
        Assert.That(imported?.Children.Select(node => node.Name), Is.EqualTo(s_folderCon1Arguments));
    }

    [Test]
    public void AnExistingFileIsImportedWithoutAnError()
    {
        using var recorder = new MessageRecorder();

        new MRemoteNGXmlImporter(_requestor.Request).Import(_fileName, new ContainerInfo());

        Assert.That(recorder.Messages.Where(message => message.Class == MessageClass.ErrorMsg), Is.Empty);
    }

    [Test]
    [CancelAfter(5000)]
    public void AFileThatDoesNotExistIsReportedOnceAndNothingIsImported()
    {
        string missingFile = Path.Combine(_tempDirectory, "Missing hosts.xml");
        var destination = new ContainerInfo { Name = "destination" };
        using var recorder = new MessageRecorder();

        new MRemoteNGXmlImporter(_requestor.Request).Import(missingFile, destination);

        Assert.Multiple(() =>
        {
            Assert.That(
                recorder.Messages.Where(message => message.Class == MessageClass.ErrorMsg),
                Has.Exactly(1).Matches<IMessage>(message => message.Text.Contains(missingFile)));
            Assert.That(recorder.Messages.Count(message => message.Class == MessageClass.ErrorMsg), Is.EqualTo(1));
            Assert.That(destination.Children, Is.Empty);
        });
    }

    /// <summary>
    /// A file another program holds open fails the import with the reason it cannot be read,
    /// not as an old format whose passwords cannot be recovered.
    /// </summary>
    [Test]
    public void ALockedFileFailsWithTheRealCauseAndIsNotRefusedAsAnOldFormat()
    {
        var destination = new ContainerInfo { Name = "destination" };
        using var refusals = new RefusalRecorder();
        using var lockingStream = new FileStream(_fileName, FileMode.Open, FileAccess.Read, FileShare.None);

        Assert.That(() => new MRemoteNGXmlImporter(_requestor.Request).Import(_fileName, destination),
                    Throws.InstanceOf<IOException>());

        Assert.Multiple(() =>
        {
            Assert.That(refusals.Headings, Is.Empty);
            Assert.That(destination.Children, Is.Empty);
        });
    }

    /// <summary>An import adds to the open connections file; it does not count as loading one.</summary>
    [Test]
    public void AnImportDoesNotMarkTheConnectionsFileLoaded()
    {
        bool wasLoaded = Runtime.ConnectionsService.IsConnectionsFileLoaded;
        try
        {
            Runtime.ConnectionsService.IsConnectionsFileLoaded = false;

            new MRemoteNGXmlImporter(_requestor.Request).Import(_fileName, new ContainerInfo());

            Assert.That(Runtime.ConnectionsService.IsConnectionsFileLoaded, Is.False);
        }
        finally
        {
            Runtime.ConnectionsService.IsConnectionsFileLoaded = wasLoaded;
        }
    }

    /// <summary>
    /// A failed import changes nothing about the open connections file, so it is still saved.
    /// It used to clear the loaded mark, and every save after it was skipped without a word
    /// until another file was loaded.
    /// </summary>
    [Test]
    public void AFailedImportLeavesTheFileLoadedAndItIsStillSaved()
    {
        string notAConnectionsFile = Path.Combine(_tempDirectory, "Not connections.xml");
        File.WriteAllText(notAConnectionsFile, "this is not XML");
        string openFile = Path.Combine(_tempDirectory, "Open connections.xml");
        ConnectionsService service = Runtime.ConnectionsService;
        bool wasLoaded = service.IsConnectionsFileLoaded;
        string? previousFileName = service.ConnectionFileName;
        try
        {
            service.IsConnectionsFileLoaded = true;

            Assert.Catch(() => new MRemoteNGXmlImporter(_requestor.Request).Import(
                notAConnectionsFile, new ContainerInfo()));
            Assert.That(service.IsConnectionsFileLoaded, Is.True);

            var model = new ConnectionTreeModel();
            model.AddRootNode(new RootNodeInfo(RootNodeType.Connection));
            service.SaveConnections(model, new SaveFilter(), openFile);

            Assert.That(File.Exists(openFile), Is.True);
        }
        finally
        {
            service.IsConnectionsFileLoaded = wasLoaded;
            service.ConnectionFileName = previousFileName;
        }
    }

    /// <summary>The password is really used: the connection's own password decrypts with it.</summary>
    [Test]
    public void AFileWithItsOwnPasswordIsImportedWithTheRightAnswer()
    {
        var requestor = new FakePasswordRequestor(answer: FilePassword);
        var destination = new ContainerInfo { Name = "destination" };

        new MRemoteNGXmlImporter(requestor.Request).Import(_protectedFileName, destination);

        var imported = destination.Children.FirstOrDefault() as ContainerInfo;
        Assert.Multiple(() =>
        {
            Assert.That(imported?.Children.Select(node => node.Name), Is.EqualTo(s_folderCon1Arguments));
            Assert.That(imported?.Children.LastOrDefault()?.Password, Is.EqualTo(ConnectionPassword));
            Assert.That(requestor.AskedFor, Has.All.EqualTo(_protectedFileName));
        });
    }

    [Test]
    public void CancellingThePromptImportsNothingAndSaysSo()
    {
        var destination = new ContainerInfo { Name = "destination" };
        using var recorder = new MessageRecorder();

        Assert.DoesNotThrow(
            () => new MRemoteNGXmlImporter(_requestor.Request).Import(_protectedFileName, destination));

        Assert.Multiple(() =>
        {
            Assert.That(destination.Children, Is.Empty);
            Assert.That(
                recorder.Matching(MessageClass.InformationMsg, $"Import of {_protectedFileName} cancelled"),
                Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void AWrongAnswerAtEveryPromptFailsTheImport()
    {
        var requestor = new FakePasswordRequestor(answer: "not the file's password");
        var destination = new ContainerInfo { Name = "destination" };

        EncryptionException? exception = Assert.Throws<EncryptionException>(
            () => new MRemoteNGXmlImporter(requestor.Request).Import(_protectedFileName, destination));

        Assert.Multiple(() =>
        {
            Assert.That(exception?.Message, Does.Contain(_protectedFileName));
            Assert.That(destination.Children, Is.Empty);
        });
    }

    [Test]
    public void AnUnprotectedFileNeverAsksForAPassword()
    {
        new MRemoteNGXmlImporter(_requestor.Request).Import(_fileName, new ContainerInfo());

        Assert.That(_requestor.AskedFor, Is.Empty);
    }

    /// <summary>
    /// A file holding a folder with one connection in it, then a connection of its own.
    /// </summary>
    /// <param name="filePassword">The password the file is protected with; empty for the default.</param>
    private static string SerializedFile(string filePassword = "")
    {
        var rootNode = new RootNodeInfo(RootNodeType.Connection) { PasswordString = filePassword };
        var folder = new ContainerInfo { Name = "folder" };
        folder.AddChild(new ConnectionInfo { Name = "con0" });
        rootNode.AddChild(folder);
        rootNode.AddChild(new ConnectionInfo { Name = "con1", Password = ConnectionPassword });
        var model = new ConnectionTreeModel();
        model.AddRootNode(rootNode);

        ICryptographyProvider cryptographyProvider = new AeadCryptographyProvider();
        var nodeSerializer = new XmlConnectionNodeSerializer(
            cryptographyProvider, rootNode.PasswordString.ConvertToSecureString(), new SaveFilter());
        return new XmlConnectionsSerializer(cryptographyProvider, nodeSerializer).Serialize(model);
    }

    /// <summary>
    /// Gives the same answer at every prompt, null being the user cancelling, and records the file
    /// each prompt was for. The tests check only whether it was asked, never how often, because
    /// the number of attempts is the authenticator's business.
    /// </summary>
    private sealed class FakePasswordRequestor
    {
        private readonly string? _answer;
        private readonly List<string> _askedFor = new List<string>();

        public FakePasswordRequestor(string? answer)
        {
            _answer = answer;
        }

        public IReadOnlyList<string> AskedFor => _askedFor;

        public SecureString? Request(string fileName)
        {
            _askedFor.Add(fileName);
            return _answer?.ConvertToSecureString();
        }
    }
}
