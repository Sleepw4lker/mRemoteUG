using mRemoteUG.App;
using mRemoteUG.Connection.Protocol;
using mRemoteUG.Connection.Protocol.RDP;
using mRemoteUG.Connection.Protocol.RAW;
using mRemoteUG.Connection.Protocol.Rlogin;
using mRemoteUG.Connection.Protocol.SSH;
using mRemoteUG.Connection.Protocol.Telnet;
using mRemoteUG.Container;
using mRemoteUG.Tree;
using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;


namespace mRemoteUG.Connection
{
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
	        var properties = CopyableProperties.GetOrAdd(GetType(), type =>
	            type.BaseType?.GetProperties().Where(prop => prop.CanRead && prop.CanWrite).ToArray()
	            ?? new PropertyInfo[0]);
	        foreach (var property in properties)
	        {
	            if (property.Name == nameof(Parent)) continue;
	            var remotePropertyValue = property.GetValue(sourceConnectionInfo, null);
                property.SetValue(this, remotePropertyValue, null);
	        }
            var clonedInheritance = sourceConnectionInfo.Inheritance.Clone();
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
            var properties = typeof(ConnectionInfo).GetProperties();
            var filteredProperties = properties.Where((prop) => !excludedPropertyNames.Contains(prop.Name));
            return filteredProperties;
        }

	    public virtual IEnumerable<PropertyInfo> GetSerializableProperties()
	    {
			var excludedProperties = new[] { "Parent", "Name", "Hostname", "Port", "Inheritance", "OpenConnections",
				"IsContainer", "IsDefault", "ConstantID", "IsQuickConnect", "PleaseConnect" };

		    return GetProperties(excludedProperties);
	    }

	    public virtual void SetParent(ContainerInfo newParent)
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
        private static readonly ConcurrentDictionary<(Type Type, string PropertyName), PropertyInfo> PropertyLookup =
            new ConcurrentDictionary<(Type, string), PropertyInfo>();

        private static PropertyInfo PropertyOf(Type type, string propertyName) =>
            PropertyLookup.GetOrAdd((type, propertyName), key => key.Type.GetProperty(key.PropertyName));

        /// <summary>The readable and writable properties to copy, cached per type.</summary>
        private static readonly ConcurrentDictionary<Type, PropertyInfo[]> CopyableProperties =
            new ConcurrentDictionary<Type, PropertyInfo[]>();

        #region Private Methods
        protected override TPropertyType GetPropertyValue<TPropertyType>(string propertyName, TPropertyType value)
        {
            if (!ShouldThisPropertyBeInherited(propertyName))
                return value;

            var couldGetInheritedValue = TryGetInheritedPropertyValue<TPropertyType>(propertyName, out var inheritedValue);

            return couldGetInheritedValue
                ? inheritedValue
                : value;
        }

	    private bool ShouldThisPropertyBeInherited(string propertyName)
        {
            return ParentIsValidInheritanceTarget() && IsInheritanceTurnedOnForThisProperty(propertyName);
        }

        private bool ParentIsValidInheritanceTarget()
        {
            return Parent != null;
        }

        private bool IsInheritanceTurnedOnForThisProperty(string propertyName)
        {
            // Inheritance has a public setter, and Clone, CopyFrom, DefaultConnectionInfo and
            // the XML deserializer all reassign it. This read sits outside the try/catch that
            // guards the rest of the lookup, so a null here used to take every inheritable
            // getter on the record down with it. Nothing to inherit from is the same answer as
            // inheritance being switched off.
            var inheritance = Inheritance;
            if (inheritance == null)
                return false;

            var inheritPropertyInfo = PropertyOf(inheritance.GetType(), propertyName);
            return inheritPropertyInfo?.GetValue(inheritance, null) is bool inherit && inherit;
        }

        private bool TryGetInheritedPropertyValue<TPropertyType>(string propertyName,
                                                                 [MaybeNullWhen(false)] out TPropertyType inheritedValue)
        {
            try
            {
                var parentPropertyInfo = PropertyOf(Parent.GetType(), propertyName);
                if (parentPropertyInfo == null)
                    throw new NullReferenceException($"Could not retrieve property data for property '{propertyName}' on parent node '{Parent.Name}'");

                var parentValue = parentPropertyInfo.GetValue(Parent, null);

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
            RDPAuthenticationLevel = (RdpProtocol.AuthenticationLevel) Enum.Parse(typeof(RdpProtocol.AuthenticationLevel), Settings.Default.ConDefaultRDPAuthenticationLevel);
            RDPMinutesToIdleTimeout = Settings.Default.ConDefaultRDPMinutesToIdleTimeout;
            RDPAlertIdleTimeout = Settings.Default.ConDefaultRDPAlertIdleTimeout;
            LoadBalanceInfo = Settings.Default.ConDefaultLoadBalanceInfo;
            UseCredSsp = Settings.Default.ConDefaultUseCredSsp;
        }

        private static ProtocolType GetDefaultProtocol()
        {
            if (Enum.TryParse(Settings.Default.ConDefaultProtocol, true, out ProtocolType protocol) &&
                ProtocolTypes.IsSupported(protocol))
                return protocol;

            return ProtocolType.RDP;
        }

        private void SetRdGatewayDefaults()
        {
            RDGatewayUsageMethod = (RdpProtocol.RDGatewayUsageMethod) Enum.Parse(typeof(RdpProtocol.RDGatewayUsageMethod), Settings.Default.ConDefaultRDGatewayUsageMethod);
            RDGatewayHostname = Settings.Default.ConDefaultRDGatewayHostname;
            RDGatewayUseConnectionCredentials = (RdpProtocol.RDGatewayUseConnectionCredentials) Enum.Parse(typeof(RdpProtocol.RDGatewayUseConnectionCredentials), Settings.Default.ConDefaultRDGatewayUseConnectionCredentials);
            RDGatewayUsername = Settings.Default.ConDefaultRDGatewayUsername;
            RDGatewayPassword = Settings.Default.ConDefaultRDGatewayPassword;
            RDGatewayDomain = Settings.Default.ConDefaultRDGatewayDomain;
        }

        private void SetAppearanceDefaults() 
        {
            Resolution = (RdpProtocol.RDPResolutions) Enum.Parse(typeof(RdpProtocol.RDPResolutions), Settings.Default.ConDefaultResolution);
            AutomaticResize = Settings.Default.ConDefaultAutomaticResize;
            Colors = (RdpProtocol.RDPColors) Enum.Parse(typeof(RdpProtocol.RDPColors), Settings.Default.ConDefaultColors);
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
            RedirectSound = (RdpProtocol.RDPSounds) Enum.Parse(typeof(RdpProtocol.RDPSounds), Settings.Default.ConDefaultRedirectSound);
            SoundQuality = (RdpProtocol.RDPSoundQuality)Enum.Parse(typeof(RdpProtocol.RDPSoundQuality), Settings.Default.ConDefaultSoundQuality);
        }

        private void SetMiscDefaults()
        {
            MacAddress = Settings.Default.ConDefaultMacAddress;
            UserField = Settings.Default.ConDefaultUserField;
        }

        private void SetNonBrowsablePropertiesDefaults()
        {
            Inheritance = new ConnectionInfoInheritance(this);
            SetNewOpenConnectionList();
            //PositionID = 0;
        }

        private void SetNewOpenConnectionList()
	    {
	        OpenConnections = new ProtocolList();
	        OpenConnections.CollectionChanged += (sender, args) => RaisePropertyChangedEvent(this, new PropertyChangedEventArgs("OpenConnections"));
	    }
        #endregion
    }
}