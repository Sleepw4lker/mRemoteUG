using mRemoteUG.Security;
using mRemoteUG.Security.KeyDerivation;
using mRemoteUG.Security.SymmetricEncryption;

using NUnit.Framework;

namespace mRemoteUG.Tests.Security;

public class KeyDerivationIterationLimitsTests
{
    [Test]
    public void TheDefaultIsInsideEveryRangeAndMatchesTheProvider()
    {
        Assert.Multiple(() =>
        {
            Assert.That(KeyDerivationIterationLimits.Default,
                        Is.GreaterThanOrEqualTo(KeyDerivationIterationLimits.Minimum));
            Assert.That(KeyDerivationIterationLimits.OptionsMaximum,
                        Is.LessThan(KeyDerivationIterationLimits.ReadMaximum));
            Assert.That(new AeadCryptographyProvider().KeyDerivationIterations,
                        Is.EqualTo(KeyDerivationIterationLimits.Default));
        });
    }

    [Test]
    public void ANullDeclarationIsTheDefault()
    {
        Assert.That(KeyDerivationIterationLimits.ParseFromFile(null), Is.EqualTo(1000));
    }

    [TestCase("1000", 1000)]
    [TestCase("5000", 5000)]
    [TestCase("50000", 50000)]
    [TestCase("50001", 50001)]
    [TestCase("1000000", 1000000)]
    [TestCase("+1000", 1000)]
    public void ACountInsideTheReadRangeIsAccepted(string declared, int expected)
    {
        Assert.That(KeyDerivationIterationLimits.ParseFromFile(declared), Is.EqualTo(expected));
    }

    [TestCase("999")]
    [TestCase("0")]
    [TestCase("-1")]
    [TestCase("abc")]
    [TestCase("")]
    [TestCase("1,000")]
    [TestCase("1000001")]
    [TestCase("2147483648")]
    public void ACountOutsideTheReadRangeIsRefusedNamingTheValueAndTheRange(string declared)
    {
        EncryptionException? thrown = Assert.Throws<EncryptionException>(
            () => KeyDerivationIterationLimits.ParseFromFile(declared));

        Assert.Multiple(() =>
        {
            Assert.That(thrown!.Message, Does.Contain("KdfIterations"));
            Assert.That(thrown.Message, Does.Contain($"\"{declared}\""));
            Assert.That(thrown.Message, Does.Contain("1000 to 1000000"));
        });
    }
}
