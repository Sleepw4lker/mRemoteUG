using mRemoteUG.Connection;
using mRemoteUG.Messages;
using mRemoteUG.Security;
using mRemoteUG.Tests.Config.Connections;
using mRemoteUG.Tests.TestHelpers;
using mRemoteUG.Tree;

using NUnit.Framework;

namespace mRemoteUG.Tests.Connection;

public class ConnectionsServiceSaveTests
{
    /// <summary>The text of the warning a save skipped for want of a loaded file logs.</summary>
    internal const string SkippedSaveText = "no connections file is loaded";

    private static readonly bool[] s_shownThenLoggedThenShownAgain = [false, true, false];
    private static readonly bool[] s_loggedOnlyOnce = [true];

    // Rebuilt for every test in Setup: each test gets a file in a directory of its own.
    private string _connectionsFile = null!;
    private string _connectionsDirectory = null!;

    [SetUp]
    public void Setup()
    {
        _connectionsFile = FileTestHelpers.NewTempFilePath(".xml");
        // NewTempFilePath returns a file inside a directory it has just created.
        _connectionsDirectory = Path.GetDirectoryName(_connectionsFile)!;
    }

    [TearDown]
    public void Teardown()
    {
        FileTestHelpers.DeleteDirectory(_connectionsDirectory);
    }

    [Test]
    public void AFailedSaveIsReportedAsAFailureNotASuccess()
    {
        File.WriteAllText(_connectionsFile, "<the last good connections file/>");
        var service = new ConnectionsService();
        bool savedRaised = false;
        service.ConnectionsSaved += (sender, args) => savedRaised = true;
        using var recorder = new MessageRecorder();

        service.SaveConnections(
            XmlConnectionsSaverTests.ModelWithRootPassword("   "),
            new SaveFilter(),
            _connectionsFile,
            forceSave: true);

        Assert.That(savedRaised, Is.False);
        Assert.That(recorder.Matching(MessageClass.InformationMsg, "Successfully saved connections"), Is.Empty);
        Assert.That(recorder.Matching(MessageClass.ErrorMsg, _connectionsFile), Is.Not.Empty);
    }

    /// <summary>
    /// Autosave runs after every edit, so a failure that persists - a read-only or locked file,
    /// a full disk - used to put the same pop-up up after every edit. Every failure is still
    /// logged; only the first in a row is shown, until a save succeeds again.
    /// </summary>
    [Test]
    public void ASaveThatKeepsFailingIsShownOnceUntilASaveSucceeds()
    {
        var service = new ConnectionsService();
        ConnectionTreeModel model = XmlConnectionsSaverTests.ModelWithRootPassword("");
        File.WriteAllText(_connectionsFile, "<the last good connections file/>");
        using var recorder = new MessageRecorder();

        try
        {
            File.SetAttributes(_connectionsFile, FileAttributes.ReadOnly);
            Save(service, model);
            Save(service, model);

            File.SetAttributes(_connectionsFile, FileAttributes.Normal);
            Save(service, model);
            Assert.That(recorder.Matching(MessageClass.InformationMsg, "Successfully saved connections"),
                        Has.Count.EqualTo(1),
                        "The save in between has to succeed, or this test proves nothing about the reset.");

            File.SetAttributes(_connectionsFile, FileAttributes.ReadOnly);
            Save(service, model);
        }
        finally
        {
            // The backups the saver takes are copies of the read-only file, and inherit the
            // attribute; Teardown could not delete the directory with any of them left.
            foreach (string file in Directory.GetFiles(_connectionsDirectory))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }
        }

        IReadOnlyList<IMessage> failures = recorder.Matching(MessageClass.ErrorMsg, _connectionsFile);
        Assert.That(failures.Select(failure => failure.OnlyLog), Is.EqualTo(s_shownThenLoggedThenShownAgain));
    }

    /// <summary>
    /// A save skipped because no file is loaded leaves a trace in the log, once: autosave runs
    /// after every edit, and one line says all there is to say until a file is loaded.
    /// </summary>
    [Test]
    public void ASkippedSaveIsLoggedOnceAndOnlyInTheLog()
    {
        var service = new ConnectionsService();
        ConnectionTreeModel model = XmlConnectionsSaverTests.ModelWithRootPassword("");
        using var recorder = new MessageRecorder();

        service.SaveConnections(model, new SaveFilter(), _connectionsFile);
        service.SaveConnections(model, new SaveFilter(), _connectionsFile);

        IReadOnlyList<IMessage> warnings = recorder.Matching(MessageClass.WarningMsg, SkippedSaveText);
        Assert.Multiple(() =>
        {
            Assert.That(warnings.Select(warning => warning.OnlyLog), Is.EqualTo(s_loggedOnlyOnce));
            Assert.That(File.Exists(_connectionsFile), Is.False);
        });
    }

    private void Save(ConnectionsService service, ConnectionTreeModel model)
    {
        service.SaveConnections(model, new SaveFilter(), _connectionsFile, forceSave: true);
    }
}
