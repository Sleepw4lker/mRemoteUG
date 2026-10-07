using System;
using System.Security;
using mRemoteUG.Security;
using mRemoteUG.Security.Authentication;
using mRemoteUG.Security.Factories;
using mRemoteUG.Tools;
using mRemoteUG.Tree.Root;

namespace mRemoteUG.Config.Serializers
{
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


        public XmlConnectionsDecryptor(BlockCipherEngines blockCipherEngine, BlockCipherModes blockCipherMode, RootNodeInfo rootNodeInfo)
        {
            _cryptographyProvider = new CryptoProviderFactory(blockCipherEngine, blockCipherMode).Build();
            _rootNodeInfo = rootNodeInfo;
        }

        public string Decrypt(string plainText)
        {
            return plainText == "" ? "" : _cryptographyProvider.Decrypt(plainText, _rootNodeInfo.PasswordString.ConvertToSecureString());
        }

        public bool ConnectionsFileIsAuthentic(string protectedString, SecureString password)
        {
            var connectionsFileIsNotEncrypted = false;
            try
            {
                connectionsFileIsNotEncrypted = _cryptographyProvider.Decrypt(protectedString, _rootNodeInfo.PasswordString.ConvertToSecureString()) == "ThisIsNotProtected";
            }
            catch (EncryptionException)
            {
                // Expected, and the answer to the question being asked: the file did not
                // decrypt with the default password, so it is protected with another one.
                // Fall through to Authenticate, which prompts for it.
            }
            return connectionsFileIsNotEncrypted || Authenticate(protectedString, _rootNodeInfo.PasswordString.ConvertToSecureString());
        }

        private bool Authenticate(string cipherText, SecureString password)
        {
            var authenticator = new PasswordAuthenticator(_cryptographyProvider, cipherText, AuthenticationRequestor);
            var authenticated = authenticator.Authenticate(password);

            if (!authenticated)
                return false;

            _rootNodeInfo.PasswordString = authenticator.LastAuthenticatedPassword.ConvertToUnsecureString();
            return true;
        }
    }
}