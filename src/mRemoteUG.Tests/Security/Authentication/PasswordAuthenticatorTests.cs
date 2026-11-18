using System.Security;

using mRemoteUG.Security;
using mRemoteUG.Security.Authentication;
using mRemoteUG.Security.SymmetricEncryption;

using NUnit.Framework;


namespace mRemoteUG.Tests.Security.Authentication;

public class PasswordAuthenticatorTests
{
    private readonly AeadCryptographyProvider _cryptographyProvider =
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
        bool authenticated = authenticator.Authenticate(_correctPassword);
        Assert.That(authenticated);
    }

    [Test]
    public void AuthenticatingWithWrongPasswordReturnsFalse()
    {
        var authenticator = new PasswordAuthenticator(_cryptographyProvider, _cipherText, () => null);
        bool authenticated = authenticator.Authenticate(_wrongPassword);
        Assert.That(!authenticated);
    }

    [Test]
    public void AuthenticationRequestorIsCalledWhenInitialPasswordIsWrong()
    {
        bool wasCalled = false;

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
        bool wasCalled = false;
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
        bool authenticated = authenticator.Authenticate(_wrongPassword);
        Assert.That(authenticated);
    }

    [Test]
    public void TheCorrectPasswordOnTheLastPromptIsTried()
    {
        SecureString thirdAnswer = "9theCorrectPass#5".ConvertToSecureString();
        var answers = new Queue<SecureString>(
        [
            "wrongAnswer1".ConvertToSecureString(),
            "wrongAnswer2".ConvertToSecureString(),
            thirdAnswer,
        ]);

        var authenticator = new PasswordAuthenticator(_cryptographyProvider, _cipherText, answers.Dequeue);
        bool authenticated = authenticator.Authenticate(_wrongPassword);

        Assert.Multiple(() =>
        {
            Assert.That(authenticator.MaxAttempts, Is.EqualTo(3), "The third answer is the last prompt.");
            Assert.That(authenticated, Is.True);
            Assert.That(authenticator.LastAuthenticatedPassword, Is.SameAs(thirdAnswer));
        });
    }

    [Test]
    public void AuthenticationFailsWhenAuthenticationRequestorGivenEmptyPassword()
    {
        var authenticator = new PasswordAuthenticator(_cryptographyProvider, _cipherText, () => new SecureString());
        bool authenticated = authenticator.Authenticate(_wrongPassword);
        Assert.That(!authenticated);
    }

    [Test]
    public void AnEmptyPasswordFromTheRequestorEndsTheAttemptsAtOnce()
    {
        int authAttempts = 0;
        SecureString? AuthenticationRequestor()
        {
            authAttempts++;
            return new SecureString();
        }

        var authenticator = new PasswordAuthenticator(_cryptographyProvider, _cipherText, AuthenticationRequestor);
        bool authenticated = authenticator.Authenticate(_wrongPassword);

        Assert.Multiple(() =>
        {
            Assert.That(authenticated, Is.False);
            Assert.That(authAttempts, Is.EqualTo(1), "An empty password is not asked for again.");
        });
    }

    [Test]
    public void AuthenticatorRespectsMaxAttempts()
    {
        var recorder = new RecordingCryptographyProvider(_cryptographyProvider);
        int authAttempts = 0;
        SecureString? AuthenticationRequestor()
        {
            authAttempts++;
            return $"wrongAnswer{authAttempts}".ConvertToSecureString();
        }

        var authenticator = new PasswordAuthenticator(recorder, _cipherText, AuthenticationRequestor);
        bool authenticated = authenticator.Authenticate(_wrongPassword);

        string[] everyKeyTried = ["wrongPassword", "wrongAnswer1", "wrongAnswer2", "wrongAnswer3"];
        Assert.Multiple(() =>
        {
            Assert.That(authenticated, Is.False);
            Assert.That(authAttempts, Is.EqualTo(authenticator.MaxAttempts));
            Assert.That(recorder.TriedKeys, Is.EqualTo(everyKeyTried),
                "Every answer is tried, the last one included.");
        });
    }

    [Test]
    public void AuthenticatorRespectsMaxAttemptsCustomValue()
    {
        const int customMaxAttempts = 5;
        var recorder = new RecordingCryptographyProvider(_cryptographyProvider);
        int authAttempts = 0;
        SecureString? AuthenticationRequestor()
        {
            authAttempts++;
            return $"wrongAnswer{authAttempts}".ConvertToSecureString();
        }

        var authenticator =
            new PasswordAuthenticator(recorder, _cipherText, AuthenticationRequestor)
            {
                MaxAttempts = customMaxAttempts
            };
        bool authenticated = authenticator.Authenticate(_wrongPassword);

        Assert.Multiple(() =>
        {
            Assert.That(authenticated, Is.False);
            Assert.That(authAttempts, Is.EqualTo(customMaxAttempts));
            Assert.That(recorder.TriedKeys.Count, Is.EqualTo(customMaxAttempts + 1),
                "The initial password and every answer are tried.");
            Assert.That(recorder.TriedKeys[^1], Is.EqualTo($"wrongAnswer{customMaxAttempts}"),
                "The last answer is tried.");
        });
    }

    [Test]
    public void AnAnswerOfOnlySpacesIsAskedAgain()
    {
        // Spaces are user input, not damaged data: unlike an empty answer, they are tried, and
        // like a wrong password they lead to another prompt rather than ending the load.
        var answers = new Queue<SecureString>(["   ".ConvertToSecureString(), _correctPassword]);
        int authAttempts = 0;
        SecureString? AuthenticationRequestor()
        {
            authAttempts++;
            return answers.Dequeue();
        }

        var authenticator = new PasswordAuthenticator(_cryptographyProvider, _cipherText, AuthenticationRequestor);
        bool authenticated = authenticator.Authenticate(_wrongPassword);

        Assert.Multiple(() =>
        {
            Assert.That(authenticated, Is.True);
            Assert.That(authAttempts, Is.EqualTo(2), "Asked once for the spaces, once more for the password.");
        });
    }

    [Test]
    public void CipherTextThatIsNotBase64ThrowsWithoutAskingForAPassword()
    {
        // Damaged data is not a wrong password: no answer to a prompt could decrypt it, so
        // asking for one would only end in a misleading "wrong password".
        bool wasCalled = false;
        SecureString? AuthenticationRequestor()
        {
            wasCalled = true;
            return _correctPassword;
        }

        var authenticator = new PasswordAuthenticator(_cryptographyProvider, "not base64!", AuthenticationRequestor);

        Assert.Multiple(() =>
        {
            Assert.That(() => authenticator.Authenticate(_correctPassword), Throws.InstanceOf<FormatException>());
            Assert.That(wasCalled, Is.False, "The requestor is never asked.");
        });
    }

    /// <summary>
    /// Decrypts with the real provider and keeps each key it was asked to try, so a test
    /// can see which answers the authenticator actually checked, not only how often it asked.
    /// </summary>
    private sealed class RecordingCryptographyProvider(ICryptographyProvider inner) : ICryptographyProvider
    {
        public List<string> TriedKeys { get; } = [];

        public int BlockSizeInBytes => inner.BlockSizeInBytes;

        public BlockCipherEngines CipherEngine => inner.CipherEngine;

        public BlockCipherModes CipherMode => inner.CipherMode;

        public int KeyDerivationIterations
        {
            get => inner.KeyDerivationIterations;
            set => inner.KeyDerivationIterations = value;
        }

        public string Encrypt(string plainText, SecureString encryptionKey) => inner.Encrypt(plainText, encryptionKey);

        public string Decrypt(string cipherText, SecureString decryptionKey)
        {
            TriedKeys.Add(decryptionKey.ConvertToUnsecureString());
            return inner.Decrypt(cipherText, decryptionKey);
        }
    }
}
