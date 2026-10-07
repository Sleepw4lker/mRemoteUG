#nullable enable
using System;
using System.Diagnostics.CodeAnalysis;
using System.Security;
using mRemoteUG.Tools;

namespace mRemoteUG.Security.Authentication
{
    public class PasswordAuthenticator
    {
        private readonly ICryptographyProvider _cryptographyProvider;
        private readonly string _cipherText;
        private readonly Func<SecureString?> _authenticationRequestor;

        public int MaxAttempts { get; set; } = 3;
        /// <summary>The password that worked, set only once one has.</summary>
        public SecureString? LastAuthenticatedPassword { get; private set; }

        public PasswordAuthenticator(ICryptographyProvider cryptographyProvider, string cipherText, Func<SecureString?> authenticationRequestor)
        {
            _cryptographyProvider = cryptographyProvider.ThrowIfNull(nameof(cryptographyProvider));
            _cipherText = cipherText.ThrowIfNullOrEmpty(nameof(cipherText));
            _authenticationRequestor = authenticationRequestor.ThrowIfNull(nameof(authenticationRequestor));
        }

        /// <remarks>
        /// Returning true is the guarantee that <see cref="LastAuthenticatedPassword"/> is set;
        /// XmlConnectionsDecryptor reads it straight after checking this, and the attribute is
        /// what lets it do so without a null check the bool has already made redundant.
        /// </remarks>
        [MemberNotNullWhen(true, nameof(LastAuthenticatedPassword))]
        public bool Authenticate(SecureString password)
        {
            var authenticated = false;
            var attempts = 0;
            while (!authenticated && attempts < MaxAttempts)
            {
                try
                {
                    _cryptographyProvider.Decrypt(_cipherText, password);
                    authenticated = true;
                    LastAuthenticatedPassword = password;
                }
                catch
                {
                    // null is the user cancelling the prompt. The old Optional<T> folded a
                    // null value into Empty, so the null check after unwrapping could never
                    // fire; cancelled and empty are now told apart by the type.
                    var providedPassword = _authenticationRequestor();
                    if (providedPassword == null)
                        return false;

                    password = providedPassword;
                    if (password.Length == 0) break;
                }
                attempts++;
            }
            return authenticated;
        }
    }
}