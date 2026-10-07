using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using mRemoteUG.App;
using mRemoteUG.Connection;
using mRemoteUG.Connection.Protocol;
using mRemoteUG.Connection.Protocol.RDP;
using mRemoteUG.Container;
using mRemoteUG.Messages;
using mRemoteUG.Security;
using mRemoteUG.Tools;
using mRemoteUG.Tree;
using mRemoteUG.Tree.Root;
using mRemoteUG.UI.Forms;
using mRemoteUG.UI.TaskDialog;

namespace mRemoteUG.Config.Serializers.Xml
{
    public class XmlConnectionsDeserializer
    {
        private XmlDocument _xmlDocument;
        private double _confVersion;
        private XmlConnectionsDecryptor _decryptor;
        private string ConnectionFileName = "";
        private const double MaxSupportedConfVersion = 2.9;

        /// <summary>
        /// The oldest connection file this build can read, which is the oldest it has ever
        /// written. 2.9 is the AES-GCM envelope with a 96-bit nonce; 2.8 and below used a
        /// 128-bit nonce that the platform AesGcm cannot accept. Those files are refused here,
        /// by version, rather than left to fail later as an authentication failure and a
        /// password prompt that cannot succeed.
        /// </summary>
        private const double MinSupportedConfVersion = 2.9;
        private readonly RootNodeInfo _rootNodeInfo = new RootNodeInfo(RootNodeType.Connection);
        private readonly List<string> _skippedConnections = new List<string>();

        public Func<SecureString?>? AuthenticationRequestor { get; set; }

        public XmlConnectionsDeserializer(Func<SecureString?>? authenticationRequestor = null)
        {
            AuthenticationRequestor = authenticationRequestor;
        }

        public ConnectionTreeModel Deserialize(string xml)
        {
            return Deserialize(xml, false);
        }

        public ConnectionTreeModel Deserialize(string xml, bool import)
        {
            try
            {
                LoadXmlConnectionData(xml);
                ValidateConnectionFileVersion();

                var rootXmlElement = _xmlDocument.DocumentElement;
                InitializeRootNode(rootXmlElement);
                CreateDecryptor(_rootNodeInfo, rootXmlElement);
                var connectionTreeModel = new ConnectionTreeModel();
                connectionTreeModel.AddRootNode(_rootNodeInfo);

                var protectedString = ReadAttribute(_xmlDocument.DocumentElement, "Protected", "");
                if (!_decryptor.ConnectionsFileIsAuthentic(protectedString, _rootNodeInfo.PasswordString.ConvertToSecureString()))
                {
                    return null;
                }

                if (ReadBoolAttribute(rootXmlElement, "FullFileEncryption", false))
                {
                    var decryptedContent = _decryptor.Decrypt(rootXmlElement.InnerText);
                    rootXmlElement.InnerXml = decryptedContent;
                }

                _skippedConnections.Clear();
                _reportedMissingAttributes.Clear();
                AddNodesFromXmlRecursive(_xmlDocument.DocumentElement, _rootNodeInfo);
                ShowSkippedConnectionsSummaryIfNeeded();

                if (!import)
                    Runtime.ConnectionsService.IsConnectionsFileLoaded = true;

                return connectionTreeModel;
            }
            catch (Exception ex)
            {
                Runtime.ConnectionsService.IsConnectionsFileLoaded = false;
                Runtime.MessageCollector.AddExceptionMessage(Language.strLoadFromXmlFailed, ex);
                throw;
            }
        }

        private void LoadXmlConnectionData(string connections)
        {
            _xmlDocument = new XmlDocument();
            if (connections != "")
                _xmlDocument.LoadXml(connections);
        }

        private void ValidateConnectionFileVersion()
        {
            if (_xmlDocument.DocumentElement != null && _xmlDocument.DocumentElement.HasAttribute("ConfVersion"))
                _confVersion = Convert.ToDouble(_xmlDocument.DocumentElement.Attributes["ConfVersion"].Value.Replace(",", "."), CultureInfo.InvariantCulture);
            else
                Runtime.MessageCollector.AddMessage(MessageClass.WarningMsg, Language.strOldConffile);

            if (_confVersion < MinSupportedConfVersion)
            {
                ShowUnsupportedLegacyVersionDialogBox();
                throw new Exception(
                    $"Connection file format {_confVersion} is no longer supported " +
                    $"(oldest supported is {MinSupportedConfVersion}).");
            }

            if (!(_confVersion > MaxSupportedConfVersion)) return;
            ShowIncompatibleVersionDialogBox();
            throw new Exception($"Incompatible connection file format (file format version {_confVersion}).");
        }

        private void ShowUnsupportedLegacyVersionDialogBox()
        {
            CTaskDialog.ShowTaskDialogBox(
                FrmMain.Default,
                Application.ProductName,
                "Connection file format no longer supported",
                "This connection file was written by an older build and cannot be read. The " +
                "encryption format changed and there is no conversion path, so passwords in " +
                "it cannot be recovered by this build.",
                string.Format("{1}{0}File Format Version: {2}{0}Oldest Supported Version: {3}",
                              Environment.NewLine, ConnectionFileName, _confVersion, MinSupportedConfVersion),
                "",
                "",
                "",
                "",
                ETaskDialogButtons.Ok,
                ESysIcons.Error,
                ESysIcons.Error
            );
        }

        private void ShowIncompatibleVersionDialogBox()
        {
            CTaskDialog.ShowTaskDialogBox(
                FrmMain.Default,
                Application.ProductName,
                "Incompatible connection file format",
                $"The format of this connection file is not supported. Please upgrade to a newer version of {Application.ProductName}.",
                string.Format("{1}{0}File Format Version: {2}{0}Highest Supported Version: {3}", Environment.NewLine, ConnectionFileName, _confVersion, MaxSupportedConfVersion),
                "",
                "",
                "",
                "",
                ETaskDialogButtons.Ok,
                ESysIcons.Error,
                ESysIcons.Error
            );
        }

        private void ShowSkippedConnectionsSummaryIfNeeded()
        {
            if (_skippedConnections.Count == 0) return;

            var message = new StringBuilder();
            message.AppendLine($"{_skippedConnections.Count} connection(s) using protocols not supported by this build were skipped:");
            message.AppendLine();
            foreach (var skipped in _skippedConnections)
                message.AppendLine($"  - {skipped}");

            // Logged (not shown via MessageBox) so this stays safe to call from
            // headless import paths and doesn't require the main window to exist.
            Runtime.MessageCollector.AddMessage(MessageClass.WarningMsg, message.ToString());
        }

        private void InitializeRootNode(XmlElement connectionsRootElement)
        {
            _rootNodeInfo.Name = ReadAttribute(connectionsRootElement, "Name", _rootNodeInfo.Name).Trim();
        }

        /// <summary>
        /// Builds the decryptor for this file. There is one supported combination, AES+GCM, and
        /// one supported file version, so there is no fallback: an unreadable file is a load
        /// failure rather than a file that silently loses its passwords.
        /// </summary>
        private void CreateDecryptor(RootNodeInfo rootNodeInfo, XmlElement connectionsRootElement)
        {
            // AES and GCM are the zero values of their enums, so these fallbacks are the
            // same outcome the previous Enum.TryParse produced when it failed to parse.
            var engine = ReadEnumAttribute(connectionsRootElement, "EncryptionEngine", BlockCipherEngines.AES);

            var mode = ReadEnumAttribute(connectionsRootElement, "BlockCipherMode", BlockCipherModes.GCM);

            if (engine != BlockCipherEngines.AES || mode != BlockCipherModes.GCM)
            {
                Runtime.MessageCollector.AddMessage(MessageClass.WarningMsg,
                    $"This connections file declares '{engine}+{mode}'. Only AES+GCM is supported.");
            }

            var keyDerivationIterations = ReadIntAttribute(connectionsRootElement, "KdfIterations", 0);

            _decryptor = new XmlConnectionsDecryptor(engine, mode, rootNodeInfo)
            {
                AuthenticationRequestor = AuthenticationRequestor,
                KeyDerivationIterations = keyDerivationIterations
            };
        }

        private void AddNodesFromXmlRecursive(XmlNode parentXmlNode, ContainerInfo parentContainer)
        {
            try
            {
                if (!parentXmlNode.HasChildNodes) return;
                foreach (XmlNode xmlNode in parentXmlNode.ChildNodes)
                {
                    var treeNodeTypeString = ReadAttribute(xmlNode, "Type", "connection");
                    var nodeType = (TreeNodeType)Enum.Parse(typeof(TreeNodeType), treeNodeTypeString, true);

                    // ReSharper disable once SwitchStatementMissingSomeCases
                    switch (nodeType)
                    {
                        case TreeNodeType.Connection:
                            var connectionInfo = GetConnectionInfoFromXml(xmlNode);
                            if (connectionInfo != null && !ProtocolTypes.IsSupported(connectionInfo.Protocol))
                            {
                                _skippedConnections.Add($"{connectionInfo.Name} ({connectionInfo.Protocol})");
                                break;
                            }
                            parentContainer.AddChild(connectionInfo);
                            break;
                        case TreeNodeType.Container:
                            var containerInfo = new ContainerInfo();
                            
                            containerInfo.CopyFrom(GetConnectionInfoFromXml(xmlNode));
                            containerInfo.IsExpanded = ReadBoolAttribute(xmlNode, "Expanded", containerInfo.IsExpanded);

                            parentContainer.AddChild(containerInfo);
                            AddNodesFromXmlRecursive(xmlNode, containerInfo);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage(Language.strAddNodeFromXmlFailed, ex);
                throw;
            }
        }

        /// <summary>Attribute names already reported absent during this load.</summary>
        private readonly HashSet<string> _reportedMissingAttributes = new HashSet<string>();

        /// <summary>
        /// Reads an attribute that a file written by another version may not carry.
        /// </summary>
        /// <remarks>
        /// XmlAttributeCollection's string indexer returns null for an attribute that is not
        /// present, so the <c>xmlnode.Attributes["X"].Value</c> shape this file was written in
        /// throws on any file whose attribute set differs. It behaved worse than it looked: the
        /// throw was caught once per connection and the half-populated connection returned
        /// anyway, so a file missing a single early attribute loaded successfully with every
        /// connection silently reset to its constructor defaults, and a file missing the node
        /// <c>Type</c> attribute failed the load outright from outside that catch.
        /// <para>
        /// A connections file is the user's own data, so an absent attribute keeps the value the
        /// record already holds and says so in the log, rather than costing the rest of the
        /// connection. Reported once per attribute name per load, because a file that is missing
        /// an attribute is missing it on every node; info-level file logging is on by default, so
        /// a tester sees this without configuring anything (ADR-0013).
        /// </para>
        /// </remarks>
        private bool TryReadAttribute(XmlNode? node, string name, out string value)
        {
            value = node?.Attributes?[name]?.Value;
            if (value != null)
                return true;

            if (_reportedMissingAttributes.Add(name))
                Runtime.MessageCollector.AddMessage(MessageClass.InformationMsg,
                    $"Connections file has no '{name}' attribute. Keeping the existing value.", true);

            return false;
        }

        /// <summary>The attribute's value, or <paramref name="fallback"/> if it is absent.</summary>
        private string ReadAttribute(XmlNode? node, string name, string fallback)
        {
            return TryReadAttribute(node, name, out var value) ? value : fallback;
        }

        /// <summary>
        /// The attribute parsed as a bool, or <paramref name="fallback"/> if absent or malformed.
        /// </summary>
        /// <remarks>
        /// A present-but-unparseable value falls back rather than throwing, for the same reason as
        /// an absent one: discarding the rest of the connection is not a proportionate response to
        /// one bad value, and either way the log carries it. This also removes a second latent
        /// failure -- the few attributes that were already guarded did it as
        /// <c>bool.Parse(... ?? "")</c>, and <c>bool.Parse("")</c> throws FormatException.
        /// </remarks>
        private bool ReadBoolAttribute(XmlNode? node, string name, bool fallback)
        {
            return TryReadAttribute(node, name, out var value) && bool.TryParse(value, out var parsed)
                ? parsed
                : fallback;
        }

        private int ReadIntAttribute(XmlNode? node, string name, int fallback)
        {
            return TryReadAttribute(node, name, out var value)
                   && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : fallback;
        }

        private TEnum ReadEnumAttribute<TEnum>(XmlNode? node, string name, TEnum fallback) where TEnum : struct
        {
            return TryReadAttribute(node, name, out var value) && Enum.TryParse(value, true, out TEnum parsed)
                ? parsed
                : fallback;
        }

        private ConnectionInfo GetConnectionInfoFromXml(XmlNode xmlnode)
        {
            if (xmlnode.Attributes == null) return null;

			var connectionId = xmlnode.Attributes["Id"]?.Value;
            if (string.IsNullOrWhiteSpace(connectionId))
                connectionId = Guid.NewGuid().ToString();
			var connectionInfo = new ConnectionInfo(connectionId);

            try
            {
                connectionInfo.Name = ReadAttribute(xmlnode, "Name", connectionInfo.Name);
                connectionInfo.Description = ReadAttribute(xmlnode, "Descr", connectionInfo.Description);
                connectionInfo.Hostname = ReadAttribute(xmlnode, "Hostname", connectionInfo.Hostname);
                connectionInfo.DisplayWallpaper = ReadBoolAttribute(xmlnode, "DisplayWallpaper", connectionInfo.DisplayWallpaper);
                connectionInfo.DisplayThemes = ReadBoolAttribute(xmlnode, "DisplayThemes", connectionInfo.DisplayThemes);
                connectionInfo.CacheBitmaps = ReadBoolAttribute(xmlnode, "CacheBitmaps", connectionInfo.CacheBitmaps);

                // Credentials live on the connection in this fork; the credential vault that
                // moved them out at 2.7 upstream was removed, so they are always read here.
                connectionInfo.Username = ReadAttribute(xmlnode, "Username", connectionInfo.Username);
                // Decrypted only when the attribute is present: decrypting a fallback would
                // either fail authentication or overwrite a real password with an empty one.
                if (TryReadAttribute(xmlnode, "Password", out var encryptedPassword))
                    connectionInfo.Password = _decryptor.Decrypt(encryptedPassword);
                connectionInfo.Domain = ReadAttribute(xmlnode, "Domain", connectionInfo.Domain);

                connectionInfo.UseConsoleSession = ReadBoolAttribute(xmlnode, "ConnectToConsole", connectionInfo.UseConsoleSession);

                connectionInfo.RedirectDiskDrives = ReadBoolAttribute(xmlnode, "RedirectDiskDrives", connectionInfo.RedirectDiskDrives);
                connectionInfo.RedirectPrinters = ReadBoolAttribute(xmlnode, "RedirectPrinters", connectionInfo.RedirectPrinters);
                connectionInfo.RedirectPorts = ReadBoolAttribute(xmlnode, "RedirectPorts", connectionInfo.RedirectPorts);
                connectionInfo.RedirectSmartCards = ReadBoolAttribute(xmlnode, "RedirectSmartCards", connectionInfo.RedirectSmartCards);
                connectionInfo.RedirectClipboard = ReadBoolAttribute(xmlnode, "RedirectClipboard", connectionInfo.RedirectClipboard);

                // Protocol is read before Port because its setter resets Port to the default for
                // the new protocol, so the saved port has to be read back afterwards or it is lost.
                connectionInfo.Protocol = ReadEnumAttribute(xmlnode, "Protocol", connectionInfo.Protocol);
                connectionInfo.Port = ReadIntAttribute(xmlnode, "Port", connectionInfo.Port);

                connectionInfo.RedirectKeys = ReadBoolAttribute(xmlnode, "RedirectKeys", connectionInfo.RedirectKeys);

                // The one attribute that does not fall back to the constructed default. Files
                // written while PuTTY support was absent carry no PuttySession, and the settings
                // default is the literal "Default Settings", so falling back would invent a saved
                // session the file never named. See LoadsAFileWrittenWhilePuttySupportWasAbsent.
                connectionInfo.PuttySession = ReadAttribute(xmlnode, "PuttySession", string.Empty);

                connectionInfo.Colors = ReadEnumAttribute(xmlnode, "Colors", connectionInfo.Colors);
                connectionInfo.Resolution = ReadEnumAttribute(xmlnode, "Resolution", connectionInfo.Resolution);
                connectionInfo.RedirectSound = ReadEnumAttribute(xmlnode, "RedirectSound", connectionInfo.RedirectSound);

                // Built up on the instance rather than in an object initializer so that each flag
                // can fall back to the value that instance was constructed with.
                var inheritance = new ConnectionInfoInheritance(connectionInfo);
                inheritance.CacheBitmaps = ReadBoolAttribute(xmlnode, "InheritCacheBitmaps", inheritance.CacheBitmaps);
                inheritance.Colors = ReadBoolAttribute(xmlnode, "InheritColors", inheritance.Colors);
                inheritance.Description = ReadBoolAttribute(xmlnode, "InheritDescription", inheritance.Description);
                inheritance.DisplayThemes = ReadBoolAttribute(xmlnode, "InheritDisplayThemes", inheritance.DisplayThemes);
                inheritance.DisplayWallpaper = ReadBoolAttribute(xmlnode, "InheritDisplayWallpaper", inheritance.DisplayWallpaper);
                inheritance.Icon = ReadBoolAttribute(xmlnode, "InheritIcon", inheritance.Icon);
                inheritance.Panel = ReadBoolAttribute(xmlnode, "InheritPanel", inheritance.Panel);
                inheritance.Port = ReadBoolAttribute(xmlnode, "InheritPort", inheritance.Port);
                inheritance.Protocol = ReadBoolAttribute(xmlnode, "InheritProtocol", inheritance.Protocol);
                inheritance.PuttySession = ReadBoolAttribute(xmlnode, "InheritPuttySession", inheritance.PuttySession);
                inheritance.RedirectDiskDrives = ReadBoolAttribute(xmlnode, "InheritRedirectDiskDrives", inheritance.RedirectDiskDrives);
                inheritance.RedirectKeys = ReadBoolAttribute(xmlnode, "InheritRedirectKeys", inheritance.RedirectKeys);
                inheritance.RedirectPorts = ReadBoolAttribute(xmlnode, "InheritRedirectPorts", inheritance.RedirectPorts);
                inheritance.RedirectPrinters = ReadBoolAttribute(xmlnode, "InheritRedirectPrinters", inheritance.RedirectPrinters);
                inheritance.RedirectSmartCards = ReadBoolAttribute(xmlnode, "InheritRedirectSmartCards", inheritance.RedirectSmartCards);
                inheritance.RedirectClipboard = ReadBoolAttribute(xmlnode, "InheritRedirectClipboard", inheritance.RedirectClipboard);
                inheritance.RedirectSound = ReadBoolAttribute(xmlnode, "InheritRedirectSound", inheritance.RedirectSound);
                inheritance.Resolution = ReadBoolAttribute(xmlnode, "InheritResolution", inheritance.Resolution);
                inheritance.UseConsoleSession = ReadBoolAttribute(xmlnode, "InheritUseConsoleSession", inheritance.UseConsoleSession);
                connectionInfo.Inheritance = inheritance;

                // Same reason as the credentials above: these three inheritance flags are
                // written by this build, so they are always read.
                connectionInfo.Inheritance.Domain = ReadBoolAttribute(xmlnode, "InheritDomain", connectionInfo.Inheritance.Domain);
                connectionInfo.Inheritance.Password = ReadBoolAttribute(xmlnode, "InheritPassword", connectionInfo.Inheritance.Password);
                connectionInfo.Inheritance.Username = ReadBoolAttribute(xmlnode, "InheritUsername", connectionInfo.Inheritance.Username);
                connectionInfo.Icon = ReadAttribute(xmlnode, "Icon", connectionInfo.Icon);
                connectionInfo.Panel = ReadAttribute(xmlnode, "Panel", connectionInfo.Panel);

                connectionInfo.PleaseConnect = ReadBoolAttribute(xmlnode, "Connected", connectionInfo.PleaseConnect);

                connectionInfo.RDPAuthenticationLevel = ReadEnumAttribute(xmlnode, "RDPAuthenticationLevel", connectionInfo.RDPAuthenticationLevel);
                connectionInfo.Inheritance.RDPAuthenticationLevel = ReadBoolAttribute(xmlnode, "InheritRDPAuthenticationLevel", connectionInfo.Inheritance.RDPAuthenticationLevel);

                connectionInfo.MacAddress = ReadAttribute(xmlnode, "MacAddress", connectionInfo.MacAddress);
                connectionInfo.Inheritance.MacAddress = ReadBoolAttribute(xmlnode, "InheritMacAddress", connectionInfo.Inheritance.MacAddress);

                connectionInfo.UserField = ReadAttribute(xmlnode, "UserField", connectionInfo.UserField);
                connectionInfo.Inheritance.UserField = ReadBoolAttribute(xmlnode, "InheritUserField", connectionInfo.Inheritance.UserField);

                // Get settings
                connectionInfo.RDGatewayUsageMethod = ReadEnumAttribute(xmlnode, "RDGatewayUsageMethod", connectionInfo.RDGatewayUsageMethod);
                connectionInfo.RDGatewayHostname = ReadAttribute(xmlnode, "RDGatewayHostname", connectionInfo.RDGatewayHostname);
                connectionInfo.RDGatewayUseConnectionCredentials = ReadEnumAttribute(xmlnode, "RDGatewayUseConnectionCredentials", connectionInfo.RDGatewayUseConnectionCredentials);
                connectionInfo.RDGatewayUsername = ReadAttribute(xmlnode, "RDGatewayUsername", connectionInfo.RDGatewayUsername);
                if (TryReadAttribute(xmlnode, "RDGatewayPassword", out var encryptedGatewayPassword))
                    connectionInfo.RDGatewayPassword = _decryptor.Decrypt(encryptedGatewayPassword);
                connectionInfo.RDGatewayDomain = ReadAttribute(xmlnode, "RDGatewayDomain", connectionInfo.RDGatewayDomain);

                // Get inheritance settings
                connectionInfo.Inheritance.RDGatewayUsageMethod = ReadBoolAttribute(xmlnode, "InheritRDGatewayUsageMethod", connectionInfo.Inheritance.RDGatewayUsageMethod);
                connectionInfo.Inheritance.RDGatewayHostname = ReadBoolAttribute(xmlnode, "InheritRDGatewayHostname", connectionInfo.Inheritance.RDGatewayHostname);
                connectionInfo.Inheritance.RDGatewayUseConnectionCredentials = ReadBoolAttribute(xmlnode, "InheritRDGatewayUseConnectionCredentials", connectionInfo.Inheritance.RDGatewayUseConnectionCredentials);
                connectionInfo.Inheritance.RDGatewayUsername = ReadBoolAttribute(xmlnode, "InheritRDGatewayUsername", connectionInfo.Inheritance.RDGatewayUsername);
                connectionInfo.Inheritance.RDGatewayPassword = ReadBoolAttribute(xmlnode, "InheritRDGatewayPassword", connectionInfo.Inheritance.RDGatewayPassword);
                connectionInfo.Inheritance.RDGatewayDomain = ReadBoolAttribute(xmlnode, "InheritRDGatewayDomain", connectionInfo.Inheritance.RDGatewayDomain);

                // Get settings
                connectionInfo.EnableFontSmoothing = ReadBoolAttribute(xmlnode, "EnableFontSmoothing", connectionInfo.EnableFontSmoothing);
                connectionInfo.EnableDesktopComposition = ReadBoolAttribute(xmlnode, "EnableDesktopComposition", connectionInfo.EnableDesktopComposition);

                // Get inheritance settings
                connectionInfo.Inheritance.EnableFontSmoothing = ReadBoolAttribute(xmlnode, "InheritEnableFontSmoothing", connectionInfo.Inheritance.EnableFontSmoothing);
                connectionInfo.Inheritance.EnableDesktopComposition = ReadBoolAttribute(xmlnode, "InheritEnableDesktopComposition", connectionInfo.Inheritance.EnableDesktopComposition);

                connectionInfo.UseCredSsp = ReadBoolAttribute(xmlnode, "UseCredSsp", connectionInfo.UseCredSsp);
                connectionInfo.Inheritance.UseCredSsp = ReadBoolAttribute(xmlnode, "InheritUseCredSsp", connectionInfo.Inheritance.UseCredSsp);

                connectionInfo.LoadBalanceInfo = ReadAttribute(xmlnode, "LoadBalanceInfo", connectionInfo.LoadBalanceInfo);
                connectionInfo.AutomaticResize = ReadBoolAttribute(xmlnode, "AutomaticResize", connectionInfo.AutomaticResize);
                connectionInfo.Inheritance.LoadBalanceInfo = ReadBoolAttribute(xmlnode, "InheritLoadBalanceInfo", connectionInfo.Inheritance.LoadBalanceInfo);
                connectionInfo.Inheritance.AutomaticResize = ReadBoolAttribute(xmlnode, "InheritAutomaticResize", connectionInfo.Inheritance.AutomaticResize);

                connectionInfo.SoundQuality = ReadEnumAttribute(xmlnode, "SoundQuality", connectionInfo.SoundQuality);
                connectionInfo.Inheritance.SoundQuality = ReadBoolAttribute(xmlnode, "InheritSoundQuality", connectionInfo.Inheritance.SoundQuality);
                connectionInfo.RDPMinutesToIdleTimeout = ReadIntAttribute(xmlnode, "RDPMinutesToIdleTimeout", connectionInfo.RDPMinutesToIdleTimeout);
                connectionInfo.Inheritance.RDPMinutesToIdleTimeout = ReadBoolAttribute(xmlnode, "InheritRDPMinutesToIdleTimeout", connectionInfo.Inheritance.RDPMinutesToIdleTimeout);
                connectionInfo.RDPAlertIdleTimeout = ReadBoolAttribute(xmlnode, "RDPAlertIdleTimeout", connectionInfo.RDPAlertIdleTimeout);
                connectionInfo.Inheritance.RDPAlertIdleTimeout = ReadBoolAttribute(xmlnode, "InheritRDPAlertIdleTimeout", connectionInfo.Inheritance.RDPAlertIdleTimeout);
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddMessage(MessageClass.ErrorMsg, string.Format(Language.strGetConnectionInfoFromXmlFailed, connectionInfo.Name, ConnectionFileName, ex.Message));
            }
            return connectionInfo;
        }
    }
}