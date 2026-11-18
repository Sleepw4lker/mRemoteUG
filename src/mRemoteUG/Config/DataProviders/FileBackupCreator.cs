using System.Globalization;

using mRemoteUG.App;
using mRemoteUG.Messages;

namespace mRemoteUG.Config.DataProviders;

public class FileBackupCreator
{
    public static void CreateBackupFile(string fileName)
    {
        try
        {
            if (WeDontNeedToBackup(fileName))
            {
                return;
            }

            // Invariant, so the name carries the Gregorian year in every culture. Backups named
            // under another calendar before #99 are aged by FileBackupPruner, not by their names.
            string backupFileName = string.Format(
                CultureInfo.InvariantCulture,
                mRemoteUG.Settings.Default.BackupFileNameFormat,
                fileName,
                DateTime.Now);
            File.Copy(fileName, backupFileName);
        }
        catch (Exception ex)
        {
            Runtime.MessageCollector.AddExceptionMessage(
                Language.strConnectionsFileBackupFailed,
                ex,
                MessageClass.WarningMsg);
            throw;
        }
    }

    private static bool WeDontNeedToBackup(string filePath)
    {
        return FeatureIsTurnedOff() || FileDoesntExist(filePath);
    }

    private static bool FileDoesntExist(string filePath)
    {
        return !File.Exists(filePath);
    }

    private static bool FeatureIsTurnedOff()
    {
        return FileBackupLookup.AreSwitchedOff;
    }
}
