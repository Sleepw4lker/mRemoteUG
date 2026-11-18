using System.Runtime.InteropServices;
using System.Text;

using mRemoteUG.Config.DataProviders;
using mRemoteUG.Tests.TestHelpers;

using NUnit.Framework;

namespace mRemoteUG.Tests.Config.DataProviders;

public class FileDataProviderTests
{
    // Rebuilt for every test in Setup: each test gets a file in a directory of its own.
    private FileDataProvider _dataProvider = null!;
    private string _testFilePath = null!;
    private string _testFileDirectory = null!;

    [SetUp]
    public void Setup()
    {
        _testFilePath = FileTestHelpers.NewTempFilePath();
        // NewTempFilePath returns a file inside a directory it has just created.
        _testFileDirectory = Path.GetDirectoryName(_testFilePath)!;
        _dataProvider = new FileDataProvider(_testFilePath);
    }

    [TearDown]
    public void Teardown()
    {
        if (File.Exists(_testFilePath))
        {
            // A read-only file would stop the recursive delete below.
            File.SetAttributes(_testFilePath, FileAttributes.Normal);
        }

        if (Directory.Exists(_testFileDirectory))
        {
            Directory.Delete(_testFileDirectory, true);
        }
    }

    [Test]
    public void SavingToAReadOnlyFileThrows()
    {
        File.WriteAllText(_testFilePath, "unchanged");
        File.SetAttributes(_testFilePath, FileAttributes.ReadOnly);

        Assert.That(() => _dataProvider.Save("new content"), Throws.InstanceOf<UnauthorizedAccessException>());
        Assert.That(File.ReadAllText(_testFilePath), Is.EqualTo("unchanged"));
    }

    [Test]
    public void SetsFileContent()
    {
        Assert.That(File.Exists(_testFilePath), Is.False);
        string expectedFileContent = Guid.NewGuid().ToString();
        _dataProvider.Save(expectedFileContent);
        string fileContent = File.ReadAllText(_testFilePath);
        Assert.That(fileContent, Is.EqualTo(expectedFileContent));
    }

    [Test]
    public void LoadReturnsTheFilesContent()
    {
        File.WriteAllText(_testFilePath, "<Connections />");

        Assert.That(_dataProvider.Load(), Is.EqualTo("<Connections />"));
    }

    /// <summary>
    /// A missing file throws, naming its path, rather than loading as an empty file: the caller
    /// reports it, and an empty string would be read as a file of format version 0.
    /// </summary>
    [Test]
    public void LoadingFileThatDoesntExistThrowsNamingItsPath()
    {
        string missingFile = Path.Combine(_testFileDirectory, "missing.xml");
        var dataProvider = new FileDataProvider(missingFile);

        Assert.That(() => dataProvider.Load(),
                    Throws.InstanceOf<FileNotFoundException>()
                          .With.Property(nameof(FileNotFoundException.FileName)).EqualTo(missingFile));
    }

    /// <summary>
    /// A file another program holds open (a sync client, a share) throws the reason it cannot be
    /// read, instead of loading as an empty file.
    /// </summary>
    [Test]
    public void LoadingAFileHeldOpenByAnotherProgramThrows()
    {
        File.WriteAllText(_testFilePath, "<Connections />");
        using var lockingStream = new FileStream(_testFilePath, FileMode.Open, FileAccess.Read, FileShare.None);

        Assert.That(() => _dataProvider.Load(), Throws.InstanceOf<IOException>());
    }

    [Test]
    public void SaveCreatesDirectoriesThatDontExist()
    {
        string folder1 = Guid.NewGuid().ToString();
        string folder2 = Guid.NewGuid().ToString();
        string fileThatShouldExist = Path.Combine(_testFileDirectory, folder1, folder2, Path.GetRandomFileName());
        _dataProvider.FilePath = fileThatShouldExist;
        _dataProvider.Save("");
        Assert.That(File.Exists(fileThatShouldExist), Is.True);
    }

    [Test]
    public void SaveOverAnExistingFileLeavesTheNewContentAndNoTemporaryFile()
    {
        File.WriteAllText(_testFilePath, "old content");

        _dataProvider.Save("new content");

        Assert.That(File.ReadAllText(_testFilePath), Is.EqualTo("new content"));
        Assert.That(Directory.GetFiles(_testFileDirectory), Is.EqualTo(new[] { _testFilePath }));
    }

    [Test]
    public void FirstSaveWithNoExistingFileWritesItAndLeavesNoTemporaryFile()
    {
        Assert.That(File.Exists(_testFilePath), Is.False);

        _dataProvider.Save("first content");

        Assert.That(File.ReadAllText(_testFilePath), Is.EqualTo("first content"));
        Assert.That(Directory.GetFiles(_testFileDirectory), Is.EqualTo(new[] { _testFilePath }));
    }

    [Test]
    public void SaveThatCannotReplaceTheFileLeavesItByteForByteUnchangedAndNoTemporaryFile()
    {
        byte[] original = Encoding.UTF8.GetBytes("<Connections>€</Connections>\r\n");
        File.WriteAllBytes(_testFilePath, original);
        File.SetAttributes(_testFilePath, FileAttributes.ReadOnly);

        Assert.That(() => _dataProvider.Save("new content"), Throws.InstanceOf<UnauthorizedAccessException>());

        Assert.That(File.ReadAllBytes(_testFilePath), Is.EqualTo(original));
        Assert.That(Directory.GetFiles(_testFileDirectory), Is.EqualTo(new[] { _testFilePath }));
    }

    [Test]
    public void SaveReplacesTheFileInsteadOfRewritingItInPlace()
    {
        // A hard link shares the file's data. Rewriting the file in place changes what the link
        // reads; writing a new file and renaming it over the old one leaves the link on the old data.
        File.WriteAllText(_testFilePath, "old content");
        string link = Path.Combine(_testFileDirectory, "hard-link-to-the-original");
        Assert.That(CreateHardLink(link, _testFilePath, IntPtr.Zero), Is.True,
            $"CreateHardLink failed with error {Marshal.GetLastWin32Error()}");

        _dataProvider.Save("new content");

        Assert.That(File.ReadAllText(_testFilePath), Is.EqualTo("new content"));
        Assert.That(File.ReadAllText(link), Is.EqualTo("old content"));
    }

    [Test]
    public void SaveWritesUtf8WithoutAByteOrderMark()
    {
        _dataProvider.Save("€");

        Assert.That(File.ReadAllBytes(_testFilePath), Is.EqualTo(new byte[] { 0xE2, 0x82, 0xAC }));
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLink(string fileName, string existingFileName, IntPtr securityAttributes);
}
