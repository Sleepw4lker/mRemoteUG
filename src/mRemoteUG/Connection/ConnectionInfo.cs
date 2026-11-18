using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using mRemoteUG.App;
using mRemoteUG.Connection.Protocol;
using mRemoteUG.Connection.Protocol.RAW;
using mRemoteUG.Connection.Protocol.RDP;
using mRemoteUG.Connection.Protocol.Rlogin;
using mRemoteUG.Connection.Protocol.SSH;
using mRemoteUG.Connection.Protocol.Telnet;
using mRemoteUG.Container;
using mRemoteUG.Tree;
using mRemoteUG.Tree.Root;


namespace mRemoteUG.Connection;

[DefaultProperty("Name")]
public class ConnectionInfo : AbstractConnectionRecord, IInheritable
{
    #region Public Properties
    [Browsable(false)]
    public ConnectionInfoInheritance Inheritance { get; set; }

    [Browsable(false)]
    public ProtocolList OpenConnections { get; protected set; }

    [Browsable(false)]
    public virtual bool IsContainer { get; set; }

    [Browsable(false)]
    public bool IsDefault { get; set; }

    [Browsable(false)]
    public ContainerInfo? Parent { get; internal set; }


    [Browsable(false)]
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public bool IsQuickConnect { get; set; }

    [Browsable(false)]
    public bool PleaseConnect { get; set; }
    #endregion

    #region Constructors

    public ConnectionInfo()
        : this(Guid.NewGuid().ToString())
    {
    }

    public ConnectionInfo(string uniqueId)
        : base(uniqueId)
    {
        SetTreeDisplayDefaults();
        SetConnectionDefaults();
        SetProtocolDefaults();
        SetRdGatewayDefaults();
        SetAppearanceDefaults();
        SetRedirectDefaults();
        SetMiscDefaults();
        SetNonBrowsablePropertiesDefaults();
        SetDefaults();
    }
    #endregion

    #region Public Methods
    public virtual ConnectionInfo Clone()
    {
        var newConnectionInfo = new ConnectionInfo();
        newConnectionInfo.CopyFrom(this);
        newConnectionInfo.Inheritance = Inheritance.Clone();
        newConnectionInfo.Inheritance.Parent = newConnectionInfo;
        return newConnectionInfo;
    }

    public void CopyFrom(ConnectionInfo sourceConnectionInfo)
    {
        PropertyInfo[] properties = s_copyableProperties.GetOrAdd(GetType(), type =>
            type.BaseType?.GetProperties().Where(prop => prop.CanRead && prop.CanWrite).ToArray()
            ?? Array.Empty<PropertyInfo>());
        foreach (PropertyInfo property in properties)
        {
            if (property.Name == nameof(Parent))
            {
                continue;
            }

            object? remotePropertyValue = property.GetValue(sourceConnectionInfo, null);
            property.SetValue(this, remotePropertyValue, null);
        }
        ConnectionInfoInheritance clonedInheritance = sourceConnectionInfo.Inheritance.Clone();
        clonedInheritance.Parent = this;
        Inheritance = clonedInheritance;
    }

    public virtual TreeNodeType GetTreeNodeType()
    {
        return TreeNodeType.Connection;
    }

    private void SetDefaults()
    {
        if (Port == 0)
        {
            SetDefaultPort();
        }
    }

    public int GetDefaultPort()
    {
        return GetDefaultPort(Protocol);
    }

    public void SetDefaultPort()
    {
        Port = GetDefaultPort();
    }

    protected virtual IEnumerable<PropertyInfo> GetProperties(string[] excludedPropertyNames)
    {
        PropertyInfo[] properties = typeof(ConnectionInfo).GetProperties();
        IEnumerable<PropertyInfo> filteredProperties =
            properties.Where((prop) => !excludedPropertyNames.Contains(prop.Name));
        return filteredProperties;
    }

    public virtual IEnumerable<PropertyInfo> GetSerializableProperties()
    {
        string[] excludedProperties = new[] { "Parent", "Name", "Hostname", "Port", "Inheritance", "OpenConnections",
            "IsContainer", "IsDefault", "ConstantID", "IsQuickConnect", "PleaseConnect" };

        return GetProperties(excludedProperties);
    }

    public virtual void SetParent(ContainerInfo? newParent)
    {
        RemoveParent();
        newParent?.AddChild(this);
    }

    public void RemoveParent()
    {
        Parent?.RemoveChild(this);
    }

    public ConnectionInfo GetRootParent()
    {
        return Parent != null ? Parent.GetRootParent() : this;
    }

    #endregion

    #region Public Enumerations
    [Flags()]
    public enum Force
    {
        None = 0,
        UseConsoleSession = 1,
        Fullscreen = 2,
        DoNotJump = 4,
        OverridePanel = 8,
        DontUseConsoleSession = 16,
        NoCredentials = 32
    }
    #endregion

    /// <summary>
    /// PropertyInfo lookups for the inheritance check and the parent read, cached per type.
    /// </summary>
    /// <remarks>
    /// Reading any of the inheritable properties used to call GetProperty by name twice:
    /// once on ConnectionInfoInheritance to ask whether inheritance is on for it, and once
    /// on the parent to fetch the value. These objects back the connection tree and the
    /// property grid, so that pair of name lookups ran for every node of every filter
    /// keystroke and every grid refresh. What GetProperty answers never changes for a
    /// given type, so it is asked once.
    /// </remarks>
    private static readonly ConcurrentDictionary<(Type Type, string PropertyName), PropertyInfo?> s_propertyLookup =
        new ConcurrentDictionary<(Type, string), PropertyInfo?>();

    private static PropertyInfo? PropertyOf(Type type, string propertyName) =>
        s_propertyLookup.GetOrAdd((type, propertyName), key => key.Type.GetProperty(key.PropertyName));

    /// <summary>The readable and writable properties to copy, cached per type.</summary>
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> s_copyableProperties =
        new ConcurrentDictionary<Type, PropertyInfo[]>();

    #region Private Methods
    protected override TPropertyType GetPropertyValue<TPropertyType>(string propertyName, TPropertyType value)
    {
        if (!ShouldThisPropertyBeInherited(propertyName))
        {
            return value;
        }

        if (TryGetInheritedPropertyValue<TPropertyType>(propertyName, out TPropertyType? inheritedValue))
        {
            return inheritedValue;
        }

        return value;
    }

    private bool ShouldThisPropertyBeInherited(string propertyName)
    {
        return ParentIsValidInheritanceTarget() && IsInheritanceTurnedOnForThisProperty(propertyName);
    }

    private bool ParentIsValidInheritanceTarget()
    {
        // The root is where the tree starts, not something a node inherits from: AddChildAt
        // switches a node's flags off on entry, and RootNodeInfo hides Password with a bool, so
        // a lookup by name on it is ambiguous.
        return Parent != null && Parent is not RootNodeInfo;
    }

    private bool IsInheritanceTurnedOnForThisProperty(string propertyName)
    {
        // Inheritance has a public setter, and Clone, CopyFrom, DefaultConnectionInfo and
        // the XML deserializer all reassign it. This read sits outside the try/catch that
        // guards the rest of the lookup, so a null here used to take every inheritable
        // getter on the record down with it. Nothing to inherit from is the same answer as
        // inheritance being switched off.
        ConnectionInfoInheritance inheritance = Inheritance;
        if (inheritance == null)
        {
            return false;
        }

        PropertyInfo? inheritPropertyInfo = PropertyOf(inheritance.GetType(), propertyName);
        return inheritPropertyInfo?.GetValue(inheritance, null) is bool inherit && inherit;
    }

    private bool TryGetInheritedPropertyValue<TPropertyType>(string propertyName,
                                                             [MaybeNullWhen(false)] out TPropertyType inheritedValue)
    {
        ContainerInfo? parent = Parent;
        if (parent == null)
        {
            inheritedValue = default;
            return false;
        }

        try
        {
            PropertyInfo? parentPropertyInfo = PropertyOf(parent.GetType(), propertyName);
            if (parentPropertyInfo == null)
            {
                throw new InvalidOperationException(
                    $"Could not retrieve property data for property '{propertyName}' on parent node '{parent.Name}'");
            }

            object? parentValue = parentPropertyInfo.GetValue(parent, null);

            // A null on the parent is not a value to inherit. Returning it would replace
            // the child's own non-null field with a null, out of a property declared not
            // to be nullable, and for the getters that then trimmed it arrived as a
            // NullReferenceException blamed on the child. "The parent has nothing" and
            // "this property is not inherited" are the same outcome for the caller.
            if (parentValue == null)
            {
                inheritedValue = default;
                return false;
            }

            inheritedValue = (TPropertyType)parentValue;
            return true;
        }
        catch (Exception e)
        {
            Runtime.MessageCollector.AddExceptionMessage($"Error retrieving inherited property '{propertyName}'", e);
            inheritedValue = default(TPropertyType);
            return false;
        }
    }

    private static int GetDefaultPort(ProtocolType protocol)
    {
        try
        {
            switch (protocol)
            {
                case ProtocolType.RDP:
                    return (int)RdpProtocol.Defaults.Port;
                case ProtocolType.SSH1:
                    return (int)ProtocolSSH1.Defaults.Port;
                case ProtocolType.SSH2:
                    return (int)ProtocolSSH2.Defaults.Port;
                case ProtocolType.Telnet:
                    return (int)ProtocolTelnet.Defaults.Port;
                case ProtocolType.Rlogin:
                    return (int)ProtocolRlogin.Defaults.Port;
                case ProtocolType.RAW:
                    return (int)RawProtocol.Defaults.Port;
                default:
                    return 0;
            }
        }
        catch (Exception ex)
        {
            Runtime.MessageCollector.AddExceptionMessage(Language.strConnectionSetDefaultPortFailed, ex);
            return 0;
        }
    }

    private void SetTreeDisplayDefaults()
    {
        Name = Language.strNewConnection;
        Description = Settings.Default.ConDefaultDescription;
        Icon = ConnectionIcon.DefaultIconName;
        Panel = Language.strGeneral;
    }

    private void SetConnectionDefaults()
    {
        Hostname = string.Empty;
    }

    private void SetProtocolDefaults()
    {
        Protocol = GetDefaultProtocol();
        Port = 0;
        PuttySession = Settings.Default.ConDefaultPuttySession;
        UseConsoleSession = Settings.Default.ConDefaultUseConsoleSession;
        RDPAuthenticationLevel = Enum.Parse<RdpProtocol.AuthenticationLevel>(
            Settings.Default.ConDefaultRDPAuthenticationLevel);
        RDPMinutesToIdleTimeout = Settings.Default.ConDefaultRDPMinutesToIdleTimeout;
        RDPAlertIdleTimeout = Settings.Default.ConDefaultRDPAlertIdleTimeout;
        LoadBalanceInfo = Settings.Default.ConDefaultLoadBalanceInfo;
        UseCredSsp = Settings.Default.ConDefaultUseCredSsp;
    }

    private static ProtocolType GetDefaultProtocol()
    {
        if (Enum.TryParse(Settings.Default.ConDefaultProtocol, true, out ProtocolType protocol) &&
            ProtocolTypes.IsSupported(protocol))
        {
            return protocol;
        }

        return ProtocolType.RDP;
    }

    private void SetRdGatewayDefaults()
    {
        RDGatewayUsageMethod = Enum.Parse<RdpProtocol.RDGatewayUsageMethod>(
            Settings.Default.ConDefaultRDGatewayUsageMethod);
        RDGatewayHostname = Settings.Default.ConDefaultRDGatewayHostname;
        RDGatewayUseConnectionCredentials = Enum.Parse<RdpProtocol.RDGatewayUseConnectionCredentials>(
            Settings.Default.ConDefaultRDGatewayUseConnectionCredentials);
        RDGatewayUsername = Settings.Default.ConDefaultRDGatewayUsername;
        RDGatewayPassword = Settings.Default.ConDefaultRDGatewayPassword;
        RDGatewayDomain = Settings.Default.ConDefaultRDGatewayDomain;
    }

    private void SetAppearanceDefaults()
    {
        Resolution = Enum.Parse<RdpProtocol.RDPResolutions>(
            Settings.Default.ConDefaultResolution);
        AutomaticResize = Settings.Default.ConDefaultAutomaticResize;
        Colors = Enum.Parse<RdpProtocol.RDPColors>(Settings.Default.ConDefaultColors);
        CacheBitmaps = Settings.Default.ConDefaultCacheBitmaps;
        DisplayWallpaper = Settings.Default.ConDefaultDisplayWallpaper;
        DisplayThemes = Settings.Default.ConDefaultDisplayThemes;
        EnableFontSmoothing = Settings.Default.ConDefaultEnableFontSmoothing;
        EnableDesktopComposition = Settings.Default.ConDefaultEnableDesktopComposition;
    }

    private void SetRedirectDefaults()
    {
        RedirectKeys = Settings.Default.ConDefaultRedirectKeys;
        RedirectDiskDrives = Settings.Default.ConDefaultRedirectDiskDrives;
        RedirectPrinters = Settings.Default.ConDefaultRedirectPrinters;
        RedirectPorts = Settings.Default.ConDefaultRedirectPorts;
        RedirectSmartCards = Settings.Default.ConDefaultRedirectSmartCards;
        RedirectClipboard = Settings.Default.ConDefaultRedirectClipboard;
        RedirectSound = Enum.Parse<RdpProtocol.RDPSounds>(
            Settings.Default.ConDefaultRedirectSound);
        SoundQuality = Enum.Parse<RdpProtocol.RDPSoundQuality>(
            Settings.Default.ConDefaultSoundQuality);
    }

    private void SetMiscDefaults()
    {
        MacAddress = Settings.Default.ConDefaultMacAddress;
        UserField = Settings.Default.ConDefaultUserField;
    }

    [MemberNotNull(nameof(Inheritance), nameof(OpenConnections))]
    private void SetNonBrowsablePropertiesDefaults()
    {
        Inheritance = new ConnectionInfoInheritance(this);
        SetNewOpenConnectionList();
        //PositionID = 0;
    }

    [MemberNotNull(nameof(OpenConnections))]
    private void SetNewOpenConnectionList()
    {
        OpenConnections = new ProtocolList();
        OpenConnections.CollectionChanged += (sender, args) =>
            RaisePropertyChangedEvent(this, new PropertyChangedEventArgs(nameof(OpenConnections)));
    }
    #endregion
}
