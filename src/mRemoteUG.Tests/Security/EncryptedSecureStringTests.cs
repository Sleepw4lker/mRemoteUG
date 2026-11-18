using System.Security;

using mRemoteUG.Security;

using NUnit.Framework;

namespace mRemoteUG.Tests.Security;

[TestFixture()]
public class EncryptedSecureStringTests
{
    private readonly string _clearTextData = "MySecureData";

    [Test]
    public void CanCreateEncryptedSecureString()
    {
        Assert.That(new EncryptedSecureString(), Is.InstanceOf<EncryptedSecureString>());
    }

    [Test]
    public void CanAssignStringValue()
    {
        var encryptedSecString = new EncryptedSecureString();
        TestDelegate testDelegate = () => encryptedSecString.SetValue(_clearTextData);
        Assert.DoesNotThrow(testDelegate);
    }

    [Test]
    public void EncryptedValueIsSameAsOriginalValue()
    {
        var encryptedSecString = new EncryptedSecureString();
        encryptedSecString.SetValue(_clearTextData);
        string decryptedData = encryptedSecString.GetClearTextValue();
        Assert.That(decryptedData, Is.EqualTo(_clearTextData));
    }

    [Test]
    public void CreatingMultipleEncryptedSecureStrings()
    {
        var encString1 = new EncryptedSecureString();
        encString1.SetValue(_clearTextData);
        var encString2 = new EncryptedSecureString();
        encString2.SetValue("somevalue");
        string decryptedString = encString1.GetClearTextValue();
        Assert.That(decryptedString, Is.EqualTo(_clearTextData));
    }

    [Test]
    public void DisposeReleasesTheValue()
    {
        var encryptedSecString = new EncryptedSecureString();
        encryptedSecString.SetValue(_clearTextData);

        encryptedSecString.Dispose();

        Assert.Throws<ObjectDisposedException>(() => encryptedSecString.GetClearTextValue());
    }

    [Test]
    public void SettingAValueAgainKeepsTheNewOne()
    {
        using var encryptedSecString = new EncryptedSecureString();
        encryptedSecString.SetValue("first");

        encryptedSecString.SetValue(_clearTextData);

        Assert.That(encryptedSecString.GetClearTextValue(), Is.EqualTo(_clearTextData));
    }

    // The machine key is cached in a static the first time any test touches the class, so
    // these call the generator directly: a test through SetValue would reuse whatever key
    // an earlier test generated.

    [TestCase(32)]
    [TestCase(7)]
    public void MachineKeyHasExactlyTheRequestedLength(int keySize)
    {
        using SecureString key = EncryptedSecureString.GenerateNewMachineKey(keySize);

        Assert.That(key.Length, Is.EqualTo(keySize));
    }

    [Test]
    public void MachineKeyCharactersAreBetweenExclamationMarkAndClosingBrace()
    {
        using SecureString key = EncryptedSecureString.GenerateNewMachineKey(32);

        Assert.That(key.ConvertToUnsecureString(), Has.All.InRange('!', '}'));
    }

    [Test]
    public void EachMachineKeyIsDifferent()
    {
        using SecureString first = EncryptedSecureString.GenerateNewMachineKey(32);
        using SecureString second = EncryptedSecureString.GenerateNewMachineKey(32);

        // 32 characters drawn from 93: a collision is not a realistic flake.
        Assert.That(first.ConvertToUnsecureString(), Is.Not.EqualTo(second.ConvertToUnsecureString()));
    }
}
