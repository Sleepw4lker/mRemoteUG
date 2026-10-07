using System;
using System.Linq;
using mRemoteUG.App;
using mRemoteUG.Config.DataProviders;
using mRemoteUG.Config.Serializers.Xml;
using mRemoteUG.Security;
using mRemoteUG.Security.Factories;
using mRemoteUG.Tree;
using mRemoteUG.Tree.Root;

namespace mRemoteUG.Config.Connections
{
    public class XmlConnectionsSaver
    {
        private readonly string _connectionFileName;
        private readonly SaveFilter _saveFilter;

        public XmlConnectionsSaver(string connectionFileName, SaveFilter saveFilter)
        {
            if (string.IsNullOrEmpty(connectionFileName))
                throw new ArgumentException($"Argument '{nameof(connectionFileName)}' cannot be null or empty");
            if (saveFilter == null)
                throw new ArgumentNullException(nameof(saveFilter));

            _connectionFileName = connectionFileName;
            _saveFilter = saveFilter;
        }

        public void Save(ConnectionTreeModel connectionTreeModel, string propertyNameTrigger = "")
        {
            try
            {
                var cryptographyProvider = new CryptoProviderFactoryFromSettings().Build();
                var connectionNodeSerializer = new XmlConnectionNodeSerializer(
                    cryptographyProvider,
                    connectionTreeModel.RootNodes.OfType<RootNodeInfo>().First().PasswordString.ConvertToSecureString(),
                    _saveFilter);
                var xmlConnectionsSerializer = new XmlConnectionsSerializer(cryptographyProvider, connectionNodeSerializer)
                {
                    UseFullEncryption = mRemoteUG.Settings.Default.EncryptCompleteConnectionsFile
                };
                var xml = xmlConnectionsSerializer.Serialize(connectionTreeModel);

                var fileDataProvider = new FileDataProviderWithRollingBackup(_connectionFileName);
                fileDataProvider.Save(xml);
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector?.AddExceptionMessage("SaveToXml failed", ex);
            }
        }
    }
}
