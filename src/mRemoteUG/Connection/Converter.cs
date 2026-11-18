using mRemoteUG.Connection.Protocol;

namespace mRemoteUG.Connection;

public static class Converter
{
    public static string ProtocolToString(ProtocolType protocol)
    {
        return protocol.ToString();
    }

    public static ProtocolType StringToProtocol(string protocol)
    {
        try
        {
            return Enum.Parse<ProtocolType>(protocol, true);
        }
        catch (Exception)
        {
            return ProtocolType.RDP;
        }
    }
}
