using System.Security;

namespace mRemoteUG.Security;

public interface ICryptographyProvider
{
    int BlockSizeInBytes { get; }

    BlockCipherEngines CipherEngine { get; }

    BlockCipherModes CipherMode { get; }

    int KeyDerivationIterations { get; set; }

    string Encrypt(string plainText, SecureString encryptionKey);

    /// <summary>
    /// Decrypts the given text.
    /// </summary>
    /// <exception cref="EncryptionException">
    /// The text cannot be authenticated with the given key.
    /// </exception>
    /// <remarks>
    /// A wrong key is not a null result: callers tell a wrong password by this exception,
    /// as AesGcm does with AuthenticationTagMismatchException. PasswordAuthenticator asks
    /// for the password again on it, and XmlConnectionsDecryptor reads it as "the file is
    /// protected with another password". Damaged input (text that is not base64, an empty
    /// key) throws something else, and is not taken for a wrong password.
    /// </remarks>
    string Decrypt(string cipherText, SecureString decryptionKey);
}
