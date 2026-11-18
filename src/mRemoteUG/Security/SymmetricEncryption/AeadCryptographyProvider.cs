/*
 * Initial work:
 * This work (Modern Encryption of a String C#, by James Tuley),
 * identified by James Tuley, is free of known copyright restrictions.
 * https://gist.github.com/4336842
 * http://creativecommons.org/publicdomain/mark/1.0/
 */

using System.Security;
using System.Security.Cryptography;
using System.Text;

using mRemoteUG.Security.KeyDerivation;
// ReSharper disable ArrangeAccessorOwnerBody

namespace mRemoteUG.Security.SymmetricEncryption;

/// <summary>AES-GCM, through the platform implementation in System.Security.Cryptography.</summary>
/// <remarks>
/// The envelope is salt 16, nonce 12, ciphertext, tag 16, base64-encoded. The nonce is 96 bits
/// because that is the only size <c>AesGcm</c> accepts, and it is what NIST SP 800-38D
/// recommends: any other length has to be folded down with GHASH first, which is why this used
/// to go through bcrypt.dll directly.
/// </remarks>
public class AeadCryptographyProvider : ICryptographyProvider
{
    private readonly Encoding _encoding;

    //Preconfigured Encryption Parameters
    // 96 bits. AesGcm accepts no other nonce size; see the remarks on the class.
    protected virtual int NonceBitSize { get; set; } = 96;
    protected virtual int MacBitSize { get; set; } = 128;
    protected virtual int KeyBitSize { get; set; } = 256;

    //Preconfigured Password Key Derivation Parameters
    protected virtual int SaltBitSize { get; set; } = 128;
    public virtual int KeyDerivationIterations { get; set; } = 1000;
    protected virtual int MinPasswordLength { get; set; } = 1;


    public int BlockSizeInBytes
    {
        get { return 16; }
    }

    public BlockCipherEngines CipherEngine
    {
        get { return BlockCipherEngines.AES; }
    }

    public BlockCipherModes CipherMode
    {
        get { return BlockCipherModes.GCM; }
    }

    public AeadCryptographyProvider()
    {
        _encoding = Encoding.UTF8;
    }

    public AeadCryptographyProvider(Encoding encoding)
    {
        _encoding = encoding;
    }

    public string Encrypt(string plainText, SecureString encryptionKey)
    {
        string encryptedText = SimpleEncryptWithPassword(plainText, encryptionKey.ConvertToUnsecureString());
        return encryptedText;
    }

    private string SimpleEncryptWithPassword(string secretMessage, string password, byte[]? nonSecretPayload = null)
    {
        if (string.IsNullOrEmpty(secretMessage))
        {
            return ""; //throw new ArgumentException(@"Secret Message Required!", nameof(secretMessage));
        }

        byte[] plainText = _encoding.GetBytes(secretMessage);
        byte[] cipherText = SimpleEncryptWithPassword(plainText, password, nonSecretPayload);
        return Convert.ToBase64String(cipherText);
    }

    private byte[] SimpleEncryptWithPassword(byte[] secretMessage, string password, byte[]? nonSecretPayload = null)
    {
        nonSecretPayload = nonSecretPayload ?? Array.Empty<byte>();

        //User Error Checks
        if (string.IsNullOrWhiteSpace(password) || password.Length < MinPasswordLength)
        {
            throw new ArgumentException(
                $"Must have a password of at least {MinPasswordLength} characters!",
                nameof(password));
        }

        if (secretMessage == null || secretMessage.Length == 0)
        {
            throw new ArgumentException(@"Secret Message Required!", nameof(secretMessage));
        }

        //Use Random Salt to minimize pre-generated weak password attacks.
        byte[] salt = GenerateSalt();

        //Generate Key
        var keyDerivationFunction = new Pkcs5S2KeyGenerator(KeyBitSize, KeyDerivationIterations);
        byte[] key = keyDerivationFunction.DeriveKey(password, salt);

        //Create Full Non Secret Payload
        byte[] payload = new byte[salt.Length + nonSecretPayload.Length];
        Array.Copy(nonSecretPayload, payload, nonSecretPayload.Length);
        Array.Copy(salt, 0, payload, nonSecretPayload.Length, salt.Length);

        return SimpleEncrypt(secretMessage, key, payload);
    }

    private byte[] SimpleEncrypt(byte[] secretMessage, byte[] key, byte[]? nonSecretPayload = null)
    {
        //User Error Checks
        if (key == null || key.Length != KeyBitSize / 8)
        {
            throw new ArgumentException($"Key needs to be {KeyBitSize} bit!", nameof(key));
        }

        if (secretMessage == null || secretMessage.Length == 0)
        {
            throw new ArgumentException(@"Secret Message Required!", nameof(secretMessage));
        }

        //Non-secret Payload Optional
        nonSecretPayload = nonSecretPayload ?? Array.Empty<byte>();

        //Using random nonce large enough not to repeat
        byte[] nonce = GenerateRandomBytes(NonceBitSize / 8);

        byte[] rawCipherText = new byte[secretMessage.Length];
        byte[] tag = new byte[MacBitSize / 8];
        using (var aesGcm = new AesGcm(key, tag.Length))
        {
            aesGcm.Encrypt(nonce, secretMessage, rawCipherText, tag, nonSecretPayload);
        }

        //Assemble Message
        var combinedStream = new MemoryStream();
        using (var binaryWriter = new BinaryWriter(combinedStream))
        {
            //Prepend Authenticated Payload
            binaryWriter.Write(nonSecretPayload);
            //Prepend Nonce
            binaryWriter.Write(nonce);
            //Write Cipher Text, followed by the authentication tag (matches the previous
            //BouncyCastle-based layout, where GCM's DoFinal appended the tag to the ciphertext)
            binaryWriter.Write(rawCipherText);
            binaryWriter.Write(tag);
        }
        return combinedStream.ToArray();
    }


    public string Decrypt(string cipherText, SecureString decryptionKey)
    {
        string decryptedText = SimpleDecryptWithPassword(cipherText, decryptionKey);
        return decryptedText;
    }

    private string SimpleDecryptWithPassword(
        string encryptedMessage,
        SecureString decryptionKey,
        int nonSecretPayloadLength = 0)
    {
        if (string.IsNullOrWhiteSpace(encryptedMessage))
        {
            return ""; //throw new ArgumentException(@"Encrypted Message Required!", nameof(encryptedMessage));
        }

        byte[] cipherText = Convert.FromBase64String(encryptedMessage);
        byte[] plainText = SimpleDecryptWithPassword(
            cipherText,
            decryptionKey.ConvertToUnsecureString(),
            nonSecretPayloadLength);
        return _encoding.GetString(plainText);
    }

    private byte[] SimpleDecryptWithPassword(byte[] encryptedMessage, string password, int nonSecretPayloadLength = 0)
    {
        // A key no password could be cannot authenticate anything, which is what
        // ICryptographyProvider.Decrypt says EncryptionException means. The prompt treats that
        // as a wrong password and asks again; an ArgumentException would end the load (#199).
        // Encrypt keeps its ArgumentException: there an unusable key is the caller's mistake.
        if (string.IsNullOrWhiteSpace(password) || password.Length < MinPasswordLength)
        {
            throw new EncryptionException(
                $"Must have a password of at least {MinPasswordLength} characters!");
        }

        if (encryptedMessage == null || encryptedMessage.Length == 0)
        {
            throw new ArgumentException(@"Encrypted Message Required!", nameof(encryptedMessage));
        }

        //Grab Salt from Payload
        byte[] salt = new byte[SaltBitSize / 8];
        Array.Copy(encryptedMessage, nonSecretPayloadLength, salt, 0, salt.Length);

        //Generate Key
        var keyDerivationFunction = new Pkcs5S2KeyGenerator(KeyBitSize, KeyDerivationIterations);
        byte[] key = keyDerivationFunction.DeriveKey(password, salt);

        return SimpleDecrypt(encryptedMessage, key, salt.Length + nonSecretPayloadLength);
    }

    private byte[] SimpleDecrypt(byte[] encryptedMessage, byte[] key, int nonSecretPayloadLength = 0)
    {
        //User Error Checks
        if (key == null || key.Length != KeyBitSize / 8)
        {
            throw new ArgumentException($"Key needs to be {KeyBitSize} bit!", nameof(key));
        }

        if (encryptedMessage == null || encryptedMessage.Length == 0)
        {
            throw new ArgumentException(@"Encrypted Message Required!", nameof(encryptedMessage));
        }

        var cipherStream = new MemoryStream(encryptedMessage);
        using (var cipherReader = new BinaryReader(cipherStream))
        {
            //Grab Payload
            byte[] nonSecretPayload = cipherReader.ReadBytes(nonSecretPayloadLength);

            //Grab Nonce
            byte[] nonce = cipherReader.ReadBytes(NonceBitSize / 8);

            int tagSizeInBytes = MacBitSize / 8;
            int remainingBytes = encryptedMessage.Length - nonSecretPayloadLength - nonce.Length;
            int cipherTextLength = remainingBytes - tagSizeInBytes;
            if (cipherTextLength < 0)
            {
                throw new EncryptionException(Language.strErrorDecryptionFailed);
            }

            byte[] cipherText = cipherReader.ReadBytes(cipherTextLength);
            byte[] tag = cipherReader.ReadBytes(tagSizeInBytes);

            byte[] plainText = new byte[cipherText.Length];
            try
            {
                using (var aesGcm = new AesGcm(key, tag.Length))
                {
                    aesGcm.Decrypt(nonce, cipherText, tag, plainText, nonSecretPayload);
                }
            }
            catch (CryptographicException e)
            {
                // AuthenticationTagMismatchException for a wrong key or tampered bytes, and
                // ArgumentException-shaped failures never reach here - a truncated envelope is
                // caught by the cipherTextLength check above.
                throw new EncryptionException(Language.strErrorDecryptionFailed, e);
            }

            return plainText;
        }
    }

    private byte[] GenerateSalt()
    {
        return GenerateRandomBytes(SaltBitSize / 8);
    }

    private static byte[] GenerateRandomBytes(int count)
    {
        // RandomNumberGenerator.GetBytes replaces RNGCryptoServiceProvider, which is
        // obsolete (SYSLIB0023). Same source of randomness, no disposable to manage.
        return RandomNumberGenerator.GetBytes(count);
    }
}
