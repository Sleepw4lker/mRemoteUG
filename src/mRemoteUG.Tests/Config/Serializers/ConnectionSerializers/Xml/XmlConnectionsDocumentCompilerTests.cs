using System.Xml.Linq;
using System.Xml.XPath;

using mRemoteUG.Config.Serializers.Xml;
using mRemoteUG.Connection;
using mRemoteUG.Container;
using mRemoteUG.Security;
using mRemoteUG.Security.Factories;
using mRemoteUG.Tree;
using mRemoteUG.Tree.Root;

using NUnit.Framework;

namespace mRemoteUG.Tests.Config.Serializers.ConnectionSerializers.Xml;

public class XmlConnectionsDocumentCompilerTests
{
    private readonly XmlConnectionsDocumentCompiler _documentCompiler;
    private readonly ConnectionTreeModel _connectionTreeModel;
    private readonly ICryptographyProvider _cryptographyProvider;
    private readonly ContainerInfo _folder1 = new ContainerInfo { Name = "folder1" };
    private readonly ContainerInfo _folder2 = new ContainerInfo { Name = "folder2" };
    private readonly ContainerInfo _folder3 = new ContainerInfo { Name = "folder3" };
    private readonly ConnectionInfo _con0 = new ConnectionInfo { Name = "con0" };
    private readonly ConnectionInfo _con1 = new ConnectionInfo { Name = "con1" };
    private readonly ConnectionInfo _con2 = new ConnectionInfo { Name = "con2" };
    private readonly ConnectionInfo _con3 = new ConnectionInfo { Name = "con3" };
    private readonly ConnectionInfo _con4 = new ConnectionInfo { Name = "con4" };

    public XmlConnectionsDocumentCompilerTests()
    {
        _connectionTreeModel = SetupConnectionTreeModel();
        _cryptographyProvider = new CryptoProviderFactory(BlockCipherEngines.AES, BlockCipherModes.GCM).Build();
        var connectionNodeSerializer = new XmlConnectionNodeSerializer(
            _cryptographyProvider,
            _connectionTreeModel.RootNodes.OfType<RootNodeInfo>().First().PasswordString.ConvertToSecureString(),
            new SaveFilter());
        _documentCompiler = new XmlConnectionsDocumentCompiler(_cryptographyProvider, connectionNodeSerializer);
    }

    [Test]
    public void XDocumentHasXmlDeclaration()
    {
        XDocument xdoc = _documentCompiler.CompileDocument(_connectionTreeModel, false);
        Assert.That(xdoc.Declaration, Is.Not.Null);
    }

    [Test]
    public void XmlDeclarationIsVersion10Utf8()
    {
        XDocument xdoc = _documentCompiler.CompileDocument(_connectionTreeModel, false);
        Assert.Multiple(() =>
        {
            Assert.That(xdoc.Declaration?.Version, Is.EqualTo("1.0"));
            Assert.That(xdoc.Declaration?.Encoding, Is.EqualTo("utf-8"));
        });
    }

    [Test]
    public void DocumentHasRootConnectionElement()
    {
        XDocument xdoc = _documentCompiler.CompileDocument(_connectionTreeModel, false);
        string? rootElementName = xdoc.Root?.Name.LocalName;
        Assert.That(rootElementName, Is.EqualTo("Connections"));
    }

    [Test]
    public void ConnectionNodesSerializedRecursively()
    {
        XDocument xdoc = _documentCompiler.CompileDocument(_connectionTreeModel, false);
        XElement? con4 = xdoc.Root?.XPathSelectElement(
            "Node[@Name='folder2']/Node[@Name='folder3']/Node[@Name='con4']");
        Assert.That(con4, Is.Not.Null);
    }

    [Test]
    public void XmlContentEncryptedWhenFullFileEncryptionTurnedOn()
    {
        XDocument xdoc = _documentCompiler.CompileDocument(_connectionTreeModel, true);
        string? rootElementValue = xdoc.Root?.Value;
        Assert.That(rootElementValue, Is.Not.EqualTo(string.Empty));
    }

    /// <summary>
    /// Exporting one connection writes the root it came from: the file's name, and the
    /// Protected marker under the file's own password, not the default root's.
    /// </summary>
    [Test]
    public void ACompiledConnectionKeepsItsRootsNameAndPassword()
    {
        var root = new RootNodeInfo(RootNodeType.Connection) { Name = "My saved hosts", PasswordString = "file pw" };
        var folder = new ContainerInfo { Name = "folder" };
        var connection = new ConnectionInfo { Name = "con" };
        root.AddChild(folder);
        folder.AddChild(connection);

        XDocument xdoc = _documentCompiler.CompileDocument(connection, false);

        string protectedValue = xdoc.Root?.Attribute("Protected")?.Value ?? "";
        Assert.Multiple(() =>
        {
            Assert.That(xdoc.Root?.Attribute("Name")?.Value, Is.EqualTo("My saved hosts"));
            Assert.That(_cryptographyProvider.Decrypt(protectedValue, "file pw".ConvertToSecureString()),
                        Is.EqualTo("ThisIsProtected"));
        });
    }


    private ConnectionTreeModel SetupConnectionTreeModel()
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
        rootNode.AddChild(_folder1);
        rootNode.AddChild(_folder2);
        rootNode.AddChild(_con0);
        _folder1.AddChild(_con1);
        _folder2.AddChild(_con2);
        _folder2.AddChild(_folder3);
        _folder3.AddChild(_con3);
        _folder3.AddChild(_con4);
        connectionTreeModel.AddRootNode(rootNode);
        return connectionTreeModel;
    }
}
