using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using mRemoteUG.App;
using mRemoteUG.Connection.Protocol;
using mRemoteUG.Connection.Protocol.RDP;
using mRemoteUG.Container;
using mRemoteUG.Messages;
using mRemoteUG.UI;
using mRemoteUG.UI.Forms;
using mRemoteUG.UI.Panels;
using mRemoteUG.UI.Window;


namespace mRemoteUG.Connection;

public class ConnectionInitiator : IConnectionInitiator
{
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
        InterfaceControl? interfaceControl = FindConnectionContainer(connectionInfo);
        if (interfaceControl == null)
        {
            return false;
        }

        var connectionWindow = (ConnectionWindow?)interfaceControl.FindForm();
        connectionWindow?.Focus();
        var findForm = (ConnectionWindow?)interfaceControl.FindForm();
        if (findForm != null)
        {
            FrmMain.Default.Layout.ActivateDocument(findForm);
        }

        // The tab is selected only when the control still sits on one; a control already
        // taken off its tab has no tab to bring forward.
        if (interfaceControl.Parent is TabPage tabPage && tabPage.Parent is TabControl tabController)
        {
            tabController.SelectedTab = tabPage;
        }

        return true;
    }

    #region Private
    private void OpenConnection(ContainerInfo containerInfo, ConnectionInfo.Force force, Form? conForm)
    {
        List<ConnectionInfo> children = containerInfo.Children;
        if (children.Count == 0)
        {
            return;
        }

        foreach (ConnectionInfo child in children)
        {
            var childAsContainer = child as ContainerInfo;
            if (childAsContainer != null)
            {
                OpenConnection(childAsContainer, force, conForm);
            }
            else
            {
                OpenConnection(child, force, conForm);
            }
        }
    }

    private void OpenConnection(ConnectionInfo connectionInfo, ConnectionInfo.Force force, Form? conForm)
    {
        try
        {
            if (connectionInfo.Hostname == "")
            {
                Runtime.MessageCollector.AddMessage(
                    MessageClass.WarningMsg,
                    Language.strConnectionOpenFailedNoHostname);
                return;
            }

            if ((force & ConnectionInfo.Force.DoNotJump) != ConnectionInfo.Force.DoNotJump)
            {
                if (SwitchToOpenConnection(connectionInfo))
                {
                    return;
                }
            }

            ProtocolBase? newProtocol = ProtocolFactory.CreateProtocol(connectionInfo);
            if (newProtocol == null)
            {
                return;
            }

            string? connectionPanel = SetConnectionPanel(connectionInfo, force);
            if (string.IsNullOrEmpty(connectionPanel))
            {
                return;
            }

            Form connectionForm = SetConnectionForm(conForm, connectionPanel);
            Control connectionContainer = SetConnectionContainer(connectionInfo, connectionForm);
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

    private static InterfaceControl? FindConnectionContainer(ConnectionInfo connectionInfo)
    {
        if (connectionInfo.OpenConnections.Count <= 0)
        {
            return null;
        }

        // No window list yet means no windows yet, so nothing can be showing this connection.
        WindowList? windowList = Runtime.WindowList;
        if (windowList == null)
        {
            return null;
        }

        for (int i = 0; i <= windowList.Count - 1; i++)
        {
            var window = windowList[i] as ConnectionWindow;
            ConnectionWindow? connectionWindow = window;
            if (connectionWindow?.TabController == null)
            {
                continue;
            }

            foreach (TabPage t in connectionWindow.TabController.TabPages)
            {
                var ic = t.Controls[0] as InterfaceControl;
                if (ic == null)
                {
                    continue;
                }

                if (ic.Info == connectionInfo)
                {
                    return ic;
                }
            }
        }
        return null;
    }

    internal static string? SetConnectionPanel(ConnectionInfo connectionInfo, ConnectionInfo.Force force)
    {
        string connectionPanel = "";
        if (connectionInfo.Panel == ""
            || (force & ConnectionInfo.Force.OverridePanel) == ConnectionInfo.Force.OverridePanel
               | Settings.Default.AlwaysShowPanelSelectionDlg)
        {
            // ShowDialog does not dispose, and this runs once per Session opened whenever the
            // Connection has no Panel of its own - so without the using it was a whole Form and
            // its components each time.
            using (var frmPnl = new FrmChoosePanel())
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

    private static Form SetConnectionForm(Form? conForm, string connectionPanel)
    {
        Form? connectionForm = conForm
            ?? (Runtime.WindowList ?? throw new InvalidOperationException("The main form has no window list yet."))
                .FromString(connectionPanel);

        if (connectionForm == null)
        {
            // AddPanel has already logged why; this replaces the NRE on Focus() below.
            connectionForm = PanelAdder.AddPanel(connectionPanel)
                ?? throw new InvalidOperationException("The connection panel could not be added.");
        }
        else
        {
            FrmMain.Default.Layout.ActivateDocument((ConnectionWindow)connectionForm);
        }

        connectionForm.Focus();
        return connectionForm;
    }

    private static Control SetConnectionContainer(ConnectionInfo connectionInfo, Form connectionForm)
    {
        // Null only when AddConnectionTab caught and logged an exception. Going on would host the
        // session in an InterfaceControl with no parent; this stops the open there instead.
        Control connectionContainer = ((ConnectionWindow)connectionForm).AddConnectionTab(connectionInfo)
            ?? throw new InvalidOperationException("The connection tab could not be added.");
        return connectionContainer;
    }

    private static void SetConnectionFormEventHandlers(ProtocolBase newProtocol, Form connectionForm)
    {
        newProtocol.Closed += ((ConnectionWindow)connectionForm).OnProtocolClosed;
    }

    private void SetConnectionEventHandlers(ProtocolBase newProtocol)
    {
        newProtocol.Disconnected += Prot_Event_Disconnected;
        newProtocol.Connected += Prot_Event_Connected;
        newProtocol.Closed += Prot_Event_Closed;
        newProtocol.ErrorOccured += Prot_Event_ErrorOccured;
    }

    private static void BuildConnectionInterfaceController(
        ConnectionInfo connectionInfo,
        ProtocolBase newProtocol,
        Control connectionContainer)
    {
        newProtocol.InterfaceControl = new InterfaceControl(connectionContainer, newProtocol, connectionInfo);
    }
    #endregion

    #region Event handlers
    [SuppressMessage("Performance", "CA1863",
                     Justification = "The format is a localized resource read at run time; "
                                     + "a cached CompositeFormat would freeze the UI language.")]
    private static void Prot_Event_Disconnected(object sender, string disconnectedMessage)
    {
        try
        {
            Runtime.MessageCollector.AddMessage(
                MessageClass.InformationMsg,
                string.Format(CultureInfo.CurrentCulture, Language.strProtocolEventDisconnected, disconnectedMessage),
                true);

            var prot = (ProtocolBase)sender;
            if (prot.AssignedInterfaceControl.Info.Protocol != ProtocolType.RDP)
            {
                return;
            }

            string reasonCode = disconnectedMessage.Split("\r\n".ToCharArray())[0];
            string desc = disconnectedMessage.Replace("\r\n", " ");

            if (Convert.ToInt32(reasonCode, CultureInfo.InvariantCulture) > 3)
            {
                Runtime.MessageCollector.AddMessage(
                    MessageClass.WarningMsg,
                    Language.strRdpDisconnected + Environment.NewLine + desc);
            }
        }
        catch (Exception ex)
        {
            Runtime.MessageCollector.AddExceptionMessage(Language.strProtocolEventDisconnectFailed, ex);
        }
    }

    [SuppressMessage("Performance", "CA1863",
                     Justification = "The format is a localized resource read at run time; "
                                     + "a cached CompositeFormat would freeze the UI language.")]
    private void Prot_Event_Closed(object sender)
    {
        try
        {
            var prot = (ProtocolBase)sender;
            Runtime.MessageCollector.AddMessage(MessageClass.InformationMsg, Language.strConnenctionCloseEvent, true);
            string connDetail = prot.AssignedInterfaceControl.Info.Hostname != ""
                ? prot.AssignedInterfaceControl.Info.Hostname
                : "UNKNOWN";

            // onlyLog: a routine close is not worth stealing the UI thread to slide the
            // notification panel open (MessageFocusDecorator) while the session is tearing down.
            Runtime.MessageCollector.AddMessage(
                MessageClass.InformationMsg,
                string.Format(
                    CultureInfo.CurrentCulture,
                    Language.strConnenctionClosedByUser,
                    connDetail,
                    prot.AssignedInterfaceControl.Info.Protocol,
                    Environment.UserName),
                true);
            prot.AssignedInterfaceControl.Info.OpenConnections.Remove(prot);
            if (_activeConnections.Contains(prot.AssignedInterfaceControl.Info.ConstantID))
            {
                _activeConnections.Remove(prot.AssignedInterfaceControl.Info.ConstantID);
            }
        }
        catch (Exception ex)
        {
            Runtime.MessageCollector.AddExceptionMessage(Language.strConnenctionCloseEventFailed, ex);
        }
    }

    [SuppressMessage("Performance", "CA1863",
                     Justification = "The format is a localized resource read at run time; "
                                     + "a cached CompositeFormat would freeze the UI language.")]
    private static void Prot_Event_Connected(object sender)
    {
        var prot = (ProtocolBase)sender;
        Runtime.MessageCollector.AddMessage(MessageClass.InformationMsg, Language.strConnectionEventConnected, true);
        Runtime.MessageCollector.AddMessage(
            MessageClass.InformationMsg,
            string.Format(
                CultureInfo.CurrentCulture,
                Language.strConnectionEventConnectedDetail,
                prot.AssignedInterfaceControl.Info.Hostname,
                prot.AssignedInterfaceControl.Info.Protocol,
                Environment.UserName,
                prot.AssignedInterfaceControl.Info.Description,
                prot.AssignedInterfaceControl.Info.UserField));
    }

    [SuppressMessage("Performance", "CA1863",
                     Justification = "The format is a localized resource read at run time; "
                                     + "a cached CompositeFormat would freeze the UI language.")]
    private static void Prot_Event_ErrorOccured(object sender, string errorMessage)
    {
        try
        {
            Runtime.MessageCollector.AddMessage(
                MessageClass.InformationMsg,
                Language.strConnectionEventErrorOccured,
                true);
            var prot = (ProtocolBase)sender;

            if (prot.AssignedInterfaceControl.Info.Protocol != ProtocolType.RDP)
            {
                return;
            }

            if (Convert.ToInt32(errorMessage, CultureInfo.InvariantCulture) > -1)
            {
                Runtime.MessageCollector.AddMessage(
                    MessageClass.WarningMsg,
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Language.strConnectionRdpErrorDetail,
                        errorMessage,
                        RdpProtocol.FatalErrors.GetError(errorMessage)));
            }
        }
        catch (Exception ex)
        {
            Runtime.MessageCollector.AddExceptionMessage(Language.strConnectionEventConnectionFailed, ex);
        }
    }
    #endregion
}
