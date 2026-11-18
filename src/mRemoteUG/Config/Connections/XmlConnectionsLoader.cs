using System.Security;

using mRemoteUG.Config.DataProviders;
using mRemoteUG.Config.Serializers.Xml;
using mRemoteUG.Tools;
using mRemoteUG.Tree;

namespace mRemoteUG.Config.Connections;

public class XmlConnectionsLoader
{
    private readonly string _connectionFilePath;

    public XmlConnectionsLoader(string connectionFilePath)
    {
        if (string.IsNullOrEmpty(connectionFilePath))
        {
            throw new ArgumentException($"{nameof(connectionFilePath)} cannot be null or empty");
        }

        if (!File.Exists(connectionFilePath))
        {
            throw new FileNotFoundException($"{connectionFilePath} does not exist");
        }

        _connectionFilePath = connectionFilePath;
    }

    /// <returns>The loaded connections, or null when the file's password was not given.</returns>
    public ConnectionTreeModel? Load()
    {
        var dataProvider = new FileDataProvider(_connectionFilePath);
        string xmlString = dataProvider.Load();
        var deserializer = new XmlConnectionsDeserializer(PromptForPassword, _connectionFilePath);
        return deserializer.Deserialize(xmlString);
    }

    private SecureString? PromptForPassword()
    {
        return MiscTools.PasswordDialog(_connectionFilePath, false);
    }
}
