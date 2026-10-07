#nullable enable
using mRemoteUG.Security.SymmetricEncryption;

namespace mRemoteUG.Security.Factories
{
    public class CryptoProviderFactory : ICryptoProviderFactory
    {
        private readonly BlockCipherEngines _engine;
        private readonly BlockCipherModes _mode;

        public CryptoProviderFactory(BlockCipherEngines engine, BlockCipherModes mode)
        {
            _engine = engine;
            _mode = mode;
        }

        public ICryptographyProvider Build()
        {
            if (_engine != BlockCipherEngines.AES || _mode != BlockCipherModes.GCM)
                throw new UnsupportedEncryptionException(_engine, _mode);

            return new AeadCryptographyProvider();
        }
    }
}
