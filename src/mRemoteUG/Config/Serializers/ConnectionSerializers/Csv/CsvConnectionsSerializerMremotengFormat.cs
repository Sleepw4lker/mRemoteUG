using System;
using System.Linq;
using System.Text;
using mRemoteUG.Connection;
using mRemoteUG.Container;
using mRemoteUG.Security;
using mRemoteUG.Tools;
using mRemoteUG.Tree;
using mRemoteUG.Tree.Root;

namespace mRemoteUG.Config.Serializers.Csv
{
	public class CsvConnectionsSerializerMremotengFormat : ISerializer<ConnectionInfo,string>
    {
        private readonly SaveFilter _saveFilter;

        public CsvConnectionsSerializerMremotengFormat(SaveFilter saveFilter)
        {
            saveFilter.ThrowIfNull(nameof(saveFilter));

            _saveFilter = saveFilter;
        }

        public string Serialize(ConnectionTreeModel connectionTreeModel)
        {
            connectionTreeModel.ThrowIfNull(nameof(connectionTreeModel));

            var rootNode = connectionTreeModel.RootNodes.First(node => node is RootNodeInfo);
            return Serialize(rootNode);
        }

        public string Serialize(ConnectionInfo serializationTarget)
        {
            serializationTarget.ThrowIfNull(nameof(serializationTarget));
            var sb = new StringBuilder();

            WriteCsvHeader(sb);
            SerializeNodesRecursive(serializationTarget, sb);
            return sb.ToString();
        }

        private void WriteCsvHeader(StringBuilder sb)
        {
            sb.Append("Name;Id;Parent;NodeType;Description;Icon;Panel;");
            if (_saveFilter.SaveUsername)
                sb.Append("Username;");
            if (_saveFilter.SavePassword)
                sb.Append("Password;");
            if (_saveFilter.SaveDomain)
                sb.Append("Domain;");
            sb.Append("Hostname;Protocol;PuttySession;Port;ConnectToConsole;UseCredSsp;RDPAuthenticationLevel;LoadBalanceInfo;Colors;Resolution;AutomaticResize;DisplayWallpaper;DisplayThemes;EnableFontSmoothing;EnableDesktopComposition;CacheBitmaps;RedirectDiskDrives;RedirectPorts;RedirectPrinters;RedirectSmartCards;RedirectClipboard;RedirectSound;RedirectKeys;MacAddress;UserField;RDGatewayUsageMethod;RDGatewayHostname;RDGatewayUseConnectionCredentials;RDGatewayUsername;RDGatewayPassword;RDGatewayDomain;");
            if (_saveFilter.SaveInheritance)
                sb.Append("InheritCacheBitmaps;InheritColors;InheritDescription;InheritDisplayThemes;InheritDisplayWallpaper;InheritEnableFontSmoothing;InheritEnableDesktopComposition;InheritDomain;InheritIcon;InheritPanel;InheritPassword;InheritPort;InheritProtocol;InheritPuttySession;InheritRedirectDiskDrives;InheritRedirectKeys;InheritRedirectPorts;InheritRedirectPrinters;InheritRedirectSmartCards;InheritRedirectClipboard;InheritRedirectSound;InheritResolution;InheritAutomaticResize;InheritUseConsoleSession;InheritUseCredSsp;InheritUsername;InheritRDPAuthenticationLevel;InheritLoadBalanceInfo;InheritMacAddress;InheritUserField;InheritRDGatewayUsageMethod;InheritRDGatewayHostname;InheritRDGatewayUseConnectionCredentials;InheritRDGatewayUsername;InheritRDGatewayPassword;InheritRDGatewayDomain;InheritRDPAlertIdleTimeout;InheritRDPMinutesToIdleTimeout;InheritSoundQuality");
        }

        private void SerializeNodesRecursive(ConnectionInfo node, StringBuilder sb)
        {
            var nodeAsContainer = node as ContainerInfo;
            if (nodeAsContainer != null)
            {
                foreach (var child in nodeAsContainer.Children)
                {
                    SerializeNodesRecursive(child, sb);
                }
            }

            // dont serialize the root node
            if (node is RootNodeInfo)
                return;

            SerializeConnectionInfo(node, sb);
        }

        private void SerializeConnectionInfo(ConnectionInfo con, StringBuilder sb)
        {
            sb.AppendLine();
            sb.Append(FormatForCsv(con.Name))
                .Append(FormatForCsv(con.ConstantID))
                .Append(FormatForCsv(con.Parent?.ConstantID ?? ""))
                .Append(FormatForCsv(con.GetTreeNodeType()))
                .Append(FormatForCsv(con.Description))
                .Append(FormatForCsv(con.Icon))
                .Append(FormatForCsv(con.Panel));

            if (_saveFilter.SaveUsername)
                sb.Append(FormatForCsv(con.Username));

            if (_saveFilter.SavePassword)
                sb.Append(FormatForCsv(con.Password));

            if (_saveFilter.SaveDomain)
                sb.Append(FormatForCsv(con.Domain));

            sb.Append(FormatForCsv(con.Hostname))
                .Append(FormatForCsv(con.Protocol))
                .Append(FormatForCsv(con.PuttySession))
                .Append(FormatForCsv(con.Port))
                .Append(FormatForCsv(con.UseConsoleSession))
                .Append(FormatForCsv(con.UseCredSsp))
                .Append(FormatForCsv(con.RDPAuthenticationLevel))
                .Append(FormatForCsv(con.LoadBalanceInfo))
                .Append(FormatForCsv(con.Colors))
                .Append(FormatForCsv(con.Resolution))
                .Append(FormatForCsv(con.AutomaticResize))
                .Append(FormatForCsv(con.DisplayWallpaper))
                .Append(FormatForCsv(con.DisplayThemes))
                .Append(FormatForCsv(con.EnableFontSmoothing))
                .Append(FormatForCsv(con.EnableDesktopComposition))
                .Append(FormatForCsv(con.CacheBitmaps))
                .Append(FormatForCsv(con.RedirectDiskDrives))
                .Append(FormatForCsv(con.RedirectPorts))
                .Append(FormatForCsv(con.RedirectPrinters))
                .Append(FormatForCsv(con.RedirectSmartCards))
                .Append(FormatForCsv(con.RedirectClipboard))
                .Append(FormatForCsv(con.RedirectSound))
                .Append(FormatForCsv(con.RedirectKeys))
                .Append(FormatForCsv(con.MacAddress))
                .Append(FormatForCsv(con.UserField))
                .Append(FormatForCsv(con.RDGatewayUsageMethod))
                .Append(FormatForCsv(con.RDGatewayHostname))
                .Append(FormatForCsv(con.RDGatewayUseConnectionCredentials))
                .Append(FormatForCsv(con.RDGatewayUsername))
                .Append(FormatForCsv(con.RDGatewayPassword))
                .Append(FormatForCsv(con.RDGatewayDomain));


            if (!_saveFilter.SaveInheritance)
                return;

            sb.Append(FormatForCsv(con.Inheritance.CacheBitmaps))
                .Append(FormatForCsv(con.Inheritance.Colors))
                .Append(FormatForCsv(con.Inheritance.Description))
                .Append(FormatForCsv(con.Inheritance.DisplayThemes))
                .Append(FormatForCsv(con.Inheritance.DisplayWallpaper))
                .Append(FormatForCsv(con.Inheritance.EnableFontSmoothing))
                .Append(FormatForCsv(con.Inheritance.EnableDesktopComposition))
                .Append(FormatForCsv(con.Inheritance.Domain))
                .Append(FormatForCsv(con.Inheritance.Icon))
                .Append(FormatForCsv(con.Inheritance.Panel))
                .Append(FormatForCsv(con.Inheritance.Password))
                .Append(FormatForCsv(con.Inheritance.Port))
                .Append(FormatForCsv(con.Inheritance.Protocol))
                .Append(FormatForCsv(con.Inheritance.PuttySession))
                .Append(FormatForCsv(con.Inheritance.RedirectDiskDrives))
                .Append(FormatForCsv(con.Inheritance.RedirectKeys))
                .Append(FormatForCsv(con.Inheritance.RedirectPorts))
                .Append(FormatForCsv(con.Inheritance.RedirectPrinters))
                .Append(FormatForCsv(con.Inheritance.RedirectSmartCards))
                .Append(FormatForCsv(con.Inheritance.RedirectClipboard))
                .Append(FormatForCsv(con.Inheritance.RedirectSound))
                .Append(FormatForCsv(con.Inheritance.Resolution))
                .Append(FormatForCsv(con.Inheritance.AutomaticResize))
                .Append(FormatForCsv(con.Inheritance.UseConsoleSession))
                .Append(FormatForCsv(con.Inheritance.UseCredSsp))
                .Append(FormatForCsv(con.Inheritance.Username))
                .Append(FormatForCsv(con.Inheritance.RDPAuthenticationLevel))
                .Append(FormatForCsv(con.Inheritance.LoadBalanceInfo))
                .Append(FormatForCsv(con.Inheritance.MacAddress))
                .Append(FormatForCsv(con.Inheritance.UserField))
                .Append(FormatForCsv(con.Inheritance.RDGatewayUsageMethod))
                .Append(FormatForCsv(con.Inheritance.RDGatewayHostname))
                .Append(FormatForCsv(con.Inheritance.RDGatewayUseConnectionCredentials))
                .Append(FormatForCsv(con.Inheritance.RDGatewayUsername))
                .Append(FormatForCsv(con.Inheritance.RDGatewayPassword))
                .Append(FormatForCsv(con.Inheritance.RDGatewayDomain))
                .Append(FormatForCsv(con.Inheritance.RDPAlertIdleTimeout))
                .Append(FormatForCsv(con.Inheritance.RDPMinutesToIdleTimeout))
                .Append(FormatForCsv(con.Inheritance.SoundQuality));
        }

        private string FormatForCsv(object value)
        {
            var cleanedString = value.ToString().Replace(";", "");
            return cleanedString + ";";
        }
    }
}