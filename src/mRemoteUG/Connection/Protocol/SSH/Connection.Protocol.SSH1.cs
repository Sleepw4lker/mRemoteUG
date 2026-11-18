namespace mRemoteUG.Connection.Protocol.SSH;

public class ProtocolSSH1 : PuttyBase
{

    public ProtocolSSH1()
    {
        this.PuttyProtocol = PuttyProtocolKind.ssh;
        this.PuttySSHVersion = PuttySshVersionKind.ssh1;
    }

    public enum Defaults
    {
        Port = 22
    }
}
