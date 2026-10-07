using mRemoteUG.App;
using mRemoteUG.Connection.Protocol.RAW;
using mRemoteUG.Connection.Protocol.RDP;
using mRemoteUG.Connection.Protocol.Rlogin;
using mRemoteUG.Connection.Protocol.SSH;
using mRemoteUG.Connection.Protocol.Telnet;
using mRemoteUG.Messages;

namespace mRemoteUG.Connection.Protocol
{
    public class ProtocolFactory
    {
        /// <summary>
        /// Builds the protocol handler for <paramref name="connectionInfo"/>, or null when
        /// its protocol is not supported by this build. Callers must handle null.
        /// </summary>
        public ProtocolBase CreateProtocol(ConnectionInfo connectionInfo)
        {
            switch (connectionInfo.Protocol)
            {
                case ProtocolType.RDP:
                    var rdpProtocol = new RdpProtocol
                    {
                        LoadBalanceInfoUseUtf8 = Settings.Default.RdpLoadBalanceInfoUseUtf8
                    };
                    rdpProtocol.tmrReconnect.Elapsed += rdpProtocol.tmrReconnect_Elapsed;
                    return rdpProtocol;
                case ProtocolType.SSH1:
                    return new ProtocolSSH1();
                case ProtocolType.SSH2:
                    return new ProtocolSSH2();
                case ProtocolType.Telnet:
                    return new ProtocolTelnet();
                case ProtocolType.Rlogin:
                    return new ProtocolRlogin();
                case ProtocolType.RAW:
                    return new RawProtocol();
                default:
                    Runtime.MessageCollector.AddMessage(MessageClass.ErrorMsg,
                        string.Format(Language.strProtocolNotSupported, connectionInfo.Protocol));
                    return null;
            }
        }
    }
}
