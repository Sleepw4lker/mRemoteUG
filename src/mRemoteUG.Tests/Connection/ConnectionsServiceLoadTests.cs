using mRemoteUG.Config.Putty;
using mRemoteUG.Connection;
using mRemoteUG.Messages;
using mRemoteUG.Security;
using mRemoteUG.Tests.TestHelpers;
using mRemoteUG.Tree;
using mRemoteUG.Tree.Root;

using NUnit.Framework;

namespace mRemoteUG.Tests.Connection;

/// <summary>
/// The loaded mark, IsConnectionsFileLoaded, has one owner: LoadConnections sets it when a load
/// succeeds, and nothing else moves it. Every save without forceSave is skipped while it is
/// false, so a mark cleared by mistake silently loses every later edit.
/// </summary>
public class ConnectionsServiceLoadTests
{
    // Snapshots taken by Setup and put back by Teardown: a load writes both settings.
    private bool _loadFromCustomLocation;
    private string _customPath = null!;
    private string _openFile = null!;
    private string _directory = null!;

    [SetUp]
    public void Setup()
    {
        _loadFromCustomLocation = Settings.Default.LoadConsFromCustomLocation;
        _customPath = Settings.Default.CustomConsPath;
        _openFile = FileTestHelpers.NewTempFilePath(".xml");
        // NewTempFilePath returns a file inside a directory it has just created.
        _directory = Path.GetDirectoryName(_openFile)!;
    }

    [TearDown]
    public void Teardown()
    {
        Settings.Default.LoadConsFromCustomLocation = _loadFromCustomLocation;
        Settings.Default.CustomConsPath = _customPath;
        FileTestHelpers.DeleteDirectory(_directory);
    }

    [Test]
    public void ASuccessfulLoadMarksTheFileLoaded()
    {
        ConnectionsService service = NewService();
        service.SaveConnections(EmptyModel(), new SaveFilter(), _openFile, forceSave: true);
        Assert.That(service.IsConnectionsFileLoaded, Is.False, "Saving alone must not mark it loaded.");

        service.LoadConnections(false, _openFile);

        Assert.Multiple(() =>
        {
            Assert.That(service.IsConnectionsFileLoaded, Is.True);
            Assert.That(service.ConnectionFileName, Is.EqualTo(_openFile));
        });
    }

    /// <summary>
    /// File > Open of another file that fails, then Cancel on the dialog shown again: the old
    /// model stays in use, so it has to stay saveable.
    /// </summary>
    [Test]
    public void AFailedLoadOfAnotherFileLeavesTheOpenFileLoadedAndInUse()
    {
        ConnectionsService service = NewService();
        service.NewConnectionsFile(_openFile);
        ConnectionTreeModel? openModel = service.ConnectionTreeModel;
        Assume.That(openModel, Is.Not.Null);
        string brokenFile = Path.Combine(_directory, "broken.xml");
        File.WriteAllText(brokenFile, "this is not XML");

        Assert.Catch(() => service.LoadConnections(false, brokenFile));

        Assert.Multiple(() =>
        {
            Assert.That(service.IsConnectionsFileLoaded, Is.True);
            Assert.That(service.ConnectionTreeModel, Is.SameAs(openModel));
            Assert.That(service.ConnectionFileName, Is.EqualTo(_openFile));
        });
    }

    /// <summary>
    /// A file another program holds open fails with the reason it cannot be read, which the
    /// caller shows, and is not refused as an old format whose passwords cannot be recovered.
    /// </summary>
    [Test]
    public void ALockedFileFailsWithTheRealCauseAndIsNotRefusedAsAnOldFormat()
    {
        ConnectionsService service = NewService();
        service.SaveConnections(EmptyModel(), new SaveFilter(), _openFile, forceSave: true);
        using var refusals = new RefusalRecorder();
        using var lockingStream = new FileStream(_openFile, FileMode.Open, FileAccess.Read, FileShare.None);

        Assert.That(() => service.LoadConnections(false, _openFile), Throws.InstanceOf<IOException>());

        Assert.Multiple(() =>
        {
            Assert.That(refusals.Headings, Is.Empty);
            Assert.That(service.IsConnectionsFileLoaded, Is.False);
        });
    }

    /// <summary>
    /// The skipped-save warning is logged once per stretch without a file, so it starts again
    /// once a file has been loaded.
    /// </summary>
    [Test]
    public void ASkippedSaveIsLoggedAgainAfterAFileHasBeenLoaded()
    {
        ConnectionsService service = NewService();
        string target = Path.Combine(_directory, "target.xml");
        using var recorder = new MessageRecorder();

        service.SaveConnections(EmptyModel(), new SaveFilter(), target);
        service.NewConnectionsFile(_openFile);
        service.IsConnectionsFileLoaded = false;
        service.SaveConnections(EmptyModel(), new SaveFilter(), target);

        Assert.Multiple(() =>
        {
            Assert.That(recorder.Matching(MessageClass.WarningMsg, ConnectionsServiceSaveTests.SkippedSaveText),
                        Has.Count.EqualTo(2));
            Assert.That(File.Exists(target), Is.False);
        });
    }

    /// <summary>A service of its own, so the process-wide Runtime.ConnectionsService is untouched.</summary>
    private static ConnectionsService NewService()
    {
        return new ConnectionsService(new PuttySessionsManager());
    }

    private static ConnectionTreeModel EmptyModel()
    {
        var model = new ConnectionTreeModel();
        model.AddRootNode(new RootNodeInfo(RootNodeType.Connection));
        return model;
    }
}
