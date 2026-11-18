using System.Xml;
using System.Xml.Linq;

using mRemoteUG.Config.Serializers.Xml;
using mRemoteUG.Connection;
using mRemoteUG.Container;
using mRemoteUG.Security;
using mRemoteUG.Security.SymmetricEncryption;
using mRemoteUG.Tree;
using mRemoteUG.Tree.Root;

using NUnit.Framework;

namespace mRemoteUG.Tests.Config.Serializers.ConnectionSerializers.Xml;

public class XmlConnectionsSerializerTests
{
    private readonly XmlConnectionsSerializer _serializer;
    private readonly ConnectionTreeModel _connectionTreeModel;
    private readonly ICryptographyProvider _cryptographyProvider;

    public XmlConnectionsSerializerTests()
    {
        _connectionTreeModel = SetupConnectionTreeModel();
        _cryptographyProvider = new AeadCryptographyProvider();
        var connectionNodeSerializer = new XmlConnectionNodeSerializer(
            _cryptographyProvider,
            _connectionTreeModel.RootNodes.OfType<RootNodeInfo>().First().PasswordString.ConvertToSecureString(),
            new SaveFilter());
        _serializer = new XmlConnectionsSerializer(_cryptographyProvider, connectionNodeSerializer);
    }

    [Test]
    public void ChildNestingSerializedCorrectly()
    {
        string serializedConnections = _serializer.Serialize(_connectionTreeModel);
        var xmlDoc = new XmlDocument();
        xmlDoc.LoadXml(serializedConnections);
        XmlNode? nodeCon4 = xmlDoc.DocumentElement?.SelectSingleNode(
            "Node[@Name='folder2']/Node[@Name='folder3']/Node[@Name='con4']");
        Assert.That(nodeCon4, Is.Not.Null);
    }

    [Test]
    public void SingleConnectionSerializedCorrectly()
    {
        var connectionInfo = new ConnectionInfo { Name = "myConnection" };
        string serializedConnections = _serializer.Serialize(connectionInfo);
        var xmlDoc = new XmlDocument();
        xmlDoc.LoadXml(serializedConnections);
        XmlNode? connectionNode = xmlDoc.DocumentElement?.SelectSingleNode($"Node[@Name='{connectionInfo.Name}']");
        Assert.That(connectionNode, Is.Not.Null);
    }

    /// <summary>
    /// Each level of the tree is indented by four spaces, one element per line.
    /// </summary>
    /// <remarks>
    /// The layout is part of the file format in practice: people diff and hand-edit the file.
    /// </remarks>
    [TestCase("folder1", 4)]
    [TestCase("con1", 8)]
    [TestCase("folder3", 8)]
    [TestCase("con4", 12)]
    public void EachLevelIsIndentedByFourSpaces(string nodeName, int expectedIndent)
    {
        string serializedConnections = _serializer.Serialize(_connectionTreeModel);

        string line = serializedConnections.Split(Environment.NewLine)
                                           .Single(l => l.Contains($" Name=\"{nodeName}\"", StringComparison.Ordinal));
        Assert.That(line.Length - line.TrimStart(' ').Length, Is.EqualTo(expectedIndent));
    }

    [TestCase("Username", "")]
    [TestCase("Domain", "")]
    [TestCase("Password", "")]
    [TestCase("InheritAutomaticResize", "false")]
    public void SerializerRespectsSaveFilterSettings(string attributeName, string expectedValue)
    {
        var connectionNodeSerializer = new XmlConnectionNodeSerializer(
            _cryptographyProvider,
            _connectionTreeModel.RootNodes.OfType<RootNodeInfo>().First().PasswordString.ConvertToSecureString(),
            new SaveFilter(true));
        var serializer = new XmlConnectionsSerializer(_cryptographyProvider, connectionNodeSerializer);
        var connectionInfo = new ConnectionInfo
        {
            Name = "myConnection",
            Inheritance = { AutomaticResize = true }
        };
        string serializedConnections = serializer.Serialize(connectionInfo);
        var xdoc = XDocument.Parse(serializedConnections);
        string? attributeValue = xdoc.Root?.Element("Node")?.Attribute(attributeName)?.Value;
        Assert.That(attributeValue, Is.EqualTo(expectedValue));
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
