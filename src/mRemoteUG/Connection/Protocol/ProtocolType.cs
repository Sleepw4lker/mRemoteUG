using System;
using mRemoteUG.Tools;

namespace mRemoteUG.Connection.Protocol
{
	public enum ProtocolType
	{
        [LocalizedAttributes.LocalizedDescription("strRDP")]
        RDP = 0,
        // VNC, HTTP, HTTPS, ICA and IntApp are retained, unused, only so that Enum.Parse()
        // does not throw when loading a connection file saved by a build that still had
        // them. Connections using one are skipped on load (see XmlConnectionsDeserializer)
        // and they are not offered anywhere in the UI (see ProtocolTypes.Supported).
        [Obsolete("Removed from this build; retained so old connection files parse without throwing")]
        VNC = 1,
        [LocalizedAttributes.LocalizedDescription("strSsh1")]
        SSH1 = 2,
        [LocalizedAttributes.LocalizedDescription("strSsh2")]
        SSH2 = 3,
        [LocalizedAttributes.LocalizedDescription("strTelnet")]
        Telnet = 4,
        [LocalizedAttributes.LocalizedDescription("strRlogin")]
        Rlogin = 5,
        [LocalizedAttributes.LocalizedDescription("strRAW")]
        RAW = 6,
        [Obsolete("Removed from this build; retained so old connection files parse without throwing")]
        HTTP = 7,
        [Obsolete("Removed from this build; retained so old connection files parse without throwing")]
        HTTPS = 8,
        [Obsolete("Removed from this build; retained so old connection files parse without throwing")]
        ICA = 9,
        [Obsolete("Removed from this build; retained so old connection files parse without throwing")]
        IntApp = 20
	}
}
