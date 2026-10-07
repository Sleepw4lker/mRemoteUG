namespace mRemoteUG.Tests.TestHelpers
{
	/// <summary>
	/// A ConnectionInfo that has only the serializable properties as string types.
	/// Only used for testing.
	/// </summary>
	internal class SerializableConnectionInfoAllPropertiesOfType<TType>
	{
		public TType Description { get; set; }
		public TType Icon { get; set; }
		public TType Panel { get; set; }
		public TType Username { get; set; }
		public TType Password { get; set; }
		public TType Domain { get; set; }
		public TType Protocol { get; set; }
		public TType PuttySession { get; set; }
		public TType UseConsoleSession { get; set; }
		public TType RDPAuthenticationLevel { get; set; }
		public TType RDPMinutesToIdleTimeout { get; set; }
		public TType RDPAlertIdleTimeout { get; set; }
		public TType LoadBalanceInfo { get; set; }
		public TType UseCredSsp { get; set; }
		public TType RDGatewayUsageMethod { get; set; }
		public TType RDGatewayHostname { get; set; }
		public TType RDGatewayUseConnectionCredentials { get; set; }
		public TType RDGatewayUsername { get; set; }
		public TType RDGatewayPassword { get; set; }
		public TType RDGatewayDomain { get; set; }
		public TType Resolution { get; set; }
		public TType AutomaticResize { get; set; }
		public TType Colors { get; set; }
		public TType CacheBitmaps { get; set; }
		public TType DisplayWallpaper { get; set; }
		public TType DisplayThemes { get; set; }
		public TType EnableFontSmoothing { get; set; }
		public TType EnableDesktopComposition { get; set; }
		public TType RedirectKeys { get; set; }
		public TType RedirectDiskDrives { get; set; }
		public TType RedirectPrinters { get; set; }
		public TType RedirectPorts { get; set; }
		public TType RedirectSmartCards { get; set; }
		public TType RedirectClipboard { get; set; }
		public TType RedirectSound { get; set; }
		public TType SoundQuality { get; set; }
		public TType MacAddress { get; set; }
		public TType UserField { get; set; }
	}
}
