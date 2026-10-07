using System;
using System.Collections.Generic;
using System.Text;
using System.Web;
using Microsoft.Win32;
using mRemoteUG.App;
using mRemoteUG.Connection;
using mRemoteUG.Connection.Protocol;
using mRemoteUG.Messages;
using mRemoteUG.Tools;


namespace mRemoteUG.Config.Putty
{
    public class PuttySessionsRegistryProvider : AbstractPuttySessionsProvider
	{
        private const string PuttySessionsKey = "Software\\SimonTatham\\PuTTY\\Sessions";

        /// <summary>
        /// Keys to watch, deepest first. Only the Sessions key matters, but watching a PuTTY
        /// parent key lets sessions be noticed the first time PuTTY creates them.
        /// </summary>
        private static readonly string[] WatchCandidateKeys =
        {
            PuttySessionsKey,
            "Software\\SimonTatham\\PuTTY",
            "Software\\SimonTatham"
        };

        private static RegistryKeyChangeWatcher _changeWatcher;

        #region Public Methods
		public override string[] GetSessionNames(bool raw = false)
		{
            var sessionsKey = Registry.CurrentUser.OpenSubKey(PuttySessionsKey);
			if (sessionsKey == null) return new string[] {};

            var sessionNames = new List<string>();
			foreach (var sessionName in sessionsKey.GetSubKeyNames())
			{
			    sessionNames.Add(raw ? sessionName : HttpUtility.UrlDecode(sessionName.Replace("+", "%2B"), Encoding.GetEncoding("iso-8859-1")));
			}
				
			if (raw && !sessionNames.Contains("Default%20Settings"))
				sessionNames.Insert(0, "Default%20Settings");
			else if (!raw && !sessionNames.Contains("Default Settings"))
				sessionNames.Insert(0, "Default Settings");
				
			return sessionNames.ToArray();
		}
        
        public override PuttySessionInfo GetSession(string sessionName)
        {
            if (string.IsNullOrEmpty(sessionName))
                return null;

            var sessionsKey = Registry.CurrentUser.OpenSubKey(PuttySessionsKey);
            var sessionKey = sessionsKey?.OpenSubKey(sessionName);
			if (sessionKey == null)	return null;

            sessionName = HttpUtility.UrlDecode(sessionName.Replace("+", "%2B"), Encoding.GetEncoding("iso-8859-1"));
            
		    var sessionInfo = new PuttySessionInfo
		    {
		        PuttySession = sessionName,
		        Name = sessionName,
		        Hostname = sessionKey.GetValue("HostName")?.ToString() ?? "",
		        Username = sessionKey.GetValue("UserName")?.ToString() ?? ""
		    };


		    var protocol = string.IsNullOrEmpty(sessionKey.GetValue("Protocol")?.ToString())
		        ? "ssh"
                : sessionKey.GetValue("Protocol").ToString();

		    switch (protocol.ToLowerInvariant())
			{
				case "raw":
					sessionInfo.Protocol = ProtocolType.RAW;
					break;
				case "rlogin":
					sessionInfo.Protocol = ProtocolType.Rlogin;
					break;
				case "serial":
					return null;
				case "ssh":
				    int.TryParse(sessionKey.GetValue("SshProt")?.ToString(), out var sshVersion);
                    /* Per PUTTY.H in PuTTYNG & PuTTYNG Upstream (PuTTY proper currently)
                     * expect 0 for SSH1, 3 for SSH2 ONLY
                     * 1 for SSH1 with a 2 fallback
                     * 2 for SSH2 with a 1 fallback
                     *
                     * default to SSH2 if any other value is received
                     */
                    sessionInfo.Protocol = sshVersion == 1 || sshVersion == 0 ? ProtocolType.SSH1 : ProtocolType.SSH2;
					break;
				case "telnet":
					sessionInfo.Protocol = ProtocolType.Telnet;
					break;
				default:
					return null;
			}

            int.TryParse(sessionKey.GetValue("PortNumber")?.ToString(), out var portNumber);
            if (portNumber == default(int))
                sessionInfo.SetDefaultPort();
            else
                sessionInfo.Port = portNumber;
				
			return sessionInfo;
		}
			
        /// <summary>
        /// Watches PuTTY's registry key so sessions added or edited outside mRemoteUG appear
        /// without a restart.
        /// </summary>
        /// <remarks>
        /// When PuTTY has never been run there is no Sessions key to watch, so this falls back to
        /// the nearest existing ancestor. The candidates are deliberately limited to PuTTY's own
        /// keys: watching something broad such as HKCU\Software with a subtree filter would fire
        /// constantly for unrelated changes.
        /// </remarks>
        public override void StartWatcher()
		{
			if (_changeWatcher != null) return;

			foreach (var candidate in WatchCandidateKeys)
			{
				try
				{
					var watcher = new RegistryKeyChangeWatcher(Registry.CurrentUser, candidate);
					if (!watcher.Start())
					{
						// Key absent; try the next-shallower candidate.
						watcher.Dispose();
						continue;
					}

					watcher.Changed += OnRegistryKeyChanged;
					_changeWatcher = watcher;
					return;
				}
				catch (Exception ex)
				{
					Runtime.MessageCollector.AddExceptionMessage(
						$"PuttySessions.Watcher.StartWatching() failed for '{candidate}'.", ex, MessageClass.WarningMsg);
					return;
				}
			}

			// Nothing to watch is normal when PuTTY is not installed, so this is informational.
			Runtime.MessageCollector.AddMessage(MessageClass.InformationMsg,
				$"PuttySessions.Watcher: no PuTTY registry key found under HKCU\\{PuttySessionsKey}; not watching for session changes.", true);
		}

		public override void StopWatcher()
		{
			// Note the field is cleared, unlike the watcher this replaced: that one disposed the
			// object but left the field set, so StartWatcher afterwards returned early and the
			// watcher never restarted.
			if (_changeWatcher == null) return;
			_changeWatcher.Changed -= OnRegistryKeyChanged;
			_changeWatcher.Dispose();
			_changeWatcher = null;
		}
        #endregion

		private void OnRegistryKeyChanged(object? sender, EventArgs e)
		{
			RaiseSessionChangedEvent(new PuttySessionChangedEventArgs());
		}
    }
}