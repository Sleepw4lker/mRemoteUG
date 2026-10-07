using System;
using mRemoteUG.Connection;
using mRemoteUG.Connection.Protocol;
using mRemoteUG.Connection.Protocol.RAW;
using mRemoteUG.Connection.Protocol.Rlogin;
using mRemoteUG.Connection.Protocol.SSH;
using mRemoteUG.Connection.Protocol.Telnet;
using NUnit.Framework;

namespace mRemoteUG.Tests.Connection.Protocol
{
    public class ProtocolFactoryTests
    {
        private ProtocolFactory _protocolFactory;

        [SetUp]
        public void Setup()
        {
            _protocolFactory = new ProtocolFactory();
        }

        // RDP is deliberately absent: RdpProtocol constructs an ActiveX control and
        // depends on the FrmMain singleton, so it cannot be built in a headless test.
        [TestCase(ProtocolType.SSH1, typeof(ProtocolSSH1))]
        [TestCase(ProtocolType.SSH2, typeof(ProtocolSSH2))]
        [TestCase(ProtocolType.Telnet, typeof(ProtocolTelnet))]
        [TestCase(ProtocolType.Rlogin, typeof(ProtocolRlogin))]
        [TestCase(ProtocolType.RAW, typeof(RawProtocol))]
        public void CreatesTheProtocolMatchingTheConnection(ProtocolType protocol, Type expectedType)
        {
            var connectionInfo = new ConnectionInfo { Protocol = protocol };

            var createdProtocol = _protocolFactory.CreateProtocol(connectionInfo);

            Assert.That(createdProtocol, Is.InstanceOf(expectedType));
        }

        [Test]
        public void ReturnsNullForAProtocolThisBuildDropped()
        {
            // VNC still exists on the enum so old connection files parse, but nothing
            // can open it. Callers rely on null rather than an exception here.
#pragma warning disable 618
            var connectionInfo = new ConnectionInfo { Protocol = ProtocolType.VNC };
#pragma warning restore 618

            var createdProtocol = _protocolFactory.CreateProtocol(connectionInfo);

            Assert.That(createdProtocol, Is.Null);
        }
    }
}
