namespace mRemoteUG.Config.DataProviders;

public class FileDataProvider
{
    public string FilePath { get; set; }

    public FileDataProvider(string filePath)
    {
        FilePath = filePath;
    }

    public virtual string Load()
    {
        // No catch: the caller reports why the file could not be read. An empty string would be
        // taken for an empty file, and read as an unsupported format version 0.
        return File.ReadAllText(FilePath);
    }

    public virtual void Save(string content)
    {
        // No catch: the caller decides how a failed write is reported, and must not take it for a success.
        CreateMissingDirectories();

        // Write a complete new file, then rename it over the old one: a crash or power cut in
        // between leaves the old file intact, never a truncated one. Same folder, so the rename
        // stays on one volume; a random name, so two instances saving at once cannot collide.
        // Not File.Replace: it would take over the backup and fails when the target is missing.
        string tempFilePath = $"{FilePath}.{Path.GetRandomFileName()}.tmp";
        bool moved = false;
        try
        {
            File.WriteAllText(tempFilePath, content);
            File.Move(tempFilePath, FilePath, overwrite: true);
            moved = true;
        }
        finally
        {
            if (!moved)
            {
                DeleteTemporaryFile(tempFilePath);
            }
        }
    }

    private static void DeleteTemporaryFile(string tempFilePath)
    {
        try
        {
            File.Delete(tempFilePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Recovered on purpose: the save has already failed, and its exception, not this one,
            // tells the caller why. A leftover .tmp file is harmless; the pruner ignores it.
        }
    }

    private void CreateMissingDirectories()
    {
        string? dirname = Path.GetDirectoryName(FilePath);
        if (dirname == null)
        {
            return;
        }

        Directory.CreateDirectory(dirname);
    }
}
