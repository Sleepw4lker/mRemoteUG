using System.Globalization;
using System.Xml.Linq;

using mRemoteUG.Config.Serializers.Xml;
using mRemoteUG.Security;
using mRemoteUG.Security.Factories;
using mRemoteUG.Security.SymmetricEncryption;
using mRemoteUG.Tree.Root;

using NUnit.Framework;

namespace mRemoteUG.Tests.Config.Serializers.ConnectionSerializers.Xml;

public class XmlRootNodeSerializerTests
{
    // Rebuilt for every test in Setup: tests change the iterations and the password.
    private AeadCryptographyProvider _cryptographyProvider = null!;
    private RootNodeInfo _rootNodeInfo = null!;

    [SetUp]
    public void Setup()
    {
        _cryptographyProvider = new AeadCryptographyProvider();
        _rootNodeInfo = new RootNodeInfo(RootNodeType.Connection);
    }

    [Test]
    public void RootElementNamedConnections()
    {
        XElement element = XmlRootNodeSerializer.SerializeRootNodeInfo(_rootNodeInfo, _cryptographyProvider);
        Assert.That(element.Name.LocalName, Is.EqualTo("Connections"));
    }

    [Test]
    public void RootElementDeclaresTheMrngNamespacePrefix()
    {
        XElement element = XmlRootNodeSerializer.SerializeRootNodeInfo(_rootNodeInfo, _cryptographyProvider);
        string? attributeValue = element.Attribute(XNamespace.Xmlns + "mrng")?.Value;
        Assert.That(attributeValue, Is.EqualTo("http://mremoteng.org"));
    }

    [Test]
    public void RootNodeInfoNameSerialized()
    {
        XElement element = XmlRootNodeSerializer.SerializeRootNodeInfo(_rootNodeInfo, _cryptographyProvider);
        string? attributeValue = element.Attribute(XName.Get("Name"))?.Value;
        Assert.That(attributeValue, Is.EqualTo("Connections"));
    }

    [Test]
    public void EncryptionEngineSerialized()
    {
        ICryptographyProvider cryptoProvider =
            new CryptoProviderFactory(BlockCipherEngines.AES, BlockCipherModes.GCM).Build();
        XElement element = XmlRootNodeSerializer.SerializeRootNodeInfo(_rootNodeInfo, cryptoProvider);
        string? attributeValue = element.Attribute(XName.Get("EncryptionEngine"))?.Value;
        Assert.That(attributeValue, Is.EqualTo(BlockCipherEngines.AES.ToString()));
    }

    [Test]
    public void EncryptionModeSerialized()
    {
        ICryptographyProvider cryptoProvider =
            new CryptoProviderFactory(BlockCipherEngines.AES, BlockCipherModes.GCM).Build();
        XElement element = XmlRootNodeSerializer.SerializeRootNodeInfo(_rootNodeInfo, cryptoProvider);
        string? attributeValue = element.Attribute(XName.Get("BlockCipherMode"))?.Value;
        Assert.That(attributeValue, Is.EqualTo(BlockCipherModes.GCM.ToString()));
    }

    [TestCase(1000)]
    [TestCase(1234)]
    [TestCase(9999)]
    [TestCase(10000)]
    public void KdfIterationsSerialized(int iterations)
    {
        _cryptographyProvider.KeyDerivationIterations = iterations;
        XElement element = XmlRootNodeSerializer.SerializeRootNodeInfo(_rootNodeInfo, _cryptographyProvider);
        string? attributeValue = element.Attribute(XName.Get("KdfIterations"))?.Value;
        Assert.That(attributeValue, Is.EqualTo(iterations.ToString(CultureInfo.InvariantCulture)));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void FullFileEncryptionFlagSerialized(bool fullFileEncryption)
    {
        XElement element =
            XmlRootNodeSerializer.SerializeRootNodeInfo(_rootNodeInfo, _cryptographyProvider, fullFileEncryption);
        string attributeValue = element.Attribute(XName.Get("FullFileEncryption"))?.Value
                                ?? throw new AssertionException("The FullFileEncryption attribute is missing.");
        Assert.That(bool.Parse(attributeValue), Is.EqualTo(fullFileEncryption));
    }

    [TestCase("", "ThisIsNotProtected")]
    [TestCase(null, "ThisIsNotProtected")]
    [TestCase("mR3m", "ThisIsNotProtected")]
    [TestCase("customPassword1", "ThisIsProtected")]
    public void ProtectedStringSerialized(string customPassword, string expectedPlainText)
    {
        _rootNodeInfo.PasswordString = customPassword;
        XElement element = XmlRootNodeSerializer.SerializeRootNodeInfo(_rootNodeInfo, _cryptographyProvider);
        string attributeValue = element.Attribute(XName.Get("Protected"))?.Value
                                ?? throw new AssertionException("The Protected attribute is missing.");
        string? attributeValuePlainText =
            _cryptographyProvider.Decrypt(attributeValue, _rootNodeInfo.PasswordString.ConvertToSecureString());
        Assert.That(attributeValuePlainText, Is.EqualTo(expectedPlainText));
    }

    /// <summary>
    /// Pins the written file format version. It has to match the deserializer
    /// MinSupportedConfVersion exactly: writing a version this build then refuses to read
    /// would make every file it saves unopenable, and the failure would only show up on the
    /// next start.
    /// </summary>
    [Test]
    public void ConfVersionSerializedIsTheOnlySupportedVersion()
    {
        XElement element = XmlRootNodeSerializer.SerializeRootNodeInfo(_rootNodeInfo, _cryptographyProvider);
        string attributeValue = element.Attribute(XName.Get("ConfVersion"))?.Value ?? "";
        Assert.That(double.Parse(attributeValue, CultureInfo.InvariantCulture), Is.EqualTo(2.9));
    }
}
