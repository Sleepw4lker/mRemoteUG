using mRemoteUG.App;
using mRemoteUG.Security;
using mRemoteUG.Security.SymmetricEncryption;

using NUnit.Framework;


namespace mRemoteUG.Tests.Security;

/// <summary>
/// The fallback password, held encrypted in Settings under <see cref="Runtime.EncryptionKey"/>.
/// </summary>
/// <remarks>
/// Each test writes <c>Settings.Default.DefaultPassword</c>, so the stored value is put back
/// afterwards: other fixtures and <c>--selftest</c> share the same settings instance.
/// </remarks>
[TestFixture]
public class DefaultCredentialsTests
{
    private string _originalStored = "";

    [SetUp]
    public void Setup()
    {
        _originalStored = Settings.Default.DefaultPassword;
    }

    [TearDown]
    public void TearDown()
    {
        Settings.Default.DefaultPassword = _originalStored;
    }

    [Test]
    public void NothingStoredReadsAsEmpty()
    {
        Settings.Default.DefaultPassword = "";

        Assert.That(DefaultCredentials.Password, Is.EqualTo(""));
    }

    [Test]
    public void APasswordSetIsReadBack()
    {
        DefaultCredentials.Password = "secret";

        Assert.Multiple(() =>
        {
            Assert.That(Settings.Default.DefaultPassword, Is.Not.EqualTo("secret"),
                        "The setting holds the cipher text, not the password.");
            Assert.That(DefaultCredentials.Password, Is.EqualTo("secret"));
        });
    }

    [Test]
    public void StoredTextThatIsNotBase64ReadsAsEmpty()
    {
        Settings.Default.DefaultPassword = "not base64 at all!";

        Assert.That(DefaultCredentials.Password, Is.EqualTo(""));
    }

    [Test]
    public void StoredTextEncryptedUnderAnotherKeyReadsAsEmpty()
    {
        Settings.Default.DefaultPassword =
            new AeadCryptographyProvider().Encrypt("secret", "another key".ConvertToSecureString());

        Assert.That(DefaultCredentials.Password, Is.EqualTo(""));
    }
}
