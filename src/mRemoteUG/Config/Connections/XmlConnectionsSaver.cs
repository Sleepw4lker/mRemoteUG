using mRemoteUG.Config.DataProviders;
using mRemoteUG.Config.Serializers.Xml;
using mRemoteUG.Security;
using mRemoteUG.Security.Factories;
using mRemoteUG.Tree;
using mRemoteUG.Tree.Root;

namespace mRemoteUG.Config.Connections;

public class XmlConnectionsSaver
{
    private readonly string _connectionFileName;
    private readonly SaveFilter _saveFilter;

    public XmlConnectionsSaver(string connectionFileName, SaveFilter saveFilter)
    {
        if (string.IsNullOrEmpty(connectionFileName))
        {
            throw new ArgumentException($"Argument '{nameof(connectionFileName)}' cannot be null or empty");
        }

        ArgumentNullException.ThrowIfNull(saveFilter);

        _connectionFileName = connectionFileName;
        _saveFilter = saveFilter;
    }

    public void Save(ConnectionTreeModel connectionTreeModel, string propertyNameTrigger = "")
    {
        // No catch: a failure has to reach ConnectionsService.SaveConnections, which tells the user
        // and does not report a save that did not happen.
        ICryptographyProvider cryptographyProvider = new CryptoProviderFactoryFromSettings().Build();
        var connectionNodeSerializer = new XmlConnectionNodeSerializer(
            cryptographyProvider,
            connectionTreeModel.RootNodes.OfType<RootNodeInfo>().First().PasswordString.ConvertToSecureString(),
            _saveFilter);
        var xmlConnectionsSerializer = new XmlConnectionsSerializer(cryptographyProvider, connectionNodeSerializer)
        {
            UseFullEncryption = mRemoteUG.Settings.Default.EncryptCompleteConnectionsFile
        };
        string xml = xmlConnectionsSerializer.Serialize(connectionTreeModel);

        var fileDataProvider = new FileDataProviderWithRollingBackup(_connectionFileName);
        fileDataProvider.Save(xml);
    }
}
