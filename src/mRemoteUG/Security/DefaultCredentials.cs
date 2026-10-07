#nullable enable
using System;
using mRemoteUG.App;
using mRemoteUG.Security.SymmetricEncryption;

namespace mRemoteUG.Security
{
    /// <summary>
    /// The password mRemoteUG falls back to when a Connection carries none of its own.
    /// </summary>
    /// <remarks>
    /// It is held encrypted in Settings under <see cref="Runtime.EncryptionKey"/>, and this is the
    /// one place that knows so. Three call sites each used to new up a provider and repeat the
    /// encrypt/decrypt by hand.
    /// <para>
    /// Reading is allowed to fail. The value moved from the legacy Rijndael provider to AES-GCM
    /// when that provider was deleted, so a password saved by an older build does not decrypt.
    /// That returns empty - the connection then behaves as though no default were set - rather
    /// than throwing while a session is being opened. Saving the page once replaces it.
    /// </para>
    /// </remarks>
    public static class DefaultCredentials
    {
        public static string Password
        {
            get
            {
                var stored = Settings.Default.DefaultPassword;
                if (string.IsNullOrEmpty(stored))
                    return "";

                try
                {
                    return new AeadCryptographyProvider().Decrypt(stored, Runtime.EncryptionKey) ?? "";
                }
                catch (Exception)
                {
                    // Written by a build using a different format. There is nothing to recover.
                    return "";
                }
            }

            set
            {
                Settings.Default.DefaultPassword =
                    new AeadCryptographyProvider().Encrypt(value, Runtime.EncryptionKey);
            }
        }
    }
}
