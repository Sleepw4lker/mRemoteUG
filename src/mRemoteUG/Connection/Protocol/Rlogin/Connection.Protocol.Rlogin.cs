namespace mRemoteUG.Connection.Protocol.Rlogin;

public class ProtocolRlogin : PuttyBase
{

    public ProtocolRlogin()
    {
        this.PuttyProtocol = PuttyProtocolKind.rlogin;
    }

    public enum Defaults
    {
        Port = 513
    }
}
