using System.Security;
using System.Xml.Linq;

using mRemoteUG.Connection;
using mRemoteUG.Container;
using mRemoteUG.Security;
using mRemoteUG.Tree;
using mRemoteUG.Tree.Root;

namespace mRemoteUG.Config.Serializers.Xml;

public class XmlConnectionsDocumentCompiler
{
    private readonly ICryptographyProvider _cryptographyProvider;
    private readonly ISerializer<ConnectionInfo, XElement> _connectionNodeSerializer;

    public XmlConnectionsDocumentCompiler(
        ICryptographyProvider cryptographyProvider,
        ISerializer<ConnectionInfo, XElement> connectionNodeSerializer)
    {
        ArgumentNullException.ThrowIfNull(cryptographyProvider);

        ArgumentNullException.ThrowIfNull(connectionNodeSerializer);

        _cryptographyProvider = cryptographyProvider;
        _connectionNodeSerializer = connectionNodeSerializer;
    }

    public XDocument CompileDocument(ConnectionTreeModel connectionTreeModel, bool fullFileEncryption)
    {
        RootNodeInfo rootNodeInfo = GetRootNodeFromConnectionTreeModel(connectionTreeModel);
        return CompileDocument(rootNodeInfo, fullFileEncryption);
    }

    public XDocument CompileDocument(ConnectionInfo serializationTarget, bool fullFileEncryption)
    {
        RootNodeInfo rootNodeInfo = GetRootNodeFromConnectionInfo(serializationTarget);
        SecureString encryptionKey = rootNodeInfo.PasswordString.ConvertToSecureString();
        XElement rootElement = CompileRootNode(rootNodeInfo, fullFileEncryption);

        CompileRecursive(serializationTarget, rootElement);
        var xmlDeclaration = new XDeclaration("1.0", "utf-8", null);
        var xmlDocument = new XDocument(xmlDeclaration, rootElement);
        if (fullFileEncryption)
        {
            xmlDocument = new XmlConnectionsDocumentEncryptor(_cryptographyProvider)
                .EncryptDocument(xmlDocument, encryptionKey);
        }

        return xmlDocument;
    }

    private void CompileRecursive(ConnectionInfo serializationTarget, XContainer parentElement)
    {
        XContainer newElement = parentElement;
        if (!(serializationTarget is RootNodeInfo))
        {
            newElement = CompileConnectionInfoNode(serializationTarget);
            parentElement.Add(newElement);
        }
        var serializationTargetAsContainer = serializationTarget as ContainerInfo;
        if (serializationTargetAsContainer == null)
        {
            return;
        }

        foreach (ConnectionInfo child in serializationTargetAsContainer.Children)
        {
            CompileRecursive(child, newElement);
        }
    }

    private static RootNodeInfo GetRootNodeFromConnectionTreeModel(ConnectionTreeModel connectionTreeModel)
    {
        return (RootNodeInfo)connectionTreeModel.RootNodes.First(node => node is RootNodeInfo);
    }

    private static RootNodeInfo GetRootNodeFromConnectionInfo(ConnectionInfo connectionInfo)
    {
        while (true)
        {
            var connectionInfoAsRootNode = connectionInfo as RootNodeInfo;
            if (connectionInfoAsRootNode != null)
            {
                return connectionInfoAsRootNode;
            }

            connectionInfo = connectionInfo?.Parent ?? new RootNodeInfo(RootNodeType.Connection);
        }
    }

    private XElement CompileRootNode(RootNodeInfo rootNodeInfo, bool fullFileEncryption)
    {
        return XmlRootNodeSerializer.SerializeRootNodeInfo(rootNodeInfo, _cryptographyProvider, fullFileEncryption);
    }

    private XElement CompileConnectionInfoNode(ConnectionInfo connectionInfo)
    {
        return _connectionNodeSerializer.Serialize(connectionInfo);
    }
}
