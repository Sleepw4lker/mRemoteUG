#nullable enable
using System;
using System.Security.Cryptography;

namespace mRemoteUG.Security.KeyDerivation
{
    /// <summary>
    /// PBKDF2 (PKCS#5 v2.0, RFC 2898) with HMAC-SHA1 as the PRF.
    /// </summary>
    /// <remarks>
    /// This was a hand-rolled derivation loop, because the *instance* Rfc2898DeriveBytes
    /// refuses salts shorter than 8 bytes while the BouncyCastle implementation it replaced
    /// did not, and keys have to come out byte-identical for existing connection files to stay
    /// readable. The static Rfc2898DeriveBytes.Pbkdf2 has no such floor, so that objection does
    /// not apply to it - and it would not bind here in any case: AeadCryptographyProvider reads
    /// the salt as exactly SaltBitSize / 8 = 16 bytes whatever the file contains.
    /// <para>
    /// What the loop cost was one managed HMAC allocation and a fresh 20-byte array per
    /// iteration - a thousand of them per stored password, on the startup path and again on
    /// every autosave. The platform implementation is the same algorithm, hardware-accelerated.
    /// Pkcs5S2KeyGoldenVectorTests pins the output byte for byte, non-ASCII password included.
    /// </para>
    /// </remarks>
    public class Pkcs5S2KeyGenerator
    {
        private readonly int _iterations;
        private readonly int _keyBitSize;

        public Pkcs5S2KeyGenerator(int keyBitSize = 256, int iterations = 1000)
        {
            if (iterations < 1000)
                throw new ArgumentOutOfRangeException($"Minimum value of {nameof(iterations)} is 1000");
            if (keyBitSize < 0)
                throw new ArgumentOutOfRangeException($"{nameof(keyBitSize)} must be positive");
            _keyBitSize = keyBitSize;
            _iterations = iterations;
        }

        public byte[] DeriveKey(string password, byte[] salt)
        {
            var passwordBytes = PasswordToBytes(password);
            return Pbkdf2HmacSha1(passwordBytes, salt, _iterations, _keyBitSize / 8);
        }

        // Matches BouncyCastle's PbeParametersGenerator.Pkcs5PasswordToBytes: each character's
        // low byte, not a full UTF-8 encoding (only differs from UTF-8 for non-ASCII passwords).
        private static byte[] PasswordToBytes(string password)
        {
            password = password ?? "";
            var bytes = new byte[password.Length];
            for (var i = 0; i < password.Length; i++)
                bytes[i] = (byte)password[i];
            return bytes;
        }

        private static byte[] Pbkdf2HmacSha1(byte[] password, byte[] salt, int iterations, int keyLengthInBytes)
        {
            return Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA1, keyLengthInBytes);
        }
    }
}
