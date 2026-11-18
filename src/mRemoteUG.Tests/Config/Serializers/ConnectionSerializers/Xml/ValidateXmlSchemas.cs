using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml;
using System.Xml.Schema;

using mRemoteUG.Config.Serializers.Xml;
using mRemoteUG.Security;
using mRemoteUG.Security.SymmetricEncryption;
using mRemoteUG.Tests.TestHelpers;
using mRemoteUG.Tree;
using mRemoteUG.Tree.Root;

using NUnit.Framework;

namespace mRemoteUG.Tests.Config.Serializers.ConnectionSerializers.Xml;

public class ValidateXmlSchemas
{
    private readonly XmlConnectionsSerializer _serializer;
    private readonly ConnectionTreeModel _connectionTreeModel;
    private readonly ICryptographyProvider _cryptographyProvider;
    private readonly XmlReaderSettings _xmlReaderSettings;

    public ValidateXmlSchemas()
    {
        _connectionTreeModel = ConnectionTreeModelBuilder.Build();
        _cryptographyProvider = new AeadCryptographyProvider();
        var connectionNodeSerializer = new XmlConnectionNodeSerializer(
            _cryptographyProvider,
            _connectionTreeModel.RootNodes.OfType<RootNodeInfo>().First().PasswordString.ConvertToSecureString(),
            new SaveFilter());
        _serializer = new XmlConnectionsSerializer(_cryptographyProvider, connectionNodeSerializer);
        _xmlReaderSettings = new XmlReaderSettings
        {
            ValidationType = ValidationType.Schema,
            ValidationFlags = XmlSchemaValidationFlags.ProcessInlineSchema |
                              XmlSchemaValidationFlags.ProcessSchemaLocation |
                              XmlSchemaValidationFlags.ReportValidationWarnings
        };
    }

    [Test]
    public void ValidateSchema()
    {
        var sb = new StringBuilder();
        string xml = _serializer.Serialize(_connectionTreeModel);

        string schemaFile = GetTargetPath("mremoteng_confcons_v2_9.xsd");
        _xmlReaderSettings.Schemas.Add("http://mremoteng.org", schemaFile);
        _xmlReaderSettings.ValidationEventHandler += (sender, args) =>
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"{args.Severity}: {args.Message}");
        };

        using (Stream stream = GenerateStreamFromString(xml))
        {
            var reader = XmlReader.Create(stream, _xmlReaderSettings);
            while (reader.Read())
            {
                ;
            }
        }

        Assert.That(sb.ToString(), Is.Empty);
    }

    public static string GetTargetPath(string fileName, [CallerFilePath] string sourceFilePath = "")
    {
        const string debugOrRelease =
#if DEBUG
            "Debug";
#else
            "Release";
#endif

        string? path = Path.GetDirectoryName(sourceFilePath);
        string filePath = $@"{path}\..\..\..\..\bin\{debugOrRelease}\Schemas\{fileName}";
        return filePath;
    }

    private static MemoryStream GenerateStreamFromString(string s)
    {
        var stream = new MemoryStream();
        var writer = new StreamWriter(stream);
        writer.Write(s);
        writer.Flush();
        stream.Position = 0;
        return stream;
    }
}
