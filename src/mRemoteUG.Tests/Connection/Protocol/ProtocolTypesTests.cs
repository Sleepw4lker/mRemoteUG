using mRemoteUG.Connection.Protocol;
using NUnit.Framework;

namespace mRemoteUG.Tests.Connection.Protocol
{
    public class ProtocolTypesTests
    {
        [Test]
        public void SupportedListsExactlyTheProtocolsThisBuildCanOpen()
        {
            Assert.That(ProtocolTypes.Supported, Is.EquivalentTo(new[]
            {
                ProtocolType.RDP,
                ProtocolType.SSH1,
                ProtocolType.SSH2,
                ProtocolType.Telnet,
                ProtocolType.Rlogin,
                ProtocolType.RAW
            }));
        }

        [TestCase(ProtocolType.RDP)]
        [TestCase(ProtocolType.SSH2)]
        [TestCase(ProtocolType.Telnet)]
        public void SupportedProtocolsAreReportedAsSupported(ProtocolType protocol)
        {
            Assert.That(ProtocolTypes.IsSupported(protocol), Is.True);
        }

        [Test]
        public void RemovedProtocolsAreNotReportedAsSupported()
        {
            // These remain on the enum only so that connection files written by older
            // builds still parse; they must never be offered or constructed.
#pragma warning disable 618
            Assert.Multiple(() =>
            {
                Assert.That(ProtocolTypes.IsSupported(ProtocolType.VNC), Is.False);
                Assert.That(ProtocolTypes.IsSupported(ProtocolType.HTTP), Is.False);
                Assert.That(ProtocolTypes.IsSupported(ProtocolType.HTTPS), Is.False);
                Assert.That(ProtocolTypes.IsSupported(ProtocolType.ICA), Is.False);
                Assert.That(ProtocolTypes.IsSupported(ProtocolType.IntApp), Is.False);
            });
#pragma warning restore 618
        }
    }
}
