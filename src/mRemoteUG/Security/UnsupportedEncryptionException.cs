#nullable enable
using System;

namespace mRemoteUG.Security
{
    /// <summary>
    /// Thrown when an encryption engine/mode combination is recognized (i.e. it's a real,
    /// formerly-supported value in <see cref="BlockCipherEngines"/>/<see cref="BlockCipherModes"/>)
    /// but is no longer implemented - as opposed to a value that fails to parse at all, which is
    /// treated as "not encrypted with this scheme" rather than "used to be, no longer is".
    /// </summary>
    [Serializable]
    public class UnsupportedEncryptionException : EncryptionException
    {
        public BlockCipherEngines Engine { get; }
        public BlockCipherModes Mode { get; }

        public UnsupportedEncryptionException(BlockCipherEngines engine, BlockCipherModes mode)
            : base($"The encryption engine/mode combination '{engine}+{mode}' is no longer supported.")
        {
            Engine = engine;
            Mode = mode;
        }
    }
}
