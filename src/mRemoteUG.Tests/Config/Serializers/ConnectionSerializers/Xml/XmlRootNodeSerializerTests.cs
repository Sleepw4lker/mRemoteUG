using System.Globalization;
using System.Xml.Linq;
using mRemoteUG.Config.Serializers.Xml;
using mRemoteUG.Security;
using mRemoteUG.Security.Factories;
using mRemoteUG.Security.SymmetricEncryption;
using mRemoteUG.Tree.Root;
using NUnit.Framework;

namespace mRemoteUG.Tests.Config.Serializers.ConnectionSerializers.Xml
{
    public class XmlRootNodeSerializerTests
    {
        private XmlRootNodeSerializer _rootNodeSerializer;
        private ICryptographyProvider _cryptographyProvider;
        private RootNodeInfo _rootNodeInfo;

        [SetUp]
        public void Setup()
        {
            _rootNodeSerializer = new XmlRootNodeSerializer();
            _cryptographyProvider = new AeadCryptographyProvider();
            _rootNodeInfo = new RootNodeInfo(RootNodeType.Connection);
        }

        [Test]
        public void RootElementNamedConnections()
        {
            var element = _rootNodeSerializer.SerializeRootNodeInfo(_rootNodeInfo, _cryptographyProvider);
            Assert.That(element.Name.LocalName, Is.EqualTo("Connections"));
        }

        [Test]
        public void RootNodeInfoNameSerialized()
        {
            var element = _rootNodeSerializer.SerializeRootNodeInfo(_rootNodeInfo, _cryptographyProvider);
            var attributeValue = element.Attribute(XName.Get("Name"))?.Value;
            Assert.That(attributeValue, Is.EqualTo("Connections"));
        }

        [Test]
        public void EncryptionEngineSerialized()
        {
            var cryptoProvider = new CryptoProviderFactory(BlockCipherEngines.AES, BlockCipherModes.GCM).Build();
            var element = _rootNodeSerializer.SerializeRootNodeInfo(_rootNodeInfo, cryptoProvider);
            var attributeValue = element.Attribute(XName.Get("EncryptionEngine"))?.Value;
            Assert.That(attributeValue, Is.EqualTo(BlockCipherEngines.AES.ToString()));
        }

        [Test]
        public void EncryptionModeSerialized()
        {
            var cryptoProvider = new CryptoProviderFactory(BlockCipherEngines.AES, BlockCipherModes.GCM).Build();
            var element = _rootNodeSerializer.SerializeRootNodeInfo(_rootNodeInfo, cryptoProvider);
            var attributeValue = element.Attribute(XName.Get("BlockCipherMode"))?.Value;
            Assert.That(attributeValue, Is.EqualTo(BlockCipherModes.GCM.ToString()));
        }

        [TestCase(1000)]
        [TestCase(1234)]
        [TestCase(9999)]
        [TestCase(10000)]
        public void KdfIterationsSerialized(int iterations)
        {
            _cryptographyProvider.KeyDerivationIterations = iterations;
            var element = _rootNodeSerializer.SerializeRootNodeInfo(_rootNodeInfo, _cryptographyProvider);
            var attributeValue = element.Attribute(XName.Get("KdfIterations"))?.Value;
            Assert.That(attributeValue, Is.EqualTo(iterations.ToString()));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void FullFileEncryptionFlagSerialized(bool fullFileEncryption)
        {
            var element = _rootNodeSerializer.SerializeRootNodeInfo(_rootNodeInfo, _cryptographyProvider, fullFileEncryption);
            var attributeValue = element.Attribute(XName.Get("FullFileEncryption"))?.Value;
            Assert.That(bool.Parse(attributeValue), Is.EqualTo(fullFileEncryption));
        }

        [TestCase("", "ThisIsNotProtected")]
        [TestCase(null, "ThisIsNotProtected")]
        [TestCase("mR3m", "ThisIsNotProtected")]
        [TestCase("customPassword1", "ThisIsProtected")]
        public void ProtectedStringSerialized(string customPassword, string expectedPlainText)
        {
            _rootNodeInfo.PasswordString = customPassword;
            var element = _rootNodeSerializer.SerializeRootNodeInfo(_rootNodeInfo, _cryptographyProvider);
            var attributeValue = element.Attribute(XName.Get("Protected"))?.Value;
            var attributeValuePlainText = _cryptographyProvider.Decrypt(attributeValue, _rootNodeInfo.PasswordString.ConvertToSecureString());
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
            var element = _rootNodeSerializer.SerializeRootNodeInfo(_rootNodeInfo, _cryptographyProvider);
            var attributeValue = element.Attribute(XName.Get("ConfVersion"))?.Value ?? "";
            Assert.That(double.Parse(attributeValue, CultureInfo.InvariantCulture), Is.EqualTo(2.9));
        }
    }
}