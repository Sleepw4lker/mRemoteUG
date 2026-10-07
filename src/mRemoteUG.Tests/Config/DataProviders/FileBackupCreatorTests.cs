using System.IO;
using mRemoteUG.Config.DataProviders;
using mRemoteUG.Tests.TestHelpers;
using NUnit.Framework;

namespace mRemoteUG.Tests.Config.DataProviders
{
	public class FileBackupCreatorTests
    {
        private FileBackupCreator _fileBackupCreator;
        private string _testFilePath;
        private string _testFilePathBackup;
        private string _testFileDirectory;
        private string _testFileRollingBackup;

        [SetUp]
        public void Setup()
        {
            _testFilePath = FileTestHelpers.NewTempFilePath();
            _testFileDirectory = Path.GetDirectoryName(_testFilePath);
            _testFileRollingBackup = Path.GetFileName(_testFilePath) + ".*-*.backup";
            _testFilePathBackup = _testFilePath + ".backup";
            _fileBackupCreator = new FileBackupCreator();
        }

        [TearDown]
        public void Teardown()
        {
			if (Directory.Exists(_testFileDirectory))
				Directory.Delete(_testFileDirectory, true);
		}

        [Test]
        public void BackupCreatedWhenFileAlreadyExists()
        {
            File.WriteAllText(_testFilePath, "");
            _fileBackupCreator.CreateBackupFile(_testFilePath);
            var rollingBackupFiles = Directory.GetFiles(_testFileDirectory, _testFileRollingBackup);
            Assert.That(rollingBackupFiles.Length, Is.EqualTo(1));
        }

        [Test]
        public void BackupNotCreatedIfFileDidntAlreadyExist()
        {
            _fileBackupCreator.CreateBackupFile(_testFilePath);
            var backupFileExists = File.Exists(_testFilePathBackup);
            Assert.That(backupFileExists, Is.False);
        }

        [Test]
        public void BackupNotCreatedWhenFeatureDisabled()
        {
            var originalEnabled = Settings.Default.BackupFileEnabled;
            try
            {
                Settings.Default.BackupFileEnabled = false;
                File.WriteAllText(_testFilePath, "");
                _fileBackupCreator.CreateBackupFile(_testFilePath);
                var rollingBackupFiles = Directory.GetFiles(_testFileDirectory, _testFileRollingBackup);
                Assert.That(rollingBackupFiles.Length, Is.EqualTo(0));
            }
            finally
            {
                Settings.Default.BackupFileEnabled = originalEnabled;
            }
        }
    }
}