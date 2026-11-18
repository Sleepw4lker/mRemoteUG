using mRemoteUG.Security;
using mRemoteUG.Security.Factories;

using NUnit.Framework;


namespace mRemoteUG.Tests.Security;

[TestFixture]
public class CryptographyProviderFactoryTests
{
    [Test]
    public void CanCreateAeadProviderWithCorrectEngine()
    {
        ICryptographyProvider cryptoProvider =
            new CryptoProviderFactory(BlockCipherEngines.AES, BlockCipherModes.GCM).Build();
        Assert.That(cryptoProvider.CipherEngine, Is.EqualTo(BlockCipherEngines.AES));
    }

    [Test]
    public void CanCreateAeadProviderWithCorrectMode()
    {
        ICryptographyProvider cryptoProvider =
            new CryptoProviderFactory(BlockCipherEngines.AES, BlockCipherModes.GCM).Build();
        Assert.That(cryptoProvider.CipherMode, Is.EqualTo(BlockCipherModes.GCM));
    }

#pragma warning disable 618 // intentionally exercising the retained-for-compat, now-unsupported enum members
    [TestCase(BlockCipherEngines.Twofish, BlockCipherModes.GCM)]
    [TestCase(BlockCipherEngines.Serpent, BlockCipherModes.GCM)]
    [TestCase(BlockCipherEngines.AES, BlockCipherModes.CCM)]
    [TestCase(BlockCipherEngines.AES, BlockCipherModes.EAX)]
    public void UnsupportedEngineOrModeThrows(BlockCipherEngines engine, BlockCipherModes mode)
    {
        Assert.Throws<UnsupportedEncryptionException>(() => new CryptoProviderFactory(engine, mode).Build());
    }

    [Test]
    public void UnsupportedEncryptionExceptionNamesTheRequestedPair()
    {
        UnsupportedEncryptionException? exception = Assert.Throws<UnsupportedEncryptionException>(
            () => new CryptoProviderFactory(BlockCipherEngines.Twofish, BlockCipherModes.GCM).Build());

        Assert.Multiple(() =>
        {
            Assert.That(exception?.Engine, Is.EqualTo(BlockCipherEngines.Twofish));
            Assert.That(exception?.Mode, Is.EqualTo(BlockCipherModes.GCM));
            Assert.That(exception?.Message, Does.Contain("Twofish+GCM"));
        });
    }
#pragma warning restore 618
}
