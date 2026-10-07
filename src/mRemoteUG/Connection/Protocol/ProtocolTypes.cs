using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace mRemoteUG.Connection.Protocol
{
    /// <summary>
    /// The protocols this build can actually open. <see cref="ProtocolType"/> still carries
    /// the protocols that were removed, so that connection files written by older builds
    /// parse instead of throwing; this is the list that decides what the UI offers, what
    /// <see cref="ProtocolFactory"/> can construct, and what survives loading.
    /// </summary>
    public static class ProtocolTypes
    {
        public static readonly IReadOnlyCollection<ProtocolType> Supported =
            new ReadOnlyCollection<ProtocolType>(new[]
            {
                ProtocolType.RDP,
                ProtocolType.SSH1,
                ProtocolType.SSH2,
                ProtocolType.Telnet,
                ProtocolType.Rlogin,
                ProtocolType.RAW
            });

        public static bool IsSupported(ProtocolType protocol) => Supported.Contains(protocol);
    }
}
