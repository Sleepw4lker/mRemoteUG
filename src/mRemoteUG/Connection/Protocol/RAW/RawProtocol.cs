namespace mRemoteUG.Connection.Protocol.RAW;

public class RawProtocol : PuttyBase
{
    public RawProtocol()
    {
        PuttyProtocol = PuttyProtocolKind.raw;
    }

    public enum Defaults
    {
        Port = 23
    }
}
