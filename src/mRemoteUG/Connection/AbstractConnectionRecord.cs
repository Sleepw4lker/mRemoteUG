using System.ComponentModel;

using mRemoteUG.Connection.Protocol;
using mRemoteUG.Connection.Protocol.RDP;
using mRemoteUG.Tools;


namespace mRemoteUG.Connection;

public abstract class AbstractConnectionRecord : INotifyPropertyChanged
{
    #region Fields
    private string _name = string.Empty;
    private string _description = string.Empty;
    private string _icon = string.Empty;
    private string _panel = string.Empty;

    private string _hostname = string.Empty;
    private string _username = "";
    private string _password = "";
    private string _domain = "";

    private ProtocolType _protocol;
    private string _puttySession = string.Empty;
    private int _port;
    private bool _useConsoleSession;
    private RdpProtocol.AuthenticationLevel _rdpAuthenticationLevel;
    private int _rdpMinutesToIdleTimeout;
    private bool _rdpAlertIdleTimeout;
    private string _loadBalanceInfo = string.Empty;
    private bool _useCredSsp;

    private RdpProtocol.RDGatewayUsageMethod _rdGatewayUsageMethod;
    private string _rdGatewayHostname = string.Empty;
    private RdpProtocol.RDGatewayUseConnectionCredentials _rdGatewayUseConnectionCredentials;
    private string _rdGatewayUsername = string.Empty;
    private string _rdGatewayPassword = string.Empty;
    private string _rdGatewayDomain = string.Empty;

    private RdpProtocol.RDPResolutions _resolution;
    private bool _automaticResize;
    private RdpProtocol.RDPColors _colors;
    private bool _cacheBitmaps;
    private bool _displayWallpaper;
    private bool _displayThemes;
    private bool _enableFontSmoothing;
    private bool _enableDesktopComposition;

    private bool _redirectKeys;
    private bool _redirectDiskDrives;
    private bool _redirectPrinters;
    private bool _redirectPorts;
    private bool _redirectSmartCards;
    private bool _redirectClipboard;
    private RdpProtocol.RDPSounds _redirectSound;
    private RdpProtocol.RDPSoundQuality _soundQuality;

    private string _macAddress = string.Empty;
    private string _userField = string.Empty;
    #endregion

    #region Properties
    #region Display
    [LocalizedAttributes.LocalizedCategory("strCategoryDisplay"),
     LocalizedAttributes.LocalizedDisplayName("strPropertyNameName"),
     LocalizedAttributes.LocalizedDescription("strPropertyDescriptionName")]
    public virtual string Name
    {
        get => _name;
        set => SetField(ref _name, value ?? string.Empty, nameof(Name));
    }

    [LocalizedAttributes.LocalizedCategory("strCategoryDisplay"),
        LocalizedAttributes.LocalizedDisplayName("strPropertyNameDescription"),
        LocalizedAttributes.LocalizedDescription("strPropertyDescriptionDescription")]
    public virtual string Description
    {
        get => GetPropertyValue(nameof(Description), _description);
        set => SetField(ref _description, value ?? string.Empty, nameof(Description));
    }

    [LocalizedAttributes.LocalizedCategory("strCategoryDisplay"),
        TypeConverter(typeof(ConnectionIcon)),
        LocalizedAttributes.LocalizedDisplayName("strPropertyNameIcon"),
        LocalizedAttributes.LocalizedDescription("strPropertyDescriptionIcon")]
    public virtual string Icon
    {
        get => GetPropertyValue(nameof(Icon), _icon);
        set => SetField(ref _icon, value ?? string.Empty, nameof(Icon));
    }

    [LocalizedAttributes.LocalizedCategory("strCategoryDisplay"),
        LocalizedAttributes.LocalizedDisplayName("strPropertyNamePanel"),
        LocalizedAttributes.LocalizedDescription("strPropertyDescriptionPanel")]
    public virtual string Panel
    {
        get => GetPropertyValue(nameof(Panel), _panel);
        set => SetField(ref _panel, value ?? string.Empty, nameof(Panel));
    }
    #endregion

    #region Connection
    [LocalizedAttributes.LocalizedCategory("strCategoryConnection", 2),
        LocalizedAttributes.LocalizedDisplayName("strPropertyNameAddress"),
        LocalizedAttributes.LocalizedDescription("strPropertyDescriptionAddress")]
    public virtual string Hostname
    {
        get => _hostname;
        set => SetField(ref _hostname, value?.Trim() ?? string.Empty, nameof(Hostname));
    }

    [LocalizedAttributes.LocalizedCategory("strCategoryConnection", 2),
        LocalizedAttributes.LocalizedDisplayName("strPropertyNameUsername"),
        LocalizedAttributes.LocalizedDescription("strPropertyDescriptionUsername")]
    public virtual string Username
    {
        get => GetPropertyValue(nameof(Username), _username);
        set => SetField(ref _username, value?.Trim() ?? string.Empty, nameof(Username));
    }

    [LocalizedAttributes.LocalizedCategory("strCategoryConnection", 2),
        LocalizedAttributes.LocalizedDisplayName("strPropertyNamePassword"),
        LocalizedAttributes.LocalizedDescription("strPropertyDescriptionPassword"),
        PasswordPropertyText(true)]
    public virtual string Password
    {
        get => GetPropertyValue(nameof(Password), _password);
        set => SetField(ref _password, value ?? string.Empty, nameof(Password));
    }

    [LocalizedAttributes.LocalizedCategory("strCategoryConnection", 2),
        LocalizedAttributes.LocalizedDisplayName("strPropertyNameDomain"),
        LocalizedAttributes.LocalizedDescription("strPropertyDescriptionDomain")]
    public string Domain
    {
        get => GetPropertyValue(nameof(Domain), _domain);
        set => SetField(ref _domain, value?.Trim() ?? string.Empty, nameof(Domain));
    }
    #endregion

    #region Protocol
    [LocalizedAttributes.LocalizedCategory("strCategoryProtocol", 3),
        LocalizedAttributes.LocalizedDisplayName("strPropertyNameProtocol"),
        LocalizedAttributes.LocalizedDescription("strPropertyDescriptionProtocol"),
        TypeConverter(typeof(Protocol.ProtocolTypeConverter))]
    public virtual ProtocolType Protocol
    {
        get => GetPropertyValue(nameof(Protocol), _protocol);
        set => SetField(ref _protocol, value, nameof(Protocol));
    }

    [LocalizedAttributes.LocalizedCategory("strCategoryProtocol", 3),
        LocalizedAttributes.LocalizedDisplayName("strPropertyNamePort"),
        LocalizedAttributes.LocalizedDescription("strPropertyDescriptionPort")]
    public virtual int Port
    {
        get => GetPropertyValue(nameof(Port), _port);
        set => SetField(ref _port, value, nameof(Port));
    }

    [LocalizedAttributes.LocalizedCategory("strCategoryProtocol", 3),
        LocalizedAttributes.LocalizedDisplayName("strPropertyNamePuttySession"),
        LocalizedAttributes.LocalizedDescription("strPropertyDescriptionPuttySession"),
        TypeConverter(typeof(Config.Putty.PuttySessionsManager.SessionList))]
    public virtual string PuttySession
    {
        get => GetPropertyValue(nameof(PuttySession), _puttySession);
        set => SetField(ref _puttySession, value ?? string.Empty, nameof(PuttySession));
    }

    [LocalizedAttributes.LocalizedCategory("strCategoryProtocol", 3),
        LocalizedAttributes.LocalizedDisplayName("strPropertyNameUseConsoleSession"),
        LocalizedAttributes.LocalizedDescription("strPropertyDescriptionUseConsoleSession"),
        TypeConverter(typeof(MiscTools.YesNoTypeConverter))]
    public bool UseConsoleSession
    {
        get => GetPropertyValue(nameof(UseConsoleSession), _useConsoleSession);
        set => SetField(ref _useConsoleSession, value, nameof(UseConsoleSession));
    }

    [LocalizedAttributes.LocalizedCategory("strCategoryProtocol", 3),
        LocalizedAttributes.LocalizedDisplayName("strPropertyNameAuthenticationLevel"),
        LocalizedAttributes.LocalizedDescription("strPropertyDescriptionAuthenticationLevel"),
        TypeConverter(typeof(MiscTools.EnumTypeConverter))]
    public RdpProtocol.AuthenticationLevel RDPAuthenticationLevel
    {
        get => GetPropertyValue(nameof(RDPAuthenticationLevel), _rdpAuthenticationLevel);
        set => SetField(ref _rdpAuthenticationLevel, value, nameof(RDPAuthenticationLevel));
    }

    [LocalizedAttributes.LocalizedCategory("strCategoryProtocol", 3),
        LocalizedAttributes.LocalizedDisplayName("strPropertyNameRDPMinutesToIdleTimeout"),
        LocalizedAttributes.LocalizedDescription("strPropertyDescriptionRDPMinutesToIdleTimeout")]
    public virtual int RDPMinutesToIdleTimeout
    {
        get => GetPropertyValue(nameof(RDPMinutesToIdleTimeout), _rdpMinutesToIdleTimeout);
        set
        {
            if (value < 0)
            {
                value = 0;
            }
            else if (value > 240)
            {
                value = 240;
            }

            SetField(ref _rdpMinutesToIdleTimeout, value, nameof(RDPMinutesToIdleTimeout));
        }
    }

    [LocalizedAttributes.LocalizedCategory("strCategoryProtocol", 3),
        LocalizedAttributes.LocalizedDisplayName("strPropertyNameRDPAlertIdleTimeout"),
        LocalizedAttributes.LocalizedDescription("strPropertyDescriptionRDPAlertIdleTimeout")]
    public bool RDPAlertIdleTimeout
    {
        get => GetPropertyValue(nameof(RDPAlertIdleTimeout), _rdpAlertIdleTimeout);
        set => SetField(ref _rdpAlertIdleTimeout, value, nameof(RDPAlertIdleTimeout));
    }

    [LocalizedAttributes.LocalizedCategory("strCategoryProtocol", 3),
        LocalizedAttributes.LocalizedDisplayName("strPropertyNameLoadBalanceInfo"),
        LocalizedAttributes.LocalizedDescription("strPropertyDescriptionLoadBalanceInfo")]
    public string LoadBalanceInfo
    {
        get => GetPropertyValue(nameof(LoadBalanceInfo), _loadBalanceInfo);
        set => SetField(ref _loadBalanceInfo, value?.Trim() ?? string.Empty, nameof(LoadBalanceInfo));
    }

    [LocalizedAttributes.LocalizedCategory("strCategoryProtocol", 3),
        LocalizedAttributes.LocalizedDisplayName("strPropertyNameUseCredSsp"),
        LocalizedAttributes.LocalizedDescription("strPropertyDescriptionUseCredSsp"),
        TypeConverter(typeof(MiscTools.YesNoTypeConverter))]
    public bool UseCredSsp
    {
        get => GetPropertyValue(nameof(UseCredSsp), _useCredSsp);
        set => SetField(ref _useCredSsp, value, nameof(UseCredSsp));
    }
    #endregion

    #region RD Gateway
    [LocalizedAttributes.LocalizedCategory("strCategoryGateway", 4),
        LocalizedAttributes.LocalizedDisplayName("strPropertyNameRDGatewayUsageMethod"),
        LocalizedAttributes.LocalizedDescription("strPropertyDescriptionRDGatewayUsageMethod"),
        TypeConverter(typeof(MiscTools.EnumTypeConverter))]
    public RdpProtocol.RDGatewayUsageMethod RDGatewayUsageMethod
    {
        get => GetPropertyValue(nameof(RDGatewayUsageMethod), _rdGatewayUsageMethod);
        set => SetField(ref _rdGatewayUsageMethod, value, nameof(RDGatewayUsageMethod));
    }

    [LocalizedAttributes.LocalizedCategory("strCategoryGateway", 4),
        LocalizedAttributes.LocalizedDisplayName("strPropertyNameRDGatewayHostname"),
        LocalizedAttributes.LocalizedDescription("strPropertyDescriptionRDGatewayHostname")]
    public string RDGatewayHostname
    {
        get => GetPropertyValue(nameof(RDGatewayHostname), _rdGatewayHostname);
        set => SetField(ref _rdGatewayHostname, value?.Trim() ?? string.Empty, nameof(RDGatewayHostname));
    }

    [LocalizedAttributes.LocalizedCategory("strCategoryGateway", 4),
        LocalizedAttributes.LocalizedDisplayName("strPropertyNameRDGatewayUseConnectionCredentials"),
        LocalizedAttributes.LocalizedDescription("strPropertyDescriptionRDGatewayUseConnectionCredentials"),
        TypeConverter(typeof(MiscTools.EnumTypeConverter))]
    public RdpProtocol.RDGatewayUseConnectionCredentials RDGatewayUseConnectionCredentials
    {
        get => GetPropertyValue(nameof(RDGatewayUseConnectionCredentials), _rdGatewayUseConnectionCredentials);
        set => SetField(ref _rdGatewayUseConnectionCredentials, value, nameof(RDGatewayUseConnectionCredentials));
    }

    [LocalizedAttributes.LocalizedCategory("strCategoryGateway", 4),
        LocalizedAttributes.LocalizedDisplayName("strPropertyNameRDGatewayUsername"),
        LocalizedAttributes.LocalizedDescription("strPropertyDescriptionRDGatewayUsername")]
    public string RDGatewayUsername
    {
        get => GetPropertyValue(nameof(RDGatewayUsername), _rdGatewayUsername);
        set => SetField(ref _rdGatewayUsername, value?.Trim() ?? string.Empty, nameof(RDGatewayUsername));
    }

    [LocalizedAttributes.LocalizedCategory("strCategoryGateway", 4),
        LocalizedAttributes.LocalizedDisplayName("strPropertyNameRDGatewayPassword"),
        LocalizedAttributes.LocalizedDescription("strPropertyNameRDGatewayPassword"),
        PasswordPropertyText(true)]
    public string RDGatewayPassword
    {
        get => GetPropertyValue(nameof(RDGatewayPassword), _rdGatewayPassword);
        set => SetField(ref _rdGatewayPassword, value ?? string.Empty, nameof(RDGatewayPassword));
    }

    [LocalizedAttributes.LocalizedCategory("strCategoryGateway", 4),
        LocalizedAttributes.LocalizedDisplayName("strPropertyNameRDGatewayDomain"),
        LocalizedAttributes.LocalizedDescription("strPropertyDescriptionRDGatewayDomain")]
    public string RDGatewayDomain
    {
        get => GetPropertyValue(nameof(RDGatewayDomain), _rdGatewayDomain);
        set => SetField(ref _rdGatewayDomain, value?.Trim() ?? string.Empty, nameof(RDGatewayDomain));
    }
    #endregion

    #region Appearance
    [LocalizedAttributes.LocalizedCategory("strCategoryAppearance", 5),
        LocalizedAttributes.LocalizedDisplayName("strPropertyNameResolution"),
        LocalizedAttributes.LocalizedDescription("strPropertyDescriptionResolution"),
        TypeConverter(typeof(MiscTools.EnumTypeConverter))]
    public RdpProtocol.RDPResolutions Resolution
    {
        get => GetPropertyValue(nameof(Resolution), _resolution);
        set => SetField(ref _resolution, value, nameof(Resolution));
    }

    [LocalizedAttributes.LocalizedCategory("strCategoryAppearance", 5),
        LocalizedAttributes.LocalizedDisplayName("strPropertyNameAutomaticResize"),
        LocalizedAttributes.LocalizedDescription("strPropertyDescriptionAutomaticResize"),
        TypeConverter(typeof(MiscTools.YesNoTypeConverter))]
    public bool AutomaticResize
    {
        get => GetPropertyValue(nameof(AutomaticResize), _automaticResize);
        set => SetField(ref _automaticResize, value, nameof(AutomaticResize));
    }

    [LocalizedAttributes.LocalizedCategory("strCategoryAppearance", 5),
        LocalizedAttributes.LocalizedDisplayName("strPropertyNameColors"),
        LocalizedAttributes.LocalizedDescription("strPropertyDescriptionColors"),
        TypeConverter(typeof(MiscTools.EnumTypeConverter))]
    public RdpProtocol.RDPColors Colors
    {
        get => GetPropertyValue(nameof(Colors), _colors);
        set => SetField(ref _colors, value, nameof(Colors));
    }

    [LocalizedAttributes.LocalizedCategory("strCategoryAppearance", 5),
        LocalizedAttributes.LocalizedDisplayName("strPropertyNameCacheBitmaps"),
        LocalizedAttributes.LocalizedDescription("strPropertyDescriptionCacheBitmaps"),
        TypeConverter(typeof(MiscTools.YesNoTypeConverter))]
    public bool CacheBitmaps
    {
        get => GetPropertyValue(nameof(CacheBitmaps), _cacheBitmaps);
        set => SetField(ref _cacheBitmaps, value, nameof(CacheBitmaps));
    }

    [LocalizedAttributes.LocalizedCategory("strCategoryAppearance", 5),
        LocalizedAttributes.LocalizedDisplayName("strPropertyNameDisplayWallpaper"),
        LocalizedAttributes.LocalizedDescription("strPropertyDescriptionDisplayWallpaper"),
        TypeConverter(typeof(MiscTools.YesNoTypeConverter))]
    public bool DisplayWallpaper
    {
        get => GetPropertyValue(nameof(DisplayWallpaper), _displayWallpaper);
        set => SetField(ref _displayWallpaper, value, nameof(DisplayWallpaper));
    }

    [LocalizedAttributes.LocalizedCategory("strCategoryAppearance", 5),
        LocalizedAttributes.LocalizedDisplayName("strPropertyNameDisplayThemes"),
        LocalizedAttributes.LocalizedDescription("strPropertyDescriptionDisplayThemes"),
        TypeConverter(typeof(MiscTools.YesNoTypeConverter))]
    public bool DisplayThemes
    {
        get => GetPropertyValue(nameof(DisplayThemes), _displayThemes);
        set => SetField(ref _displayThemes, value, nameof(DisplayThemes));
    }

    [LocalizedAttributes.LocalizedCategory("strCategoryAppearance", 5),
        LocalizedAttributes.LocalizedDisplayName("strPropertyNameEnableFontSmoothing"),
        LocalizedAttributes.LocalizedDescription("strPropertyDescriptionEnableFontSmoothing"),
        TypeConverter(typeof(MiscTools.YesNoTypeConverter))]
    public bool EnableFontSmoothing
    {
        get => GetPropertyValue(nameof(EnableFontSmoothing), _enableFontSmoothing);
        set => SetField(ref _enableFontSmoothing, value, nameof(EnableFontSmoothing));
    }

    [LocalizedAttributes.LocalizedCategory("strCategoryAppearance", 5),
        LocalizedAttributes.LocalizedDisplayName("strPropertyNameEnableDesktopComposition"),
        LocalizedAttributes.LocalizedDescription("strPropertyDescriptionEnableDesktopComposition"),
        TypeConverter(typeof(MiscTools.YesNoTypeConverter))]
    public bool EnableDesktopComposition
    {
        get => GetPropertyValue(nameof(EnableDesktopComposition), _enableDesktopComposition);
        set => SetField(ref _enableDesktopComposition, value, nameof(EnableDesktopComposition));
    }
    #endregion

    #region Redirect
    [LocalizedAttributes.LocalizedCategory("strCategoryRedirect", 6),
        LocalizedAttributes.LocalizedDisplayName("strPropertyNameRedirectKeys"),
        LocalizedAttributes.LocalizedDescription("strPropertyDescriptionRedirectKeys"),
        TypeConverter(typeof(MiscTools.YesNoTypeConverter))]
    public bool RedirectKeys
    {
        get => GetPropertyValue(nameof(RedirectKeys), _redirectKeys);
        set => SetField(ref _redirectKeys, value, nameof(RedirectKeys));
    }

    [LocalizedAttributes.LocalizedCategory("strCategoryRedirect", 6),
        LocalizedAttributes.LocalizedDisplayName("strPropertyNameRedirectDrives"),
        LocalizedAttributes.LocalizedDescription("strPropertyDescriptionRedirectDrives"),
        TypeConverter(typeof(MiscTools.YesNoTypeConverter))]
    public bool RedirectDiskDrives
    {
        get => GetPropertyValue(nameof(RedirectDiskDrives), _redirectDiskDrives);
        set => SetField(ref _redirectDiskDrives, value, nameof(RedirectDiskDrives));
    }

    [LocalizedAttributes.LocalizedCategory("strCategoryRedirect", 6),
        LocalizedAttributes.LocalizedDisplayName("strPropertyNameRedirectPrinters"),
        LocalizedAttributes.LocalizedDescription("strPropertyDescriptionRedirectPrinters"),
        TypeConverter(typeof(MiscTools.YesNoTypeConverter))]
    public bool RedirectPrinters
    {
        get => GetPropertyValue(nameof(RedirectPrinters), _redirectPrinters);
        set => SetField(ref _redirectPrinters, value, nameof(RedirectPrinters));
    }

    [LocalizedAttributes.LocalizedCategory("strCategoryRedirect", 6),
        LocalizedAttributes.LocalizedDisplayName("strPropertyNameRedirectPorts"),
        LocalizedAttributes.LocalizedDescription("strPropertyDescriptionRedirectPorts"),
        TypeConverter(typeof(MiscTools.YesNoTypeConverter))]
    public bool RedirectPorts
    {
        get => GetPropertyValue(nameof(RedirectPorts), _redirectPorts);
        set => SetField(ref _redirectPorts, value, nameof(RedirectPorts));
    }

    [LocalizedAttributes.LocalizedCategory("strCategoryRedirect", 6),
        LocalizedAttributes.LocalizedDisplayName("strPropertyNameRedirectSmartCards"),
        LocalizedAttributes.LocalizedDescription("strPropertyDescriptionRedirectSmartCards"),
        TypeConverter(typeof(MiscTools.YesNoTypeConverter))]
    public bool RedirectSmartCards
    {
        get => GetPropertyValue(nameof(RedirectSmartCards), _redirectSmartCards);
        set => SetField(ref _redirectSmartCards, value, nameof(RedirectSmartCards));
    }

    [LocalizedAttributes.LocalizedCategory("strCategoryRedirect", 6),
        LocalizedAttributes.LocalizedDisplayName("strPropertyNameRedirectClipboard"),
        LocalizedAttributes.LocalizedDescription("strPropertyDescriptionRedirectClipboard"),
        TypeConverter(typeof(MiscTools.YesNoTypeConverter))]
    public bool RedirectClipboard
    {
        get => GetPropertyValue(nameof(RedirectClipboard), _redirectClipboard);
        set => SetField(ref _redirectClipboard, value, nameof(RedirectClipboard));
    }

    [LocalizedAttributes.LocalizedCategory("strCategoryRedirect", 6),
        LocalizedAttributes.LocalizedDisplayName("strPropertyNameRedirectSounds"),
        LocalizedAttributes.LocalizedDescription("strPropertyDescriptionRedirectSounds"),
        TypeConverter(typeof(MiscTools.EnumTypeConverter))]
    public RdpProtocol.RDPSounds RedirectSound
    {
        get => GetPropertyValue(nameof(RedirectSound), _redirectSound);
        set => SetField(ref _redirectSound, value, nameof(RedirectSound));
    }

    [LocalizedAttributes.LocalizedCategory("strCategoryRedirect", 6),
        LocalizedAttributes.LocalizedDisplayName("strPropertyNameSoundQuality"),
        LocalizedAttributes.LocalizedDescription("strPropertyDescriptionSoundQuality"),
        TypeConverter(typeof(MiscTools.EnumTypeConverter))]
    public RdpProtocol.RDPSoundQuality SoundQuality
    {
        get => GetPropertyValue(nameof(SoundQuality), _soundQuality);
        set => SetField(ref _soundQuality, value, nameof(SoundQuality));
    }
    #endregion

    #region Misc
    [Browsable(false)]
    public string ConstantID { get; /*set;*/ }

    [LocalizedAttributes.LocalizedCategory("strCategoryMiscellaneous", 7),
        LocalizedAttributes.LocalizedDisplayName("strPropertyNameMACAddress"),
        LocalizedAttributes.LocalizedDescription("strPropertyDescriptionMACAddress")]
    public virtual string MacAddress
    {
        get => GetPropertyValue(nameof(MacAddress), _macAddress);
        set => SetField(ref _macAddress, value ?? string.Empty, nameof(MacAddress));
    }

    [LocalizedAttributes.LocalizedCategory("strCategoryMiscellaneous", 7),
        LocalizedAttributes.LocalizedDisplayName("strPropertyNameUser1"),
        LocalizedAttributes.LocalizedDescription("strPropertyDescriptionUser1")]
    public virtual string UserField
    {
        get => GetPropertyValue(nameof(UserField), _userField);
        set => SetField(ref _userField, value ?? string.Empty, nameof(UserField));
    }
    #endregion
    #endregion

    protected AbstractConnectionRecord(string uniqueId)
    {
        ConstantID = uniqueId.ThrowIfNullOrEmpty(nameof(uniqueId));
    }

    /// <remarks>
    /// Returns the backing value. It must not reflect on this instance: GetProperty(name)
    /// followed by GetValue re-enters the very property getter that called this, with no
    /// base case, and ends the stack. That is what it used to do. It was unreachable only
    /// because ConnectionInfo is the sole direct subclass and overrides this, and because
    /// the one test subclass never reads a property back.
    /// </remarks>
    protected virtual TPropertyType GetPropertyValue<TPropertyType>(string propertyName, TPropertyType value)
    {
        return value;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void RaisePropertyChangedEvent(object? sender, PropertyChangedEventArgs args)
    {
        PropertyChanged?.Invoke(sender, new PropertyChangedEventArgs(args.PropertyName));
    }

    protected void SetField<T>(ref T field, T value, string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        RaisePropertyChangedEvent(this, new PropertyChangedEventArgs(propertyName));
    }
}
