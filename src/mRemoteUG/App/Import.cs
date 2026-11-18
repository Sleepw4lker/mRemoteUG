using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using mRemoteUG.Config.Import;
using mRemoteUG.Connection;
using mRemoteUG.Container;
using mRemoteUG.Tools;

namespace mRemoteUG.App;

public static class Import
{
    [SuppressMessage("Performance", "CA1863",
                     Justification = "The format is a localized resource read at run time; "
                                     + "a cached CompositeFormat would freeze the UI language.")]
    public static void ImportFromFile(ContainerInfo importDestinationContainer)
    {
        try
        {
            using (var openFileDialog = new OpenFileDialog())
            {
                openFileDialog.CheckFileExists = true;
                openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
                openFileDialog.Multiselect = true;

                var fileTypes = new List<string>();
                fileTypes.AddRange(new[] { Language.strFiltermRemoteXML, "*.xml" });
                fileTypes.AddRange(new[] { Language.strFilterAll, "*.*" });

                openFileDialog.Filter = string.Join("|", fileTypes.ToArray());

                if (openFileDialog.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                HeadlessFileImport(
                    openFileDialog.FileNames,
                    importDestinationContainer,
                    Runtime.ConnectionsService,
                    fileName => MessageBox.Show(
                        string.Format(CultureInfo.CurrentCulture, Language.strImportFileFailedContent, fileName),
                        Language.strImportFileFailedMainInstruction,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Exclamation,
                        MessageBoxDefaultButton.Button1));
            }
        }
        catch (Exception ex)
        {
            Runtime.MessageCollector.AddExceptionMessage("Unable to import file.", ex);
        }
    }

    public static void HeadlessFileImport(
        IEnumerable<string> filePaths,
        ContainerInfo importDestinationContainer,
        ConnectionsService connectionsService,
        Action<string>? exceptionAction = null)
    {
        using (connectionsService.BatchedSavingContext())
        {
            foreach (string fileName in filePaths)
            {
                try
                {
                    MRemoteNGXmlImporter importer = BuildConnectionImporterFromFileExtension(fileName);
                    importer.Import(fileName, importDestinationContainer);
                }
                catch (Exception ex)
                {
                    exceptionAction?.Invoke(fileName);
                    Runtime.MessageCollector.AddExceptionMessage(
                        $"Error occurred while importing file '{fileName}'.",
                        ex);
                }
            }
        }
    }

    private static MRemoteNGXmlImporter BuildConnectionImporterFromFileExtension(string fileName)
    {
        string extension = Path.GetExtension(fileName) ?? "";
        switch (extension.ToLowerInvariant())
        {
            case ".xml":
                return new MRemoteNGXmlImporter(fileName => MiscTools.PasswordDialog(fileName, false));
            default:
                throw new InvalidDataException("Unrecognized file format.");
        }
    }
}
