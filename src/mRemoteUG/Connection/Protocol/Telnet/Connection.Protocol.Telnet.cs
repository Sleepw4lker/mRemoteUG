namespace mRemoteUG.Connection.Protocol.Telnet;

public class ProtocolTelnet : PuttyBase
{

    public ProtocolTelnet()
    {
        this.PuttyProtocol = PuttyProtocolKind.telnet;
    }

    public enum Defaults
    {
        Port = 23
    }
}
