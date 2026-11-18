using System.Security;
using System.Xml;
using System.Xml.Linq;

using mRemoteUG.Security;

namespace mRemoteUG.Config.Serializers.Xml;

public class XmlConnectionsDocumentEncryptor
{
    private readonly ICryptographyProvider _cryptographyProvider;

    public XmlConnectionsDocumentEncryptor(ICryptographyProvider cryptographyProvider)
    {
        _cryptographyProvider = cryptographyProvider;
    }

    public XDocument EncryptDocument(XDocument documentToEncrypt, SecureString encryptionKey)
    {
        // A document without a root used to fail with a NullReferenceException at the first
        // read of it below; it still fails there, now saying why.
        XElement root = documentToEncrypt.Root
            ?? throw new ArgumentException("The document has no root element.", nameof(documentToEncrypt));
        string contentToEncrypt = GetContentToEncrypt(root);
        string encryptedContent = _cryptographyProvider.Encrypt(contentToEncrypt, encryptionKey);
        XDocument encryptedDocument = ReplaceInnerXml(root, encryptedContent);
        return encryptedDocument;
    }

    private static string GetContentToEncrypt(XNode element)
    {
        XmlReader reader = element.CreateReader();
        reader.MoveToContent();
        return reader.ReadInnerXml();
    }

    private static XDocument ReplaceInnerXml(XElement originalRoot, string newContent)
    {
        XElement newRootElement = ShallowCloneRootNode(originalRoot);
        newRootElement.SetValue(newContent);
        return new XDocument(newRootElement);
    }

    private static XElement ShallowCloneRootNode(XElement originalElement)
    {
        var newElement = new XElement(originalElement.Name);
        foreach (XAttribute attribute in originalElement.Attributes())
        {
            newElement.Add(new XAttribute(attribute));
        }

        return newElement;
    }
}
