using mRemoteUG.Config.DataProviders;
using mRemoteUG.Tests.TestHelpers;

using NUnit.Framework;

namespace mRemoteUG.Tests.Config.DataProviders;

public class FileDataProviderWithRollingBackupTests
{
    // Rebuilt for every test in Setup: each test gets a file in a directory of its own.
    private FileDataProviderWithRollingBackup _dataProvider = null!;
    private string _testFilePath = null!;
    private string _testFileDirectory = null!;
    private string _testFileRollingBackup = null!;

    [SetUp]
    public void Setup()
    {
        _testFilePath = FileTestHelpers.NewTempFilePath();
        // NewTempFilePath returns a file inside a directory it has just created.
        _testFileDirectory = Path.GetDirectoryName(_testFilePath)!;
        _testFileRollingBackup = Path.GetFileName(_testFilePath) + ".*-*.backup";
        _dataProvider = new FileDataProviderWithRollingBackup(_testFilePath);
    }

    [TearDown]
    public void Teardown()
    {
        if (Directory.Exists(_testFileDirectory))
        {
            Directory.Delete(_testFileDirectory, true);
        }
    }

    [Test]
    public void RollingBackupCreatedIfRegularBackupExists()
    {
        for (int i = 0; i < 3; i++)
        {
            _dataProvider.Save("");
            Thread.Sleep(100);
        }

        string[] rollingBackupFiles = Directory.GetFiles(_testFileDirectory, _testFileRollingBackup);
        Assert.That(rollingBackupFiles.Length, Is.EqualTo(2));
    }

    [Test]
    public void NoRollingBackupCreatedIfRegularFileDoesntExists()
    {
        _dataProvider.Save("");
        string[] rollingBackupFiles = Directory.GetFiles(_testFileDirectory, _testFileRollingBackup);
        Assert.That(rollingBackupFiles.Length, Is.EqualTo(0));
    }

    [Test]
    public void SaveLeavesExactlyOneBackupHoldingTheOldContentAndNoTemporaryFile()
    {
        File.WriteAllText(_testFilePath, "old content");

        _dataProvider.Save("new content");

        string[] rollingBackupFiles = Directory.GetFiles(_testFileDirectory, _testFileRollingBackup);
        Assert.That(rollingBackupFiles, Has.Length.EqualTo(1));
        Assert.That(File.ReadAllText(rollingBackupFiles[0]), Is.EqualTo("old content"));
        Assert.That(File.ReadAllText(_testFilePath), Is.EqualTo("new content"));
        Assert.That(Directory.GetFiles(_testFileDirectory), Has.Length.EqualTo(2));
    }
}
