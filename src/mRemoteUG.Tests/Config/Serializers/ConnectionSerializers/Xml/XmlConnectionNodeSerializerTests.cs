using System.Reflection;
using System.Xml.Linq;

using mRemoteUG.Config.Serializers.Xml;
using mRemoteUG.Connection;
using mRemoteUG.Connection.Protocol;
using mRemoteUG.Security;
using mRemoteUG.Security.SymmetricEncryption;

using NUnit.Framework;

namespace mRemoteUG.Tests.Config.Serializers.ConnectionSerializers.Xml;

/// <summary>
/// The attributes one connection element carries, and what the save filter leaves out of it.
/// </summary>
/// <remarks>
/// Every value here is set to something other than what an empty attribute or a missing one
/// would read back as. The document-level tests only ever serialize default connections, and
/// against those "wrote the value", "wrote an empty string" and "wrote nothing" all look alike.
/// </remarks>
public class XmlConnectionNodeSerializerTests
{
    // ProtocolBase has no abstract members; a minimal subclass avoids
    // RdpProtocol's dependency on the FrmMain singleton (STA/WinForms-only).
    private sealed class DummyProtocol : ProtocolBase
    {
    }

    private const string EncryptionPassword = "mR3m";
    private readonly AeadCryptographyProvider _cryptographyProvider = new AeadCryptographyProvider();

    /// <summary>The name of every inheritance flag; the attribute is that name prefixed with "Inherit".</summary>
    private static IEnumerable<string> InheritanceFlags()
    {
        return new ConnectionInfoInheritance(null, true).GetProperties().Select(property => property.Name);
    }

    [TestCase(nameof(SaveFilter.SaveUsername), "Username")]
    [TestCase(nameof(SaveFilter.SaveUsername), "RDGatewayUsername")]
    [TestCase(nameof(SaveFilter.SaveDomain), "Domain")]
    [TestCase(nameof(SaveFilter.SaveDomain), "RDGatewayDomain")]
    [TestCase(nameof(SaveFilter.SavePassword), "Password")]
    [TestCase(nameof(SaveFilter.SavePassword), "RDGatewayPassword")]
    public void ACredentialIsWrittenEmptyWhenItsSaveFilterFlagIsOff(string saveFilterFlag, string attributeName)
    {
        var saveFilter = new SaveFilter();
        typeof(SaveFilter).GetProperty(saveFilterFlag)!.SetValue(saveFilter, false);

        XElement element = Serialize(ConnectionWithCredentials(), saveFilter);

        Assert.That(element.Attribute(attributeName)?.Value, Is.Empty);
    }

    [TestCase("Username", "user")]
    [TestCase("RDGatewayUsername", "gateway-user")]
    [TestCase("Domain", "domain")]
    [TestCase("RDGatewayDomain", "gateway-domain")]
    public void ACredentialIsWrittenWhenTheSaveFilterAllowsIt(string attributeName, string expectedValue)
    {
        XElement element = Serialize(ConnectionWithCredentials(), new SaveFilter());

        Assert.That(element.Attribute(attributeName)?.Value, Is.EqualTo(expectedValue));
    }

    [TestCase("Password", "secret")]
    [TestCase("RDGatewayPassword", "gateway-secret")]
    public void APasswordIsWrittenEncryptedWhenTheSaveFilterAllowsIt(string attributeName, string expectedPlainText)
    {
        XElement element = Serialize(ConnectionWithCredentials(), new SaveFilter());

        string cipherText = element.Attribute(attributeName)?.Value ?? "";
        Assert.That(_cryptographyProvider.Decrypt(cipherText, EncryptionPassword.ConvertToSecureString()),
                    Is.EqualTo(expectedPlainText));
    }

    [Test]
    public void AnInheritedPasswordIsWrittenEmpty()
    {
        ConnectionInfo connectionInfo = ConnectionWithCredentials();
        connectionInfo.Inheritance.Password = true;

        XElement element = Serialize(connectionInfo, new SaveFilter());

        Assert.That(element.Attribute("Password")?.Value, Is.Empty);
    }

    [TestCase(0, "false")]
    [TestCase(1, "true")]
    [TestCase(2, "true")]
    public void ConnectedSaysWhetherTheConnectionHasAnOpenSession(int openSessions, string expectedValue)
    {
        ConnectionInfo connectionInfo = ConnectionWithCredentials();
        for (int i = 0; i < openSessions; i++)
        {
            connectionInfo.OpenConnections.Add(new DummyProtocol());
        }

        XElement element = Serialize(connectionInfo, new SaveFilter());

        Assert.That(element.Attribute("Connected")?.Value, Is.EqualTo(expectedValue));
    }

    [TestCaseSource(nameof(InheritanceFlags))]
    public void AnInheritanceFlagThatIsOnIsWrittenTrue(string inheritanceFlag)
    {
        ConnectionInfo connectionInfo = ConnectionWithCredentials();
        InheritanceProperty(inheritanceFlag).SetValue(connectionInfo.Inheritance, true);

        XElement element = Serialize(connectionInfo, new SaveFilter());

        Assert.That(element.Attribute("Inherit" + inheritanceFlag)?.Value, Is.EqualTo("true"));
    }

    [TestCaseSource(nameof(InheritanceFlags))]
    public void AnInheritanceFlagIsWrittenFalseWhenInheritanceIsNotSaved(string inheritanceFlag)
    {
        ConnectionInfo connectionInfo = ConnectionWithCredentials();
        InheritanceProperty(inheritanceFlag).SetValue(connectionInfo.Inheritance, true);

        XElement element = Serialize(connectionInfo, new SaveFilter { SaveInheritance = false });

        Assert.That(element.Attribute("Inherit" + inheritanceFlag)?.Value, Is.EqualTo("false"));
    }

    private XElement Serialize(ConnectionInfo connectionInfo, SaveFilter saveFilter)
    {
        var serializer = new XmlConnectionNodeSerializer(
            _cryptographyProvider, EncryptionPassword.ConvertToSecureString(), saveFilter);
        return serializer.Serialize(connectionInfo);
    }

    private static PropertyInfo InheritanceProperty(string inheritanceFlag)
    {
        return typeof(ConnectionInfoInheritance).GetProperty(inheritanceFlag)!;
    }

    private static ConnectionInfo ConnectionWithCredentials()
    {
        var connectionInfo = new ConnectionInfo { Name = "con" };
        // DefaultConnectionInheritance.Instance is process-wide and other fixtures turn it on;
        // a flag left on here would hide the credential it covers.
        connectionInfo.Inheritance.TurnOffInheritanceCompletely();
        connectionInfo.Username = "user";
        connectionInfo.Domain = "domain";
        connectionInfo.Password = "secret";
        connectionInfo.RDGatewayUsername = "gateway-user";
        connectionInfo.RDGatewayDomain = "gateway-domain";
        connectionInfo.RDGatewayPassword = "gateway-secret";
        return connectionInfo;
    }
}
