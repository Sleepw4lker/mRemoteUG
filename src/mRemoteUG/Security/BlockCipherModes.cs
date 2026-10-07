#nullable enable
using System;

// ReSharper disable InconsistentNaming
namespace mRemoteUG.Security
{
    public enum BlockCipherModes
    {
        GCM,

        [Obsolete("No longer implemented after the BouncyCastle removal. Retained only so old files that recorded this mode are recognized (and reported as unsupported) instead of silently misinterpreted.")]
        CCM,

        [Obsolete("No longer implemented after the BouncyCastle removal. Retained only so old files that recorded this mode are recognized (and reported as unsupported) instead of silently misinterpreted.")]
        EAX
    }
}
