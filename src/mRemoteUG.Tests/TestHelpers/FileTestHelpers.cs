using mRemoteUG.Tools;

namespace mRemoteUG.Tests.TestHelpers;

public class FileTestHelpers
{
    public static void DeleteTestFile(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public static void DeleteFilesInDirectory(string directory, string fileMatching)
    {
        string[] filesToDelete = Directory.GetFiles(directory, fileMatching, SearchOption.TopDirectoryOnly);
        foreach (string file in filesToDelete)
        {
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }
    }

    public static void DeleteDirectory(string? directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }

    public static string NewTempFilePath(string extension = "")
    {
        string folderPath = GetTestSpecificTempDirectory();
        string newPath = Path.Combine(folderPath, Path.GetRandomFileName());
        if (!string.IsNullOrWhiteSpace(extension))
        {
            newPath = newPath + extension;
        }

        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        return newPath;
    }

    /// <summary>
    /// Gets a testing directory that should be unique for a
    /// particular mRemoteUG test.
    /// </summary>
    public static string GetTestSpecificTempDirectory()
    {
        return Path.Combine(Path.GetTempPath(), "mRemoteUG.Tests", Path.GetRandomFileName());
    }

    public static DisposableAction DisposableTempFile(out string filePath, string extension = "")
    {
        string file = NewTempFilePath(extension);
        filePath = file;
        File.AppendAllText(file, "");
        return new DisposableAction(
            () => { },
            () => DeleteDirectory(Path.GetDirectoryName(file)));
    }
}
