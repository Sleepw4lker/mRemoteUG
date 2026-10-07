#nullable enable
using System;
using System.Security;
using mRemoteUG.Security;
using mRemoteUG.Security.Factories;
using mRemoteUG.Security.SymmetricEncryption;
using NUnit.Framework;
using NUnit.Framework.Constraints;


namespace mRemoteUG.Tests.Security
{
    public class AeadCryptographyProviderTests
    {
        // NUnit builds the fixture once and no test here reassigns any of these, so they are
        // initialized where they are declared. That retires a [SetUp] whose only job was to
        // assign them, and a [TearDown] that nulled one of them for no one: NUnit discards
        // the fixture regardless.
        private readonly ICryptographyProvider _cryptographyProvider = new AeadCryptographyProvider();
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
            var cipherText = _cryptographyProvider.Encrypt(_plainText, _encryptionKey);
            Assert.That(cipherText.IsBase64String, Is.True);
        }

        [Test]
        public void DecryptedTextIsEqualToOriginalPlainText()
        {
            var cryptoProvider = new CryptoProviderFactory(BlockCipherEngines.AES, BlockCipherModes.GCM).Build();
            var cipherText = cryptoProvider.Encrypt(_plainText, _encryptionKey);
            var decryptedCipherText = cryptoProvider.Decrypt(cipherText, _encryptionKey);
            Assert.That(decryptedCipherText, Is.EqualTo(_plainText));
        }

        [Test]
        public void EncryptingTheSameValueReturnsNewCipherTextEachTime()
        {
            var cipherText1 = _cryptographyProvider.Encrypt(_plainText, _encryptionKey);
            var cipherText2 = _cryptographyProvider.Encrypt(_plainText, _encryptionKey);
            Assert.That(cipherText1, Is.Not.EqualTo(cipherText2));
        }

        [Test]
        public void DecryptionFailureThrowsException()
        {
            var cipherText = _cryptographyProvider.Encrypt(_plainText, _encryptionKey);
            ActualValueDelegate<string?> decryptMethod = () => _cryptographyProvider.Decrypt(cipherText, "wrongKey".ConvertToSecureString());
            Assert.That(decryptMethod, Throws.TypeOf<EncryptionException>());
        }

        [Test]
        public void GetCipherEngine()
        {
            var cryptoProvider = new CryptoProviderFactory(BlockCipherEngines.AES, BlockCipherModes.GCM).Build();
            Assert.That(cryptoProvider.CipherEngine, Is.EqualTo(BlockCipherEngines.AES));
        }

        [Test]
        public void GetCipherMode()
        {
            var cryptoProvider = new CryptoProviderFactory(BlockCipherEngines.AES, BlockCipherModes.GCM).Build();
            Assert.That(cryptoProvider.CipherMode, Is.EqualTo(BlockCipherModes.GCM));
        }

        [Test]
        public void TamperedCipherTextIsRejected()
        {
            var cipherText = _cryptographyProvider.Encrypt(_plainText, _encryptionKey);
            var raw = Convert.FromBase64String(cipherText);

            // Flip a bit inside the ciphertext body, past the 16-byte salt and 12-byte nonce.
            raw[30] ^= 0x01;

            Assert.Throws<EncryptionException>(
                () => _cryptographyProvider.Decrypt(Convert.ToBase64String(raw), _encryptionKey));
        }

        [Test]
        public void TamperedAuthenticationTagIsRejected()
        {
            var cipherText = _cryptographyProvider.Encrypt(_plainText, _encryptionKey);
            var raw = Convert.FromBase64String(cipherText);

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
            var raw = Convert.FromBase64String(_cryptographyProvider.Encrypt(_plainText, _encryptionKey));
            Assert.That(raw.Length, Is.EqualTo(16 + 12 + _plainText.Length + 16));
        }
    }
}
