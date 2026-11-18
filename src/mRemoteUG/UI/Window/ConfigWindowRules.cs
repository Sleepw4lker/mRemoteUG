using mRemoteUG.Connection;
using mRemoteUG.Connection.Protocol;
using mRemoteUG.Connection.Protocol.RDP;
using mRemoteUG.Container;
using mRemoteUG.Tree.Root;

namespace mRemoteUG.UI.Window;

/// <summary>
/// The decisions <see cref="ConfigWindow"/> makes about what it shows: which properties its grid
/// hides, and which of its toolbar buttons are enabled and checked for the selected object.
/// </summary>
/// <remarks>
/// Moved out of the window unchanged, so they can be tested without a form. The window asks
/// these and applies the answer; it decides nothing itself.
/// </remarks>
internal static class ConfigWindowRules
{
    /// <summary>
    /// The view buttons checked after one of them is checked or unchecked.
    /// </summary>
    /// <remarks>
    /// Checking a view unchecks the other three; unchecking one leaves the others as they are.
    /// </remarks>
    internal static ConfigToolbarButtons SetChecked(
        ConfigToolbarButtons checkedViews,
        ConfigToolbarButtons view,
        bool value)
    {
        return value ? view : checkedViews & ~view;
    }

    /// <summary>
    /// The toolbar state, and what the grid shows, when <paramref name="propertyGridObject"/> is
    /// selected while <paramref name="checkedViews"/> are checked.
    /// </summary>
    /// <exception cref="InvalidCastException">
    /// An inheritance object whose parent is not a connection, selected in the inheritance view.
    /// </exception>
    /// <exception cref="NullReferenceException">
    /// An inheritance object without a parent, selected in the inheritance view.
    /// </exception>
    internal static ConfigToolbarState ToolbarFor(object? propertyGridObject, ConfigToolbarButtons checkedViews)
    {
        if (propertyGridObject is ConnectionInfo connectionInfo)
        {
            if (connectionInfo is ContainerInfo containerInfo)
            {
                if (containerInfo is RootNodeInfo rootNodeInfo)
                {
                    return RootToolbar(rootNodeInfo, checkedViews);
                }

                ConfigToolbarButtons folderEnabled =
                    ConfigToolbarButtons.Properties
                    | ConfigToolbarButtons.Icon
                    | (containerInfo.Parent != null && !(containerInfo.Parent is RootNodeInfo)
                        ? ConfigToolbarButtons.Inheritance
                        : ConfigToolbarButtons.None);

                return new ConfigToolbarState(
                    folderEnabled, ConfigToolbarButtons.Properties, true, containerInfo, containerInfo);
            }

            return ConnectionToolbar(connectionInfo, checkedViews);
        }

        if (propertyGridObject is ConnectionInfoInheritance inheritance)
        {
            return InheritanceToolbar(inheritance, checkedViews);
        }

        return new ConfigToolbarState(ConfigToolbarButtons.None, checkedViews, false, null, null);
    }

    private static ConfigToolbarState RootToolbar(RootNodeInfo rootNodeInfo, ConfigToolbarButtons checkedViews)
    {
        // ReSharper disable once SwitchStatementMissingSomeCases
        switch (rootNodeInfo.Type)
        {
            case RootNodeType.Connection:
                return new ConfigToolbarState(
                    ConfigToolbarButtons.Properties
                    | ConfigToolbarButtons.DefaultProperties
                    | ConfigToolbarButtons.DefaultInheritance,
                    ConfigToolbarButtons.Properties,
                    true,
                    rootNodeInfo,
                    rootNodeInfo);
            case RootNodeType.PuttySessions:
                return new ConfigToolbarState(
                    ConfigToolbarButtons.Properties, ConfigToolbarButtons.Properties, true, rootNodeInfo, rootNodeInfo);
        }

        return new ConfigToolbarState(ConfigToolbarButtons.None, checkedViews, true, rootNodeInfo, rootNodeInfo);
    }

    private static ConfigToolbarState ConnectionToolbar(
        ConnectionInfo connectionInfo,
        ConfigToolbarButtons checkedViews)
    {
        const ConfigToolbarButtons ownViews = ConfigToolbarButtons.Properties
                                              | ConfigToolbarButtons.Inheritance
                                              | ConfigToolbarButtons.Icon
                                              | ConfigToolbarButtons.HostStatus;

        if (checkedViews.HasFlag(ConfigToolbarButtons.Properties))
        {
            ConfigToolbarButtons enabled =
                ConfigToolbarButtons.Properties
                | ConfigToolbarButtons.Icon
                | ConfigToolbarButtons.HostStatus
                | (!(connectionInfo is PuttySessionInfo) &&
                   connectionInfo.Parent != null &&
                   !(connectionInfo.Parent is RootNodeInfo)
                    ? ConfigToolbarButtons.Inheritance
                    : ConfigToolbarButtons.None);

            return new ConfigToolbarState(enabled, checkedViews, true, connectionInfo, connectionInfo);
        }

        if (checkedViews.HasFlag(ConfigToolbarButtons.DefaultProperties))
        {
            if (connectionInfo is DefaultConnectionInfo)
            {
                return new ConfigToolbarState(
                    ConfigToolbarButtons.Properties
                    | ConfigToolbarButtons.DefaultProperties
                    | ConfigToolbarButtons.DefaultInheritance
                    | ConfigToolbarButtons.Icon,
                    checkedViews,
                    true,
                    connectionInfo,
                    connectionInfo);
            }

            return new ConfigToolbarState(
                ownViews, ConfigToolbarButtons.Properties, true, connectionInfo, connectionInfo);
        }

        if (checkedViews.HasFlag(ConfigToolbarButtons.Inheritance))
        {
            return new ConfigToolbarState(ownViews, checkedViews, true, connectionInfo.Inheritance, connectionInfo);
        }

        if (checkedViews.HasFlag(ConfigToolbarButtons.DefaultInheritance))
        {
            return new ConfigToolbarState(
                ownViews, ConfigToolbarButtons.Properties, true, connectionInfo, connectionInfo);
        }

        return new ConfigToolbarState(ConfigToolbarButtons.None, checkedViews, false, null, connectionInfo);
    }

    private static ConfigToolbarState InheritanceToolbar(
        ConnectionInfoInheritance inheritance,
        ConfigToolbarButtons checkedViews)
    {
        if (checkedViews.HasFlag(ConfigToolbarButtons.Inheritance))
        {
            // Only a connection's own inheritance is shown with this view checked, and its parent
            // is that connection. The throw replaces the NRE below, which ConfigWindow also caught.
            ConnectionInfo parent = (ConnectionInfo?)inheritance.Parent
                ?? throw new InvalidOperationException("The inheritance shown belongs to no connection.");
            ConfigToolbarButtons enabled =
                ConfigToolbarButtons.Properties
                | ConfigToolbarButtons.Inheritance
                | ConfigToolbarButtons.Icon
                | (!parent.IsContainer ? ConfigToolbarButtons.HostStatus : ConfigToolbarButtons.None);

            return new ConfigToolbarState(enabled, ConfigToolbarButtons.Inheritance, true, inheritance, parent);
        }

        if (checkedViews.HasFlag(ConfigToolbarButtons.DefaultInheritance))
        {
            return new ConfigToolbarState(
                ConfigToolbarButtons.Properties
                | ConfigToolbarButtons.DefaultProperties
                | ConfigToolbarButtons.DefaultInheritance,
                ConfigToolbarButtons.DefaultInheritance,
                true,
                inheritance,
                null);
        }

        return new ConfigToolbarState(ConfigToolbarButtons.None, checkedViews, true, inheritance, null);
    }

    /// <summary>
    /// The names of the properties the grid hides while it shows <paramref name="selectedObject"/>.
    /// </summary>
    /// <remarks>
    /// A name can appear more than once; the grid hides it either way.
    /// </remarks>
    internal static string[] HiddenProperties(object? selectedObject)
    {
        var strHide = new List<string>();
        var o = selectedObject as RootNodeInfo;
        if (o != null)
        {
            RootNodeInfo rootInfo = o;
            if (rootInfo.Type == RootNodeType.PuttySessions)
            {
                strHide.Add("Password");
            }
            strHide.Add("CacheBitmaps");
            strHide.Add("Colors");
            strHide.Add("DisplayThemes");
            strHide.Add("DisplayWallpaper");
            strHide.Add("EnableFontSmoothing");
            strHide.Add("EnableDesktopComposition");
            strHide.Add("Domain");
            strHide.Add("RDGatewayDomain");
            strHide.Add("RDGatewayHostname");
            strHide.Add("RDGatewayPassword");
            strHide.Add("RDGatewayUsageMethod");
            strHide.Add("RDGatewayUseConnectionCredentials");
            strHide.Add("RDGatewayUsername");
            strHide.Add("RDPAuthenticationLevel");
            strHide.Add("RDPMinutesToIdleTimeout");
            strHide.Add("RDPAlertIdleTimeout");
            strHide.Add("LoadBalanceInfo");
            strHide.Add("RedirectDiskDrives");
            strHide.Add("RedirectKeys");
            strHide.Add("RedirectPorts");
            strHide.Add("RedirectPrinters");
            strHide.Add("RedirectSmartCards");
            strHide.Add("RedirectClipboard");
            strHide.Add("RedirectSound");
            strHide.Add("Resolution");
            strHide.Add("AutomaticResize");
            strHide.Add("UseConsoleSession");
            strHide.Add("UseCredSsp");
            strHide.Add("Icon");
            strHide.Add("Panel");
            strHide.Add("Hostname");
            strHide.Add("Username");
            strHide.Add("Protocol");
            strHide.Add("Port");
            strHide.Add("PuttySession");
            strHide.Add("MacAddress");
            strHide.Add("UserField");
            strHide.Add("Description");
            strHide.Add("SoundQuality");
        }
        else if (selectedObject is ConnectionInfo)
        {
            var conI = (ConnectionInfo)selectedObject;
            // Each protocol shows only the properties that apply to it.
            // RDP keeps its conditional sub-property rules; the PuTTY-driven protocols
            // hide the RDP-specific settings wholesale.
            switch (conI.Protocol)
            {
                case ProtocolType.RDP:
                    strHide.Add("PuttySession");
                    if (conI.RDPMinutesToIdleTimeout <= 0)
                    {
                        strHide.Add("RDPAlertIdleTimeout");
                    }
                    if (conI.RDGatewayUsageMethod == RdpProtocol.RDGatewayUsageMethod.Never)
                    {
                        strHide.Add("RDGatewayDomain");
                        strHide.Add("RDGatewayHostname");
                        strHide.Add("RDGatewayPassword");
                        strHide.Add("RDGatewayUseConnectionCredentials");
                        strHide.Add("RDGatewayUsername");
                    }
                    else if (conI.RDGatewayUseConnectionCredentials
                             == RdpProtocol.RDGatewayUseConnectionCredentials.Yes)
                    {
                        strHide.Add("RDGatewayDomain");
                        strHide.Add("RDGatewayPassword");
                        strHide.Add("RDGatewayUsername");
                    }
                    if (!(conI.Resolution == RdpProtocol.RDPResolutions.FitToWindow
                          || conI.Resolution == RdpProtocol.RDPResolutions.Fullscreen))
                    {
                        strHide.Add("AutomaticResize");
                    }
                    if (conI.RedirectSound != RdpProtocol.RDPSounds.BringToThisComputer)
                    {
                        strHide.Add("SoundQuality");
                    }
                    break;
                case ProtocolType.SSH1:
                case ProtocolType.SSH2:
                    strHide.Add("CacheBitmaps");
                    strHide.Add("Colors");
                    strHide.Add("DisplayThemes");
                    strHide.Add("DisplayWallpaper");
                    strHide.Add("EnableFontSmoothing");
                    strHide.Add("EnableDesktopComposition");
                    strHide.Add("Domain");
                    strHide.Add("RDGatewayDomain");
                    strHide.Add("RDGatewayHostname");
                    strHide.Add("RDGatewayPassword");
                    strHide.Add("RDGatewayUsageMethod");
                    strHide.Add("RDGatewayUseConnectionCredentials");
                    strHide.Add("RDGatewayUsername");
                    strHide.Add("RDPAuthenticationLevel");
                    strHide.Add("RDPMinutesToIdleTimeout");
                    strHide.Add("RDPAlertIdleTimeout");
                    strHide.Add("LoadBalanceInfo");
                    strHide.Add("RedirectDiskDrives");
                    strHide.Add("RedirectKeys");
                    strHide.Add("RedirectPorts");
                    strHide.Add("RedirectPrinters");
                    strHide.Add("RedirectSmartCards");
                    strHide.Add("RedirectClipboard");
                    strHide.Add("RedirectSound");
                    strHide.Add("Resolution");
                    strHide.Add("AutomaticResize");
                    strHide.Add("UseConsoleSession");
                    strHide.Add("UseCredSsp");
                    strHide.Add("SoundQuality");
                    break;
                case ProtocolType.Telnet:
                case ProtocolType.Rlogin:
                case ProtocolType.RAW:
                    strHide.Add("CacheBitmaps");
                    strHide.Add("Colors");
                    strHide.Add("DisplayThemes");
                    strHide.Add("DisplayWallpaper");
                    strHide.Add("EnableFontSmoothing");
                    strHide.Add("EnableDesktopComposition");
                    strHide.Add("Domain");
                    strHide.Add("RDGatewayDomain");
                    strHide.Add("RDGatewayHostname");
                    strHide.Add("RDGatewayPassword");
                    strHide.Add("RDGatewayUsageMethod");
                    strHide.Add("RDGatewayUseConnectionCredentials");
                    strHide.Add("RDGatewayUsername");
                    strHide.Add("RDPAuthenticationLevel");
                    strHide.Add("RDPMinutesToIdleTimeout");
                    strHide.Add("RDPAlertIdleTimeout");
                    strHide.Add("LoadBalanceInfo");
                    strHide.Add("RedirectDiskDrives");
                    strHide.Add("RedirectKeys");
                    strHide.Add("RedirectPorts");
                    strHide.Add("RedirectPrinters");
                    strHide.Add("RedirectSmartCards");
                    strHide.Add("RedirectClipboard");
                    strHide.Add("RedirectSound");
                    strHide.Add("Resolution");
                    strHide.Add("AutomaticResize");
                    strHide.Add("UseConsoleSession");
                    strHide.Add("UseCredSsp");
                    strHide.Add("SoundQuality");
                    strHide.Add("Username");
                    strHide.Add("Password");
                    break;
            }

            if (!(conI is DefaultConnectionInfo))
            {
                if (conI.Inheritance.CacheBitmaps)
                {
                    strHide.Add("CacheBitmaps");
                }

                if (conI.Inheritance.Colors)
                {
                    strHide.Add("Colors");
                }

                if (conI.Inheritance.Description)
                {
                    strHide.Add("Description");
                }

                if (conI.Inheritance.DisplayThemes)
                {
                    strHide.Add("DisplayThemes");
                }

                if (conI.Inheritance.DisplayWallpaper)
                {
                    strHide.Add("DisplayWallpaper");
                }

                if (conI.Inheritance.EnableFontSmoothing)
                {
                    strHide.Add("EnableFontSmoothing");
                }

                if (conI.Inheritance.EnableDesktopComposition)
                {
                    strHide.Add("EnableDesktopComposition");
                }

                if (conI.Inheritance.Domain)
                {
                    strHide.Add("Domain");
                }

                if (conI.Inheritance.Icon)
                {
                    strHide.Add("Icon");
                }

                if (conI.Inheritance.Password)
                {
                    strHide.Add("Password");
                }

                if (conI.Inheritance.Port)
                {
                    strHide.Add("Port");
                }

                if (conI.Inheritance.PuttySession)
                {
                    strHide.Add("PuttySession");
                }

                if (conI.Inheritance.Protocol)
                {
                    strHide.Add("Protocol");
                }

                if (conI.Inheritance.RedirectDiskDrives)
                {
                    strHide.Add("RedirectDiskDrives");
                }

                if (conI.Inheritance.RedirectKeys)
                {
                    strHide.Add("RedirectKeys");
                }

                if (conI.Inheritance.RedirectPorts)
                {
                    strHide.Add("RedirectPorts");
                }

                if (conI.Inheritance.RedirectPrinters)
                {
                    strHide.Add("RedirectPrinters");
                }

                if (conI.Inheritance.RedirectSmartCards)
                {
                    strHide.Add("RedirectSmartCards");
                }

                if (conI.Inheritance.RedirectClipboard)
                {
                    strHide.Add("RedirectClipboard");
                }

                if (conI.Inheritance.RedirectSound)
                {
                    strHide.Add("RedirectSound");
                }

                if (conI.Inheritance.Resolution)
                {
                    strHide.Add("Resolution");
                }

                if (conI.Inheritance.AutomaticResize)
                {
                    strHide.Add("AutomaticResize");
                }

                if (conI.Inheritance.UseConsoleSession)
                {
                    strHide.Add("UseConsoleSession");
                }

                if (conI.Inheritance.UseCredSsp)
                {
                    strHide.Add("UseCredSsp");
                }

                if (conI.Inheritance.RDPAuthenticationLevel)
                {
                    strHide.Add("RDPAuthenticationLevel");
                }

                if (conI.Inheritance.RDPMinutesToIdleTimeout)
                {
                    strHide.Add("RDPMinutesToIdleTimeout");
                }

                if (conI.Inheritance.RDPAlertIdleTimeout)
                {
                    strHide.Add("RDPAlertIdleTimeout");
                }

                if (conI.Inheritance.LoadBalanceInfo)
                {
                    strHide.Add("LoadBalanceInfo");
                }

                if (conI.Inheritance.Username)
                {
                    strHide.Add("Username");
                }

                if (conI.Inheritance.Panel)
                {
                    strHide.Add("Panel");
                }

                if (conI.IsContainer)
                {
                    strHide.Add("Hostname");
                }

                if (conI.Inheritance.MacAddress)
                {
                    strHide.Add("MacAddress");
                }

                if (conI.Inheritance.UserField)
                {
                    strHide.Add("UserField");
                }

                if (conI.Inheritance.RDGatewayUsageMethod)
                {
                    strHide.Add("RDGatewayUsageMethod");
                }

                if (conI.Inheritance.RDGatewayHostname)
                {
                    strHide.Add("RDGatewayHostname");
                }

                if (conI.Inheritance.RDGatewayUsername)
                {
                    strHide.Add("RDGatewayUsername");
                }

                if (conI.Inheritance.RDGatewayPassword)
                {
                    strHide.Add("RDGatewayPassword");
                }

                if (conI.Inheritance.RDGatewayDomain)
                {
                    strHide.Add("RDGatewayDomain");
                }

                if (conI.Inheritance.RDGatewayUseConnectionCredentials)
                {
                    strHide.Add("RDGatewayUseConnectionCredentials");
                }

                if (conI.Inheritance.RDGatewayHostname)
                {
                    strHide.Add("RDGatewayHostname");
                }

                if (conI.Inheritance.SoundQuality)
                {
                    strHide.Add("SoundQuality");
                }
            }
            else
            {
                strHide.Add("Hostname");
                strHide.Add("Name");
            }
        }

        return strHide.ToArray();
    }
}
