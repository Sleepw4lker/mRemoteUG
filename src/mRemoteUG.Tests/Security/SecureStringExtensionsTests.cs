#nullable enable
using System;
using System.Security;
using mRemoteUG.Security;
using NUnit.Framework;


namespace mRemoteUG.Tests.Security
{
    public class SecureStringExtensionsTests
    {
        [Test]
        public void ConvertToSecureString()
        {
            var securedString = "MySecureString".ConvertToSecureString();
            Assert.That(securedString.Length, Is.GreaterThan(0));
        }

        [Test]
        public void ConvertToUnsecureString()
        {
            var originalText = "MySecureString";
            var securedString = originalText.ConvertToSecureString();
            var unsecuredString = securedString.ConvertToUnsecureString();
            Assert.That(unsecuredString, Is.EqualTo(originalText));
        }

        [Test]
        public void ConvertToSecureStringOnNullStringThrowsException()
        {
            string? myString = null;
            // The null is what this test is for; ! asserts to the compiler that passing it is
            // deliberate, so the runtime guard is what gets exercised.
            Assert.Throws<ArgumentNullException>(() => myString!.ConvertToSecureString());
        }

        [Test]
        public void ConvertToUnsecureStringOnNullStringThrowsException()
        {
            SecureString? secureString = null;
            // As above: the null is the case under test.
            Assert.Throws<ArgumentNullException>(() => secureString!.ConvertToUnsecureString());
        }
    }
}