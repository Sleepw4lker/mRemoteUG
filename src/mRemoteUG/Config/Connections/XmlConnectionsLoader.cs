using mRemoteUG.Config.DataProviders;
using mRemoteUG.Config.Serializers.Xml;
using mRemoteUG.Tools;
using mRemoteUG.Tree;
using System;
using System.IO;
using System.Security;

namespace mRemoteUG.Config.Connections
{
    public class XmlConnectionsLoader
    {
        private readonly string _connectionFilePath;

        public XmlConnectionsLoader(string connectionFilePath)
        {
            if (string.IsNullOrEmpty(connectionFilePath))
                throw new ArgumentException($"{nameof(connectionFilePath)} cannot be null or empty");

            if (!File.Exists(connectionFilePath))
                throw new FileNotFoundException($"{connectionFilePath} does not exist");

            _connectionFilePath = connectionFilePath;
        }

        public ConnectionTreeModel Load()
        {
            var dataProvider = new FileDataProvider(_connectionFilePath);
            var xmlString = dataProvider.Load();
            var deserializer = new XmlConnectionsDeserializer(PromptForPassword);
            return deserializer.Deserialize(xmlString);
        }

        private SecureString? PromptForPassword()
        {
            var password = MiscTools.PasswordDialog("", false);
            return password;
        }
    }
}
