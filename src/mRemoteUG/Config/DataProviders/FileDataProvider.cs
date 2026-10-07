using System;
using System.IO;
using mRemoteUG.App;

namespace mRemoteUG.Config.DataProviders
{
    public class FileDataProvider
    {
        public string FilePath { get; set; }

        public FileDataProvider(string filePath)
        {
            FilePath = filePath;
        }

        public virtual string Load()
        {
            var fileContents = "";
            try
            {
                fileContents = File.ReadAllText(FilePath);
            }
            catch (FileNotFoundException ex)
            {
                Runtime.MessageCollector.AddExceptionMessage($"Could not load file. File does not exist '{FilePath}'", ex);
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage($"Failed to load file {FilePath}", ex);
            }
            return fileContents;
        }

        public virtual void Save(string content)
        {
            try
            {
                CreateMissingDirectories();
                File.WriteAllText(FilePath, content);
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage($"Failed to save file {FilePath}", ex);
            }
        }

        private void CreateMissingDirectories()
        {
            var dirname = Path.GetDirectoryName(FilePath);
            if (dirname == null) return;
            Directory.CreateDirectory(dirname);
        }
    }
}