using mRemoteUG.Config.Connections;
using mRemoteUG.Config.DataProviders;
using mRemoteUG.Config.Serializers;
using mRemoteUG.Config.Serializers.Csv;
using mRemoteUG.Config.Serializers.Xml;
using mRemoteUG.Connection;
using mRemoteUG.Container;
using mRemoteUG.Security;
using mRemoteUG.Security.Factories;
using mRemoteUG.Tree;
using mRemoteUG.Tree.Root;
using mRemoteUG.UI.Forms;


namespace mRemoteUG.App;

public static class Export
{
    public static void ExportToFile(ConnectionInfo? selectedNode, ConnectionTreeModel? connectionTreeModel)
    {
        try
        {
            var saveFilter = new SaveFilter();

            using (var exportForm = new ExportForm())
            {
                if (selectedNode is ContainerInfo folder && folder.GetTreeNodeType() == TreeNodeType.Container)
                {
                    exportForm.SelectedFolder = folder;
                }
                else if (selectedNode?.GetTreeNodeType() == TreeNodeType.Connection)
                {
                    // A connection without a parent offers no folder to export, rather than
                    // failing the export before the dialog opens.
                    if (selectedNode.Parent?.GetTreeNodeType() == TreeNodeType.Container)
                    {
                        exportForm.SelectedFolder = selectedNode.Parent;
                    }

                    exportForm.SelectedConnection = selectedNode;
                }

                if (exportForm.ShowDialog(FrmMain.Default) != DialogResult.OK)
                {
                    return;
                }

                // Each throw replaces a NullReferenceException at the same point, and the catch
                // below logs it the same way. The form enables each "selected" option only while
                // its node is set; the model is null only before a connection file has loaded.
                ConnectionInfo exportTarget;
                switch (exportForm.Scope)
                {
                    case ExportForm.ExportScope.SelectedFolder:
                        exportTarget = exportForm.SelectedFolder
                            ?? throw new InvalidOperationException("Export scope is a folder, but none is selected.");
                        break;
                    case ExportForm.ExportScope.SelectedConnection:
                        exportTarget = exportForm.SelectedConnection
                            ?? throw new InvalidOperationException(
                                "Export scope is a connection, but none is selected.");
                        break;
                    default:
                        exportTarget = (connectionTreeModel
                                ?? throw new InvalidOperationException("No connection file is loaded."))
                            .RootNodes.First(node => node is RootNodeInfo);
                        break;
                }

                saveFilter.SaveUsername = exportForm.IncludeUsername;
                saveFilter.SavePassword = exportForm.IncludePassword;
                saveFilter.SaveDomain = exportForm.IncludeDomain;
                saveFilter.SaveInheritance = exportForm.IncludeInheritance;

                SaveExportFile(exportForm.FileName, exportForm.SaveFormat, saveFilter, exportTarget);
            }

        }
        catch (Exception ex)
        {
            Runtime.MessageCollector.AddExceptionMessage("App.Export.ExportToFile() failed.", ex);
        }
    }

    private static void SaveExportFile(
        string fileName,
        SaveFormat saveFormat,
        SaveFilter saveFilter,
        ConnectionInfo exportTarget)
    {
        try
        {
            ISerializer<ConnectionInfo, string> serializer;
            switch (saveFormat)
            {
                case SaveFormat.mRXML:
                    ICryptographyProvider cryptographyProvider = new CryptoProviderFactoryFromSettings().Build();
                    var rootNode = exportTarget.GetRootParent() as RootNodeInfo;
                    var connectionNodeSerializer = new XmlConnectionNodeSerializer(
                        cryptographyProvider,
                        rootNode?.PasswordString.ConvertToSecureString()
                            ?? new RootNodeInfo(RootNodeType.Connection).PasswordString.ConvertToSecureString(),
                        saveFilter);
                    serializer = new XmlConnectionsSerializer(cryptographyProvider, connectionNodeSerializer);
                    break;
                case SaveFormat.mRCSV:
                    serializer = new CsvConnectionsSerializerMremotengFormat(saveFilter);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(saveFormat), saveFormat, null);
            }
            string serializedData = serializer.Serialize(exportTarget);
            var fileDataProvider = new FileDataProvider(fileName);
            fileDataProvider.Save(serializedData);
        }
        catch (Exception ex)
        {
            Runtime.MessageCollector.AddExceptionMessage($"Export.SaveExportFile(\"{fileName}\") failed.", ex);
        }
    }
}
