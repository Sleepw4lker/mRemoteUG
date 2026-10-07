#nullable enable
using System.Security;
using mRemoteUG.Security;
using mRemoteUG.Security.Authentication;
using mRemoteUG.Security.SymmetricEncryption;
using mRemoteUG.Tools;
using NUnit.Framework;


namespace mRemoteUG.Tests.Security.Authentication
{
    public class PasswordAuthenticatorTests
    {
        private readonly ICryptographyProvider _cryptographyProvider =
            new AeadCryptographyProvider { KeyDerivationIterations = 10000 };
        private readonly string _cipherText;
        private readonly SecureString _correctPassword = "9theCorrectPass#5".ConvertToSecureString();
        private readonly SecureString _wrongPassword = "wrongPassword".ConvertToSecureString();

        // A constructor rather than a [SetUp], because _cipherText is derived from two of the
        // fields above and a field initializer may not reference them. Nothing here is mutated
        // by a test, so once per fixture is the same as once per test.
        public PasswordAuthenticatorTests()
        {
            // Produced here rather than pasted in. This fixture is about the authenticator
            // deciding which password is right, not about any particular stored envelope, and a
            // literal would have to be re-minted every time the envelope changes.
            _cipherText = _cryptographyProvider.Encrypt("ThisIsProtected", _correctPassword);
        }

        [Test]
        public void AuthenticatingWithCorrectPasswordReturnsTrue()
        {
            var authenticator = new PasswordAuthenticator(_cryptographyProvider, _cipherText, () => null);
            var authenticated = authenticator.Authenticate(_correctPassword);
            Assert.That(authenticated);
        }

        [Test]
        public void AuthenticatingWithWrongPasswordReturnsFalse()
        {
            var authenticator = new PasswordAuthenticator(_cryptographyProvider, _cipherText, () => null);
            var authenticated = authenticator.Authenticate(_wrongPassword);
            Assert.That(!authenticated);
        }

        [Test]
        public void AuthenticationRequestorIsCalledWhenInitialPasswordIsWrong()
        {
            var wasCalled = false;

            SecureString? AuthenticationRequestor()
            {
                wasCalled = true;
                return _correctPassword;
            }

            var authenticator = new PasswordAuthenticator(_cryptographyProvider, _cipherText, AuthenticationRequestor);
            authenticator.Authenticate(_wrongPassword);
            Assert.That(wasCalled);
        }

        [Test]
        public void AuthenticationRequestorNotCalledWhenInitialPasswordIsCorrect()
        {
            var wasCalled = false;
            SecureString? AuthenticationRequestor()
            {
                wasCalled = true;
                return _correctPassword;
            }

            var authenticator = new PasswordAuthenticator(_cryptographyProvider, _cipherText, AuthenticationRequestor);
            authenticator.Authenticate(_correctPassword);
            Assert.That(!wasCalled);
        }

        [Test]
        public void ProvidingCorrectPasswordToTheAuthenticationRequestorReturnsTrue()
        {
            var authenticator = new PasswordAuthenticator(_cryptographyProvider, _cipherText, () => _correctPassword);
            var authenticated = authenticator.Authenticate(_wrongPassword);
            Assert.That(authenticated);
        }

        [Test]
        public void AuthenticationFailsWhenAuthenticationRequestorGivenEmptyPassword()
        {
            var authenticator = new PasswordAuthenticator(_cryptographyProvider, _cipherText, () => new SecureString());
            var authenticated = authenticator.Authenticate(_wrongPassword);
            Assert.That(!authenticated);
        }

        [Test]
        public void AuthenticatorRespectsMaxAttempts()
        {
            var authAttempts = 0;
            SecureString? AuthenticationRequestor()
            {
                authAttempts++;
                return _wrongPassword;
            }

            var authenticator = new PasswordAuthenticator(_cryptographyProvider, _cipherText, AuthenticationRequestor);
            authenticator.Authenticate(_wrongPassword);
            Assert.That(authAttempts == authenticator.MaxAttempts);
        }

        [Test]
        public void AuthenticatorRespectsMaxAttemptsCustomValue()
        {
            const int customMaxAttempts = 5;
            var authAttempts = 0;
            SecureString? AuthenticationRequestor()
            {
                authAttempts++;
                return _wrongPassword;
            }

            var authenticator =
                new PasswordAuthenticator(_cryptographyProvider, _cipherText, AuthenticationRequestor)
                {
                    MaxAttempts = customMaxAttempts
                };
            authenticator.Authenticate(_wrongPassword);
            Assert.That(authAttempts == customMaxAttempts);
        }
    }
}