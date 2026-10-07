#nullable enable
using System.Security;

namespace mRemoteUG.Security
{
    public interface ICryptographyProvider
    {
        int BlockSizeInBytes { get; }

        BlockCipherEngines CipherEngine { get; }

        BlockCipherModes CipherMode { get; }

        int KeyDerivationIterations { get; set; }

        string Encrypt(string plainText, SecureString encryptionKey);

        /// <summary>
        /// Decrypts the given text, or returns null if it could not be authenticated with
        /// the given key.
        /// </summary>
        /// <remarks>
        /// A wrong key is not an exceptional condition here: it is how the connections file
        /// asks for a password. AeadCryptographyProvider returns null for a failed AES-GCM tag
        /// check, and XmlConnectionsDecryptor reads that as "prompt for the real password".
        /// </remarks>
        string? Decrypt(string cipherText, SecureString decryptionKey);
    }
}