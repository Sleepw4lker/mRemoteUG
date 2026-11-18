namespace mRemoteUG.Connection.Protocol.SSH;

public class ProtocolSSH2 : PuttyBase
{

    public ProtocolSSH2()
    {
        this.PuttyProtocol = PuttyProtocolKind.ssh;
        this.PuttySSHVersion = PuttySshVersionKind.ssh2;
    }

    public enum Defaults
    {
        Port = 22
    }
}
