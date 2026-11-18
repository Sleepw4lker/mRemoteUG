namespace mRemoteUG.Config.DataProviders;

public class FileBackupPruner
{
    /// <summary>
    /// Deletes all but the <paramref name="maxBackupsToKeep"/> newest backups of
    /// <paramref name="filePath"/>, aged as <see cref="FileBackupLookup.NewestFirst"/> ages them.
    /// </summary>
    public static void PruneBackupFiles(string filePath, int maxBackupsToKeep)
    {
        foreach (FileBackup backup in FileBackupLookup.NewestFirst(filePath).Skip(maxBackupsToKeep))
        {
            File.Delete(backup.Path);
        }
    }
}
