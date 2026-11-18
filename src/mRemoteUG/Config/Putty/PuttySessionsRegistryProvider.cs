using Microsoft.Win32;

using mRemoteUG.App;
using mRemoteUG.Connection;
using mRemoteUG.Connection.Protocol;
using mRemoteUG.Messages;
using mRemoteUG.Tools;


namespace mRemoteUG.Config.Putty;

public class PuttySessionsRegistryProvider : AbstractPuttySessionsProvider
{
    private const string PuttySessionsKey = "Software\\SimonTatham\\PuTTY\\Sessions";

    /// <summary>
    /// Null until <see cref="StartWatcher"/> finds a key to watch, and again after <see cref="StopWatcher"/>.
    /// </summary>
    private static RegistryKeyChangeWatcher? s_changeWatcher;

    /// <summary>
    /// The key under HKCU the sessions are read from and watched, PuTTY's own by default.
    /// </summary>
    /// <remarks>
    /// Settable only so the tests can point the provider at a scratch key and never touch the
    /// user's real PuTTY sessions. The path needs at least two parent keys, which
    /// <see cref="WatchCandidateKeys"/> falls back to.
    /// </remarks>
    internal string SessionsKeyPath { get; set; } = PuttySessionsKey;

    #region Public Methods
    public override string[] GetSessionNames(bool raw = false)
    {
        using RegistryKey? sessionsKey = Registry.CurrentUser.OpenSubKey(SessionsKeyPath);
        if (sessionsKey == null)
        {
            return Array.Empty<string>();
        }

        var sessionNames = new List<string>();
        foreach (string sessionName in sessionsKey.GetSubKeyNames())
        {
            sessionNames.Add(raw
                ? sessionName
                : PuttySessionName.Decode(sessionName));
        }

        if (raw && !sessionNames.Contains("Default%20Settings"))
        {
            sessionNames.Insert(0, "Default%20Settings");
        }
        else if (!raw && !sessionNames.Contains("Default Settings"))
        {
            sessionNames.Insert(0, "Default Settings");
        }

        return sessionNames.ToArray();
    }

    public override PuttySessionInfo? GetSession(string sessionName)
    {
        if (string.IsNullOrEmpty(sessionName))
        {
            return null;
        }

        using RegistryKey? sessionsKey = Registry.CurrentUser.OpenSubKey(SessionsKeyPath);
        using RegistryKey? sessionKey = sessionsKey?.OpenSubKey(sessionName);
        if (sessionKey == null)
        {
            return null;
        }

        sessionName = PuttySessionName.Decode(sessionName);

        var sessionInfo = new PuttySessionInfo
        {
            PuttySession = sessionName,
            Name = sessionName,
            Hostname = sessionKey.GetValue("HostName")?.ToString() ?? "",
            Username = sessionKey.GetValue("UserName")?.ToString() ?? ""
        };


        string? storedProtocol = sessionKey.GetValue("Protocol")?.ToString();
        string protocol = string.IsNullOrEmpty(storedProtocol) ? "ssh" : storedProtocol;

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
                // A missing, empty or non-numeric value is not SSH-1: PuTTY's own default is 3.
                if (!int.TryParse(sessionKey.GetValue("SshProt")?.ToString(), out int sshVersion))
                {
                    sshVersion = 3;
                }

                /* Per PUTTY.H in PuTTYNG & PuTTYNG Upstream (PuTTY proper currently)
                 * expect 0 for SSH1, 3 for SSH2 ONLY
                 * 1 for SSH1 with a 2 fallback
                 * 2 for SSH2 with a 1 fallback
                 *
                 * only an explicit 0 or 1 means SSH1; a missing, empty, non-numeric or any other
                 * value means SSH2, as in PuTTY (whose default is 3)
                 */
                sessionInfo.Protocol = sshVersion == 1 || sshVersion == 0 ? ProtocolType.SSH1 : ProtocolType.SSH2;
                break;
            case "telnet":
                sessionInfo.Protocol = ProtocolType.Telnet;
                break;
            default:
                return null;
        }

        _ = int.TryParse(sessionKey.GetValue("PortNumber")?.ToString(), out int portNumber);
        if (portNumber == default(int))
        {
            sessionInfo.SetDefaultPort();
        }
        else
        {
            sessionInfo.Port = portNumber;
        }

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
        if (s_changeWatcher != null)
        {
            return;
        }

        foreach (string candidate in WatchCandidateKeys())
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
                s_changeWatcher = watcher;
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
            $"PuttySessions.Watcher: no PuTTY registry key found under HKCU\\{SessionsKeyPath}; "
            + "not watching for session changes.",
            true);
    }

    public override void StopWatcher()
    {
        // Note the field is cleared, unlike the watcher this replaced: that one disposed the
        // object but left the field set, so StartWatcher afterwards returned early and the
        // watcher never restarted.
        if (s_changeWatcher == null)
        {
            return;
        }

        s_changeWatcher.Changed -= OnRegistryKeyChanged;
        s_changeWatcher.Dispose();
        s_changeWatcher = null;
    }
    #endregion

    /// <summary>
    /// Keys to watch, deepest first: the Sessions key, then PuTTY's key, then its parent. Only
    /// the Sessions key matters, but watching a PuTTY parent key lets sessions be noticed the
    /// first time PuTTY creates them.
    /// </summary>
    private string[] WatchCandidateKeys()
    {
        string puttyKey = ParentKeyPath(SessionsKeyPath);
        return new[] { SessionsKeyPath, puttyKey, ParentKeyPath(puttyKey) };
    }

    private static string ParentKeyPath(string keyPath)
    {
        return keyPath.Substring(0, keyPath.LastIndexOf('\\'));
    }

    private void OnRegistryKeyChanged(object? sender, EventArgs e)
    {
        RaiseSessionChangedEvent(new PuttySessionChangedEventArgs());
    }
}
