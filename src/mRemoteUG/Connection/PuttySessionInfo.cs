using System.ComponentModel;

using mRemoteUG.Connection.Protocol;
using mRemoteUG.Tree;


namespace mRemoteUG.Connection;

public class PuttySessionInfo : ConnectionInfo
{
    #region Properties
    [ReadOnly(true)]
    public override string PuttySession { get; set; } = string.Empty;

    [ReadOnly(true)]
    public override string Name { get; set; } = string.Empty;

    [ReadOnly(true), Browsable(false)]
    public override string Description { get; set; } = string.Empty;

    [ReadOnly(true), Browsable(false)]
    public override string Icon
    {
        get { return "PuTTY"; }
        set { }
    }

    [ReadOnly(true), Browsable(false)]
    public override string Panel
    {
        // The one string on the record that can still be null, deliberately (ADR-0022): with no
        // parent there is no Panel, and a PuTTY Profile is never serialized. ConnectionInitiator
        // reads that null as "do not open", where an empty string would ask for a panel instead.
#pragma warning disable CS8603 // Possible null reference return.
        get { return Parent?.Panel; }
#pragma warning restore CS8603
        set { }
    }

    [ReadOnly(true)]
    public override string Hostname { get; set; } = string.Empty;

    [ReadOnly(true)]
    public override string Username { get; set; } = string.Empty;

    [ReadOnly(true), Browsable(false)]
    public override string Password { get; set; } = string.Empty;

    [ReadOnly(true)]
    public override ProtocolType Protocol { get; set; }

    [ReadOnly(true)]
    public override int Port { get; set; }

    [ReadOnly(true), Browsable(false)]
    public override string MacAddress { get; set; } = string.Empty;

    [ReadOnly(true), Browsable(false)]
    public override string UserField { get; set; } = string.Empty;
    #endregion


    public override TreeNodeType GetTreeNodeType()
    {
        return TreeNodeType.PuttySession;
    }

}
