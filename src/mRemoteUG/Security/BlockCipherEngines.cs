#nullable enable
using System;

// ReSharper disable InconsistentNaming
namespace mRemoteUG.Security
{
    public enum BlockCipherEngines
    {
        AES,

        [Obsolete("No longer implemented after the BouncyCastle removal. Retained only so old files that recorded this engine are recognized (and reported as unsupported) instead of silently misinterpreted.")]
        Twofish,

        [Obsolete("No longer implemented after the BouncyCastle removal. Retained only so old files that recorded this engine are recognized (and reported as unsupported) instead of silently misinterpreted.")]
        Serpent
    }
}
