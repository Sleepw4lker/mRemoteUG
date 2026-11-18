using System.Globalization;

using mRemoteUG.Config.DataProviders;
using mRemoteUG.Tests.TestHelpers;

using NUnit.Framework;

namespace mRemoteUG.Tests.Config.DataProviders;

public class FileBackupCreatorTests
{
    // Rebuilt for every test in Setup: each test gets a file in a directory of its own.
    private string _testFilePath = null!;
    private string _testFilePathBackup = null!;
    private string _testFileDirectory = null!;
    private string _testFileRollingBackup = null!;

    [SetUp]
    public void Setup()
    {
        _testFilePath = FileTestHelpers.NewTempFilePath();
        // NewTempFilePath returns a file inside a directory it has just created.
        _testFileDirectory = Path.GetDirectoryName(_testFilePath)!;
        _testFileRollingBackup = Path.GetFileName(_testFilePath) + ".*-*.backup";
        _testFilePathBackup = _testFilePath + ".backup";
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
    public void BackupCreatedWhenFileAlreadyExists()
    {
        File.WriteAllText(_testFilePath, "");
        FileBackupCreator.CreateBackupFile(_testFilePath);
        string[] rollingBackupFiles = Directory.GetFiles(_testFileDirectory, _testFileRollingBackup);
        Assert.That(rollingBackupFiles.Length, Is.EqualTo(1));
    }

    [Test]
    public void BackupNotCreatedIfFileDidntAlreadyExist()
    {
        FileBackupCreator.CreateBackupFile(_testFilePath);
        bool backupFileExists = File.Exists(_testFilePathBackup);
        Assert.That(backupFileExists, Is.False);
    }

    [Test]
    public void BackupNotCreatedWhenFeatureDisabled()
    {
        bool originalEnabled = Settings.Default.BackupFileEnabled;
        try
        {
            Settings.Default.BackupFileEnabled = false;
            File.WriteAllText(_testFilePath, "");
            FileBackupCreator.CreateBackupFile(_testFilePath);
            string[] rollingBackupFiles = Directory.GetFiles(_testFileDirectory, _testFileRollingBackup);
            Assert.That(rollingBackupFiles.Length, Is.EqualTo(0));
        }
        finally
        {
            Settings.Default.BackupFileEnabled = originalEnabled;
        }
    }

    // Under these cultures the current calendar is not Gregorian: Thai Buddhist, Um Al-Qura and
    // Persian. The name must still carry the Gregorian date the backup was taken (#99).
    [TestCase("th-TH")]
    [TestCase("ar-SA")]
    [TestCase("fa-IR")]
    public void BackupNameUsesTheGregorianCalendarWhateverTheCurrentCulture(string cultureName)
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(cultureName);
            Assert.That(CultureInfo.CurrentCulture.Calendar, Is.Not.InstanceOf<GregorianCalendar>());
            File.WriteAllText(_testFilePath, "");
            DateTime before = DateTime.Now;
            FileBackupCreator.CreateBackupFile(_testFilePath);
            DateTime after = DateTime.Now;

            string backup = Path.GetFileName(Directory.GetFiles(_testFileDirectory, _testFileRollingBackup).Single());
            string prefix = Path.GetFileName(_testFilePath) + ".";
            const string suffix = ".backup";
            string stamp = backup.Substring(prefix.Length, backup.Length - prefix.Length - suffix.Length);
            var taken = DateTime.ParseExact(stamp, "yyyyMMdd-HHmmssffff", CultureInfo.InvariantCulture);

            // The name keeps tenths of a millisecond, so it can read up to that much before 'before'.
            Assert.That(taken, Is.InRange(before.AddMilliseconds(-1), after));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }
}
