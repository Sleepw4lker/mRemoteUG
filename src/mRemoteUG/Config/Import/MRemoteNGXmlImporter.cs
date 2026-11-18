using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security;

using mRemoteUG.App;
using mRemoteUG.Config.DataProviders;
using mRemoteUG.Config.Serializers.Xml;
using mRemoteUG.Container;
using mRemoteUG.Messages;
using mRemoteUG.Security;
using mRemoteUG.Tree;


namespace mRemoteUG.Config.Import;

// ReSharper disable once InconsistentNaming
public class MRemoteNGXmlImporter : IConnectionImporter<string>
{
    private readonly Func<string, SecureString?> _passwordRequestor;

    /// <param name="passwordRequestor">Asks for the password of a file protected with one of its own,
    /// called with the file name. Null is the user cancelling.</param>
    public MRemoteNGXmlImporter(Func<string, SecureString?> passwordRequestor)
    {
        ArgumentNullException.ThrowIfNull(passwordRequestor);
        _passwordRequestor = passwordRequestor;
    }

    [SuppressMessage("Performance", "CA1863",
                     Justification = "The format is a localized resource read at run time; "
                                     + "a cached CompositeFormat would freeze the UI language.")]
    public void Import(string fileName, ContainerInfo destinationContainer)
    {
        if (fileName == null)
        {
            Runtime.MessageCollector.AddMessage(MessageClass.ErrorMsg, "Unable to import file. File path is null.");
            return;
        }

        if (!File.Exists(fileName))
        {
            Runtime.MessageCollector.AddMessage(
                MessageClass.ErrorMsg,
                $"Unable to import file. File does not exist. Path: {fileName}");
            return;
        }

        var dataProvider = new FileDataProvider(fileName);
        string xmlString = dataProvider.Load();
        // The deserializer returns null both when the user cancels the prompt and when no answer
        // was the password; only the prompt itself can tell the two apart.
        bool cancelled = false;
        SecureString? RequestPassword()
        {
            SecureString? password = _passwordRequestor(fileName);
            cancelled |= password == null;
            return password;
        }

        var xmlConnectionsDeserializer = new XmlConnectionsDeserializer(RequestPassword, fileName);
        ConnectionTreeModel? connectionTreeModel = xmlConnectionsDeserializer.Deserialize(xmlString, true);
        if (connectionTreeModel == null)
        {
            if (cancelled)
            {
                Runtime.MessageCollector.AddMessage(MessageClass.InformationMsg, $"Import of {fileName} cancelled");
                return;
            }

            // Every answer was wrong, or empty, which the authenticator takes as giving up. The
            // caller reports this as a failed import, as it does any other exception.
            throw new EncryptionException(
                string.Format(CultureInfo.CurrentCulture, Language.strErrorDecryptionFailed, fileName));
        }

        var rootImportContainer = new ContainerInfo { Name = Path.GetFileNameWithoutExtension(fileName) };
        rootImportContainer.AddChildRange(connectionTreeModel.RootNodes.First().Children.ToArray());
        destinationContainer.AddChild(rootImportContainer);
    }
}
