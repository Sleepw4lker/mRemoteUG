using System.Security;

using mRemoteUG.Security;
using mRemoteUG.Security.Factories;
using mRemoteUG.Security.SymmetricEncryption;

using NUnit.Framework;
using NUnit.Framework.Constraints;


namespace mRemoteUG.Tests.Security;

public class AeadCryptographyProviderTests
{
    // NUnit builds the fixture once and no test here reassigns any of these, so they are
    // initialized where they are declared. That retires a [SetUp] whose only job was to
    // assign them, and a [TearDown] that nulled one of them for no one: NUnit discards
    // the fixture regardless.
    private readonly AeadCryptographyProvider _cryptographyProvider = new AeadCryptographyProvider();
    private readonly SecureString _encryptionKey = "mypassword111111".ConvertToSecureString();
    private readonly string _plainText = "MySecret!";

    [Test]
    public void GetBlockSizeReturnsProperValueForAes()
    {
        Assert.That(_cryptographyProvider.BlockSizeInBytes, Is.EqualTo(16));
    }

    [Test]
    public void EncryptionOutputsBase64String()
    {
        string cipherText = _cryptographyProvider.Encrypt(_plainText, _encryptionKey);
        byte[] buffer = new byte[cipherText.Length];
        Assert.That(Convert.TryFromBase64String(cipherText, buffer, out _), Is.True);
    }

    [Test]
    public void DecryptedTextIsEqualToOriginalPlainText()
    {
        ICryptographyProvider cryptoProvider =
            new CryptoProviderFactory(BlockCipherEngines.AES, BlockCipherModes.GCM).Build();
        string cipherText = cryptoProvider.Encrypt(_plainText, _encryptionKey);
        string? decryptedCipherText = cryptoProvider.Decrypt(cipherText, _encryptionKey);
        Assert.That(decryptedCipherText, Is.EqualTo(_plainText));
    }

    [Test]
    public void EncryptingTheSameValueReturnsNewCipherTextEachTime()
    {
        string cipherText1 = _cryptographyProvider.Encrypt(_plainText, _encryptionKey);
        string cipherText2 = _cryptographyProvider.Encrypt(_plainText, _encryptionKey);
        Assert.That(cipherText1, Is.Not.EqualTo(cipherText2));
    }

    [Test]
    public void DecryptionFailureThrowsException()
    {
        string cipherText = _cryptographyProvider.Encrypt(_plainText, _encryptionKey);
        ActualValueDelegate<string?> decryptMethod =
            () => _cryptographyProvider.Decrypt(cipherText, "wrongKey".ConvertToSecureString());
        Assert.That(decryptMethod, Throws.TypeOf<EncryptionException>());
    }

    [Test]
    public void DecryptingWithAWhitespaceKeyIsRejectedBeforeDecryption()
    {
        string cipherText = _cryptographyProvider.Encrypt(_plainText, _encryptionKey);

        // No key made only of spaces can authenticate anything, so it is rejected the way a wrong
        // key is: PasswordAuthenticator reads EncryptionException as "ask again" (#199).
        Assert.Throws<EncryptionException>(
            () => _cryptographyProvider.Decrypt(cipherText, "   ".ConvertToSecureString()));
    }

    [Test]
    public void AOneCharacterKeyRoundTrips()
    {
        SecureString key = "k".ConvertToSecureString();

        string cipherText = _cryptographyProvider.Encrypt(_plainText, key);

        Assert.That(_cryptographyProvider.Decrypt(cipherText, key), Is.EqualTo(_plainText));
    }

    [Test]
    public void GetCipherEngine()
    {
        ICryptographyProvider cryptoProvider =
            new CryptoProviderFactory(BlockCipherEngines.AES, BlockCipherModes.GCM).Build();
        Assert.That(cryptoProvider.CipherEngine, Is.EqualTo(BlockCipherEngines.AES));
    }

    [Test]
    public void GetCipherMode()
    {
        ICryptographyProvider cryptoProvider =
            new CryptoProviderFactory(BlockCipherEngines.AES, BlockCipherModes.GCM).Build();
        Assert.That(cryptoProvider.CipherMode, Is.EqualTo(BlockCipherModes.GCM));
    }

    [Test]
    public void TamperedCipherTextIsRejected()
    {
        string cipherText = _cryptographyProvider.Encrypt(_plainText, _encryptionKey);
        byte[] raw = Convert.FromBase64String(cipherText);

        // Flip a bit inside the ciphertext body, past the 16-byte salt and 12-byte nonce.
        raw[30] ^= 0x01;

        Assert.Throws<EncryptionException>(
            () => _cryptographyProvider.Decrypt(Convert.ToBase64String(raw), _encryptionKey));
    }

    [Test]
    public void TamperedAuthenticationTagIsRejected()
    {
        string cipherText = _cryptographyProvider.Encrypt(_plainText, _encryptionKey);
        byte[] raw = Convert.FromBase64String(cipherText);

        // The tag is the last 16 bytes.
        raw[raw.Length - 1] ^= 0x01;

        Assert.Throws<EncryptionException>(
            () => _cryptographyProvider.Decrypt(Convert.ToBase64String(raw), _encryptionKey));
    }

    /// <summary>
    /// Pins the on-disk envelope: salt 16, nonce 12, ciphertext, tag 16. The nonce length is
    /// not a free choice - AesGcm accepts 12 bytes and nothing else - so a change here means
    /// every stored password has become unreadable, which should fail loudly rather than in
    /// the field.
    /// </summary>
    [Test]
    public void EnvelopeIsSaltThenNonceThenCipherTextThenTag()
    {
        byte[] raw = Convert.FromBase64String(_cryptographyProvider.Encrypt(_plainText, _encryptionKey));
        Assert.That(raw.Length, Is.EqualTo(16 + 12 + _plainText.Length + 16));
    }
}
