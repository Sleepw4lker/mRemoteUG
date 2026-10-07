#nullable enable
namespace mRemoteUG.Security.Factories
{
    public class CryptoProviderFactoryFromSettings : ICryptoProviderFactory
    {
        public ICryptographyProvider Build()
        {
            var provider = new CryptoProviderFactory(BlockCipherEngines.AES, BlockCipherModes.GCM).Build();
            provider.KeyDerivationIterations = Settings.Default.EncryptionKeyDerivationIterations;
            return provider;
        }
    }
}