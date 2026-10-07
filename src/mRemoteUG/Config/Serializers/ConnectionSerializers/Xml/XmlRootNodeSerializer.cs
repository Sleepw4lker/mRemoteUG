using System.Xml.Linq;
using mRemoteUG.Security;
using mRemoteUG.Tree.Root;

namespace mRemoteUG.Config.Serializers.Xml
{
	public class XmlRootNodeSerializer
    {
        public XElement SerializeRootNodeInfo(RootNodeInfo rootNodeInfo, ICryptographyProvider cryptographyProvider, bool fullFileEncryption = false)
        {
            XNamespace xmlNamespace = "http://mremoteng.org";
            var element = new XElement(xmlNamespace + "Connections");
            element.Add(new XAttribute(XNamespace.Xmlns+"mrng", xmlNamespace));
            element.Add(new XAttribute(XName.Get("Name"), rootNodeInfo.Name));
			element.Add(new XAttribute(XName.Get("Export"), "false"));
			element.Add(new XAttribute(XName.Get("EncryptionEngine"), cryptographyProvider.CipherEngine));
            element.Add(new XAttribute(XName.Get("BlockCipherMode"), cryptographyProvider.CipherMode));
            element.Add(new XAttribute(XName.Get("KdfIterations"), cryptographyProvider.KeyDerivationIterations));
            element.Add(new XAttribute(XName.Get("FullFileEncryption"), fullFileEncryption.ToString().ToLowerInvariant()));
            element.Add(CreateProtectedAttribute(rootNodeInfo, cryptographyProvider));
            // 2.9 marks the AES-GCM envelope with a 96-bit nonce. Files at 2.8 and below were
            // written with a 128-bit nonce that AesGcm cannot read, so the deserializer refuses
            // them outright rather than failing later as a wrong password.
            element.Add(new XAttribute(XName.Get("ConfVersion"), "2.9"));
            return element;
        }

        private XAttribute CreateProtectedAttribute(RootNodeInfo rootNodeInfo, ICryptographyProvider cryptographyProvider)
        {
            var attribute = new XAttribute(XName.Get("Protected"), "");
            var plainText = rootNodeInfo.Password ? "ThisIsProtected" : "ThisIsNotProtected";
            var encryptionPassword = rootNodeInfo.PasswordString.ConvertToSecureString();
            attribute.Value = cryptographyProvider.Encrypt(plainText, encryptionPassword);
            return attribute;
        }
    }
}