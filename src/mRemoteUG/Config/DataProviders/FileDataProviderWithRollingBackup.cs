namespace mRemoteUG.Config.DataProviders;

public class FileDataProviderWithRollingBackup : FileDataProvider
{
    public FileDataProviderWithRollingBackup(string filePath) : base(filePath)
    {
    }

    public override void Save(string content)
    {
        FileBackupCreator.CreateBackupFile(FilePath);
        base.Save(content);
    }
}
