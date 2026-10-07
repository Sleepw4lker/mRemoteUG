using mRemoteUG.App;
using mRemoteUG.Messages;
using mRemoteUG.Tools;
using System;
using System.ComponentModel;
using mRemoteUG.Connection.Protocol;
using mRemoteUG.Tree;


namespace mRemoteUG.Connection
{
	public class PuttySessionInfo : ConnectionInfo
	{
        #region Properties
        [ReadOnly(true)]
        public override string PuttySession { get; set; }

        [ReadOnly(true)]
        public override string Name { get; set; }

        [ReadOnly(true), Browsable(false)]
        public override string Description { get; set; }

        [ReadOnly(true), Browsable(false)]
        public override string Icon
        {
            get { return "PuTTY"; }
            set { }
        }

        [ReadOnly(true), Browsable(false)]
        public override string Panel
        {
            get { return Parent?.Panel; }
            set { }
        }

        [ReadOnly(true)]
        public override string Hostname { get; set; }

        [ReadOnly(true)]
        public override string Username { get; set; }

        [ReadOnly(true), Browsable(false)]
        public override string Password { get; set; }

        [ReadOnly(true)]
        public override ProtocolType Protocol { get; set; }

        [ReadOnly(true)]
        public override int Port { get; set; }

        [ReadOnly(true), Browsable(false)]
        public override string MacAddress { get; set; }

        [ReadOnly(true), Browsable(false)]
        public override string UserField { get; set; }
        #endregion


        public override TreeNodeType GetTreeNodeType()
        {
            return TreeNodeType.PuttySession;
        }

	}
}
