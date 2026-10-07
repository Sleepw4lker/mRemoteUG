using System;
using mRemoteUG.Connection;
using mRemoteUG.Connection.Protocol;
using mRemoteUG.Connection.Protocol.RDP;

namespace mRemoteUG.Tests.TestHelpers
{
    internal static class ConnectionInfoHelpers
	{
		private static readonly Random _random = new Random();

		/// <summary>
		/// Returns a <see cref="ConnectionInfo"/> object with randomized
		/// values in all fields.
		/// </summary>
		internal static ConnectionInfo GetRandomizedConnectionInfo(bool randomizeInheritance = false)
		{
			var connectionInfo = new ConnectionInfo
			{
				// string types
				Name = RandomString(),
				Hostname = RandomString(),
				Description = RandomString(),
				Domain = RandomString(),
				Icon = RandomString(),
				LoadBalanceInfo = RandomString(),
				MacAddress = RandomString(),
				Panel = RandomString(),
				Password = RandomString(),
				RDGatewayHostname = RandomString(),
				RDGatewayUsername = RandomString(),
				RDGatewayDomain = RandomString(),
				RDGatewayPassword = RandomString(),
				UserField = RandomString(),
				Username = RandomString(),

				// bool types
				AutomaticResize = RandomBool(),
				CacheBitmaps = RandomBool(),
				DisplayThemes = RandomBool(),
				DisplayWallpaper = RandomBool(),
				EnableDesktopComposition = RandomBool(),
				EnableFontSmoothing = RandomBool(),
				IsContainer = RandomBool(),
				IsDefault = RandomBool(),
				IsQuickConnect = RandomBool(),
				PleaseConnect = RandomBool(),
				RDPAlertIdleTimeout = RandomBool(),
				RedirectDiskDrives = RandomBool(),
				RedirectKeys = RandomBool(),
				RedirectPorts = RandomBool(),
				RedirectPrinters = RandomBool(),
				RedirectSmartCards = RandomBool(),
				RedirectClipboard = RandomBool(),
				UseConsoleSession = RandomBool(),
				UseCredSsp = RandomBool(),

				// ints
				Port = RandomInt(),
				RDPMinutesToIdleTimeout = RandomInt(),

				// enums
				Colors = RandomEnum<RdpProtocol.RDPColors>(),
				Protocol = ProtocolType.RDP,
				PuttySession = RandomString(),
				RDGatewayUsageMethod = RandomEnum<RdpProtocol.RDGatewayUsageMethod>(),
				RDGatewayUseConnectionCredentials = RandomEnum<RdpProtocol.RDGatewayUseConnectionCredentials>(),
				RDPAuthenticationLevel = RandomEnum<RdpProtocol.AuthenticationLevel>(),
				RedirectSound = RandomEnum<RdpProtocol.RDPSounds>(),
				Resolution = RandomEnum<RdpProtocol.RDPResolutions>(),
				SoundQuality = RandomEnum<RdpProtocol.RDPSoundQuality>(),
			};

			if (randomizeInheritance)
				connectionInfo.Inheritance = GetRandomizedInheritance(connectionInfo);

			return connectionInfo;
		}

		internal static ConnectionInfoInheritance GetRandomizedInheritance(ConnectionInfo parent)
		{
			var inheritance = new ConnectionInfoInheritance(parent, true);
			foreach (var property in inheritance.GetProperties())
			{
				property.SetValue(inheritance, RandomBool());
			}
			return inheritance;
		}

		internal static string RandomString()
		{
			return Guid.NewGuid().ToString("N");
		}

		internal static bool RandomBool()
		{
			return _random.Next() % 2 == 0;
		}

		internal static int RandomInt()
		{
			return _random.Next();
		}

		internal static T RandomEnum<T>() where T : struct, IConvertible
		{
			if (!typeof(T).IsEnum)
				throw new ArgumentException("T must be an enum");

			var values = Enum.GetValues(typeof(T));
			return (T)values.GetValue(_random.Next(values.Length));
		}
    }
}
