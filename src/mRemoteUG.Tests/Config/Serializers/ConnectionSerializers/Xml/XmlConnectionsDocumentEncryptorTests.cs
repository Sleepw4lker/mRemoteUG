using System.Xml.Linq;

using mRemoteUG.Config.Serializers.Xml;
using mRemoteUG.Connection;
using mRemoteUG.Container;
using mRemoteUG.Security;
using mRemoteUG.Security.Factories;
using mRemoteUG.Tree;
using mRemoteUG.Tree.Root;

using NUnit.Framework;

namespace mRemoteUG.Tests.Config.Serializers.ConnectionSerializers.Xml;

public class XmlConnectionsDocumentEncryptorTests
{
    private readonly XmlConnectionsDocumentEncryptor _documentEncryptor;
    private readonly XDocument _originalDocument;

    public XmlConnectionsDocumentEncryptorTests()
    {
        ConnectionTreeModel connectionTreeModel = SetupConnectionTreeModel();
        ICryptographyProvider cryptoProvider =
            new CryptoProviderFactory(BlockCipherEngines.AES, BlockCipherModes.GCM).Build();
        var connectionNodeSerializer = new XmlConnectionNodeSerializer(
            cryptoProvider,
            connectionTreeModel.RootNodes.OfType<RootNodeInfo>().First().PasswordString.ConvertToSecureString(),
            new SaveFilter());
        _originalDocument = new XmlConnectionsDocumentCompiler(cryptoProvider, connectionNodeSerializer)
            .CompileDocument(connectionTreeModel, false);
        _documentEncryptor = new XmlConnectionsDocumentEncryptor(cryptoProvider);
    }

    [Test]
    public void RootNodeValueIsEncrypted()
    {
        XDocument encryptedDocument =
            _documentEncryptor.EncryptDocument(_originalDocument, "mR3m".ConvertToSecureString());
        string? encryptedContent = encryptedDocument.Root?.Value;
        Assert.That(encryptedContent, Is.Not.EqualTo(string.Empty));
    }

    private static ConnectionTreeModel SetupConnectionTreeModel()
    {
        /*
         * Root
         * |--- con0
         * |--- folder1
         * |    L--- con1
         * L--- folder2
         *      |--- con2
         *      L--- folder3
         *           |--- con3
         *           L--- con4
         */
        var connectionTreeModel = new ConnectionTreeModel();
        var rootNode = new RootNodeInfo(RootNodeType.Connection);
        var folder1 = new ContainerInfo { Name = "folder1" };
        var folder2 = new ContainerInfo { Name = "folder2" };
        var folder3 = new ContainerInfo { Name = "folder3" };
        var con0 = new ConnectionInfo { Name = "con0" };
        var con1 = new ConnectionInfo { Name = "con1" };
        var con2 = new ConnectionInfo { Name = "con2" };
        var con3 = new ConnectionInfo { Name = "con3" };
        var con4 = new ConnectionInfo { Name = "con4" };
        rootNode.AddChild(folder1);
        rootNode.AddChild(folder2);
        rootNode.AddChild(con0);
        folder1.AddChild(con1);
        folder2.AddChild(con2);
        folder2.AddChild(folder3);
        folder3.AddChild(con3);
        folder3.AddChild(con4);
        connectionTreeModel.AddRootNode(rootNode);
        return connectionTreeModel;
    }
}
