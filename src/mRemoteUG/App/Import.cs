using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using mRemoteUG.Config.Import;
using mRemoteUG.Connection;
using mRemoteUG.Container;

namespace mRemoteUG.App
{
	public static class Import
    {
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
                    fileTypes.AddRange(new[] {Language.strFiltermRemoteXML, "*.xml"});
                    fileTypes.AddRange(new[] {Language.strFilterAll, "*.*"});

                    openFileDialog.Filter = string.Join("|", fileTypes.ToArray());

                    if (openFileDialog.ShowDialog() != DialogResult.OK)
                        return;

					HeadlessFileImport(
						openFileDialog.FileNames,
						importDestinationContainer,
						Runtime.ConnectionsService,
						fileName => MessageBox.Show(string.Format(Language.strImportFileFailedContent, fileName), Language.strImportFileFailedMainInstruction,
							MessageBoxButtons.OK, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1));
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
	        Action<string> exceptionAction = null)
        {
	        using (connectionsService.BatchedSavingContext())
	        {
		        foreach (var fileName in filePaths)
		        {
			        try
			        {
				        var importer = BuildConnectionImporterFromFileExtension(fileName);
				        importer.Import(fileName, importDestinationContainer);
			        }
			        catch (Exception ex)
			        {
				        exceptionAction?.Invoke(fileName);
				        Runtime.MessageCollector.AddExceptionMessage($"Error occurred while importing file '{fileName}'.", ex);
			        }
		        }
	        }
		}

        private static IConnectionImporter<string> BuildConnectionImporterFromFileExtension(string fileName)
        {
            var extension = Path.GetExtension(fileName) ?? "";
            switch (extension.ToLowerInvariant())
            {
                case ".xml":
                    return new MRemoteNGXmlImporter();
                default:
                    throw new InvalidDataException("Unrecognized file format.");
            }
        }
    }
}
