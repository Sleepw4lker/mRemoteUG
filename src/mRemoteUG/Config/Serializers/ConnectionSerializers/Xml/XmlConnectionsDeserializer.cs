using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security;
using System.Text;
using System.Xml;

using mRemoteUG.App;
using mRemoteUG.Config.DataProviders;
using mRemoteUG.Connection;
using mRemoteUG.Connection.Protocol;
using mRemoteUG.Container;
using mRemoteUG.Messages;
using mRemoteUG.Security;
using mRemoteUG.Security.KeyDerivation;
using mRemoteUG.Tree;
using mRemoteUG.Tree.Root;
using mRemoteUG.UI.Forms;
using mRemoteUG.UI.TaskDialog;

namespace mRemoteUG.Config.Serializers.Xml;

public class XmlConnectionsDeserializer
{
    private double _confVersion;
    private readonly string _connectionFileName;
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

    /// <param name="authenticationRequestor">Asks for the file password when the file needs one.</param>
    /// <param name="connectionFileName">The path of the file the XML came from, named in the messages
    /// about it. Empty when the XML did not come from a file.</param>
    public XmlConnectionsDeserializer(
        Func<SecureString?>? authenticationRequestor = null,
        string connectionFileName = "")
    {
        AuthenticationRequestor = authenticationRequestor;
        _connectionFileName = connectionFileName;
    }

    /// <returns>The connections, or null when the file's password was not given.</returns>
    public ConnectionTreeModel? Deserialize(string xml)
    {
        return Deserialize(xml, false);
    }

    /// <returns>The connections, or null when the file's password was not given.</returns>
    public ConnectionTreeModel? Deserialize(string xml, bool import)
    {
        try
        {
            if (IsEmpty(xml))
            {
                RefuseEmptyFile();
            }

            XmlDocument xmlDocument = LoadXmlConnectionData(xml);
            ValidateConnectionFileVersion(xmlDocument.DocumentElement);

            // Not null: LoadXml refuses a document without a root element.
            XmlElement rootXmlElement = xmlDocument.DocumentElement
                ?? throw new InvalidDataException("The connections file has no root element.");
            InitializeRootNode(rootXmlElement);
            XmlConnectionsDecryptor decryptor = CreateDecryptor(_rootNodeInfo, rootXmlElement);
            var connectionTreeModel = new ConnectionTreeModel();
            connectionTreeModel.AddRootNode(_rootNodeInfo);

            string protectedString = ReadAttribute(rootXmlElement, "Protected", "");
            if (!decryptor.ConnectionsFileIsAuthentic(
                    protectedString,
                    _rootNodeInfo.PasswordString.ConvertToSecureString()))
            {
                return null;
            }

            if (ReadBoolAttribute(rootXmlElement, "FullFileEncryption", false))
            {
                string decryptedContent = decryptor.Decrypt(rootXmlElement.InnerText);
                rootXmlElement.InnerXml = decryptedContent;
            }

            _skippedConnections.Clear();
            _reportedMissingAttributes.Clear();
            AddNodesFromXmlRecursive(rootXmlElement, _rootNodeInfo, decryptor);
            ShowSkippedConnectionsSummaryIfNeeded();

            return connectionTreeModel;
        }
        catch (Exception ex)
        {
            Runtime.MessageCollector.AddExceptionMessage(Language.strLoadFromXmlFailed, ex);
            throw;
        }
    }

    private static XmlDocument LoadXmlConnectionData(string connections)
    {
        var xmlDocument = new XmlDocument();
        xmlDocument.LoadXml(connections);
        return xmlDocument;
    }

    /// <summary>
    /// Nothing but whitespace and byte-order marks. No connection file can be that: it is one
    /// that was cut off or overwritten, not one in an old format.
    /// </summary>
    private static bool IsEmpty(string xml)
    {
        return xml.All(character => char.IsWhiteSpace(character) || character == '﻿');
    }

    [DoesNotReturn]
    private void RefuseEmptyFile()
    {
        string file = string.IsNullOrEmpty(_connectionFileName)
            ? "The connection file"
            : $"The connection file at {_connectionFileName}";
        ShowRefusal(
            "Connection file is empty",
            $"{file} is empty, probably because it was cut off while being saved. {DescribeBackups()}",
            _connectionFileName);
        throw new InvalidDataException($"The connection file {_connectionFileName} is empty.");
    }

    /// <summary>Where the empty file's backups are and which is the newest, or why there is none.</summary>
    private string DescribeBackups()
    {
        string? folder = Path.GetDirectoryName(_connectionFileName);
        if (string.IsNullOrEmpty(folder))
        {
            // Not read from a file, so there is nothing to have a backup of.
            return "";
        }

        if (FileBackupLookup.AreSwitchedOff)
        {
            return "Backups are switched off in the options, so there is no backup of it.";
        }

        FileBackup? newest;
        try
        {
            newest = FileBackupLookup.Newest(_connectionFileName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The refusal still goes out; it says why it cannot name a backup.
            return $"Its backups would be in {folder}, but that folder could not be searched: {ex.Message}";
        }

        if (newest == null)
        {
            return $"No backup of it was found in {folder}.";
        }

        string taken = newest.TakenUtc.ToLocalTime().ToString("G", CultureInfo.CurrentCulture);
        return $"Its backups are in {folder}; the newest is {Path.GetFileName(newest.Path)} from {taken}.";
    }

    private void ValidateConnectionFileVersion(XmlElement? connectionsRootElement)
    {
        if (connectionsRootElement != null && connectionsRootElement.HasAttribute("ConfVersion"))
        {
            _confVersion = Convert.ToDouble(
                connectionsRootElement.GetAttribute("ConfVersion").Replace(",", "."),
                CultureInfo.InvariantCulture);
        }
        else
        {
            Runtime.MessageCollector.AddMessage(MessageClass.WarningMsg, Language.strOldConffile);
        }

        if (_confVersion < MinSupportedConfVersion)
        {
            ShowUnsupportedLegacyVersionDialogBox();
            throw new InvalidDataException(
                $"Connection file format {_confVersion} is no longer supported " +
                $"(oldest supported is {MinSupportedConfVersion}).");
        }

        if (!(_confVersion > MaxSupportedConfVersion))
        {
            return;
        }

        ShowIncompatibleVersionDialogBox();
        throw new InvalidDataException($"Incompatible connection file format (file format version {_confVersion}).");
    }

    // Not null: ProductName reads [AssemblyProduct] first, and Properties/AssemblyInfo.cs sets it.
    private static string DialogTitle => Application.ProductName!;

    /// <summary>
    /// Tells the user why a connection file is not read: its heading, the explanation, and the
    /// details (the file and the versions involved). Every refusal goes through here.
    /// Replaced by tests: the real dialog is modal and owned by the main form.
    /// </summary>
    internal static Action<string, string, string> ShowRefusal = (heading, explanation, details) =>
        CTaskDialog.ShowTaskDialogBox(
            FrmMain.Default,
            DialogTitle,
            heading,
            explanation,
            details,
            "",
            "",
            "",
            "",
            ETaskDialogButtons.Ok,
            ESysIcons.Error,
            ESysIcons.Error);

    private void ShowUnsupportedLegacyVersionDialogBox()
    {
        ShowRefusal(
            "Connection file format no longer supported",
            "This connection file was written by an older build and cannot be read. The " +
            "encryption format changed and there is no conversion path, so passwords in " +
            "it cannot be recovered by this build.",
            string.Format(CultureInfo.CurrentCulture, "{1}{0}File Format Version: {2}{0}Oldest Supported Version: {3}",
                          Environment.NewLine, _connectionFileName, _confVersion, MinSupportedConfVersion));
    }

    private void ShowIncompatibleVersionDialogBox()
    {
        ShowRefusal(
            "Incompatible connection file format",
            $"The format of this connection file is not supported. "
            + $"Please upgrade to a newer version of {Application.ProductName}.",
            string.Format(
                CultureInfo.CurrentCulture,
                "{1}{0}File Format Version: {2}{0}Highest Supported Version: {3}",
                Environment.NewLine,
                _connectionFileName,
                _confVersion,
                MaxSupportedConfVersion));
    }

    private void ShowSkippedConnectionsSummaryIfNeeded()
    {
        if (_skippedConnections.Count == 0)
        {
            return;
        }

        var message = new StringBuilder();
        message.AppendLine(
            CultureInfo.CurrentCulture,
            $"{_skippedConnections.Count} connection(s) using protocols not supported by this build were skipped:");
        message.AppendLine();
        foreach (string skipped in _skippedConnections)
        {
            message.AppendLine(CultureInfo.CurrentCulture, $"  - {skipped}");
        }

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
    private XmlConnectionsDecryptor CreateDecryptor(RootNodeInfo rootNodeInfo, XmlElement connectionsRootElement)
    {
        // AES and GCM are the zero values of their enums, so these fallbacks are the
        // same outcome the previous Enum.TryParse produced when it failed to parse.
        BlockCipherEngines engine =
            ReadEnumAttribute(connectionsRootElement, "EncryptionEngine", BlockCipherEngines.AES);

        BlockCipherModes mode = ReadEnumAttribute(connectionsRootElement, "BlockCipherMode", BlockCipherModes.GCM);

        if (engine != BlockCipherEngines.AES || mode != BlockCipherModes.GCM)
        {
            Runtime.MessageCollector.AddMessage(MessageClass.WarningMsg,
                $"This connections file declares '{engine}+{mode}'. Only AES+GCM is supported.");
        }

        // Absent means the default; present but out of range or not a number fails the load
        // here, with a message, rather than inside the key derivation.
        string? declaredIterations = connectionsRootElement.Attributes["KdfIterations"]?.Value;
        int keyDerivationIterations = KeyDerivationIterationLimits.ParseFromFile(declaredIterations);

        return new XmlConnectionsDecryptor(engine, mode, rootNodeInfo)
        {
            AuthenticationRequestor = AuthenticationRequestor,
            KeyDerivationIterations = keyDerivationIterations
        };
    }

    private void AddNodesFromXmlRecursive(
        XmlElement parentXmlNode,
        ContainerInfo parentContainer,
        XmlConnectionsDecryptor decryptor)
    {
        try
        {
            if (!parentXmlNode.HasChildNodes)
            {
                return;
            }

            foreach (XmlNode childNode in parentXmlNode.ChildNodes)
            {
                // A comment (or any other node that is not an element) is not a connection.
                // It used to be read as one with no attributes, which came back null and
                // failed the whole load inside AddChild.
                if (childNode is not XmlElement xmlNode)
                {
                    continue;
                }

                string treeNodeTypeString = ReadAttribute(xmlNode, "Type", "connection");
                TreeNodeType nodeType = Enum.Parse<TreeNodeType>(treeNodeTypeString, true);

                // ReSharper disable once SwitchStatementMissingSomeCases
                switch (nodeType)
                {
                    case TreeNodeType.Connection:
                        ConnectionInfo connectionInfo = GetConnectionInfoFromXml(xmlNode, decryptor);
                        if (!ProtocolTypes.IsSupported(connectionInfo.Protocol))
                        {
                            _skippedConnections.Add($"{connectionInfo.Name} ({connectionInfo.Protocol})");
                            break;
                        }
                        parentContainer.AddChild(connectionInfo);
                        break;
                    case TreeNodeType.Container:
                        var containerInfo = new ContainerInfo();

                        containerInfo.CopyFrom(GetConnectionInfoFromXml(xmlNode, decryptor));
                        containerInfo.IsExpanded = ReadBoolAttribute(xmlNode, "Expanded", containerInfo.IsExpanded);

                        parentContainer.AddChild(containerInfo);
                        AddNodesFromXmlRecursive(xmlNode, containerInfo, decryptor);
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
    private bool TryReadAttribute(XmlNode? node, string name, [NotNullWhen(true)] out string? value)
    {
        value = node?.Attributes?[name]?.Value;
        if (value != null)
        {
            return true;
        }

        if (_reportedMissingAttributes.Add(name))
        {
            Runtime.MessageCollector.AddMessage(MessageClass.InformationMsg,
                $"Connections file has no '{name}' attribute. Keeping the existing value.", true);
        }

        return false;
    }

    /// <summary>The attribute's value, or <paramref name="fallback"/> if it is absent.</summary>
    private string ReadAttribute(XmlNode? node, string name, string fallback)
    {
        return TryReadAttribute(node, name, out string? value) ? value : fallback;
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
        return TryReadAttribute(node, name, out string? value) && bool.TryParse(value, out bool parsed)
            ? parsed
            : fallback;
    }

    private int ReadIntAttribute(XmlNode? node, string name, int fallback)
    {
        return TryReadAttribute(node, name, out string? value)
               && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : fallback;
    }

    private TEnum ReadEnumAttribute<TEnum>(XmlNode? node, string name, TEnum fallback) where TEnum : struct
    {
        return TryReadAttribute(node, name, out string? value) && Enum.TryParse(value, true, out TEnum parsed)
            ? parsed
            : fallback;
    }

    [SuppressMessage("Performance", "CA1863",
                     Justification = "The format is a localized resource read at run time; "
                                     + "a cached CompositeFormat would freeze the UI language.")]
    private ConnectionInfo GetConnectionInfoFromXml(XmlElement xmlnode, XmlConnectionsDecryptor decryptor)
    {
        string? connectionId = xmlnode.Attributes["Id"]?.Value;
        if (string.IsNullOrWhiteSpace(connectionId))
        {
            connectionId = Guid.NewGuid().ToString();
        }

        var connectionInfo = new ConnectionInfo(connectionId);

        try
        {
            connectionInfo.Name = ReadAttribute(xmlnode, "Name", connectionInfo.Name);
            connectionInfo.Description = ReadAttribute(xmlnode, "Descr", connectionInfo.Description);
            connectionInfo.Hostname = ReadAttribute(xmlnode, "Hostname", connectionInfo.Hostname);
            connectionInfo.DisplayWallpaper = ReadBoolAttribute(
                xmlnode, "DisplayWallpaper", connectionInfo.DisplayWallpaper);
            connectionInfo.DisplayThemes = ReadBoolAttribute(xmlnode, "DisplayThemes", connectionInfo.DisplayThemes);
            connectionInfo.CacheBitmaps = ReadBoolAttribute(xmlnode, "CacheBitmaps", connectionInfo.CacheBitmaps);

            // Credentials live on the connection in this fork; the credential vault that
            // moved them out at 2.7 upstream was removed, so they are always read here.
            connectionInfo.Username = ReadAttribute(xmlnode, "Username", connectionInfo.Username);
            // Decrypted only when the attribute is present: decrypting a fallback would
            // either fail authentication or overwrite a real password with an empty one.
            if (TryReadAttribute(xmlnode, "Password", out string? encryptedPassword))
            {
                connectionInfo.Password = decryptor.Decrypt(encryptedPassword);
            }

            connectionInfo.Domain = ReadAttribute(xmlnode, "Domain", connectionInfo.Domain);

            connectionInfo.UseConsoleSession = ReadBoolAttribute(
                xmlnode, "ConnectToConsole", connectionInfo.UseConsoleSession);

            connectionInfo.RedirectDiskDrives = ReadBoolAttribute(
                xmlnode, "RedirectDiskDrives", connectionInfo.RedirectDiskDrives);
            connectionInfo.RedirectPrinters = ReadBoolAttribute(
                xmlnode, "RedirectPrinters", connectionInfo.RedirectPrinters);
            connectionInfo.RedirectPorts = ReadBoolAttribute(xmlnode, "RedirectPorts", connectionInfo.RedirectPorts);
            connectionInfo.RedirectSmartCards = ReadBoolAttribute(
                xmlnode, "RedirectSmartCards", connectionInfo.RedirectSmartCards);
            connectionInfo.RedirectClipboard = ReadBoolAttribute(
                xmlnode, "RedirectClipboard", connectionInfo.RedirectClipboard);

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
            inheritance.DisplayWallpaper = ReadBoolAttribute(
                xmlnode, "InheritDisplayWallpaper", inheritance.DisplayWallpaper);
            inheritance.Icon = ReadBoolAttribute(xmlnode, "InheritIcon", inheritance.Icon);
            inheritance.Panel = ReadBoolAttribute(xmlnode, "InheritPanel", inheritance.Panel);
            inheritance.Port = ReadBoolAttribute(xmlnode, "InheritPort", inheritance.Port);
            inheritance.Protocol = ReadBoolAttribute(xmlnode, "InheritProtocol", inheritance.Protocol);
            inheritance.PuttySession = ReadBoolAttribute(xmlnode, "InheritPuttySession", inheritance.PuttySession);
            inheritance.RedirectDiskDrives = ReadBoolAttribute(
                xmlnode, "InheritRedirectDiskDrives", inheritance.RedirectDiskDrives);
            inheritance.RedirectKeys = ReadBoolAttribute(xmlnode, "InheritRedirectKeys", inheritance.RedirectKeys);
            inheritance.RedirectPorts = ReadBoolAttribute(xmlnode, "InheritRedirectPorts", inheritance.RedirectPorts);
            inheritance.RedirectPrinters = ReadBoolAttribute(
                xmlnode, "InheritRedirectPrinters", inheritance.RedirectPrinters);
            inheritance.RedirectSmartCards = ReadBoolAttribute(
                xmlnode, "InheritRedirectSmartCards", inheritance.RedirectSmartCards);
            inheritance.RedirectClipboard = ReadBoolAttribute(
                xmlnode, "InheritRedirectClipboard", inheritance.RedirectClipboard);
            inheritance.RedirectSound = ReadBoolAttribute(xmlnode, "InheritRedirectSound", inheritance.RedirectSound);
            inheritance.Resolution = ReadBoolAttribute(xmlnode, "InheritResolution", inheritance.Resolution);
            inheritance.UseConsoleSession = ReadBoolAttribute(
                xmlnode, "InheritUseConsoleSession", inheritance.UseConsoleSession);
            connectionInfo.Inheritance = inheritance;

            // Same reason as the credentials above: these three inheritance flags are
            // written by this build, so they are always read.
            connectionInfo.Inheritance.Domain = ReadBoolAttribute(
                xmlnode, "InheritDomain", connectionInfo.Inheritance.Domain);
            connectionInfo.Inheritance.Password = ReadBoolAttribute(
                xmlnode, "InheritPassword", connectionInfo.Inheritance.Password);
            connectionInfo.Inheritance.Username = ReadBoolAttribute(
                xmlnode, "InheritUsername", connectionInfo.Inheritance.Username);
            connectionInfo.Icon = ReadAttribute(xmlnode, "Icon", connectionInfo.Icon);
            connectionInfo.Panel = ReadAttribute(xmlnode, "Panel", connectionInfo.Panel);

            connectionInfo.PleaseConnect = ReadBoolAttribute(xmlnode, "Connected", connectionInfo.PleaseConnect);

            connectionInfo.RDPAuthenticationLevel = ReadEnumAttribute(
                xmlnode, "RDPAuthenticationLevel", connectionInfo.RDPAuthenticationLevel);
            connectionInfo.Inheritance.RDPAuthenticationLevel = ReadBoolAttribute(
                xmlnode, "InheritRDPAuthenticationLevel", connectionInfo.Inheritance.RDPAuthenticationLevel);

            connectionInfo.MacAddress = ReadAttribute(xmlnode, "MacAddress", connectionInfo.MacAddress);
            connectionInfo.Inheritance.MacAddress = ReadBoolAttribute(
                xmlnode, "InheritMacAddress", connectionInfo.Inheritance.MacAddress);

            connectionInfo.UserField = ReadAttribute(xmlnode, "UserField", connectionInfo.UserField);
            connectionInfo.Inheritance.UserField = ReadBoolAttribute(
                xmlnode, "InheritUserField", connectionInfo.Inheritance.UserField);

            // Get settings
            connectionInfo.RDGatewayUsageMethod = ReadEnumAttribute(
                xmlnode, "RDGatewayUsageMethod", connectionInfo.RDGatewayUsageMethod);
            connectionInfo.RDGatewayHostname = ReadAttribute(
                xmlnode, "RDGatewayHostname", connectionInfo.RDGatewayHostname);
            connectionInfo.RDGatewayUseConnectionCredentials = ReadEnumAttribute(
                xmlnode, "RDGatewayUseConnectionCredentials", connectionInfo.RDGatewayUseConnectionCredentials);
            connectionInfo.RDGatewayUsername = ReadAttribute(
                xmlnode, "RDGatewayUsername", connectionInfo.RDGatewayUsername);
            if (TryReadAttribute(xmlnode, "RDGatewayPassword", out string? encryptedGatewayPassword))
            {
                connectionInfo.RDGatewayPassword = decryptor.Decrypt(encryptedGatewayPassword);
            }

            connectionInfo.RDGatewayDomain = ReadAttribute(xmlnode, "RDGatewayDomain", connectionInfo.RDGatewayDomain);

            // Get inheritance settings
            connectionInfo.Inheritance.RDGatewayUsageMethod = ReadBoolAttribute(
                xmlnode, "InheritRDGatewayUsageMethod", connectionInfo.Inheritance.RDGatewayUsageMethod);
            connectionInfo.Inheritance.RDGatewayHostname = ReadBoolAttribute(
                xmlnode, "InheritRDGatewayHostname", connectionInfo.Inheritance.RDGatewayHostname);
            connectionInfo.Inheritance.RDGatewayUseConnectionCredentials = ReadBoolAttribute(
                xmlnode,
                "InheritRDGatewayUseConnectionCredentials",
                connectionInfo.Inheritance.RDGatewayUseConnectionCredentials);
            connectionInfo.Inheritance.RDGatewayUsername = ReadBoolAttribute(
                xmlnode, "InheritRDGatewayUsername", connectionInfo.Inheritance.RDGatewayUsername);
            connectionInfo.Inheritance.RDGatewayPassword = ReadBoolAttribute(
                xmlnode, "InheritRDGatewayPassword", connectionInfo.Inheritance.RDGatewayPassword);
            connectionInfo.Inheritance.RDGatewayDomain = ReadBoolAttribute(
                xmlnode, "InheritRDGatewayDomain", connectionInfo.Inheritance.RDGatewayDomain);

            // Get settings
            connectionInfo.EnableFontSmoothing = ReadBoolAttribute(
                xmlnode, "EnableFontSmoothing", connectionInfo.EnableFontSmoothing);
            connectionInfo.EnableDesktopComposition = ReadBoolAttribute(
                xmlnode, "EnableDesktopComposition", connectionInfo.EnableDesktopComposition);

            // Get inheritance settings
            connectionInfo.Inheritance.EnableFontSmoothing = ReadBoolAttribute(
                xmlnode, "InheritEnableFontSmoothing", connectionInfo.Inheritance.EnableFontSmoothing);
            connectionInfo.Inheritance.EnableDesktopComposition = ReadBoolAttribute(
                xmlnode, "InheritEnableDesktopComposition", connectionInfo.Inheritance.EnableDesktopComposition);

            connectionInfo.UseCredSsp = ReadBoolAttribute(xmlnode, "UseCredSsp", connectionInfo.UseCredSsp);
            connectionInfo.Inheritance.UseCredSsp = ReadBoolAttribute(
                xmlnode, "InheritUseCredSsp", connectionInfo.Inheritance.UseCredSsp);

            connectionInfo.LoadBalanceInfo = ReadAttribute(xmlnode, "LoadBalanceInfo", connectionInfo.LoadBalanceInfo);
            connectionInfo.AutomaticResize = ReadBoolAttribute(
                xmlnode, "AutomaticResize", connectionInfo.AutomaticResize);
            connectionInfo.Inheritance.LoadBalanceInfo = ReadBoolAttribute(
                xmlnode, "InheritLoadBalanceInfo", connectionInfo.Inheritance.LoadBalanceInfo);
            connectionInfo.Inheritance.AutomaticResize = ReadBoolAttribute(
                xmlnode, "InheritAutomaticResize", connectionInfo.Inheritance.AutomaticResize);

            connectionInfo.SoundQuality = ReadEnumAttribute(xmlnode, "SoundQuality", connectionInfo.SoundQuality);
            connectionInfo.Inheritance.SoundQuality = ReadBoolAttribute(
                xmlnode, "InheritSoundQuality", connectionInfo.Inheritance.SoundQuality);
            connectionInfo.RDPMinutesToIdleTimeout = ReadIntAttribute(
                xmlnode, "RDPMinutesToIdleTimeout", connectionInfo.RDPMinutesToIdleTimeout);
            connectionInfo.Inheritance.RDPMinutesToIdleTimeout = ReadBoolAttribute(
                xmlnode, "InheritRDPMinutesToIdleTimeout", connectionInfo.Inheritance.RDPMinutesToIdleTimeout);
            connectionInfo.RDPAlertIdleTimeout = ReadBoolAttribute(
                xmlnode, "RDPAlertIdleTimeout", connectionInfo.RDPAlertIdleTimeout);
            connectionInfo.Inheritance.RDPAlertIdleTimeout = ReadBoolAttribute(
                xmlnode, "InheritRDPAlertIdleTimeout", connectionInfo.Inheritance.RDPAlertIdleTimeout);
        }
        catch (Exception ex)
        {
            Runtime.MessageCollector.AddMessage(
                MessageClass.ErrorMsg,
                string.Format(
                    CultureInfo.CurrentCulture,
                    Language.strGetConnectionInfoFromXmlFailed,
                    connectionInfo.Name,
                    _connectionFileName,
                    ex.Message));
        }
        return connectionInfo;
    }
}
