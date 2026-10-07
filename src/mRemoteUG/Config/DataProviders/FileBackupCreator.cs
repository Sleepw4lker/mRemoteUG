using System;
using System.IO;
using mRemoteUG.App;
using mRemoteUG.Messages;

namespace mRemoteUG.Config.DataProviders
{
    public class FileBackupCreator
    {
        public void CreateBackupFile(string fileName)
        {
            try
            {
                if (WeDontNeedToBackup(fileName))
                    return;

                var backupFileName = string.Format(mRemoteUG.Settings.Default.BackupFileNameFormat, fileName, DateTime.Now);
                File.Copy(fileName, backupFileName);
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage(Language.strConnectionsFileBackupFailed, ex, MessageClass.WarningMsg);
                throw;
            }
        }

        private bool WeDontNeedToBackup(string filePath)
        {
            return FeatureIsTurnedOff() || FileDoesntExist(filePath);
        }

        private bool FileDoesntExist(string filePath)
        {
            return !File.Exists(filePath);
        }

        private bool FeatureIsTurnedOff()
        {
            return !mRemoteUG.Settings.Default.BackupFileEnabled || mRemoteUG.Settings.Default.BackupFileKeepCount == 0;
        }
    }
}