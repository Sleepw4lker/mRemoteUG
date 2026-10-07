using mRemoteUG;
using mRemoteUG.Connection.Protocol.RDP;
using NUnit.Framework;

namespace mRemoteUG.Tests.Connection.Protocol
{
    /// <summary>
    /// Pins that a fatal RDP error is described to the user, not named at them.
    /// </summary>
    /// <remarks>
    /// The table these read from used to hold the resource *names* as quoted literals, so
    /// GetError("1") answered the string "Language.strRdpErrorCode1" and that is what reached
    /// the notification panel. Asserting the text is not the name is the whole point: an
    /// assertion that the result is merely non-empty passed against the bug.
    /// </remarks>
    public class RdpFatalErrorsTests
    {
        [TestCase("0", nameof(Language.strRdpErrorUnknown))]
        [TestCase("1", nameof(Language.strRdpErrorCode1))]
        [TestCase("2", nameof(Language.strRdpErrorOutOfMemory))]
        [TestCase("3", nameof(Language.strRdpErrorWindowCreation))]
        [TestCase("4", nameof(Language.strRdpErrorCode2))]
        [TestCase("5", nameof(Language.strRdpErrorCode3))]
        [TestCase("6", nameof(Language.strRdpErrorCode4))]
        [TestCase("7", nameof(Language.strRdpErrorConnection))]
        [TestCase("100", nameof(Language.strRdpErrorWinsock))]
        public void GetErrorReturnsTheResourceTextNotItsName(string id, string resourceName)
        {
            var description = RdpProtocol.FatalErrors.GetError(id);

            Assert.Multiple(() =>
            {
                Assert.That(description, Is.Not.Null.And.Not.Empty);
                Assert.That(description, Does.Not.Contain(resourceName),
                            "the resource name leaked through instead of its value");
                Assert.That(description, Does.Not.StartWith("Language."),
                            "the description is a resource reference, not a message");
            });
        }

        [Test]
        public void GetErrorFallsBackToTheUnknownMessageForACodeItDoesNotKnow()
        {
            Assert.That(RdpProtocol.FatalErrors.GetError("4242"),
                        Is.EqualTo(Language.strRdpErrorUnknown));
        }
    }
}
