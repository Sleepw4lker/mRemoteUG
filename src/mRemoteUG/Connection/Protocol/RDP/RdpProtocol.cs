using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net.Sockets;
using System.Runtime.InteropServices;

using mRemoteUG.App;
using mRemoteUG.Messages;
using mRemoteUG.Security;
using mRemoteUG.Tools;
using mRemoteUG.UI.Controls;
using mRemoteUG.UI.Forms;

using MSTSCLib;

namespace mRemoteUG.Connection.Protocol.RDP;

public class RdpProtocol : ProtocolBase
{
    /// <summary>
    /// Seconds mstscax may spend waiting for a graceful session shutdown while we are closing
    /// the tab. The session is going away regardless; this only bounds how long the UI waits.
    /// </summary>
    private const int ShutdownTimeoutSecondsOnClose = 3;

    /// <summary>
    /// How long to wait for the hosted control's window handle before giving up.
    /// </summary>
    /// <remarks>
    /// Generous, because it is not a budget anything normally spends: the handle is there
    /// on the first turn of the loop. It exists so that "shortly" cannot become "never".
    /// </remarks>
    private static readonly TimeSpan s_controlCreationTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The OCX, seen through the newest interface the mstscax type library declares.
    /// </summary>
    /// <remarks>
    /// <c>MsRdpClient11NotSafeForScripting</c>'s IID *is* <c>IMsRdpClient10</c>'s, and that is
    /// also the default interface of <c>MsRdpClient12NotSafeForScripting</c>, so this one type
    /// drives either coclass - casting to it is exactly QueryInterface(IID_IMsRdpClient10).
    /// It also carries <c>IMsTscAxEvents_Event</c>, so the event sinks below need no second
    /// reference. <c>AxInteropSurfaceTests</c> pins both facts.
    /// <para>
    /// There is no IMsRdpClient11 or IMsRdpClient12 in the type library; IMsRdpClient10 is the
    /// ceiling, and anything newer is reached through the side interfaces
    /// (IMsRdpClientNonScriptable8, IMsRdpExtendedSettings, IMsRdpPreferredRedirectionInfo).
    /// </para>
    /// <para>
    /// Null until <see cref="Initialize"/> has created the control, and null again once
    /// <see cref="ReleaseTheSessionsComReferences"/> has run during teardown (ADR-0005).
    /// <see cref="Initialize"/> hands its own non-null reference to the helpers it calls; every
    /// other entry point checks this field once, at its top.
    /// </para>
    /// </remarks>
    private MsRdpClient11NotSafeForScripting? _rdpClient;

    /// <summary>
    /// mstscax.dll's own file version - <em>not</em> the coclass generation, which is why it
    /// is only ever logged and never branched on. It reads 10.0.26xxx on Windows 11 whichever
    /// coclass was created, so the version comparisons this class used to make could not
    /// discriminate between clients at all.
    /// </summary>
    private Version? _rdpVersion;

    /// <summary>
    /// The connection this session was opened for: null until <see cref="Initialize"/> sets it,
    /// and never cleared, so it is set whenever <see cref="_rdpClient"/> is.
    /// </summary>
    private ConnectionInfo? _connectionInfo;
    private bool _loginComplete;

    /// <summary>
    /// True from <c>OnConnected</c> until the session goes away.
    /// </summary>
    /// <remarks>
    /// The display-scale passes below need "there is a session to talk to", which is not the
    /// same question as <see cref="_loginComplete"/>, and the earlier answer is the one that
    /// matters: a scale set while the session still shows the logon screen is the scale the
    /// user's desktop is then created at.
    /// <para>
    /// Tracked here rather than read from the OCX's <c>Connected</c> property, which answers a
    /// different question - 0 disconnected, 1 connected, 2 connecting - and is nowhere
    /// documented to have settled by the time the OCX runs its own event sink. A gate that
    /// silently read 2 there would take the whole of this with it, with nothing to show why.
    /// </para>
    /// </remarks>
    private bool _sessionConnected;

    /// <summary>
    /// The DPI this session is being viewed at, as last seen on the UI thread.
    /// </summary>
    /// <remarks>
    /// <see cref="SetDisplayScale"/> needs a DPI, and one of its callers -
    /// <see cref="TmrReconnectElapsed"/> - runs on a timer thread, where reading
    /// <c>Control.DeviceDpi</c> would be touching the UI from the wrong thread. So the value
    /// is recorded where it is known for certain, on the UI thread: once when the control is
    /// set up, and again on every DPI change. Read rather than measured, which is the point.
    /// </remarks>
    private int _lastKnownDpi = RdpDisplayScale.DefaultDpi;
    private bool _redirectKeys;
    private bool _alertOnIdleDisconnect;
    private readonly FrmMain _frmMain = FrmMain.Default;

    /// <summary>
    /// Covers the OCX until the session has something of its own to show, or null once it has
    /// been taken away.
    /// </summary>
    private ConnectingOverlay? _connectingOverlay;

    #region Properties
    /// <remarks>False, and setting it does nothing, while there is no RDP control.</remarks>
    public bool SmartSize
    {
        get
        {
            return _rdpClient is { } client && client.AdvancedSettings2.SmartSizing;
        }
        private set
        {
            if (_rdpClient is not { } client)
            {
                return;
            }

            client.AdvancedSettings2.SmartSizing = value;
            ReconnectForResize();
        }
    }

    /// <remarks>False, and setting it does nothing, while there is no RDP control.</remarks>
    public bool Fullscreen
    {
        get
        {
            return _rdpClient is { } client && client.FullScreen;
        }
        private set
        {
            if (_rdpClient is not { } client)
            {
                return;
            }

            client.FullScreen = value;
            ReconnectForResize();
        }
    }

    private void SetRedirectKeys(MsRdpClient11NotSafeForScripting client, bool redirectKeys)
    {
        _redirectKeys = redirectKeys;
        try
        {
            if (!_redirectKeys)
            {
                return;
            }

            Debug.Assert(Convert.ToBoolean(client.SecuredSettingsEnabled));
            IMsRdpClientSecuredSettings msRdpClientSecuredSettings = client.SecuredSettings2;
            msRdpClientSecuredSettings.KeyboardHookMode = 1; // Apply key combinations at the remote server.
        }
        catch (Exception ex)
        {
            Runtime.MessageCollector.AddExceptionMessage(Language.strRdpSetRedirectKeysFailed, ex);
        }
    }

    public bool LoadBalanceInfoUseUtf8 { get; set; }
    #endregion

    #region Constructors
    public RdpProtocol()
    {
        // A placeholder, not the real control: it only exists so ProtocolBase.Initialize has
        // something to name, parent and dock before CreateNewestAvailableRdpControl swaps in
        // the negotiated one. It must not be an AxHost. Parenting one CoCreates it (see the
        // comment in RdpClientCandidates.CreateNewest), so an ActiveX placeholder would both
        // create a control we immediately discard and move a "no RDP control available"
        // failure into ProtocolBase.Initialize's generic catch - losing the per-candidate
        // attempt list that is the only way to diagnose it on a remote machine.
        Control = new Panel();
    }
    #endregion

    #region Public Methods
    public override bool Initialize()
    {
        base.Initialize();
        try
        {
            if (!CreateNewestAvailableRdpControl())
            {
                return false;
            }
            InterfaceControl interfaceControl = AssignedInterfaceControl;
            ConnectionInfo connectionInfo = interfaceControl.Info;
            _connectionInfo = connectionInfo;

            // Before the wait below, not after it: that loop pumps messages, so this is where the
            // OCX first gets to paint its white page. See ConnectingOverlay.
            ShowTheConnectingOverlay();

            MsRdpClient11NotSafeForScripting rdpClient;
            AxHost control;
            try
            {
                // Bounded. This pumped and slept with no exit condition at all, on the UI thread,
                // during creation of an ActiveX control - and it now runs while the main window is
                // still being brought up (see FrmMain.CompleteStartup), which is where a handle
                // chain is least certain. The pumping is deliberately unchanged: this is a bound,
                // not a redesign. See ADR-0005.
                //
                // Control is read through AssignedControl on every turn rather than kept in a
                // local: the loop pumps, and a close that runs inside it clears Control. That
                // throws into the outer catch, as the bare dereference always did, rather than
                // waiting out the timeout on a control the teardown already owns.
                var creation = Stopwatch.StartNew();
                while (!AssignedControl.Created && creation.Elapsed < s_controlCreationTimeout)
                {
                    Thread.Sleep(0);
                    Application.DoEvents();
                }

                if (!AssignedControl.Created)
                {
                    Runtime.MessageCollector.AddMessage(MessageClass.WarningMsg,
                        $"The RDP control had no window handle after {creation.ElapsedMilliseconds} ms; " +
                        "giving up rather than waiting indefinitely.");
                    AssignedControl.Dispose();
                    return false;
                }
                // QueryInterface(IID_IMsRdpClient10) in all but name. This is the per-connection
                // proof that the control really can be driven through its newest interface;
                // it fails loudly into strRdpControlCreationFailed below rather than silently
                // degrading to an older vtable.
                control = (AxHost)AssignedControl;
                // A null OCX used to reach the first property read below and fail there, into
                // the outer catch; it fails into the same catch here, with a message.
                rdpClient = (MsRdpClient11NotSafeForScripting)(control.GetOcx()
                    ?? throw new InvalidOperationException("The RDP control returned no OCX."));
                _rdpClient = rdpClient;
            }
            catch (System.Runtime.InteropServices.COMException ex)
            {
                Runtime.MessageCollector.AddExceptionMessage(Language.strRdpControlCreationFailed, ex);
                AssignedControl.Dispose();
                return false;
            }

            _rdpVersion = new Version(rdpClient.Version);
            LogTheClientWeGot(control);

            rdpClient.Server = connectionInfo.Hostname;

            SetCredentials(rdpClient, connectionInfo);
            SetResolution(rdpClient, interfaceControl);
            _lastKnownDpi = CurrentDpi();
            SetDisplayScale(rdpClient, Control?.DeviceDpi);
            rdpClient.FullScreenTitle = connectionInfo.Name;

            _alertOnIdleDisconnect = connectionInfo.RDPAlertIdleTimeout;
            rdpClient.AdvancedSettings2.MinutesToIdleTimeout = connectionInfo.RDPMinutesToIdleTimeout;

            // Deliberately not individually guarded, unlike SetRedirection below. Every
            // assignment from here to ConnectingText is a client-side property write on an OCX
            // that has not connected to anything yet, so no server - however old - can make one
            // fail; and they are all bound against IMsRdpClient10, which the v11 floor
            // guarantees. What a failure here would mean is a genuinely broken control, and
            // aborting the connection with strRdpSetPropsFailed is the right answer to that.
            // Guarding them one by one would trade a clear abort for a session that connects
            // with a silently wrong security level or no credentials.

            //not user changeable
            rdpClient.AdvancedSettings2.GrabFocusOnConnect = true;
            rdpClient.AdvancedSettings3.EnableAutoReconnect = true;
            rdpClient.AdvancedSettings3.MaxReconnectAttempts = Settings.Default.RdpReconnectionCount;
            rdpClient.AdvancedSettings2.keepAliveInterval = 60000; //in milliseconds (10,000 = 10 seconds)
            rdpClient.AdvancedSettings5.AuthenticationLevel = 0;
            rdpClient.AdvancedSettings2.EncryptionEnabled = 1;

            rdpClient.AdvancedSettings2.overallConnectionTimeout = Settings.Default.ConRDPOverallConnectionTimeout;

            rdpClient.AdvancedSettings2.BitmapPeristence = Convert.ToInt32(connectionInfo.CacheBitmaps);
            rdpClient.AdvancedSettings7.EnableCredSspSupport = connectionInfo.UseCredSsp;
            rdpClient.AdvancedSettings8.AudioQualityMode = (uint)connectionInfo.SoundQuality;

            SetUseConsoleSession(rdpClient, connectionInfo);
            SetPort(rdpClient, connectionInfo);
            SetRedirectKeys(rdpClient, connectionInfo.RedirectKeys);
            SetRedirection(rdpClient, connectionInfo);
            SetAuthenticationLevel(rdpClient, connectionInfo);
            SetLoadBalanceInfo(rdpClient, connectionInfo);
            SetRdGateway(rdpClient, connectionInfo);

            rdpClient.ColorDepth = (int)connectionInfo.Colors;

            SetPerformanceFlags(rdpClient, connectionInfo);

            rdpClient.ConnectingText = Language.strConnecting;

            return true;
        }
        catch (Exception ex)
        {
            Runtime.MessageCollector.AddExceptionMessage(Language.strRdpSetPropsFailed, ex);
            return false;
        }
    }

    public override bool Connect()
    {
        _loginComplete = false;
        // A new session may be a new server - a broker can redirect a reconnect elsewhere - so
        // re-probe the display-control channel rather than inherit the last session's answer.
        _sessionRefusedDisplayUpdates = false;
        // Same object, same reason: one RdpProtocol opens more than one session. A session
        // that comes up at the size the last one had matches this cache, and the skip reason
        // is logged only on a transition, so a stale one silences the new session the first
        // time it skips a resize.
        //
        // Note what does *not* come through here. TmrReconnectElapsed calls
        // _rdpClient.Connect() directly, and mstscax's own EnableAutoReconnect never enters
        // managed code at all, so neither automatic reconnect resets any of this - which is
        // exactly why the display-scale passes below do not consult the cache.
        _lastRequestedSessionSize = Size.Empty;
        _lastRequestedSessionScale = (0, 0);
        _lastResizeSkipReason = null;
        _sessionConnected = false;

        // After the resets above, which hold whether or not there is a session to open: the
        // state they clear belongs to the last one. No client means Initialize did not create a
        // control, or the teardown has released it.
        if (_rdpClient is not { } client)
        {
            return false;
        }

        SetEventHandlers(client);

        try
        {
            client.Connect();
            base.Connect();
            return true;
        }
        catch (Exception ex)
        {
            Runtime.MessageCollector.AddExceptionMessage(Language.strConnectionOpenFailed, ex);
        }

        return false;
    }

    public override void Disconnect()
    {
        // _rdpClient is null once CleanupProtocolResources has run, i.e. the session is already
        // closing. Nothing to disconnect, and Close() would be a no-op anyway.
        if (_rdpClient is not { } client)
        {
            return;
        }

        try
        {
            client.Disconnect();
        }
        catch (Exception ex)
        {
            Runtime.MessageCollector.AddExceptionMessage(Language.strRdpDisconnectFailed, ex);
            Close();
        }
    }

    public void ToggleFullscreen()
    {
        try
        {
            Fullscreen = !Fullscreen;
        }
        catch (Exception ex)
        {
            Runtime.MessageCollector.AddExceptionMessage(Language.strRdpToggleFullscreenFailed, ex);
        }
    }

    public void ToggleSmartSize()
    {
        try
        {
            SmartSize = !SmartSize;
        }
        catch (Exception ex)
        {
            Runtime.MessageCollector.AddExceptionMessage(Language.strRdpToggleSmartSizeFailed, ex);
        }
    }

    public override void Focus()
    {
        try
        {
            Control control = AssignedControl;
            if (control.ContainsFocus == false)
            {
                control.Focus();
            }
        }
        catch (Exception ex)
        {
            Runtime.MessageCollector.AddExceptionMessage(Language.strRdpFocusFailed, ex);
        }
    }

    /// <summary>
    /// The control's size when the current move/resize gesture started, or
    /// <see cref="Size.Empty"/> when no gesture is in progress.
    /// </summary>
    /// <remarks>
    /// ResizeBegin and ResizeEnd come from the main window's modal move/resize loop, whereas
    /// Resize comes from the connection panel. While a gesture is running the session is left
    /// alone and resized once at ResizeEnd, rather than on every frame of the drag.
    /// </remarks>
    private Size _controlBeginningSize;

    public override void ResizeBegin(object? sender, EventArgs e)
    {
        _controlBeginningSize = AssignedControl.Size;
    }

    public override void Resize(object? sender, EventArgs e)
    {
        // The control itself is docked, so it has already followed the panel by now. Outside a
        // drag - a maximise, a restore, a snap - there is no ResizeEnd coming, so act here.
        if (_controlBeginningSize.IsEmpty)
        {
            ReconnectForResize();
        }
        base.Resize(sender, e);
    }

    public override void ResizeEnd(object? sender, EventArgs e)
    {
        if (AssignedControl.Size != _controlBeginningSize)
        {
            ReconnectForResize();
        }
        _controlBeginningSize = Size.Empty;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// <see cref="ProtocolBase.Control"/>, for the code that dereferenced it bare.
    /// </summary>
    /// <remarks>
    /// The constructor assigns the placeholder and the teardown clears it last, so every caller
    /// here runs while there is one. Outside that window this code used to throw a
    /// <see cref="NullReferenceException"/>; it now throws at the same point, into the same
    /// handler, with a message that says what was missing - the way
    /// <see cref="ProtocolBase.AssignedInterfaceControl"/> does for the panel.
    /// </remarks>
    private Control AssignedControl =>
        Control ?? throw new InvalidOperationException($"{nameof(RdpProtocol)} has no hosted control.");

    private bool CreateNewestAvailableRdpControl()
    {
        Control placeholder = AssignedControl;
        var attempts = new System.Collections.Generic.List<string>();

        AxHost? created = RdpClientCandidates.CreateNewest(
            placeholder.Parent,
            placeholder.Name,
            placeholder.Dock,
            attempts);

        // Logged on success as well as failure, and kept at Information: which control version
        // a machine actually settled on is the first thing worth knowing about a remote
        // session, and it is recorded once per session rather than repeatedly. Detail that can
        // repeat within a session belongs at Debug, which --verbose turns on for a run.
        Runtime.MessageCollector.AddMessage(MessageClass.InformationMsg,
            $"RDP control selection [{SessionLabel}]: {string.Join("; ", attempts)}", true);

        placeholder.Dispose();

        if (created == null)
        {
            Runtime.MessageCollector.AddExceptionMessage(Language.strRdpControlCreationFailed,
                new InvalidOperationException(
                    "No RDP ActiveX control (v11 or v12) could be created: " + string.Join("; ", attempts)));
            return false;
        }

        Control = created;
        created.SizeChanged += HostedControl_SizeChanged;
        return true;
    }

    /// <summary>
    /// Logs every size the hosted control takes, against the sizes around it, and has the
    /// session follow it.
    /// </summary>
    /// <remarks>
    /// The log line is what found both layout defects behind a dock from 150% to 200%: tab
    /// controls scaled past their window, then a tab strip that grew without the connection window
    /// resizing. Debug, so it costs nothing unless <c>--verbose</c> is on.
    /// </remarks>
    private void HostedControl_SizeChanged(object? sender, EventArgs e)
    {
        // A closed session's control keeps raising this while it is parked (ADR-0005), with no
        // panel left to describe and no session to resize - measured, once per close.
        if (sender is not Control control || InterfaceControl is null || _rdpClient is null)
        {
            return;
        }

        try
        {
            string window = "no window";
            if (control.IsHandleCreated && NativeMethods.GetWindowRect(control.Handle, out NativeMethods.RECT rect))
            {
                window = $"window {rect.right - rect.left}x{rect.bottom - rect.top}";
            }

            Size tabPage = AssignedInterfaceControl.Parent?.ClientSize ?? Size.Empty;
            Size connectionWindow = ConnectionWindow?.ClientSize ?? Size.Empty;
            Rectangle screen = Screen.FromControl(control).Bounds;

            Runtime.MessageCollector.AddMessage(MessageClass.DebugMsg,
                $"Hosted control [{SessionLabel}] is now {control.Width}x{control.Height} ({window}) at " +
                $"{CurrentDpi()} DPI (the control's cached DeviceDpi is {control.DeviceDpi}); interface control " +
                $"{AssignedInterfaceControl.Size.Width}x" +
                $"{AssignedInterfaceControl.Size.Height}, tab page {tabPage.Width}x{tabPage.Height}, " +
                $"connection window {connectionWindow.Width}x{connectionWindow.Height}, screen " +
                $"{screen.Width}x{screen.Height}; the session was last asked for " +
                $"{_lastRequestedSessionSize.Width}x{_lastRequestedSessionSize.Height}.", true);
        }
        catch (Exception ex)
        {
            Runtime.MessageCollector.AddMessage(MessageClass.DebugMsg,
                $"Hosted control [{SessionLabel}]: could not describe its size ({ex.Message}).", true);
        }

        FollowHostedControlSize(control);
    }

    /// <summary>Set while a <see cref="FollowHostedControlSize"/> callback is queued.</summary>
    private bool _hostedResizePending;

    /// <summary>
    /// Resizes the session to the hosted control once the current message is done with it.
    /// </summary>
    /// <remarks>
    /// The connection window's <c>Resize</c> is not enough on its own. Measured on a dock from
    /// 150% to 200%: the DPI follow-up's larger font made the tab strip 12 px taller, the hosted
    /// control 12 px shorter, and the connection window kept its size - so nothing resized the
    /// session, and mstscax showed scrollbars for the difference.
    /// <para>
    /// Posted rather than run here: one DPI change passes the control through several
    /// in-between sizes (4723x2467, then 3858x2142, measured) inside a single message, and only
    /// the size it settles at should reach the session. A resize the connection window already
    /// sent is not sent twice: <see cref="ReconnectForResize"/> drops a request for the size and
    /// scale it last asked for. During a drag nothing is sent; <see cref="ResizeEnd"/> does that.
    /// </para>
    /// </remarks>
    private void FollowHostedControlSize(Control control)
    {
        if (_hostedResizePending || !control.IsHandleCreated)
        {
            return;
        }

        _hostedResizePending = true;
        control.BeginInvoke(() =>
        {
            _hostedResizePending = false;
            if (control.IsDisposed || !_controlBeginningSize.IsEmpty
                || InterfaceControl is null || _rdpClient is null)
            {
                return;
            }

            try
            {
                Runtime.MessageCollector.AddMessage(MessageClass.DebugMsg,
                    $"Resize [{SessionLabel}]: following the hosted control at " +
                    $"{control.Width}x{control.Height}.", true);
                ReconnectForResize();
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddMessage(MessageClass.DebugMsg,
                    $"Resize [{SessionLabel}]: could not follow the hosted control ({ex.Message}).", true);
            }
        });
    }

    /// <summary>
    /// Records which client this session actually got, once, at a level that reaches the log
    /// file on a default install.
    /// </summary>
    /// <remarks>
    /// The optional interfaces are probed as locals rather than kept in fields: nothing here
    /// writes to them, and holding them would only invite someone to start setting v10-era
    /// properties - see the note above <see cref="SetRedirection"/> for why we don't.
    /// <c>IMsRdpClient10</c> is not probed because the cast in <see cref="Initialize"/> has
    /// already proved it; had it failed, we would not be here.
    /// </remarks>
    private void LogTheClientWeGot(AxHost control)
    {
        object? ocx = control.GetOcx();
        var optional = new System.Collections.Generic.List<string>();
        if (ocx is IMsRdpClientNonScriptable8)
        {
            optional.Add(nameof(IMsRdpClientNonScriptable8));
        }

        if (ocx is IMsRdpExtendedSettings)
        {
            optional.Add(nameof(IMsRdpExtendedSettings));
        }

        if (ocx is IMsRdpPreferredRedirectionInfo)
        {
            optional.Add(nameof(IMsRdpPreferredRedirectionInfo));
        }

        // Debug: the interface inventory restates what the selection line above already said
        // for the case anyone reads a normal log for.
        Runtime.MessageCollector.AddMessage(MessageClass.DebugMsg,
            $"RDP client [{SessionLabel}]: control={control.GetType().Name}, mstscax={_rdpVersion}, " +
            $"IMsRdpClient10=yes, optional=[{string.Join(", ", optional)}]", true);
    }

    /// <summary>
    /// The DPI of the monitor this session's window is on, asked of Windows.
    /// </summary>
    /// <remarks>
    /// The hosted control's window when it has one, else the panel's. Not <c>DeviceDpi</c>, which
    /// read the DPI the process started at for a control created after a monitor change - see
    /// <see cref="UI.DpiScaling.WindowDpi"/>.
    /// <para>
    /// Virtual, like the three calls on the control below it, so that a test can replay a
    /// measured monitor change: no session can be opened on the development machine.
    /// </para>
    /// </remarks>
    protected virtual int CurrentDpi()
    {
        Control? window = Control is { IsHandleCreated: true } ? Control : InterfaceControl;
        return UI.DpiScaling.WindowDpi(window);
    }

    /// <summary>
    /// Records why a resize was skipped, so the log names the guard that stopped it.
    /// </summary>
    /// <remarks>
    /// Every guard below is a silent early return: when automatic resizing does not happen
    /// there is nothing in the log to say which condition was false, which makes the problem
    /// undiagnosable on a machine that can only send a log back. Only transitions are logged,
    /// so a resize gesture cannot flood the file.
    /// </remarks>
    private string? _lastResizeSkipReason;

    /// <summary>The size the session was last asked to match.</summary>
    private Size _lastRequestedSessionSize;

    /// <summary>The display scale the session was last asked for, as (desktop, device).</summary>
    /// <remarks>
    /// Kept alongside the size because the size alone is not enough to decide whether the
    /// session is already in the state we want: dragging a window to a monitor of a different
    /// DPI without resizing it changes the scale and nothing else, and an early return keyed on
    /// size would swallow exactly that.
    /// </remarks>
    private (uint Desktop, uint Device) _lastRequestedSessionScale;

    private void SkipResize(string reason)
    {
        if (_lastResizeSkipReason == reason)
        {
            return;
        }

        _lastResizeSkipReason = reason;
        Runtime.MessageCollector.AddMessage(MessageClass.DebugMsg,
            $"Resize [{SessionLabel}] skipped: {reason}", true);
    }

    [SuppressMessage("Performance", "CA1863",
                     Justification = "The format is a localized resource read at run time; "
                                     + "a cached CompositeFormat would freeze the UI language.")]
    private void ReconnectForResize()
    {
        // _connectionInfo is set before _rdpClient and never cleared, so the second test only
        // tells the analyzer what is already true.
        if (_rdpClient is not { } client || _connectionInfo is not { } connectionInfo)
        {
            SkipResize("the RDP control has not been initialised yet");
            return;
        }

        if (!_loginComplete)
        {
            SkipResize("login is not complete (OnLoginComplete has not been received)");
            return;
        }

        if (WhyTheSessionKeepsItsResolution(client) is { } reason)
        {
            SkipResize(reason);
            return;
        }

        _lastResizeSkipReason = null;

        try
        {
            Size size = SizeOfTheWindow(client);
            int dpi = CurrentDpi();
            (uint, uint) scale = (RdpDisplayScale.DesktopScaleFactor(dpi), RdpDisplayScale.DeviceScaleFactor(dpi));

            // Resize fires for every WM_SIZE, so only talk to the session when what it was last
            // asked for has actually changed. The scale is part of that: a window dragged to a
            // monitor of a different DPI can arrive at the same size and a different scale.
            if (size == _lastRequestedSessionSize && scale == _lastRequestedSessionScale)
            {
                return;
            }

            _lastRequestedSessionSize = size;
            _lastRequestedSessionScale = scale;

            Runtime.MessageCollector.AddMessage(
                MessageClass.DebugMsg,
                $"Resizing RDP connection to host '{connectionInfo.Hostname}' to {size.Width}x{size.Height}");

            if (!_sessionRefusedDisplayUpdates)
            {
                switch (UpdateSessionDisplay(client, size, dpi, latchRefusal: true))
                {
                    case DisplayUpdate.Applied:
                        return;

                    case DisplayUpdate.RefusedForNow:
                        // Never a reconnect for this: measured, that reconnect dropped the
                        // session again, over and over. Forgotten, so the next resize retries,
                        // and so the next DpiChanged pass falls back to the window's size. The
                        // next logon - after a reconnect too - sends the window's size anyway.
                        _lastRequestedSessionSize = Size.Empty;
                        _lastRequestedSessionScale = default;
                        return;
                }
            }

            ReconnectAtSize(client, (uint)size.Width, (uint)size.Height);
        }
        catch (Exception ex)
        {
            Runtime.MessageCollector.AddExceptionMessage(
                string.Format(
                    CultureInfo.CurrentCulture,
                    Language.ChangeConnectionResolutionError,
                    connectionInfo.Hostname),
                ex, MessageClass.WarningMsg, false);
        }
    }

    /// <summary>
    /// Why this session's resolution does not follow its window, or null when it does.
    /// </summary>
    /// <remarks>
    /// With <c>AutomaticResize</c> off, a fixed resolution, or SmartSize on, the user has said
    /// the session's pixel count should not follow the window. Shared by
    /// <see cref="ReconnectForResize"/> and the display-scale passes, which must agree on it: a
    /// pass that sent a following session its reported size would undo a resize, and one that
    /// sent a fixed session the window's size would undo the user's setting.
    /// </remarks>
    private string? WhyTheSessionKeepsItsResolution(MsRdpClient11NotSafeForScripting client)
    {
        // The panel's, read where it always was: the same record as _connectionInfo, but a
        // session whose panel the teardown has already cleared still stops here.
        ConnectionInfo panelInfo = AssignedInterfaceControl.Info;
        if (!panelInfo.AutomaticResize)
        {
            return "this connection has AutomaticResize turned off";
        }

        if (!(panelInfo.Resolution == RDPResolutions.FitToWindow
              | panelInfo.Resolution == RDPResolutions.Fullscreen))
        {
            return $"resolution is {panelInfo.Resolution}, which is neither FitToWindow nor Fullscreen";
        }

        if (client.AdvancedSettings2.SmartSizing)
        {
            return "SmartSize is on, so the session is scaled rather than resized";
        }

        return null;
    }

    /// <summary>The size a session that follows its window should have now.</summary>
    /// <remarks>
    /// The hosted control's size, or its screen's in full screen: what is on the screen now,
    /// not anything the control reports or was asked for earlier.
    /// </remarks>
    private Size SizeOfTheWindow(MsRdpClient11NotSafeForScripting client)
    {
        Control control = AssignedControl;
        return !client.FullScreen ? control.Size : Screen.FromControl(control).Bounds.Size;
    }

    /// <summary>
    /// Set once this session has refused <c>UpdateSessionDisplaySettings</c>.
    /// </summary>
    /// <remarks>
    /// That refusal is a fact about the session, not about one resize: the display-control
    /// virtual channel arrived in RDP 8.1, so a Server 2008 R2 or 2012 host refuses every
    /// call. Without the latch, a single drag throws a COM exception and writes a log line per
    /// frame. Cleared in <see cref="Connect"/>, because <c>TmrReconnectElapsed</c> reuses this
    /// same object and a Connection Broker can send the reconnect to a different host.
    /// <para>
    /// This deliberately does not go through <see cref="SkipResize"/>, which prefixes its
    /// lines "Resize [...] skipped": the resize is not skipped here, it falls through to
    /// <c>Reconnect</c> and happens. Same transition-only logging idiom, true message.
    /// </para>
    /// </remarks>
    private bool _sessionRefusedDisplayUpdates;

    /// <summary>
    /// Tells the session how large it should be and at what scale.
    /// </summary>
    /// <remarks>
    /// The last two arguments used to be hard-coded to 100, 100 - "no scaling" - which was the
    /// only honest answer while this process was DPI-unaware and every size it measured was
    /// virtualised to 96 DPI. Now that they are physical pixels, a 4K panel gets a 4K session,
    /// and without a scale factor to go with it the remote desktop renders its UI at 100% and
    /// is unreadable. These two arguments are what makes a remote session HiDPI-capable rather
    /// than merely large.
    /// <para>
    /// <c>ulPhysicalWidth</c> and <c>ulPhysicalHeight</c> are **millimetres**, not pixels. They
    /// were being passed the pixel size, which claimed a monitor roughly 1.9 metres wide. Zero
    /// is out of the accepted 10-10000 range and is therefore ignored, which is what we want:
    /// we do not know the monitor's physical size and nothing here needs it.
    /// </para>
    /// <para>
    /// Every constraint here fails silently - the call returns S_OK and the session simply does
    /// not change - so the values sent are logged rather than assumed. The exception path below
    /// only catches a server that has no display-control channel at all.
    /// </para>
    /// </remarks>
    /// <param name="client">The session's control, from a caller that has checked for one.</param>
    /// <param name="size">The desktop size to ask for, in physical pixels.</param>
    /// <param name="dpi">The DPI the scale factors are derived from.</param>
    /// <param name="latchRefusal">
    /// Whether a refusal should be taken as a fact about the session rather than about this
    /// one call. It is one for a server with no display-control channel at all, which is what
    /// the latch is for. It is not one for a pass that runs before the session has a desktop:
    /// latching there would turn a call that was merely early into no scaling at all for the
    /// rest of the session's life.
    /// </param>
    private DisplayUpdate UpdateSessionDisplay(
        MsRdpClient11NotSafeForScripting client, Size size, int dpi, bool latchRefusal)
    {
        if (!RdpDisplayScale.TryNormaliseSessionSize(size, out Size accepted))
        {
            Runtime.MessageCollector.AddMessage(MessageClass.DebugMsg,
                $"Resize [{SessionLabel}]: {size.Width}x{size.Height} is outside the " +
                $"{RdpDisplayScale.MinSessionDimension}-{RdpDisplayScale.MaxSessionDimension} " +
                "the display-control channel accepts; falling back to a reconnect.", true);
            return DisplayUpdate.Refused;
        }

        uint desktopScale = RdpDisplayScale.DesktopScaleFactor(dpi);
        uint deviceScale = RdpDisplayScale.DeviceScaleFactor(dpi);

        try
        {
            SendSessionDisplaySettings(client, (uint)accepted.Width, (uint)accepted.Height,
                                       desktopScale, deviceScale);

            Runtime.MessageCollector.AddMessage(MessageClass.DebugMsg,
                $"Resize [{SessionLabel}]: asked the session for {accepted.Width}x{accepted.Height} " +
                $"at {dpi} DPI (desktopScaleFactor {desktopScale}, deviceScaleFactor {deviceScale}).",
                true);
            return DisplayUpdate.Applied;
        }
        catch (Exception ex) when (RdpDisplayScale.IsRefusalForNow(ex.HResult))
        {
            Runtime.MessageCollector.AddMessage(MessageClass.DebugMsg,
                $"Resize [{SessionLabel}]: the session refused UpdateSessionDisplaySettings for now " +
                $"({ex.Message}); its display channel is not open yet. Not held against the session, " +
                "and not retried by reconnecting.", true);
            return DisplayUpdate.RefusedForNow;
        }
        catch (Exception ex)
        {
            _sessionRefusedDisplayUpdates = latchRefusal;
            Runtime.MessageCollector.AddMessage(MessageClass.DebugMsg,
                $"Resize [{SessionLabel}]: the session refused UpdateSessionDisplaySettings ({ex.Message}). " +
                "That channel needs a server running RDP 8.1 or newer, so this is expected against " +
                "Server 2008 R2 or 2012; " +
                (latchRefusal
                    ? "using reconnect-based resize for the rest of this session."
                    : "not held against the session - this pass ran before it had a desktop."),
                true);
            return DisplayUpdate.Refused;
        }
    }

    /// <summary>The one call that sends a monitor layout to the session.</summary>
    /// <remarks>
    /// Virtual, with <see cref="ReportedDesktopSize"/> and <see cref="ReconnectAtSize"/>, so a
    /// test can stand in for a session: without a server, the control answers neither the way a
    /// live one does. The three zeros are the physical size, in millimetres, and the orientation;
    /// see <see cref="UpdateSessionDisplay"/>.
    /// </remarks>
    protected virtual void SendSessionDisplaySettings(
        MsRdpClient11NotSafeForScripting client, uint width, uint height, uint desktopScale, uint deviceScale)
    {
        client.UpdateSessionDisplaySettings(width, height, 0, 0, 0, desktopScale, deviceScale);
    }

    /// <summary>The size the control reports the session has taken.</summary>
    /// <remarks>
    /// Lags behind the requests sent: see <see cref="RdpDisplayScale.TryChooseRescaleSize"/>.
    /// </remarks>
    protected virtual Size ReportedDesktopSize(MsRdpClient11NotSafeForScripting client)
    {
        return new Size(client.DesktopWidth, client.DesktopHeight);
    }

    /// <summary>Resizes the session by reconnecting it, for a server that refused the layout.</summary>
    protected virtual void ReconnectAtSize(MsRdpClient11NotSafeForScripting client, uint width, uint height)
    {
        client.Reconnect(width, height);
    }

    /// <summary>What became of a call to <c>UpdateSessionDisplaySettings</c>.</summary>
    private enum DisplayUpdate
    {
        /// <summary>The call went through.</summary>
        Applied,

        /// <summary>
        /// Refused with <c>E_UNEXPECTED</c>, before the display channel was open; see
        /// <see cref="RdpDisplayScale.IsRefusalForNow"/>.
        /// </summary>
        RefusedForNow,

        /// <summary>Refused for good, or a size the protocol cannot express.</summary>
        Refused
    }

    /// <summary>Which point in a session's life a display-scale pass is running at.</summary>
    private enum DisplayScalePass
    {
        /// <summary>
        /// <c>OnConnected</c>: the session exists, and on a host nobody is logged on to it has
        /// no desktop in it yet.
        /// </summary>
        SessionCreated,

        /// <summary><c>OnLoginComplete</c>: a desktop is being, or has been, created.</summary>
        LoginComplete,

        /// <summary>The hosting panel has moved to a monitor of a different DPI.</summary>
        DpiChanged
    }

    /// <summary>
    /// Tells the session what scale it is being viewed at, and for a session that follows its
    /// window, what size.
    /// </summary>
    /// <remarks>
    /// Deliberately does not stop where <see cref="ReconnectForResize"/> does. With
    /// <c>AutomaticResize</c> off, a fixed resolution, or SmartSize on, the user has said the
    /// session's pixel count should not follow the window. None of that says anything about
    /// scale - a fixed 1920x1080 session dragged onto a 200% monitor still wants telling it is
    /// being viewed at 200%, or its text stays half the size it should be. Such a session is sent
    /// the size the control reports, so its resolution stays fixed and only the scale changes.
    /// <para>
    /// A session that does follow its window is sent the window's size, from every pass but
    /// <c>SessionCreated</c>. Measured in the lab (#246): an undock that also changed the
    /// network had every resize refused with <c>E_UNEXPECTED</c> while the control reconnected,
    /// and the <c>LoginComplete</c> pass then sent the size the control reported - the docked
    /// size, 3462x1915, into a Tab of 1627x1011, with the new scale at an unchanged resolution.
    /// The session showed scrollbars and kept its old scale until the next dock. Sending the
    /// window's size makes the <c>LoginComplete</c> pass the replay of whatever was refused or
    /// dropped before it, as one request carrying size and scale together, and needs no record
    /// of what was refused. <c>SessionCreated</c> keeps the reported size: it runs before
    /// logon, and its job is the scale.
    /// </para>
    /// <para>
    /// **This is not what makes a new session come up scaled.** That is
    /// <see cref="SetDisplayScale"/>, which declares the scale before the session exists at all;
    /// measured on the target machine, asking here instead does not work, and ADR-0028 records
    /// the round that established it. What these passes answer is everything a pre-connect value
    /// cannot reach:
    /// <list type="bullet">
    /// <item>the window being dragged to a monitor of a different DPI (<c>DpiChanged</c>);</item>
    /// <item>either automatic reconnect, neither of which re-runs <c>SetDisplayScale</c> -
    /// <c>TmrReconnectElapsed</c> calls <c>_rdpClient.Connect()</c> straight, and mstscax's own
    /// <c>EnableAutoReconnect</c> never enters managed code;</item>
    /// <item>a connection where <c>SetDisplayScale</c> logged that it could not declare the
    /// scale, because the control answered no <c>IMsRdpExtendedSettings</c>.</item>
    /// </list>
    /// </para>
    /// </remarks>
    private void TryApplyDisplayScale(int dpi, DisplayScalePass pass)
    {
        if (_rdpClient is not { } client || !_sessionConnected || _sessionRefusedDisplayUpdates)
        {
            return;
        }

        try
        {
            Size reported = ReportedDesktopSize(client);
            bool followsTheWindow = pass != DisplayScalePass.SessionCreated
                                    && WhyTheSessionKeepsItsResolution(client) is null;
            Size current = followsTheWindow ? SizeOfTheWindow(client) : reported;
            Size size = current;
            (uint, uint) scale = (RdpDisplayScale.DesktopScaleFactor(dpi), RdpDisplayScale.DeviceScaleFactor(dpi));

            // A DPI change arrives just after the resize that comes with the move, and the size
            // the control reports lags behind that resize - TryChooseRescaleSize says by how much.
            // It also returns false when the session has already been asked for this size and
            // scale, which stops a run of DPI changes talking to the session on every one of them.
            // When nothing has been asked for - a refusal for now forgets the request - it falls
            // back to `current`, which for a following session is the window, not the lagging
            // report. SessionCreated and LoginComplete happen once each per logon, and the second
            // exists precisely to make good what the host was not yet in a state to act on, so
            // neither of them consults the last request.
            if (pass == DisplayScalePass.DpiChanged &&
                !RdpDisplayScale.TryChooseRescaleSize(
                    _lastRequestedSessionSize, _lastRequestedSessionScale, current, dpi, out size))
            {
                Runtime.MessageCollector.AddMessage(MessageClass.DebugMsg,
                    $"Display scale [{SessionLabel}]: {pass} pass at {dpi} DPI skipped, the session " +
                    $"was already asked for {size.Width}x{size.Height} at this scale (the control " +
                    $"reports {reported.Width}x{reported.Height}).", true);
                return;
            }

            Runtime.MessageCollector.AddMessage(MessageClass.DebugMsg,
                $"Display scale [{SessionLabel}]: {pass} pass at {dpi} DPI, the session is " +
                $"{size.Width}x{size.Height} (the control reports {reported.Width}x{reported.Height}; " +
                (followsTheWindow ? $"the window is {current.Width}x{current.Height})."
                 : pass == DisplayScalePass.SessionCreated ? "the size it was created at, before logon)."
                 : "its resolution does not follow the window)."),
                true);

            if (UpdateSessionDisplay(
                    client, size, dpi, latchRefusal: pass != DisplayScalePass.SessionCreated)
                != DisplayUpdate.Applied)
            {
                return;
            }

            // Recorded only once a call has gone through, so that a pass refused before the session
            // had a desktop leaves the later ones something to do.
            _lastRequestedSessionSize = size;
            _lastRequestedSessionScale = scale;
        }
        catch (Exception ex)
        {
            Runtime.MessageCollector.AddMessage(MessageClass.DebugMsg,
                $"Resize [{SessionLabel}]: could not apply a display scale of {dpi} DPI ({ex.Message}).",
                true);
        }
    }

    /// <summary>
    /// The hosting panel has moved to a monitor with a different DPI.
    /// </summary>
    public override void NotifyDpiChanged(int dpi)
    {
        base.NotifyDpiChanged(dpi);

        // Remembered for TmrReconnectElapsed, which cannot read it for itself. This is the
        // only reason a reconnect after the window has changed monitors comes up at the scale
        // it is actually being viewed at rather than the one the tab was opened on.
        _lastKnownDpi = dpi;

        TryApplyDisplayScale(dpi, DisplayScalePass.DpiChanged);
    }

    /// <summary>
    /// Declares the display scale before the session is created.
    /// </summary>
    /// <remarks>
    /// These are the two values mstsc reads from <c>desktopscalefactor:i:</c> and
    /// <c>devicescalefactor:i:</c> in a .rdp file. Setting them here is what makes a session come
    /// up at the right scale instead of being rescaled once it exists, which is the difference
    /// that matters on a host nobody is logged on to - see ADR-0028, and
    /// <see cref="TryApplyDisplayScale"/> for the post-connect passes that stay.
    /// <para>
    /// It reaches into the undocumented <c>IMsRdpExtendedSettings.Property</c> bag, which the
    /// restraint recorded above <see cref="SetRedirection"/> says to stay out of. ADR-0028 is that
    /// restraint being spent deliberately, on two names and nothing else.
    /// </para>
    /// <para>
    /// **The value must be VT_UI4.** Measured by <c>--selftest</c> on mstscax 10.0.26100: both
    /// names are accepted and read back, and a boxed <c>int</c> throws E_FAIL (0x80004005). The
    /// cast to <c>uint</c> is therefore load-bearing, not incidental - <c>CheckRdpControl</c>
    /// re-measures it on every run so a build of mstscax that changes its mind says so.
    /// </para>
    /// <para>
    /// Guarded, unlike its neighbours in <c>Initialize</c>: an unknown name in that bag throws,
    /// and an mstscax that does not carry one of these must cost the connection nothing. Reported
    /// at Information rather than Debug because it is one line per session - ADR-0017's bound -
    /// and because it is the only record of a value nothing here can observe the effect of.
    /// </para>
    /// </remarks>
    private void SetDisplayScale(MsRdpClient11NotSafeForScripting client, int? cachedDpi = null)
    {
        int dpi = _lastKnownDpi;
        uint desktopScale = RdpDisplayScale.DesktopScaleFactor(dpi);
        uint deviceScale = RdpDisplayScale.DeviceScaleFactor(dpi);

        // The cached DeviceDpi alongside, for as long as it is worth comparing: it is what this
        // used to declare, and it was measured stale for a session opened after a monitor change.
        string asked = $"desktopScaleFactor {desktopScale}, deviceScaleFactor {deviceScale} " +
                    $"at {dpi} DPI" + (cachedDpi is { } cached ? $" (the control's cached DeviceDpi is {cached})" : "");

        if (!(client is IMsRdpExtendedSettings extended))
        {
            Runtime.MessageCollector.AddMessage(MessageClass.InformationMsg,
                $"RDP display scale [{SessionLabel}]: this control answers no " +
                $"IMsRdpExtendedSettings, so {asked} could not be declared before connecting; the " +
                "session will be rescaled once it exists instead.", true);
            return;
        }

        string failures = string.Empty;

        foreach ((string name, uint value) in new[] { ("DesktopScaleFactor", desktopScale),
                                             ("DeviceScaleFactor", deviceScale) })
        {
            // Boxed as uint, deliberately. See the remarks above.
            object boxed = value;
            try
            {
                extended.set_Property(name, ref boxed);
            }
            catch (Exception ex)
            {
                failures += (failures.Length == 0 ? string.Empty : "; ") +
                            $"{name}: {ex.Message.Trim()}";
            }
        }

        Runtime.MessageCollector.AddMessage(MessageClass.InformationMsg,
            failures.Length == 0
                ? $"RDP display scale [{SessionLabel}]: declared {asked} before connecting."
                : $"RDP display scale [{SessionLabel}]: declaring {asked} before connecting failed " +
                  $"({failures}); the session will be rescaled once it exists instead.", true);
    }

    private void SetRdGateway(MsRdpClient11NotSafeForScripting client, ConnectionInfo connectionInfo)
    {
        try
        {
            if (client.TransportSettings.GatewayIsSupported == 0)
            {
                Runtime.MessageCollector.AddMessage(MessageClass.DebugMsg, Language.strRdpGatewayNotSupported, true);
                return;
            }
            Runtime.MessageCollector.AddMessage(MessageClass.DebugMsg, Language.strRdpGatewayIsSupported, true);

            if (connectionInfo.RDGatewayUsageMethod != RDGatewayUsageMethod.Never)
            {
                client.TransportSettings.GatewayUsageMethod = (uint)connectionInfo.RDGatewayUsageMethod;
                client.TransportSettings.GatewayHostname = connectionInfo.RDGatewayHostname;
                client.TransportSettings.GatewayProfileUsageMethod = 1; // TSC_PROXY_PROFILE_MODE_EXPLICIT
                if (connectionInfo.RDGatewayUseConnectionCredentials == RDGatewayUseConnectionCredentials.SmartCard)
                {
                    client.TransportSettings.GatewayCredsSource = 1; // TSC_PROXY_CREDS_MODE_SMARTCARD
                }
                if ((Force & ConnectionInfo.Force.NoCredentials) != ConnectionInfo.Force.NoCredentials)
                {
                    if (connectionInfo.RDGatewayUseConnectionCredentials == RDGatewayUseConnectionCredentials.Yes)
                    {
                        string userName = GetUserName(connectionInfo.Username ?? "");
                        string domain = GetDomain(connectionInfo.Domain ?? "");

                        client.TransportSettings2.GatewayUsername = userName;
                        client.TransportSettings2.GatewayDomain = domain;
                        client.TransportSettings2.GatewayPassword = GetPassword(
                            (connectionInfo.Password ?? ""),
                            userName,
                            domain,
                            connectionInfo.Hostname);
                    }
                    else if (connectionInfo.RDGatewayUseConnectionCredentials
                             == RDGatewayUseConnectionCredentials.SmartCard)
                    {
                        client.TransportSettings2.GatewayCredSharing = 0;
                    }
                    else
                    {
                        string userName = GetUserName(connectionInfo.RDGatewayUsername);
                        string domain = GetDomain(connectionInfo.RDGatewayDomain);

                        client.TransportSettings2.GatewayUsername = userName;
                        client.TransportSettings2.GatewayDomain = domain;
                        client.TransportSettings2.GatewayPassword = GetPassword(
                            connectionInfo.RDGatewayPassword,
                            userName,
                            domain,
                            connectionInfo.Hostname);
                        client.TransportSettings2.GatewayCredSharing = 0;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Runtime.MessageCollector.AddExceptionMessage(Language.strRdpSetGatewayFailed, ex);
        }
    }

    [SuppressMessage("Performance", "CA1863",
                     Justification = "The format is a localized resource read at run time; "
                                     + "a cached CompositeFormat would freeze the UI language.")]
    private void SetUseConsoleSession(MsRdpClient11NotSafeForScripting client, ConnectionInfo connectionInfo)
    {
        try
        {
            bool value;

            if ((Force & ConnectionInfo.Force.UseConsoleSession) == ConnectionInfo.Force.UseConsoleSession)
            {
                value = true;
            }
            else if ((Force & ConnectionInfo.Force.DontUseConsoleSession) == ConnectionInfo.Force.DontUseConsoleSession)
            {
                value = false;
            }
            else
            {
                value = connectionInfo.UseConsoleSession;
            }

            Runtime.MessageCollector.AddMessage(
                MessageClass.DebugMsg,
                string.Format(CultureInfo.CurrentCulture, Language.strRdpSetConsoleSwitch, _rdpVersion),
                true);
            client.AdvancedSettings7.ConnectToAdministerServer = value;
        }
        catch (Exception ex)
        {
            Runtime.MessageCollector.AddExceptionMessage(Language.strRdpSetConsoleSessionFailed, ex);
        }
    }

    internal static string GetUserName(string userName)
    {
        if (string.IsNullOrEmpty(userName))
        {
            if (Settings.Default.EmptyCredentials == "windows")
            {
                userName = Environment.UserName;
            }
            else if (Settings.Default.EmptyCredentials == "custom")
            {
                userName = Settings.Default.DefaultUsername;
            }
        }
        return userName;
    }
    internal static string GetDomain(string domain)
    {
        if (string.IsNullOrEmpty(domain))
        {
            if (Settings.Default.EmptyCredentials == "windows")
            {
                domain = Environment.UserDomainName;
            }
            else if (Settings.Default.EmptyCredentials == "custom")
            {
                domain = Settings.Default.DefaultDomain;
            }
        }
        return domain;
    }
    internal static string GetPassword(string password, string userName, string domain, string host)
    {
        if (string.IsNullOrEmpty(password))
        {
            if (Settings.Default.EmptyCredentials == "custom")
            {
                if (Settings.Default.DefaultPassword != "")
                {
                    password = DefaultCredentials.Password;
                }
            }
        }
        return password;
    }

    private void SetCredentials(MsRdpClient11NotSafeForScripting client, ConnectionInfo connectionInfo)
    {
        try
        {
            if ((Force & ConnectionInfo.Force.NoCredentials) == ConnectionInfo.Force.NoCredentials)
            {
                return;
            }

            string userName = GetUserName(connectionInfo.Username ?? "");
            string domain = GetDomain(connectionInfo.Domain ?? "");

            client.Domain = domain;
            client.UserName = userName;
            client.AdvancedSettings2.ClearTextPassword = GetPassword(
                (connectionInfo.Password ?? ""),
                userName,
                domain,
                connectionInfo.Hostname);

        }
        catch (Exception ex)
        {
            Runtime.MessageCollector.AddExceptionMessage(Language.strRdpSetCredentialsFailed, ex);
        }
    }

    /// <summary>
    /// The monitor a full-screen session should fill.
    /// </summary>
    /// <remarks>
    /// This used to ask which monitor the *main window* was on, which is only the right answer
    /// when there is one monitor. It is the connection panel that goes full screen, and it can
    /// be on another screen entirely. Per-monitor DPI makes the difference worse than a wrong
    /// pixel count: the two monitors can be at different scales, so the session would be told
    /// both the wrong size and the wrong scale.
    /// </remarks>
    private Screen FullscreenScreen =>
        Screen.FromControl(Control ?? (Control?)InterfaceControl ?? _frmMain);

    private void SetResolution(MsRdpClient11NotSafeForScripting client, InterfaceControl interfaceControl)
    {
        try
        {
            if ((Force & ConnectionInfo.Force.Fullscreen) == ConnectionInfo.Force.Fullscreen)
            {
                client.FullScreen = true;
                client.DesktopWidth = FullscreenScreen.Bounds.Width;
                client.DesktopHeight = FullscreenScreen.Bounds.Height;

                return;
            }

            if ((interfaceControl.Info.Resolution == RDPResolutions.FitToWindow)
                || (interfaceControl.Info.Resolution == RDPResolutions.SmartSize))
            {
                client.DesktopWidth = interfaceControl.Size.Width;
                client.DesktopHeight = interfaceControl.Size.Height;

                if (interfaceControl.Info.Resolution == RDPResolutions.SmartSize)
                {
                    client.AdvancedSettings2.SmartSizing = true;
                }


            }
            else if (interfaceControl.Info.Resolution == RDPResolutions.Fullscreen)
            {
                client.FullScreen = true;
                client.DesktopWidth = FullscreenScreen.Bounds.Width;
                client.DesktopHeight = FullscreenScreen.Bounds.Height;
            }
            else
            {
                Rectangle resolution = GetResolutionRectangle(interfaceControl.Info.Resolution);
                client.DesktopWidth = resolution.Width;
                client.DesktopHeight = resolution.Height;
            }
        }
        catch (Exception ex)
        {
            Runtime.MessageCollector.AddExceptionMessage(Language.strRdpSetResolutionFailed, ex);
        }
    }

    private static void SetPort(MsRdpClient11NotSafeForScripting client, ConnectionInfo connectionInfo)
    {
        try
        {
            if (connectionInfo.Port != (int)Defaults.Port)
            {
                client.AdvancedSettings2.RDPPort = connectionInfo.Port;
            }
        }
        catch (Exception ex)
        {
            Runtime.MessageCollector.AddExceptionMessage(Language.strRdpSetPortFailed, ex);
        }
    }

    /// <summary>
    /// Asks for each redirection the connection wants, independently of the others.
    /// </summary>
    /// <remarks>
    /// These are six separate requests and a server may refuse any one of them on its own.
    /// They used to share a single try, so the first refusal silently dropped the remaining
    /// five - a host that would not redirect printers also cost you drives, ports, smart cards,
    /// clipboard and audio.
    /// <para>
    /// Note what is deliberately <em>not</em> set here. Raising the control floor to v11 does
    /// not raise the wire floor: mstscax negotiates the protocol version per connection, so a
    /// v12 control talks to Server 2008 R2 quite happily. The compatibility risk comes only
    /// from properties that <em>pin</em> a capability instead of letting it be negotiated, and
    /// we set none of them - <c>IMsRdpClientAdvancedSettings8.ClientProtocolSpec</c>,
    /// <c>NetworkConnectionType</c>, <c>BandwidthDetection</c>, <c>NegotiateSecurityLayer</c>,
    /// or anything in the undocumented <c>IMsRdpExtendedSettings.Property</c> bag
    /// (<c>EnableHardwareMode</c>, <c>DisableUDPTransport</c>, ...), whose unknown names throw.
    /// Reaching them all became possible with the v11 retype, which is exactly why the
    /// restraint is written down here rather than left implicit.
    /// </para>
    /// </remarks>
    private static void SetRedirection(MsRdpClient11NotSafeForScripting client, ConnectionInfo connectionInfo)
    {
        TrySetRedirection("drives", () =>
            client.AdvancedSettings2.RedirectDrives = connectionInfo.RedirectDiskDrives);
        TrySetRedirection("ports", () =>
            client.AdvancedSettings2.RedirectPorts = connectionInfo.RedirectPorts);
        TrySetRedirection("printers", () =>
            client.AdvancedSettings2.RedirectPrinters = connectionInfo.RedirectPrinters);
        TrySetRedirection("smart cards", () =>
            client.AdvancedSettings2.RedirectSmartCards = connectionInfo.RedirectSmartCards);
        TrySetRedirection("clipboard", () =>
            client.AdvancedSettings6.RedirectClipboard = connectionInfo.RedirectClipboard);
        TrySetRedirection("sound", () =>
            client.SecuredSettings2.AudioRedirectionMode = (int)connectionInfo.RedirectSound);
    }

    private static void TrySetRedirection(string what, Action set)
    {
        try
        {
            set();
        }
        catch (Exception ex)
        {
            Runtime.MessageCollector.AddExceptionMessage($"{Language.strRdpSetRedirectionFailed} ({what})", ex);
        }
    }

    private static void SetPerformanceFlags(MsRdpClient11NotSafeForScripting client, ConnectionInfo connectionInfo)
    {
        try
        {
            int pFlags = 0;
            if (connectionInfo.DisplayThemes == false)
            {
                pFlags += Convert.ToInt32(RDPPerformanceFlags.DisableThemes, CultureInfo.InvariantCulture);
            }

            if (connectionInfo.DisplayWallpaper == false)
            {
                pFlags += Convert.ToInt32(RDPPerformanceFlags.DisableWallpaper, CultureInfo.InvariantCulture);
            }

            if (connectionInfo.EnableFontSmoothing)
            {
                pFlags += Convert.ToInt32(RDPPerformanceFlags.EnableFontSmoothing, CultureInfo.InvariantCulture);
            }

            if (connectionInfo.EnableDesktopComposition)
            {
                pFlags += Convert.ToInt32(RDPPerformanceFlags.EnableDesktopComposition, CultureInfo.InvariantCulture);
            }

            client.AdvancedSettings2.PerformanceFlags = pFlags;
        }
        catch (Exception ex)
        {
            Runtime.MessageCollector.AddExceptionMessage(Language.strRdpSetPerformanceFlagsFailed, ex);
        }
    }

    private static void SetAuthenticationLevel(MsRdpClient11NotSafeForScripting client, ConnectionInfo connectionInfo)
    {
        try
        {
            client.AdvancedSettings5.AuthenticationLevel = (uint)connectionInfo.RDPAuthenticationLevel;
        }
        catch (Exception ex)
        {
            Runtime.MessageCollector.AddExceptionMessage(Language.strRdpSetAuthenticationLevelFailed, ex);
        }
    }

    private void SetLoadBalanceInfo(MsRdpClient11NotSafeForScripting client, ConnectionInfo connectionInfo)
    {
        if (string.IsNullOrEmpty(connectionInfo.LoadBalanceInfo))
        {
            return;
        }
        try
        {
            client.AdvancedSettings2.LoadBalanceInfo = LoadBalanceInfoUseUtf8
                ? AzureLoadBalanceInfoEncoder.Encode(connectionInfo.LoadBalanceInfo)
                : connectionInfo.LoadBalanceInfo;
        }
        catch (Exception ex)
        {
            Runtime.MessageCollector.AddExceptionMessage("Unable to set load balance info.", ex);
        }
    }

    /// <summary>
    /// Unhooks the OCX event sink before the control is disposed.
    /// </summary>
    /// <remarks>
    /// <see cref="SetEventHandlers"/> subscribes on the raw OCX (obtained via GetOcx()), not on
    /// the AxHost wrapper, so the COM connection point holds our managed sink until we
    /// explicitly unadvise. Tearing the control down with the sink still attached means COM has
    /// to unwind it during release.
    /// <para>
    /// Deliberately no Marshal.ReleaseComObject here: GetOcx() hands back the same RCW that
    /// AxHost itself holds, so releasing it would make the following Control.Dispose() throw
    /// InvalidComObjectException and leak the native object. Dropping the managed references is
    /// enough - AxHost owns the release.
    /// </para>
    /// </remarks>
    protected override void CleanupProtocolResources()
    {
        // Whatever state the session reached, the tab is going away and nothing should be left
        // parented into an InterfaceControl that is about to be disposed.
        HideTheConnectingOverlay("session closed");

        if (_rdpClient is not { } client)
        {
            _leaveFullscreenEvent = null;
            return;
        }

        // Before unadvising: if the session is still negotiating, re-advise minimal sinks that do
        // nothing but record that it has settled. Otherwise the unadvise below would leave a
        // control parked mid-logon with no way of ever learning that it had finished.
        WatchForTheSessionToSettle(client);

        // Unadvise the rest, so nothing below can call back into us.
        try
        {
            client.OnConnecting -= RDPEvent_OnConnecting;
            client.OnConnected -= RDPEvent_OnConnected;
            client.OnLoginComplete -= RDPEvent_OnLoginComplete;
            client.OnFatalError -= RDPEvent_OnFatalError;
            client.OnDisconnected -= RDPEvent_OnDisconnected;
            client.OnLeaveFullScreenMode -= RDPEvent_OnLeaveFullscreenMode;
            client.OnIdleTimeoutNotification -= RDPEvent_OnIdleTimeoutNotification;
            client.OnAutoReconnecting2 -= RDPEvent_OnAutoReconnecting2;
            client.OnAutoReconnected -= RDPEvent_OnAutoReconnected;
        }
        catch (Exception ex)
        {
            Runtime.MessageCollector.AddMessage(MessageClass.DebugMsg,
                $"Couldn't unsubscribe RDP event handlers: {ex.Message}");
        }

        var stopwatch = Stopwatch.StartNew();

        short connectedState = QueryConnectedState();

        // Stop the auto-reconnect machinery before the control is released. Connect() turns
        // EnableAutoReconnect on with MaxReconnectAttempts = 5, and releasing a session that is
        // mid-reconnect makes mstscax unwind every remaining attempt first - which is how
        // closing one tab blocked the UI for 42 seconds while twelve others took ~120 ms.
        try
        {
            client.AdvancedSettings3.EnableAutoReconnect = false;
            client.AdvancedSettings3.MaxReconnectAttempts = 0;
        }
        catch (Exception ex)
        {
            Runtime.MessageCollector.AddMessage(MessageClass.DebugMsg,
                $"Couldn't disable RDP auto-reconnect before close: {ex.Message}");
        }

        // Bound the graceful-shutdown wait as well. The tab is going away either way.
        try { client.AdvancedSettings2.shutdownTimeout = ShutdownTimeoutSecondsOnClose; }
        catch (Exception) { /* not fatal */ }

        WaitUntilTheControlIsReadyToBeDestroyed(client);

        LogClose($"RDP client released (Connected was {connectedState}, now {QueryConnectedState()}) " +
                 $"at {stopwatch.ElapsedMilliseconds} ms");

        _leaveFullscreenEvent = null;

        // Keep the client only when the control is about to be parked: the probe below has to
        // have something left to ask about the session. Otherwise the control is disposed in the
        // next stage, so this is already the moment immediately before its release and the
        // references can go now. On the parked path HostedControlPreDisposeHook does it instead.
        if (_controlIsReadyToBeDestroyed)
        {
            ReleaseTheSessionsComReferences();
        }
    }

    /// <summary>
    /// Drives a parked control the rest of the way, one step per idle turn: wait for the
    /// session to settle, then ask it to disconnect, then wait for it to be down.
    /// </summary>
    /// <remarks>
    /// Every step runs on an idle turn rather than inline, so none of them can block the close
    /// itself. The disconnect in particular is only ever asked for once the session has settled,
    /// which is the whole point - see <see cref="SessionHasSettled"/>.
    /// </remarks>
    protected override Func<bool> HostedControlSafeToDisposeProbe()
    {
        bool disconnectAsked = false;

        return () =>
        {
            if (QueryConnectedState() == 0)
            {
                StopWatchingForTheSessionToSettle();
                return true;
            }

            if (!SessionHasSettled)
            {
                return false;
            }

            if (disconnectAsked)
            {
                return false;
            }

            disconnectAsked = true;
            LogClose("parked session has settled; asking it to disconnect");
            DisconnectTheSession();
            return QueryConnectedState() == 0;
        };
    }

    /// <summary>
    /// Whether the session has reached a state in which it is safe to be told to disconnect.
    /// </summary>
    /// <remarks>
    /// A session between <c>OnConnected</c> and <c>OnLoginComplete</c> is still negotiating -
    /// licensing, authentication, the initial desktop - and one at <c>Connected == 2</c> has not
    /// even got that far. Telling either to disconnect makes mstscax unwind a handshake that is
    /// still in flight, and it does that on the calling thread, which is ours. That is why
    /// closing a tab moments after logon hangs where closing the same tab a minute later does
    /// not.
    /// <para>
    /// Settled means one of: login completed, the session already went down, or it failed. The
    /// first is only knowable from <c>OnLoginComplete</c>, which is why the teardown keeps that
    /// sink advised - see <see cref="WatchForTheSessionToSettle"/> - rather than unadvising
    /// everything up front as it used to.
    /// </para>
    /// </remarks>
    protected virtual bool SessionHasSettled => _sessionHasSettled || LoginHasCompleted || QueryConnectedState() == 0;

    /// <summary>Virtual so a test can close a tab mid-logon without a server to log on to.</summary>
    protected virtual bool LoginHasCompleted => _loginComplete;

    /// <summary>Virtual so a test can see whether the teardown asked, without a session to ask.</summary>
    protected virtual void DisconnectTheSession()
    {
        try { _rdpClient?.Disconnect(); }
        catch (Exception ex)
        {
            Runtime.MessageCollector.AddMessage(MessageClass.DebugMsg,
                $"Couldn't disconnect the RDP client before close: {ex.Message}");
        }
    }

    private bool _sessionHasSettled;
    private MSTSCLib.IMsTscAxEvents_OnLoginCompleteEventHandler? _settledOnLoginComplete;
    private MSTSCLib.IMsTscAxEvents_OnDisconnectedEventHandler? _settledOnDisconnected;
    private MSTSCLib.IMsTscAxEvents_OnFatalErrorEventHandler? _settledOnFatalError;

    /// <summary>
    /// Keeps the three sinks that say how a session ended advised past the teardown's unadvise,
    /// so a control parked mid-logon can still learn that it has settled.
    /// </summary>
    private void WatchForTheSessionToSettle(MsRdpClient11NotSafeForScripting client)
    {
        if (SessionHasSettled)
        {
            return;
        }

        _settledOnLoginComplete = () => _sessionHasSettled = true;
        _settledOnDisconnected = _ => _sessionHasSettled = true;
        _settledOnFatalError = _ => _sessionHasSettled = true;

        try
        {
            client.OnLoginComplete += _settledOnLoginComplete;
            client.OnDisconnected += _settledOnDisconnected;
            client.OnFatalError += _settledOnFatalError;
        }
        catch (Exception ex)
        {
            // Without these we cannot tell when it settles, so treat it as settled rather than
            // park the control forever.
            _sessionHasSettled = true;
            Runtime.MessageCollector.AddMessage(MessageClass.DebugMsg,
                $"Couldn't watch for the RDP session to settle: {ex.Message}");
        }
    }

    private void StopWatchingForTheSessionToSettle()
    {
        if (_settledOnLoginComplete == null)
        {
            return;
        }

        // No client means the references are already released, and the release unadvised these
        // first. Each unadvise below used to throw into its own catch in that case.
        if (_rdpClient is { } client)
        {
            try { client.OnLoginComplete -= _settledOnLoginComplete; } catch (Exception) { /* already gone */ }
            try { client.OnDisconnected -= _settledOnDisconnected; } catch (Exception) { /* already gone */ }
            try { client.OnFatalError -= _settledOnFatalError; } catch (Exception) { /* already gone */ }
        }

        _settledOnLoginComplete = null;
        _settledOnDisconnected = null;
        _settledOnFatalError = null;
    }

    /// <summary>
    /// Gives back every COM reference this protocol holds on its session.
    /// </summary>
    /// <remarks>
    /// <c>_rdpClient</c> itself is deliberately not released - see the remark on
    /// <see cref="CleanupProtocolResources"/>: <c>GetOcx()</c> hands back the RCW the AxHost
    /// already owns, and releasing that one makes the AxHost's own dispose throw.
    /// <para>
    /// The settings objects are a different matter, and measurement is what separates them.
    /// <c>AdvancedSettings2</c> through <c>AdvancedSettings8</c> all return one COM object;
    /// <c>SecuredSettings</c>/<c>SecuredSettings2</c> a second; <c>TransportSettings</c>/
    /// <c>TransportSettings2</c> a third. Reading their IUnknown pointers shows four distinct
    /// objects in total - those three plus the control - so releasing the three is not releasing
    /// the control's RCW under the AxHost. (<c>IMsRdpExtendedSettings</c> is *not* one of them: it
    /// is reached by casting the client, so it is the control's own RCW and must be left alone.)
    /// </para>
    /// <para>
    /// Why it matters: a contained settings object holds a reference on its container, so while
    /// those RCWs are alive the native control - and every buffer mstscax allocated for the
    /// session, which is the bulk of what a session costs - survives <c>Control.Dispose()</c> and
    /// waits for a finalizer. Nothing tells the GC any of that native memory exists, so on an idle
    /// application the wait can be arbitrarily long. <c>FinalReleaseComObject</c> hands back only
    /// the references the RCW itself took, so this is balanced rather than an over-release: it was
    /// measured here, and afterwards the control still reads, writes and disposes normally.
    /// </para>
    /// </remarks>
    private void ReleaseTheSessionsComReferences()
    {
        if (_rdpClient is not { } client)
        {
            return;
        }

        // Must come first: these sinks are advised on the client, and unadvising through a
        // released RCW would throw.
        StopWatchingForTheSessionToSettle();

        // Fetching each one hands back the RCW that already exists for it, so nothing here adds a
        // reference that is not given back on the next line. A control that answers none of them -
        // there is no such control at the v11 floor, but a teardown is not the place to find out -
        // is logged and skipped.
        foreach ((string name, Func<object> fetch) in new (string, Func<object>)[]
                 {
                     ("AdvancedSettings", () => client.AdvancedSettings8),
                     ("SecuredSettings", () => client.SecuredSettings2),
                     ("TransportSettings", () => client.TransportSettings2)
                 })
        {
            try
            {
                Marshal.FinalReleaseComObject(fetch());
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddMessage(MessageClass.DebugMsg,
                    $"Couldn't release the RDP {name} COM reference: {ex.Message}");
            }
        }

        _rdpClient = null;
    }

    /// <summary>
    /// False once the control has told us it is not ready to be destroyed, or its session has
    /// not gone down. <see cref="ProtocolBase"/> then abandons the control rather than dispose
    /// it.
    /// </summary>
    private bool _controlIsReadyToBeDestroyed = true;

    /// <summary>
    /// How long to wait for the session to actually go down before giving up on it. Its own
    /// budget, separate from the handshake's, so a control that answers the handshake promptly
    /// and then takes its time disconnecting still gets the full allowance. Shortened by tests.
    /// </summary>
    internal static TimeSpan SessionDownTimeout = TimeSpan.FromSeconds(ShutdownTimeoutSecondsOnClose);

    /// <summary>
    /// The session's state: 0 disconnected, 1 connected, 2 connecting.
    /// </summary>
    /// <remarks>
    /// Virtual because the whole question this answers - "is the session still up?" - needs a
    /// server to be up for, and the one test machine that matters has no route to one. A test
    /// overrides this to hold a session up that nothing else can.
    /// </remarks>
    protected virtual short QueryConnectedState()
    {
        // A control we cannot ask is a control that is not going to block us: report it down.
        try { return _rdpClient?.Connected ?? 0; }
        catch (Exception) { return 0; }
    }

    protected override bool HostedControlCanBeDisposed =>
        base.HostedControlCanBeDisposed && _controlIsReadyToBeDestroyed;

    /// <summary>
    /// On the parked path, the release the teardown could not do.
    /// </summary>
    /// <remarks>
    /// Stage 2 leaves the client and its settings objects alone when the control is to be parked,
    /// because the probe still has to ask the session whether it is down. So the work moves here,
    /// to the one moment that is both after the last probe and before the release.
    /// </remarks>
    protected override Action HostedControlPreDisposeHook() => ReleaseTheSessionsComReferences;

    /// <summary>
    /// Settles whether the hosted control can safely be destroyed, in two steps.
    /// </summary>
    /// <remarks>
    /// Both have to be satisfied. <c>RequestClose()</c> is how a container asks an RDP control
    /// whether it may be torn down, and destroying one that is still asking to be waited for is
    /// outside what mstscax supports. But permission is not the same as a finished session, and
    /// the second is what actually blocks the release - see
    /// <see cref="WaitForTheSessionToGoDown"/>.
    /// </remarks>
    private void WaitUntilTheControlIsReadyToBeDestroyed(MsRdpClient11NotSafeForScripting client)
    {
        var stopwatch = Stopwatch.StartNew();

        // Nothing may be asked of a session that is still negotiating - not RequestClose(), not
        // Disconnect(). Both are outgoing COM calls on the UI thread with no bound on how long
        // mstscax takes to answer while a handshake is in flight. Park the control instead and
        // let the idle-turn probe do all of it once the session has settled.
        if (!SessionHasSettled)
        {
            LogClose($"session has not settled yet (Connected={QueryConnectedState()}, login not "
                     + "complete) - parking the control rather than interrupting the handshake");
            _controlIsReadyToBeDestroyed = false;
            return;
        }

        bool handshakeSatisfied = RunTheCloseHandshake(client, stopwatch);
        bool sessionIsDown = WaitForTheSessionToGoDown(stopwatch);

        _controlIsReadyToBeDestroyed = handshakeSatisfied && sessionIsDown;
    }

    /// <summary>
    /// Asks the control's permission to destroy it, and waits if it says to.
    /// </summary>
    /// <returns>False only if it asked us to wait and then never came back.</returns>
    private bool RunTheCloseHandshake(MsRdpClient11NotSafeForScripting client, Stopwatch stopwatch)
    {
        ControlCloseStatus status;
        try
        {
            status = client.RequestClose();
        }
        catch (Exception ex)
        {
            LogClose($"RequestClose() threw {ex.GetType().Name}: {ex.Message}");
            return true;
        }

        if (status != ControlCloseStatus.controlCloseWaitForEvents)
        {
            LogClose($"RequestClose() returned {status} at {stopwatch.ElapsedMilliseconds} ms");
            return true;
        }

        bool ready = false;
        MSTSCLib.IMsTscAxEvents_OnDisconnectedEventHandler onDisconnected = _ => ready = true;
        MSTSCLib.IMsTscAxEvents_OnConfirmCloseEventHandler onConfirmClose = () => ready = true;

        try
        {
            client.OnDisconnected += onDisconnected;
            client.OnConfirmClose += onConfirmClose;

            PumpUntil(() => ready, TimeSpan.FromSeconds(ShutdownTimeoutSecondsOnClose), stopwatch);
        }
        catch (Exception ex)
        {
            LogClose($"Waiting for the control to be ready threw {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            try { client.OnDisconnected -= onDisconnected; } catch (Exception) { /* already gone */ }
            try { client.OnConfirmClose -= onConfirmClose; } catch (Exception) { /* already gone */ }
        }

        LogClose(ready
            ? $"RequestClose() asked us to wait; the control was ready after {stopwatch.ElapsedMilliseconds} ms"
            : $"RequestClose() asked us to wait; it never said it was ready after {stopwatch.ElapsedMilliseconds} ms");

        return ready;
    }

    /// <summary>
    /// Disconnects the session and waits for it to actually be gone.
    /// </summary>
    /// <remarks>
    /// The handshake above is necessary but not sufficient, and the difference is what freezes
    /// the application. <c>RequestClose()</c> answers <c>controlCloseWaitForEvents</c> and the
    /// control then raises <c>OnConfirmClose</c> - "yes, go ahead and close me" - which is not a
    /// disconnect. Treating it as permission to destroy the control meant disposing one whose
    /// session teardown had barely started, and mstscax blocks inside the release until the
    /// network side finishes: 47.5 seconds on the UI thread, in the log that found this.
    /// <para>
    /// The same log makes the rule plain. Ten sessions that were already disconnected when
    /// Close ran disposed in 44-73 ms each, without exception. Five closed while still live
    /// disposed in 72-124 ms four times and blocked on the fifth. So wait for the session to be
    /// down, and if it will not go down inside the budget, say so and let
    /// <see cref="ProtocolBase"/> abandon the control instead.
    /// </para>
    /// <para>
    /// The wait pumps, so the application stays responsive throughout it - which is the whole
    /// trade. A bounded, responsive wait is worth taking to avoid an unbounded block that takes
    /// the desktop with it.
    /// </para>
    /// </remarks>
    private bool WaitForTheSessionToGoDown(Stopwatch stopwatch)
    {
        // Connected: 0 = disconnected, 1 = connected, 2 = connecting.
        if (QueryConnectedState() == 0)
        {
            return true;
        }

        // Disconnect explicitly so the teardown happens here, where it is measured and where we
        // can wait it out, rather than inside Dispose where it is neither. Only ever reached
        // once the session has settled: this call is unbounded, and interrupting a handshake
        // still in flight is what makes it take its time.
        DisconnectTheSession();

        bool down = PumpUntil(() => QueryConnectedState() == 0, SessionDownTimeout, stopwatch);

        LogClose(down
                     ? $"session down at {stopwatch.ElapsedMilliseconds} ms"
                     : $"session still reports Connected={QueryConnectedState()} after " +
                       $"{stopwatch.ElapsedMilliseconds} ms - the control will not be disposed");

        return down;
    }

    /// <summary>
    /// Pumps the message loop until <paramref name="condition"/> holds or the budget runs out.
    /// </summary>
    /// <remarks>
    /// Pumping is safe here: <see cref="ProtocolBase"/> defers any close that arrives
    /// mid-teardown onto an idle turn, and every event sink of ours has been unadvised by this
    /// point. The budget is measured from where the caller is now, so each phase of the close
    /// gets its own allowance rather than racing the ones before it.
    /// </remarks>
    private static bool PumpUntil(Func<bool> condition, TimeSpan timeout, Stopwatch stopwatch)
    {
        TimeSpan deadline = stopwatch.Elapsed + timeout;
        while (stopwatch.Elapsed < deadline)
        {
            if (condition())
            {
                return true;
            }

            Application.DoEvents();
            Thread.Sleep(10);
        }

        return condition();
    }

    /// <summary>
    /// Puts the themed "Connecting..." panel in front of the RDP control.
    /// </summary>
    /// <remarks>
    /// The OCX keeps its own <c>ConnectingText</c> as well. It is never seen while the overlay is
    /// up, but it is what full-screen and reconnect UI fall back on, and it costs nothing.
    /// </remarks>
    private void ShowTheConnectingOverlay()
    {
        _connectingOverlay = ConnectingOverlay.ShowOver(Control, Language.strConnecting);

        Runtime.MessageCollector.AddMessage(MessageClass.DebugMsg,
            $"Connecting overlay [{SessionLabel}]: " +
            (_connectingOverlay == null ? "not shown - the RDP control has no parent" : "shown"), true);
    }

    /// <summary>
    /// Takes the overlay away, recording what ended the wait.
    /// </summary>
    /// <remarks>
    /// The reason is logged rather than inferred later because nothing behavioural about a real
    /// session can be observed on the machine this is written on: which event actually uncovers a
    /// session, and how long it took to arrive, is readable only from mRemoteUG.log afterwards.
    /// </remarks>
    private void HideTheConnectingOverlay(string reason)
    {
        if (_connectingOverlay == null)
        {
            return;
        }

        Runtime.MessageCollector.AddMessage(MessageClass.DebugMsg,
            $"Connecting overlay [{SessionLabel}]: removed ({reason})", true);

        _connectingOverlay.Remove();
        _connectingOverlay = null;
    }

    private void SetEventHandlers(MsRdpClient11NotSafeForScripting client)
    {
        try
        {
            client.OnConnecting += RDPEvent_OnConnecting;
            client.OnConnected += RDPEvent_OnConnected;
            client.OnLoginComplete += RDPEvent_OnLoginComplete;
            client.OnFatalError += RDPEvent_OnFatalError;
            client.OnDisconnected += RDPEvent_OnDisconnected;
            client.OnLeaveFullScreenMode += RDPEvent_OnLeaveFullscreenMode;
            client.OnIdleTimeoutNotification += RDPEvent_OnIdleTimeoutNotification;
            client.OnAutoReconnecting2 += RDPEvent_OnAutoReconnecting2;
            client.OnAutoReconnected += RDPEvent_OnAutoReconnected;
        }
        catch (Exception ex)
        {
            Runtime.MessageCollector.AddExceptionMessage(Language.strRdpSetEventHandlersFailed, ex);
        }
    }
    #endregion

    #region Private Events & Handlers
    private void RDPEvent_OnIdleTimeoutNotification()
    {
        // Advised only by Connect, which needs a client, and so a connection, to exist.
        if (_connectionInfo is not { } connectionInfo)
        {
            return;
        }

        // As in RDPEvent_OnDisconnected: leave the OCX's event sink before tearing the control
        // down. The message box matters too - pumping a modal loop from inside a COM callback
        // is its own hazard.
        PostToUiThread(() =>
        {
            Close(); //Simply close the RDP Session if the idle timeout has been triggered.

            string message = "The " + connectionInfo.Name + " session was disconnected due to inactivity";
            Runtime.MessageCollector.AddMessage(MessageClass.InformationMsg, message, true);

            if (!_alertOnIdleDisconnect)
            {
                return;
            }

            // Deliberately a message box rather than a message through the collector, so the
            // Pop-ups settings do not apply. The gate on this one is the per-connection
            // RDPAlertIdleTimeout option just checked above - an alert the user asked for on
            // this connection - whereas the Pop-ups group governs unsolicited messages, and
            // Information pop-ups are off by default. Routing it there would silently disable
            // a setting the user had deliberately turned on.
            const string caption = "Session Disconnected";
            MessageBox.Show(message, caption, MessageBoxButtons.OK, MessageBoxIcon.Information);
        });
    }


    private void RDPEvent_OnFatalError(int errorCode)
    {
        HideTheConnectingOverlay($"fatal error {errorCode}");
        OnErrorOccured(this, Convert.ToString(errorCode, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Describes the session endings that are deliberate rather than failures, or null when
    /// this isn't one of them.
    /// </summary>
    /// <remarks>
    /// mstsc's own GetErrorDescription() maps these onto "an internal error has occurred",
    /// so a perfectly clean logoff used to be recorded as a fault. Only the codes that
    /// unambiguously mean "someone ended this on purpose" are listed; everything else - idle
    /// and logon timeouts, being replaced by another connection, licensing, credentials - still
    /// goes to GetErrorDescription, because those are worth reporting as-is.
    /// </remarks>
    private static string? DescribeIntentionalDisconnect(ExtendedDisconnectReasonCode extendedReason)
    {
        switch (extendedReason)
        {
            case ExtendedDisconnectReasonCode.exDiscReasonAPIInitiatedDisconnect:
                return "Disconnected by the client.";
            case ExtendedDisconnectReasonCode.exDiscReasonAPIInitiatedLogoff:
                return "Logged off by the client.";
            case ExtendedDisconnectReasonCode.exDiscReasonRpcInitiatedDisconnectByUser:
                return "Disconnected by the user.";
            case ExtendedDisconnectReasonCode.exDiscReasonLogoffByUser:
                return "The user logged off the remote session.";
            case ExtendedDisconnectReasonCode.exDiscReasonShutdown:
                return "The remote computer is shutting down.";
            case ExtendedDisconnectReasonCode.exDiscReasonReboot:
                return "The remote computer is restarting.";
            default:
                return null;
        }
    }

    /// <summary>
    /// The control has lost the session and is reconnecting by itself.
    /// </summary>
    /// <remarks>
    /// <c>EnableAutoReconnect</c> is on, so a dropped session first shows the control's own
    /// "trying to reconnect" screen and raises this, not <c>OnDisconnected</c>. Before this was
    /// logged, a lab run had no trace of the drop at all. Giving up ends in
    /// <see cref="RDPEvent_OnDisconnected"/>, which logs its own reason.
    /// </remarks>
    private void RDPEvent_OnAutoReconnecting2(int disconnectReason, bool networkAvailable,
                                              int attemptCount, int maxAttemptCount)
    {
        Runtime.MessageCollector.AddMessage(MessageClass.InformationMsg,
            $"RDP auto-reconnect [{SessionLabel}]: the session dropped (discReason={disconnectReason} " +
            $"(0x{disconnectReason:X})), network {(networkAvailable ? "available" : "not available")}; " +
            $"attempt {attemptCount} of {maxAttemptCount}.", true);
    }

    /// <summary>
    /// The control has re-established a dropped session by itself.
    /// </summary>
    /// <remarks>
    /// Logged, and deliberately nothing more. The logon that follows raises
    /// <see cref="RDPEvent_OnLoginComplete"/> again, measured, and its pass sends the scale for the
    /// window's DPI, with the window's size for a session that follows it - which makes good the
    /// resizes refused during the reconnect. Sending size and scale from here as well was tried:
    /// it ran before the display channel was open, and the reconnect it fell back to dropped the
    /// session again, every ten seconds, until the host locked the session.
    /// </remarks>
    private void RDPEvent_OnAutoReconnected()
    {
        Runtime.MessageCollector.AddMessage(MessageClass.InformationMsg,
            $"RDP auto-reconnect [{SessionLabel}]: reconnected.", true);
    }

    private void RDPEvent_OnDisconnected(int discReason)
    {
        // Advised only while there is a client: the teardown unadvises this sink before it lets
        // the client go.
        if (_rdpClient is not { } client)
        {
            return;
        }

        _sessionConnected = false;

#pragma warning disable IDE1006 // The name mirrors the RDP client API's disconnect reason.
        const int UI_ERR_NORMAL_DISCONNECT = 0xB08;
#pragma warning restore IDE1006

        // First, whatever follows. A session that never came up must not be left behind a
        // permanent "Connecting...", and the reconnect UI below is drawn where the overlay is.
        HideTheConnectingOverlay($"disconnected, discReason={discReason}");

        ExtendedDisconnectReasonCode extendedReason = ExtendedDisconnectReasonCode.exDiscReasonNoInfo;
        try { extendedReason = client.ExtendedDisconnectReason; }
        catch (Exception) { /* the OCX may already be unusable */ }

        // These two codes are the only way to tell a clean logoff from a real failure.
        Runtime.MessageCollector.AddMessage(MessageClass.InformationMsg,
            $"RDP disconnect: discReason={discReason} (0x{discReason:X}), " +
            $"ExtendedDisconnectReason={(int)extendedReason} ({extendedReason})", true);

        if (discReason != UI_ERR_NORMAL_DISCONNECT)
        {
            string reason = DescribeIntentionalDisconnect(extendedReason)
                         ?? client.GetErrorDescription((uint)discReason, (uint)extendedReason);
            OnDisconnected(this, discReason + "\r\n" + reason);
        }

        if (Settings.Default.ReconnectOnDisconnect)
        {
            // Through ReplaceReconnectGroup: a flapping server arrives here again and again, and
            // the group being replaced owns a running animation timer.
            // The local is the group ReplaceReconnectGroup has just made ActiveReconnectGroup.
            var group = new ReconnectGroup();
            ReplaceReconnectGroup(group);
            group.CloseClicked += OnReconnectGroupCloseClicked;
            Control control = AssignedControl;
            group.Left = (int)((double)control.Width / 2 - (double)group.Width / 2);
            group.Top = (int)((double)control.Height / 2 - (double)group.Height / 2);
            group.Parent = control;
            group.Show();
            TmrReconnect.Enabled = true;
        }
        else
        {
            // We are inside the OCX's own event sink, and when a whole folder is disconnected at
            // once every other session is in the same position. Queue the teardown for an idle
            // turn so the sinks unwind first and the teardowns run one at a time - releasing a
            // control while the others are still unwinding is what hangs the UI thread.
            PostToUiThreadWhenIdle(Close);
        }
    }

    private void RDPEvent_OnConnecting()
    {
        OnConnecting(this);
    }

    private void RDPEvent_OnConnected()
    {
        _sessionConnected = true;

        // Not OnLoginComplete: between connected and logged on the OCX is already showing the
        // remote side - a server logon screen, on a connection without NLA - and covering that
        // would hide the prompt the user has to answer.
        HideTheConnectingOverlay("connected");

        // SetDisplayScale has already declared this scale before the session existed, so on a
        // connection that went through Initialize this is a no-op that costs one call. It earns
        // its keep on the paths that never reach SetDisplayScale - see TryApplyDisplayScale.
        TryApplyDisplayScale(CurrentDpi(), DisplayScalePass.SessionCreated);

        OnConnected(this);
    }

    private void RDPEvent_OnLoginComplete()
    {
        _loginComplete = true;

        // Belt and braces. OnConnected should already have taken it away; a session that
        // somehow reached logon without it must not stay covered.
        HideTheConnectingOverlay("login complete");

        // OnConnected has already asked for this scale, and for a session this connection created
        // that is the pass that counts. This one answers what it cannot: a reconnect into a
        // session whose desktop was already up, a host that was not yet in a state to act on the
        // earlier pass, and every resize dropped before logon or refused for now during a
        // reconnect - it sends a session that follows its window the window's size now, with the
        // scale, as one request (#246). It deliberately does not consult the cache, which may
        // describe a session that has since been re-created, and it does not nudge the
        // resolution: ADR-0028 records that the nudge settled nothing.
        TryApplyDisplayScale(CurrentDpi(), DisplayScalePass.LoginComplete);
    }

    private void RDPEvent_OnLeaveFullscreenMode()
    {
        Fullscreen = false;
        _leaveFullscreenEvent?.Invoke(this, new EventArgs());
    }
    #endregion

    #region Public Events & Handlers
    private EventHandler? _leaveFullscreenEvent;

    public event EventHandler LeaveFullscreen
    {
        add
        {
            _leaveFullscreenEvent = (EventHandler?)Delegate.Combine(_leaveFullscreenEvent, value);
        }
        remove
        {
            _leaveFullscreenEvent = (EventHandler?)Delegate.Remove(_leaveFullscreenEvent, value);
        }
    }
    #endregion

    #region Enums
    public enum Defaults
    {
        Port = 3389
    }

    public enum RDPColors
    {
        [LocalizedAttributes.LocalizedDescription("strRDP256Colors")]
        Colors256 = 8,
        [LocalizedAttributes.LocalizedDescription("strRDP32768Colors")]
        Colors15Bit = 15,
        [LocalizedAttributes.LocalizedDescription("strRDP65536Colors")]
        Colors16Bit = 16,
        [LocalizedAttributes.LocalizedDescription("strRDP16777216Colors")]
        Colors24Bit = 24,
        [LocalizedAttributes.LocalizedDescription("strRDP4294967296Colors")]
        Colors32Bit = 32
    }

    public enum RDPSounds
    {
        [LocalizedAttributes.LocalizedDescription("strRDPSoundBringToThisComputer")]
        BringToThisComputer = 0,
        [LocalizedAttributes.LocalizedDescription("strRDPSoundLeaveAtRemoteComputer")]
        LeaveAtRemoteComputer = 1,
        [LocalizedAttributes.LocalizedDescription("strRDPSoundDoNotPlay")]
        DoNotPlay = 2
    }

    public enum RDPSoundQuality
    {
        [LocalizedAttributes.LocalizedDescription("strRDPSoundQualityDynamic")]
        Dynamic = 0,
        [LocalizedAttributes.LocalizedDescription("strRDPSoundQualityMedium")]
        Medium = 1,
        [LocalizedAttributes.LocalizedDescription("strRDPSoundQualityHigh")]
        High = 2
    }


    private enum RDPPerformanceFlags
    {
        [Description("strRDPDisableWallpaper")] DisableWallpaper = 0x1,
        //			[Description("strRDPDisableFullWindowdrag")]DisableFullWindowDrag = 0x2,
        //			[Description("strRDPDisableMenuAnimations")]DisableMenuAnimations = 0x4,
        [Description("strRDPDisableThemes")] DisableThemes = 0x8,
        //			[Description("strRDPDisableCursorShadow")]DisableCursorShadow = 0x20,
        //			[Description("strRDPDisableCursorblinking")]DisableCursorBlinking = 0x40,
        [Description("strRDPEnableFontSmoothing")] EnableFontSmoothing = 0x80,
        [Description("strRDPEnableDesktopComposition")] EnableDesktopComposition = 0x100
    }

    public enum RDPResolutions
    {
        [LocalizedAttributes.LocalizedDescription("strRDPFitToPanel")]
        FitToWindow,
        [LocalizedAttributes.LocalizedDescription("strFullscreen")]
        Fullscreen,
        [LocalizedAttributes.LocalizedDescription("strRDPSmartSize")]
        SmartSize,
        [Description("800x600")]
        Res800x600,
        [Description("1024x768")]
        Res1024x768,
        [Description("1152x864")]
        Res1152x864,
        [Description("1280x800")]
        Res1280x800,
        [Description("1280x1024")]
        Res1280x1024,
        [Description("1366x768")]
        Res1366x768,
        [Description("1440x900")]
        Res1440x900,
        [Description("1600x900")]
        Res1600x900,
        [Description("1600x1200")]
        Res1600x1200,
        [Description("1680x1050")]
        Res1680x1050,
        [Description("1920x1080")]
        Res1920x1080,
        [Description("1920x1200")]
        Res1920x1200,
        [Description("2048x1536")]
        Res2048x1536,
        [Description("2560x1440")]
        Res2560x1440,
        [Description("2560x1600")]
        Res2560x1600,
        [Description("2560x2048")]
        Res2560x2048,
        [Description("3840x2160")]
        Res3840x2160
    }

    public enum AuthenticationLevel
    {
        [LocalizedAttributes.LocalizedDescription("strAlwaysConnectEvenIfAuthFails")]
        NoAuth = 0,
        [LocalizedAttributes.LocalizedDescription("strDontConnectWhenAuthFails")]
        AuthRequired = 1,
        [LocalizedAttributes.LocalizedDescription("strWarnIfAuthFails")]
        WarnOnFailedAuth = 2
    }

    public enum RDGatewayUsageMethod
    {
        [LocalizedAttributes.LocalizedDescription("strNever")]
        Never = 0, // TSC_PROXY_MODE_NONE_DIRECT
        [LocalizedAttributes.LocalizedDescription("strAlways")]
        Always = 1, // TSC_PROXY_MODE_DIRECT
        [LocalizedAttributes.LocalizedDescription("strDetect")]
        Detect = 2 // TSC_PROXY_MODE_DETECT
    }

    public enum RDGatewayUseConnectionCredentials
    {
        [LocalizedAttributes.LocalizedDescription("strUseDifferentUsernameAndPassword")]
        No = 0,
        [LocalizedAttributes.LocalizedDescription("strUseSameUsernameAndPassword")]
        Yes = 1,
        [LocalizedAttributes.LocalizedDescription("strUseSmartCard")]
        SmartCard = 2
    }
    #endregion

    #region Resolution
    public static Rectangle GetResolutionRectangle(RDPResolutions resolution)
    {
        string[]? resolutionParts = null;
        if (resolution != RDPResolutions.FitToWindow
            & resolution != RDPResolutions.Fullscreen
            & resolution != RDPResolutions.SmartSize)
        {
            resolutionParts = resolution.ToString().Replace("Res", "").Split('x');
        }
        if (resolutionParts == null || resolutionParts.Length != 2)
        {
            return new Rectangle(0, 0, 0, 0);
        }
        else
        {
            return new Rectangle(
                0,
                0,
                Convert.ToInt32(resolutionParts[0], CultureInfo.InvariantCulture),
                Convert.ToInt32(resolutionParts[1], CultureInfo.InvariantCulture));
        }
    }
    #endregion

    #region Fatal Errors
    public static class FatalErrors
    {
        /// <summary>
        /// Describes an RDP fatal error code as reported by the control's OnFatalError.
        /// </summary>
        /// <remarks>
        /// This was a Hashtable whose values were the resource *names* in quotes - the
        /// literal string "Language.strRdpErrorCode1" and so on, not the property. GetError
        /// returned them unchanged, so a fatal error showed the user the name of a resource
        /// instead of its text. Codes are per IMsTscAxEvents::OnFatalError.
        /// </remarks>
        public static string GetError(string id) =>
            id switch
            {
                "0" => Language.strRdpErrorUnknown,
                "1" => Language.strRdpErrorCode1,
                "2" => Language.strRdpErrorOutOfMemory,
                "3" => Language.strRdpErrorWindowCreation,
                "4" => Language.strRdpErrorCode2,
                "5" => Language.strRdpErrorCode3,
                "6" => Language.strRdpErrorCode4,
                "7" => Language.strRdpErrorConnection,
                "100" => Language.strRdpErrorWinsock,
                _ => Language.strRdpErrorUnknown
            };
    }
    #endregion

    #region Reconnect Stuff
    [SuppressMessage("Performance", "CA1863",
                     Justification = "The format is a localized resource read at run time; "
                                     + "a cached CompositeFormat would freeze the UI language.")]
    public void TmrReconnectElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        // _connectionInfo is set before _rdpClient and never cleared, so the second test only
        // tells the analyzer what is already true.
        if (_rdpClient is not { } client || _connectionInfo is not { } connectionInfo)
        {
            return;
        }

        try
        {
            bool srvReady = IsPortOpen(connectionInfo.Hostname, connectionInfo.Port);

            // A tick already in flight when the teardown took the group down used to fail on
            // this line, into the catch below; it still does, with a message.
            ReconnectGroup group = ActiveReconnectGroup
                ?? throw new InvalidOperationException("No reconnect group is up.");
            group.ServerReady = srvReady;

            if (group.ReconnectWhenReady && srvReady)
            {
                TmrReconnect.Enabled = false;
                group.DisposeReconnectGroup();

                // The scale has to be declared again, because this does not go through
                // Initialize. Without it the new session is created at whatever scale the tab
                // was opened on, which is stale the moment the window has been moved to a
                // monitor of a different one - and correcting it afterwards is the thing
                // ADR-0028 records does not reliably work.
                //
                // Deliberately on this thread rather than posted to the UI one: it is a
                // property write on the same OCX that the Connect below is called on, from the
                // same place, so it is marshalled the same way and introduces no cross-thread
                // behaviour that was not already here. The DPI comes from _lastKnownDpi for
                // that reason - this runs on a timer thread and must not read Control.
                SetDisplayScale(client);
                client.Connect();
            }
        }
        catch (Exception ex)
        {
            Runtime.MessageCollector.AddExceptionMessage(
                string.Format(CultureInfo.CurrentCulture, Language.AutomaticReconnectError, connectionInfo.Hostname),
                ex, MessageClass.WarningMsg, false);
        }
    }

    private static bool IsPortOpen(string hostname, int port)
    {
        try
        {
            using (var client = new TcpClient())
            {
                IAsyncResult result = client.BeginConnect(hostname, port, null, null);
                bool success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(500));
                if (!success)
                {
                    return false;
                }

                client.EndConnect(result);
                return true;
            }
        }
        catch (Exception)
        {
            return false;
        }
    }
    #endregion
}
