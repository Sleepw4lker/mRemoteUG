using System.Text;
using System.Xml;
using System.Xml.Linq;

using mRemoteUG.Connection;
using mRemoteUG.Security;
using mRemoteUG.Tree;
using mRemoteUG.Tree.Root;

namespace mRemoteUG.Config.Serializers.Xml;

public class XmlConnectionsSerializer : ISerializer<ConnectionTreeModel, string>, ISerializer<ConnectionInfo, string>
{
    private readonly ICryptographyProvider _cryptographyProvider;
    private readonly ISerializer<ConnectionInfo, XElement> _connectionNodeSerializer;

    public bool UseFullEncryption { get; set; }

    public XmlConnectionsSerializer(
        ICryptographyProvider cryptographyProvider,
        ISerializer<ConnectionInfo, XElement> connectionNodeSerializer)
    {
        _cryptographyProvider = cryptographyProvider;
        _connectionNodeSerializer = connectionNodeSerializer;
    }

    public string Serialize(ConnectionTreeModel connectionTreeModel)
    {
        var rootNode = (RootNodeInfo)connectionTreeModel.RootNodes.First(node => node is RootNodeInfo);
        return SerializeConnectionsData(rootNode);
    }

    public string Serialize(ConnectionInfo serializationTarget)
    {
        return SerializeConnectionsData(serializationTarget);
    }

    private string SerializeConnectionsData(ConnectionInfo serializationTarget)
    {
        // No catch: an empty document returned on failure would be written over the connections file.
        var documentCompiler = new XmlConnectionsDocumentCompiler(_cryptographyProvider, _connectionNodeSerializer);
        XDocument xmlDocument = documentCompiler.CompileDocument(serializationTarget, UseFullEncryption);
        return WriteXmlToString(xmlDocument);
    }

    private static string WriteXmlToString(XNode xmlDocument)
    {
        string xmlString;
        var xmlWriterSettings = new XmlWriterSettings { Indent = true, IndentChars = "    ", Encoding = Encoding.UTF8 };
        var memoryStream = new MemoryStream();
        using (var xmlTextWriter = XmlWriter.Create(memoryStream, xmlWriterSettings))
        {
            xmlDocument.WriteTo(xmlTextWriter);
            xmlTextWriter.Flush();
            var streamReader = new StreamReader(memoryStream, Encoding.UTF8, true);
            memoryStream.Seek(0, SeekOrigin.Begin);
            xmlString = streamReader.ReadToEnd();
        }
        return xmlString;
    }
}
