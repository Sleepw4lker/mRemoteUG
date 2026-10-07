#nullable enable
using System;
using mRemoteUG.Security.KeyDerivation;
using NUnit.Framework;

namespace mRemoteUG.Tests.Security.KeyDerivation
{
    /// <summary>
    /// Byte-for-byte vectors for the key derivation, so the implementation behind it can be
    /// changed without silently making every existing connection file unreadable.
    /// </summary>
    /// <remarks>
    /// Captured from the hand-rolled PBKDF2-HMAC-SHA1 loop that was here before, which is the
    /// implementation every confCons.xml in the wild was encrypted with. A key that comes out
    /// differently is not a failing test, it is an unreadable file.
    /// <para>
    /// The non-ASCII case is the one that matters most. PasswordToBytes deliberately takes
    /// each character's low byte rather than encoding UTF-8 - it matches BouncyCastle's
    /// PbeParametersGenerator.Pkcs5PasswordToBytes - so any replacement that reaches for
    /// Encoding.UTF8 produces different keys for exactly these passwords and identical ones
    /// for every ASCII password anyone would test with.
    /// </para>
    /// </remarks>
    public class Pkcs5S2KeyGoldenVectorTests
    {
        private static readonly byte[] Salt = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 };

        [TestCase("", 1000, 256, "GNXM9eJ1ZHP3L7FmRhlUZ6FGfiUlh8dK83rBk2aaD9w=")]
        [TestCase("mR3m", 1000, 256, "XZ5v2M80mliL2EyPfXQBzqHmEb5uE/fNpLelLk5g5dU=")]
        [TestCase("Test-Passw0rd!", 1000, 256, "0uvTt4mgfzAGJvqbxf6FeMWs07dIapiDFadBvew6ePc=")]
        [TestCase("paßwort-über-éè-中文", 1000, 256, "am9c1lH92I72BcXPsu0ztt8yYfqcN0mMxz+uHcqJXTQ=")]
        [TestCase("mR3m", 5000, 256, "TRrwTumT8cgzoYx/EnkC+BmO4Vi8YFFigzYCamEo4ZY=")]
        [TestCase("mR3m", 1000, 128, "XZ5v2M80mliL2EyPfXQBzg==")]
        public void DerivedKeyMatchesTheCapturedVector(string password, int iterations, int keyBits, string expectedBase64)
        {
            var key = new Pkcs5S2KeyGenerator(keyBits, iterations).DeriveKey(password, Salt);
            Assert.That(Convert.ToBase64String(key), Is.EqualTo(expectedBase64));
        }
    }
}
