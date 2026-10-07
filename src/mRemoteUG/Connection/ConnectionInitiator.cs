using mRemoteUG.App;
using mRemoteUG.Connection.Protocol;
using mRemoteUG.Connection.Protocol.RDP;
using mRemoteUG.Container;
using mRemoteUG.Messages;
using mRemoteUG.UI.Forms;
using mRemoteUG.UI.Panels;
using mRemoteUG.UI.Window;
using System;
using System.Collections.Generic;
using System.Windows.Forms;


namespace mRemoteUG.Connection
{
    public class ConnectionInitiator : IConnectionInitiator
    {
        private readonly PanelAdder _panelAdder = new PanelAdder();
        private readonly List<string> _activeConnections = new List<string>();

        /// <summary>
        /// List of unique IDs of the currently active connections
        /// </summary>
        public IEnumerable<string> ActiveConnections => _activeConnections;

        public void OpenConnection(ContainerInfo containerInfo, ConnectionInfo.Force force = ConnectionInfo.Force.None)
        {
            OpenConnection(containerInfo, force, null);
        }

        public void OpenConnection(ConnectionInfo connectionInfo)
        {
            try
            {
                OpenConnection(connectionInfo, ConnectionInfo.Force.None);
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage(Language.strConnectionOpenFailed, ex);
            }
        }

        public void OpenConnection(ConnectionInfo connectionInfo, ConnectionInfo.Force force)
        {
            try
            {
                OpenConnection(connectionInfo, force, null);
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage(Language.strConnectionOpenFailed, ex);
            }
        }

        public bool SwitchToOpenConnection(ConnectionInfo connectionInfo)
        {
            var interfaceControl = FindConnectionContainer(connectionInfo);
            if (interfaceControl == null) return false;
            var connectionWindow = (ConnectionWindow)interfaceControl.FindForm();
            connectionWindow?.Focus();
            var findForm = (ConnectionWindow)interfaceControl.FindForm();
            if (findForm != null) FrmMain.Default.Layout.ActivateDocument(findForm);
            var tabPage = (TabPage)interfaceControl.Parent;
            if (tabPage.Parent is TabControl tabController)
                tabController.SelectedTab = tabPage;
            return true;
        }

        #region Private
        private void OpenConnection(ContainerInfo containerInfo, ConnectionInfo.Force force, Form conForm)
        {
            var children = containerInfo.Children;
            if (children.Count == 0) return;
            foreach (var child in children)
            {
                var childAsContainer = child as ContainerInfo;
                if (childAsContainer != null)
                    OpenConnection(childAsContainer, force, conForm);
                else
                    OpenConnection(child, force, conForm);
            }
        }

        private void OpenConnection(ConnectionInfo connectionInfo, ConnectionInfo.Force force, Form conForm)
        {
            try
            {
                if (connectionInfo.Hostname == "")
                {
                    Runtime.MessageCollector.AddMessage(MessageClass.WarningMsg, Language.strConnectionOpenFailedNoHostname);
                    return;
                }

                if ((force & ConnectionInfo.Force.DoNotJump) != ConnectionInfo.Force.DoNotJump)
                {
                    if (SwitchToOpenConnection(connectionInfo))
                        return;
                }

                var protocolFactory = new ProtocolFactory();
                var newProtocol = protocolFactory.CreateProtocol(connectionInfo);
                if (newProtocol == null) return;

                var connectionPanel = SetConnectionPanel(connectionInfo, force);
                if (string.IsNullOrEmpty(connectionPanel)) return;
                var connectionForm = SetConnectionForm(conForm, connectionPanel);
                var connectionContainer = SetConnectionContainer(connectionInfo, connectionForm);
                SetConnectionFormEventHandlers(newProtocol, connectionForm);
                SetConnectionEventHandlers(newProtocol);
                BuildConnectionInterfaceController(connectionInfo, newProtocol, connectionContainer);

                newProtocol.Force = force;

                if (newProtocol.Initialize() == false)
                {
                    newProtocol.Close();
                    return;
                }

                if (newProtocol.Connect() == false)
                {
                    newProtocol.Close();
                    return;
                }

                connectionInfo.OpenConnections.Add(newProtocol);
                _activeConnections.Add(connectionInfo.ConstantID);
                FrmMain.Default.SelectedConnection = connectionInfo;
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage(Language.strConnectionOpenFailed, ex);
            }
        }

        private static InterfaceControl FindConnectionContainer(ConnectionInfo connectionInfo)
        {
            if (connectionInfo.OpenConnections.Count <= 0) return null;
            for (var i = 0; i <= Runtime.WindowList.Count - 1; i++)
            {
                var window = Runtime.WindowList[i] as ConnectionWindow;
                var connectionWindow = window;
                if (connectionWindow?.TabController == null) continue;
                foreach (TabPage t in connectionWindow.TabController.TabPages)
                {
                    var ic = t.Controls[0] as InterfaceControl;
                    if (ic == null) continue;
                    if (ic.Info == connectionInfo)
                    {
                        return ic;
                    }
                }
            }
            return null;
        }

        private static string SetConnectionPanel(ConnectionInfo connectionInfo, ConnectionInfo.Force force)
        {
            var connectionPanel = "";
            if (connectionInfo.Panel == "" || (force & ConnectionInfo.Force.OverridePanel) == ConnectionInfo.Force.OverridePanel | Settings.Default.AlwaysShowPanelSelectionDlg)
            {
                // ShowDialog does not dispose, and this runs once per Session opened whenever the
                // Connection has no Panel of its own - so without the using it was a whole Form and
                // its components each time.
                using (var frmPnl = new frmChoosePanel())
                {
                    if (frmPnl.ShowDialog() == DialogResult.OK)
                    {
                        connectionPanel = frmPnl.Panel;
                    }
                    else
                    {
                        return null;
                    }
                }
            }
            else
            {
                connectionPanel = connectionInfo.Panel;
            }
            return connectionPanel;
        }

        private Form SetConnectionForm(Form conForm, string connectionPanel)
        {
            var connectionForm = conForm ?? Runtime.WindowList.FromString(connectionPanel);

            if (connectionForm == null)
                connectionForm = _panelAdder.AddPanel(connectionPanel);
            else
                FrmMain.Default.Layout.ActivateDocument((ConnectionWindow)connectionForm);

            connectionForm.Focus();
            return connectionForm;
        }

        private static Control SetConnectionContainer(ConnectionInfo connectionInfo, Form connectionForm)
        {
            Control connectionContainer = ((ConnectionWindow)connectionForm).AddConnectionTab(connectionInfo);
            return connectionContainer;
        }

        private static void SetConnectionFormEventHandlers(ProtocolBase newProtocol, Form connectionForm)
        {
            newProtocol.Closed += ((ConnectionWindow)connectionForm).Prot_Event_Closed;
        }

        private void SetConnectionEventHandlers(ProtocolBase newProtocol)
        {
            newProtocol.Disconnected += Prot_Event_Disconnected;
            newProtocol.Connected += Prot_Event_Connected;
            newProtocol.Closed += Prot_Event_Closed;
            newProtocol.ErrorOccured += Prot_Event_ErrorOccured;
        }

        private static void BuildConnectionInterfaceController(ConnectionInfo connectionInfo, ProtocolBase newProtocol, Control connectionContainer)
        {
            newProtocol.InterfaceControl = new InterfaceControl(connectionContainer, newProtocol, connectionInfo);
        }
        #endregion

        #region Event handlers
        private static void Prot_Event_Disconnected(object sender, string disconnectedMessage)
        {
            try
            {
                Runtime.MessageCollector.AddMessage(MessageClass.InformationMsg, string.Format(Language.strProtocolEventDisconnected, disconnectedMessage), true);

                var prot = (ProtocolBase)sender;
                if (prot.InterfaceControl.Info.Protocol != ProtocolType.RDP) return;
                var reasonCode = disconnectedMessage.Split("\r\n".ToCharArray())[0];
                var desc = disconnectedMessage.Replace("\r\n", " ");

                if (Convert.ToInt32(reasonCode) > 3)
                    Runtime.MessageCollector.AddMessage(MessageClass.WarningMsg, Language.strRdpDisconnected + Environment.NewLine + desc);
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage(Language.strProtocolEventDisconnectFailed, ex);
            }
        }

        private void Prot_Event_Closed(object sender)
        {
            try
            {
                var prot = (ProtocolBase)sender;
                Runtime.MessageCollector.AddMessage(MessageClass.InformationMsg, Language.strConnenctionCloseEvent, true);
                string connDetail = prot.InterfaceControl.Info.Hostname != "" ? prot.InterfaceControl.Info.Hostname : "UNKNOWN";

                // onlyLog: a routine close is not worth stealing the UI thread to slide the
                // notification panel open (MessageFocusDecorator) while the session is tearing down.
                Runtime.MessageCollector.AddMessage(MessageClass.InformationMsg, string.Format(Language.strConnenctionClosedByUser, connDetail, prot.InterfaceControl.Info.Protocol, Environment.UserName), true);
                prot.InterfaceControl.Info.OpenConnections.Remove(prot);
                if (_activeConnections.Contains(prot.InterfaceControl.Info.ConstantID))
                    _activeConnections.Remove(prot.InterfaceControl.Info.ConstantID);
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage(Language.strConnenctionCloseEventFailed, ex);
            }
        }

        private static void Prot_Event_Connected(object sender)
        {
            var prot = (ProtocolBase)sender;
            Runtime.MessageCollector.AddMessage(MessageClass.InformationMsg, Language.strConnectionEventConnected, true);
            Runtime.MessageCollector.AddMessage(MessageClass.InformationMsg, string.Format(Language.strConnectionEventConnectedDetail, prot.InterfaceControl.Info.Hostname, prot.InterfaceControl.Info.Protocol, Environment.UserName, prot.InterfaceControl.Info.Description, prot.InterfaceControl.Info.UserField));
        }

        private static void Prot_Event_ErrorOccured(object sender, string errorMessage)
        {
            try
            {
                Runtime.MessageCollector.AddMessage(MessageClass.InformationMsg, Language.strConnectionEventErrorOccured, true);
                var prot = (ProtocolBase)sender;

                if (prot.InterfaceControl.Info.Protocol != ProtocolType.RDP) return;
                if (Convert.ToInt32(errorMessage) > -1)
                    Runtime.MessageCollector.AddMessage(MessageClass.WarningMsg, string.Format(Language.strConnectionRdpErrorDetail, errorMessage, RdpProtocol.FatalErrors.GetError(errorMessage)));
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage(Language.strConnectionEventConnectionFailed, ex);
            }
        }
        #endregion
    }
}