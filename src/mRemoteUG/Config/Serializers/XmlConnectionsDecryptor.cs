using System.Security;

using mRemoteUG.Security;
using mRemoteUG.Security.Authentication;
using mRemoteUG.Security.Factories;
using mRemoteUG.Tree.Root;

namespace mRemoteUG.Config.Serializers;

public class XmlConnectionsDecryptor
{
    private readonly ICryptographyProvider _cryptographyProvider;
    private readonly RootNodeInfo _rootNodeInfo;

    public Func<SecureString?>? AuthenticationRequestor { get; set; }

    public int KeyDerivationIterations
    {
        get { return _cryptographyProvider.KeyDerivationIterations; }
        set { _cryptographyProvider.KeyDerivationIterations = value; }
    }


    public XmlConnectionsDecryptor(
        BlockCipherEngines blockCipherEngine,
        BlockCipherModes blockCipherMode,
        RootNodeInfo rootNodeInfo)
    {
        _cryptographyProvider = new CryptoProviderFactory(blockCipherEngine, blockCipherMode).Build();
        _rootNodeInfo = rootNodeInfo;
    }

    public string Decrypt(string plainText)
    {
        return plainText == ""
            ? ""
            : _cryptographyProvider.Decrypt(plainText, _rootNodeInfo.PasswordString.ConvertToSecureString());
    }

    public bool ConnectionsFileIsAuthentic(string protectedString, SecureString password)
    {
        bool connectionsFileIsNotEncrypted = false;
        try
        {
            connectionsFileIsNotEncrypted =
                _cryptographyProvider.Decrypt(protectedString, _rootNodeInfo.PasswordString.ConvertToSecureString())
                == "ThisIsNotProtected";
        }
        catch (EncryptionException)
        {
            // Expected, and the answer to the question being asked: the file did not
            // decrypt with the default password, so it is protected with another one.
            // Fall through to Authenticate, which prompts for it.
        }
        return connectionsFileIsNotEncrypted
            || Authenticate(protectedString, _rootNodeInfo.PasswordString.ConvertToSecureString());
    }

    private bool Authenticate(string cipherText, SecureString password)
    {
        // Without a requestor nobody can be asked for the password. The import path builds its
        // deserializer that way, and PasswordAuthenticator refused the null with an
        // ArgumentNullException on this same line; this keeps the throw and names the cause.
        Func<SecureString?> authenticationRequestor = AuthenticationRequestor
            ?? throw new InvalidOperationException(
                "The connections file is protected with a password and there is no way to ask for it.");
        var authenticator = new PasswordAuthenticator(_cryptographyProvider, cipherText, authenticationRequestor);
        bool authenticated = authenticator.Authenticate(password);

        if (!authenticated)
        {
            return false;
        }

        // Not null: Authenticate returned true, which its [MemberNotNullWhen] says sets it. The
        // compiler applies that attribute only to calls on `this`, not on another instance.
        _rootNodeInfo.PasswordString = authenticator.LastAuthenticatedPassword!.ConvertToUnsecureString();
        return true;
    }
}
