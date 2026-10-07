using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using mRemoteUG.App;
using mRemoteUG.Messages;
using mRemoteUG.Security;
using mRemoteUG.Tools;
using mRemoteUG.UI.Controls;
using mRemoteUG.UI.Forms;
using MSTSCLib;

namespace mRemoteUG.Connection.Protocol.RDP
{
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
        private static readonly TimeSpan ControlCreationTimeout = TimeSpan.FromSeconds(10);

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
        /// </remarks>
        private MsRdpClient11NotSafeForScripting _rdpClient;

        /// <summary>
        /// mstscax.dll's own file version - <em>not</em> the coclass generation, which is why it
        /// is only ever logged and never branched on. It reads 10.0.26xxx on Windows 11 whichever
        /// coclass was created, so the version comparisons this class used to make could not
        /// discriminate between clients at all.
        /// </summary>
        private Version _rdpVersion;
        private ConnectionInfo _connectionInfo;
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
        /// <see cref="tmrReconnect_Elapsed"/> - runs on a timer thread, where reading
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
        private ConnectingOverlay _connectingOverlay;

        #region Properties
        public bool SmartSize
		{
			get
			{
				return _rdpClient.AdvancedSettings2.SmartSizing;
			}
            private set
			{
				_rdpClient.AdvancedSettings2.SmartSizing = value;
				ReconnectForResize();
			}
		}
		
        public bool Fullscreen
		{
			get
			{
				return _rdpClient.FullScreen;
			}
            private set
			{
				_rdpClient.FullScreen = value;
				ReconnectForResize();
			}
		}

	    private bool RedirectKeys
		{
/*
			get
			{
				return _redirectKeys;
			}
*/
			set
			{
				_redirectKeys = value;
				try
				{
					if (!_redirectKeys)
					{
						return;
					}
							
					Debug.Assert(Convert.ToBoolean(_rdpClient.SecuredSettingsEnabled));
                    var msRdpClientSecuredSettings = _rdpClient.SecuredSettings2;
					msRdpClientSecuredSettings.KeyboardHookMode = 1; // Apply key combinations at the remote server.
				}
				catch (Exception ex)
				{
					Runtime.MessageCollector.AddExceptionMessage(Language.strRdpSetRedirectKeysFailed, ex);
				}
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
				_connectionInfo = InterfaceControl.Info;

				// Before the wait below, not after it: that loop pumps messages, so this is where the
				// OCX first gets to paint its white page. See ConnectingOverlay.
				ShowTheConnectingOverlay();

				try
				{
					// Bounded. This pumped and slept with no exit condition at all, on the UI thread,
					// during creation of an ActiveX control - and it now runs while the main window is
					// still being brought up (see FrmMain.CompleteStartup), which is where a handle
					// chain is least certain. The pumping is deliberately unchanged: this is a bound,
					// not a redesign. See ADR-0005.
					var creation = Stopwatch.StartNew();
					while (!Control.Created && creation.Elapsed < ControlCreationTimeout)
					{
						Thread.Sleep(0);
                        Application.DoEvents();
					}

					if (!Control.Created)
					{
						Runtime.MessageCollector.AddMessage(MessageClass.WarningMsg,
							$"The RDP control had no window handle after {creation.ElapsedMilliseconds} ms; " +
							"giving up rather than waiting indefinitely.");
						Control.Dispose();
						return false;
					}
                    // QueryInterface(IID_IMsRdpClient10) in all but name. This is the per-connection
                    // proof that the control really can be driven through its newest interface;
                    // it fails loudly into strRdpControlCreationFailed below rather than silently
                    // degrading to an older vtable.
                    _rdpClient = (MsRdpClient11NotSafeForScripting)((AxHost)Control).GetOcx();
				}
				catch (System.Runtime.InteropServices.COMException ex)
				{
					Runtime.MessageCollector.AddExceptionMessage(Language.strRdpControlCreationFailed, ex);
					Control.Dispose();
					return false;
				}
						
				_rdpVersion = new Version(_rdpClient.Version);
				LogTheClientWeGot();

				_rdpClient.Server = _connectionInfo.Hostname;

                SetCredentials();
                SetResolution();
                _lastKnownDpi = Control?.DeviceDpi ?? RdpDisplayScale.DefaultDpi;
                SetDisplayScale();
                _rdpClient.FullScreenTitle = _connectionInfo.Name;

                _alertOnIdleDisconnect = _connectionInfo.RDPAlertIdleTimeout;
                _rdpClient.AdvancedSettings2.MinutesToIdleTimeout = _connectionInfo.RDPMinutesToIdleTimeout;

                // Deliberately not individually guarded, unlike SetRedirection below. Every
                // assignment from here to ConnectingText is a client-side property write on an OCX
                // that has not connected to anything yet, so no server - however old - can make one
                // fail; and they are all bound against IMsRdpClient10, which the v11 floor
                // guarantees. What a failure here would mean is a genuinely broken control, and
                // aborting the connection with strRdpSetPropsFailed is the right answer to that.
                // Guarding them one by one would trade a clear abort for a session that connects
                // with a silently wrong security level or no credentials.

                //not user changeable
                _rdpClient.AdvancedSettings2.GrabFocusOnConnect = true;
				_rdpClient.AdvancedSettings3.EnableAutoReconnect = true;
				_rdpClient.AdvancedSettings3.MaxReconnectAttempts = Settings.Default.RdpReconnectionCount;
				_rdpClient.AdvancedSettings2.keepAliveInterval = 60000; //in milliseconds (10,000 = 10 seconds)
				_rdpClient.AdvancedSettings5.AuthenticationLevel = 0;
				_rdpClient.AdvancedSettings2.EncryptionEnabled = 1;
						
				_rdpClient.AdvancedSettings2.overallConnectionTimeout = Settings.Default.ConRDPOverallConnectionTimeout;
						
				_rdpClient.AdvancedSettings2.BitmapPeristence = Convert.ToInt32(_connectionInfo.CacheBitmaps);
				_rdpClient.AdvancedSettings7.EnableCredSspSupport = _connectionInfo.UseCredSsp;
				_rdpClient.AdvancedSettings8.AudioQualityMode = (uint)_connectionInfo.SoundQuality;

                SetUseConsoleSession();
                SetPort();
				RedirectKeys = _connectionInfo.RedirectKeys;
                SetRedirection();
                SetAuthenticationLevel();
				SetLoadBalanceInfo();
                SetRdGateway();
						
				_rdpClient.ColorDepth = (int)_connectionInfo.Colors;

                SetPerformanceFlags();
						
				_rdpClient.ConnectingText = Language.strConnecting;

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
			// Note what does *not* come through here. tmrReconnect_Elapsed calls
			// _rdpClient.Connect() directly, and mstscax's own EnableAutoReconnect never enters
			// managed code at all, so neither automatic reconnect resets any of this - which is
			// exactly why the display-scale passes below do not consult the cache.
			_lastRequestedSessionSize = Size.Empty;
			_lastRequestedSessionScale = (0, 0);
			_lastResizeSkipReason = null;
			_sessionConnected = false;
			SetEventHandlers();

            try
			{
				_rdpClient.Connect();
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
			if (_rdpClient == null)
				return;

			try
			{
				_rdpClient.Disconnect();
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
				if (Control.ContainsFocus == false)
				{
					Control.Focus();
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
			_controlBeginningSize = Control.Size;
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
			if (Control.Size != _controlBeginningSize)
			{
				ReconnectForResize();
			}
			_controlBeginningSize = Size.Empty;
		}
        #endregion
		
        #region Private Methods
		private bool CreateNewestAvailableRdpControl()
		{
			var placeholder = Control;
			var attempts = new System.Collections.Generic.List<string>();

			var created = RdpClientCandidates.CreateNewest(placeholder.Parent, placeholder.Name, placeholder.Dock, attempts);

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
					new System.Runtime.InteropServices.COMException(
						"No RDP ActiveX control (v11 or v12) could be created: " + string.Join("; ", attempts)));
				return false;
			}

			Control = created;
			return true;
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
		private void LogTheClientWeGot()
		{
			var ocx = ((AxHost)Control).GetOcx();
			var optional = new System.Collections.Generic.List<string>();
			if (ocx is IMsRdpClientNonScriptable8) optional.Add(nameof(IMsRdpClientNonScriptable8));
			if (ocx is IMsRdpExtendedSettings) optional.Add(nameof(IMsRdpExtendedSettings));
			if (ocx is IMsRdpPreferredRedirectionInfo) optional.Add(nameof(IMsRdpPreferredRedirectionInfo));

			// Debug: the interface inventory restates what the selection line above already said
			// for the case anyone reads a normal log for.
			Runtime.MessageCollector.AddMessage(MessageClass.DebugMsg,
				$"RDP client [{SessionLabel}]: control={Control.GetType().Name}, mstscax={_rdpVersion}, " +
				$"IMsRdpClient10=yes, optional=[{string.Join(", ", optional)}]", true);
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
		private string _lastResizeSkipReason;

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
			if (_lastResizeSkipReason == reason) return;
			_lastResizeSkipReason = reason;
			Runtime.MessageCollector.AddMessage(MessageClass.DebugMsg,
				$"Resize [{SessionLabel}] skipped: {reason}", true);
		}

		private void ReconnectForResize()
		{
			if (_rdpClient == null)
			{
				SkipResize("the RDP control has not been initialised yet");
				return;
			}

			if (!_loginComplete)
			{
				SkipResize("login is not complete (OnLoginComplete has not been received)");
				return;
			}

			if (!InterfaceControl.Info.AutomaticResize)
			{
				SkipResize("this connection has AutomaticResize turned off");
				return;
			}

			if (!(InterfaceControl.Info.Resolution == RDPResolutions.FitToWindow | InterfaceControl.Info.Resolution == RDPResolutions.Fullscreen))
			{
				SkipResize($"resolution is {InterfaceControl.Info.Resolution}, which is neither FitToWindow nor Fullscreen");
				return;
			}

			if (SmartSize)
			{
				SkipResize("SmartSize is on, so the session is scaled rather than resized");
				return;
			}

			_lastResizeSkipReason = null;

		    try
		    {
		        var size = !Fullscreen ? Control.Size : Screen.FromControl(Control).Bounds.Size;
		        var dpi = Control.DeviceDpi;
		        var scale = (RdpDisplayScale.DesktopScaleFactor(dpi), RdpDisplayScale.DeviceScaleFactor(dpi));

		        // Resize fires for every WM_SIZE, so only talk to the session when what it was last
		        // asked for has actually changed. The scale is part of that: a window dragged to a
		        // monitor of a different DPI can arrive at the same size and a different scale.
		        if (size == _lastRequestedSessionSize && scale == _lastRequestedSessionScale)
		            return;
		        _lastRequestedSessionSize = size;
		        _lastRequestedSessionScale = scale;

                Runtime.MessageCollector.AddMessage(MessageClass.DebugMsg, $"Resizing RDP connection to host '{_connectionInfo.Hostname}' to {size.Width}x{size.Height}");

                if (!_sessionRefusedDisplayUpdates && TryUpdateSessionDisplaySettings(size, dpi, latchRefusal: true))
                {
                    return;
                }

			    _rdpClient.Reconnect((uint)size.Width, (uint)size.Height);
		    }
		    catch (Exception ex)
		    {
                Runtime.MessageCollector.AddExceptionMessage(string.Format(Language.ChangeConnectionResolutionError, _connectionInfo.Hostname),
                    ex, MessageClass.WarningMsg, false);
		    }
		}

		/// <summary>
		/// Set once this session has refused <c>UpdateSessionDisplaySettings</c>.
		/// </summary>
		/// <remarks>
		/// That refusal is a fact about the session, not about one resize: the display-control
		/// virtual channel arrived in RDP 8.1, so a Server 2008 R2 or 2012 host refuses every
		/// call. Without the latch, a single drag throws a COM exception and writes a log line per
		/// frame. Cleared in <see cref="Connect"/>, because <c>tmrReconnect_Elapsed</c> reuses this
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
        /// <param name="latchRefusal">
        /// Whether a refusal should be taken as a fact about the session rather than about this
        /// one call. It is one for a server with no display-control channel at all, which is what
        /// the latch is for. It is not one for a pass that runs before the session has a desktop:
        /// latching there would turn a call that was merely early into no scaling at all for the
        /// rest of the session's life.
        /// </param>
        private bool TryUpdateSessionDisplaySettings(Size size, int dpi, bool latchRefusal)
        {
            if (!RdpDisplayScale.TryNormaliseSessionSize(size, out var accepted))
            {
                Runtime.MessageCollector.AddMessage(MessageClass.DebugMsg,
                    $"Resize [{SessionLabel}]: {size.Width}x{size.Height} is outside the " +
                    $"{RdpDisplayScale.MinSessionDimension}-{RdpDisplayScale.MaxSessionDimension} " +
                    "the display-control channel accepts; falling back to a reconnect.", true);
                return false;
            }

            var desktopScale = RdpDisplayScale.DesktopScaleFactor(dpi);
            var deviceScale = RdpDisplayScale.DeviceScaleFactor(dpi);

            try
            {
                _rdpClient.UpdateSessionDisplaySettings((uint)accepted.Width, (uint)accepted.Height,
                                                        0, 0, 0, desktopScale, deviceScale);

                Runtime.MessageCollector.AddMessage(MessageClass.DebugMsg,
                    $"Resize [{SessionLabel}]: asked the session for {accepted.Width}x{accepted.Height} " +
                    $"at {dpi} DPI (desktopScaleFactor {desktopScale}, deviceScaleFactor {deviceScale}).",
                    true);
                return true;
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
                return false;
            }
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
		/// Tells the session what scale it is being viewed at, without resizing it.
		/// </summary>
		/// <remarks>
		/// Deliberately does not share <see cref="ReconnectForResize"/>'s guards. Those are right
		/// for a resize: with <c>AutomaticResize</c> off, a fixed resolution, or SmartSize on, the
		/// user has said the session's pixel count should not follow the window. None of that says
		/// anything about scale - a fixed 1920x1080 session dragged onto a 200% monitor still wants
		/// telling it is being viewed at 200%, or its text stays half the size it should be.
		/// <para>
		/// The size is read back from the control rather than recomputed from the panel, so a fixed
		/// resolution stays fixed and only the scale changes.
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
		/// <c>tmrReconnect_Elapsed</c> calls <c>_rdpClient.Connect()</c> straight, and mstscax's own
		/// <c>EnableAutoReconnect</c> never enters managed code;</item>
		/// <item>a connection where <c>SetDisplayScale</c> logged that it could not declare the
		/// scale, because the control answered no <c>IMsRdpExtendedSettings</c>.</item>
		/// </list>
		/// </para>
		/// </remarks>
		private void TryApplyDisplayScale(int dpi, DisplayScalePass pass)
		{
			if (_rdpClient == null || !_sessionConnected || _sessionRefusedDisplayUpdates)
				return;

			try
			{
				var size = new Size(_rdpClient.DesktopWidth, _rdpClient.DesktopHeight);
				var scale = (RdpDisplayScale.DesktopScaleFactor(dpi), RdpDisplayScale.DeviceScaleFactor(dpi));

				// The cache is here to stop a run of DPI changes talking to the session on every one of
				// them. SessionCreated and LoginComplete happen once each per session, and the second
				// exists precisely to repeat the first in case the host was not yet in a state to act on
				// it, so neither of them consults it.
				if (pass == DisplayScalePass.DpiChanged &&
				    size == _lastRequestedSessionSize && scale == _lastRequestedSessionScale)
					return;

				Runtime.MessageCollector.AddMessage(MessageClass.DebugMsg,
					$"Display scale [{SessionLabel}]: {pass} pass at {dpi} DPI, the session is " +
					$"{size.Width}x{size.Height}.", true);

				if (!TryUpdateSessionDisplaySettings(
						size, dpi, latchRefusal: pass != DisplayScalePass.SessionCreated))
					return;

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

			// Remembered for tmrReconnect_Elapsed, which cannot read it for itself. This is the
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
		private void SetDisplayScale()
		{
			var dpi = _lastKnownDpi;
			var desktopScale = RdpDisplayScale.DesktopScaleFactor(dpi);
			var deviceScale = RdpDisplayScale.DeviceScaleFactor(dpi);
			var asked = $"desktopScaleFactor {desktopScale}, deviceScaleFactor {deviceScale} " +
			            $"at {dpi} DPI";

			if (!(_rdpClient is IMsRdpExtendedSettings extended))
			{
				Runtime.MessageCollector.AddMessage(MessageClass.InformationMsg,
					$"RDP display scale [{SessionLabel}]: this control answers no " +
					$"IMsRdpExtendedSettings, so {asked} could not be declared before connecting; the " +
					"session will be rescaled once it exists instead.", true);
				return;
			}

			var failures = string.Empty;

			foreach (var (name, value) in new[] { ("DesktopScaleFactor", desktopScale),
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

		private void SetRdGateway()
		{
			try
			{
				if (_rdpClient.TransportSettings.GatewayIsSupported == 0)
				{
					Runtime.MessageCollector.AddMessage(MessageClass.DebugMsg, Language.strRdpGatewayNotSupported, true);
					return;
				}
			    Runtime.MessageCollector.AddMessage(MessageClass.DebugMsg, Language.strRdpGatewayIsSupported, true);

			    if (_connectionInfo.RDGatewayUsageMethod != RDGatewayUsageMethod.Never)
				{
					_rdpClient.TransportSettings.GatewayUsageMethod = (uint)_connectionInfo.RDGatewayUsageMethod;
					_rdpClient.TransportSettings.GatewayHostname = _connectionInfo.RDGatewayHostname;
					_rdpClient.TransportSettings.GatewayProfileUsageMethod = 1; // TSC_PROXY_PROFILE_MODE_EXPLICIT
					if (_connectionInfo.RDGatewayUseConnectionCredentials == RDGatewayUseConnectionCredentials.SmartCard)
					{
						_rdpClient.TransportSettings.GatewayCredsSource = 1; // TSC_PROXY_CREDS_MODE_SMARTCARD
					}
					if ((Force & ConnectionInfo.Force.NoCredentials) != ConnectionInfo.Force.NoCredentials)
					{
						if (_connectionInfo.RDGatewayUseConnectionCredentials == RDGatewayUseConnectionCredentials.Yes)
						{
							var userName = GetUserName(_connectionInfo?.Username ?? "");
							var domain = GetDomain(_connectionInfo?.Domain ?? "");

							_rdpClient.TransportSettings2.GatewayUsername = userName;
							_rdpClient.TransportSettings2.GatewayDomain = domain;
							_rdpClient.TransportSettings2.GatewayPassword = GetPassword((_connectionInfo?.Password ?? ""), userName, domain, _connectionInfo.Hostname);
						}
						else if (_connectionInfo.RDGatewayUseConnectionCredentials == RDGatewayUseConnectionCredentials.SmartCard)
						{
							_rdpClient.TransportSettings2.GatewayCredSharing = 0;
						}
						else
						{
							var userName = GetUserName(_connectionInfo.RDGatewayUsername);
							var domain = GetDomain(_connectionInfo.RDGatewayDomain);

							_rdpClient.TransportSettings2.GatewayUsername = userName;
							_rdpClient.TransportSettings2.GatewayDomain = domain;
							_rdpClient.TransportSettings2.GatewayPassword = GetPassword(_connectionInfo.RDGatewayPassword, userName, domain, _connectionInfo.Hostname);
							_rdpClient.TransportSettings2.GatewayCredSharing = 0;
						}
					}
				}
			}
			catch (Exception ex)
			{
				Runtime.MessageCollector.AddExceptionMessage(Language.strRdpSetGatewayFailed, ex);
			}
		}
				
		private void SetUseConsoleSession()
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
					value = _connectionInfo.UseConsoleSession;
				}
						
				Runtime.MessageCollector.AddMessage(MessageClass.DebugMsg, string.Format(Language.strRdpSetConsoleSwitch, _rdpVersion), true);
				_rdpClient.AdvancedSettings7.ConnectToAdministerServer = value;
            }
			catch (Exception ex)
			{
				Runtime.MessageCollector.AddExceptionMessage(Language.strRdpSetConsoleSessionFailed, ex);
			}
		}

		private string GetUserName(string userName)
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
		private string GetDomain(string domain)
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
		private string GetPassword(string password, string userName, string domain, string host)
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

		private void SetCredentials()
		{
			try
			{
				if ((Force & ConnectionInfo.Force.NoCredentials) == ConnectionInfo.Force.NoCredentials)
				{
					return;
				}

				var userName = GetUserName(_connectionInfo?.Username ?? "");
				var domain = GetDomain(_connectionInfo?.Domain ?? "");

				_rdpClient.Domain = domain;
				_rdpClient.UserName = userName;
				_rdpClient.AdvancedSettings2.ClearTextPassword = GetPassword((_connectionInfo?.Password ?? ""), userName, domain, _connectionInfo.Hostname);

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
			Screen.FromControl(Control ?? (Control)InterfaceControl ?? _frmMain);

		private void SetResolution()
		{
			try
			{
				if ((Force & ConnectionInfo.Force.Fullscreen) == ConnectionInfo.Force.Fullscreen)
				{
					_rdpClient.FullScreen = true;
                    _rdpClient.DesktopWidth = FullscreenScreen.Bounds.Width;
                    _rdpClient.DesktopHeight = FullscreenScreen.Bounds.Height;
							
					return;
				}
						
				if ((InterfaceControl.Info.Resolution == RDPResolutions.FitToWindow) || (InterfaceControl.Info.Resolution == RDPResolutions.SmartSize))
				{
					_rdpClient.DesktopWidth = InterfaceControl.Size.Width;
					_rdpClient.DesktopHeight = InterfaceControl.Size.Height;

				    if (InterfaceControl.Info.Resolution == RDPResolutions.SmartSize)
				    {
                        _rdpClient.AdvancedSettings2.SmartSizing = true;
                    }

                    
				}
				else if (InterfaceControl.Info.Resolution == RDPResolutions.Fullscreen)
				{
					_rdpClient.FullScreen = true;
                    _rdpClient.DesktopWidth = FullscreenScreen.Bounds.Width;
                    _rdpClient.DesktopHeight = FullscreenScreen.Bounds.Height;
				}
				else
				{
					var resolution = GetResolutionRectangle(_connectionInfo.Resolution);
					_rdpClient.DesktopWidth = resolution.Width;
					_rdpClient.DesktopHeight = resolution.Height;
				}
			}
			catch (Exception ex)
			{
				Runtime.MessageCollector.AddExceptionMessage(Language.strRdpSetResolutionFailed, ex);
			}
		}
				
		private void SetPort()
		{
			try
			{
				if (_connectionInfo.Port != (int)Defaults.Port)
				{
					_rdpClient.AdvancedSettings2.RDPPort = _connectionInfo.Port;
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
		private void SetRedirection()
		{
			TrySetRedirection("drives", () => _rdpClient.AdvancedSettings2.RedirectDrives = _connectionInfo.RedirectDiskDrives);
			TrySetRedirection("ports", () => _rdpClient.AdvancedSettings2.RedirectPorts = _connectionInfo.RedirectPorts);
			TrySetRedirection("printers", () => _rdpClient.AdvancedSettings2.RedirectPrinters = _connectionInfo.RedirectPrinters);
			TrySetRedirection("smart cards", () => _rdpClient.AdvancedSettings2.RedirectSmartCards = _connectionInfo.RedirectSmartCards);
			TrySetRedirection("clipboard", () => _rdpClient.AdvancedSettings6.RedirectClipboard = _connectionInfo.RedirectClipboard);
			TrySetRedirection("sound", () => _rdpClient.SecuredSettings2.AudioRedirectionMode = (int)_connectionInfo.RedirectSound);
		}

		private void TrySetRedirection(string what, Action set)
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
				
		private void SetPerformanceFlags()
		{
			try
			{
				var pFlags = 0;
				if (_connectionInfo.DisplayThemes == false)
				{
					pFlags += Convert.ToInt32(RDPPerformanceFlags.DisableThemes);
				}
						
				if (_connectionInfo.DisplayWallpaper == false)
				{
					pFlags += Convert.ToInt32(RDPPerformanceFlags.DisableWallpaper);
				}
						
				if (_connectionInfo.EnableFontSmoothing)
				{
					pFlags += Convert.ToInt32(RDPPerformanceFlags.EnableFontSmoothing);
				}
						
				if (_connectionInfo.EnableDesktopComposition)
				{
					pFlags += Convert.ToInt32(RDPPerformanceFlags.EnableDesktopComposition);
				}
						
				_rdpClient.AdvancedSettings2.PerformanceFlags = pFlags;
			}
			catch (Exception ex)
			{
				Runtime.MessageCollector.AddExceptionMessage(Language.strRdpSetPerformanceFlagsFailed, ex);
			}
		}
				
		private void SetAuthenticationLevel()
		{
			try
			{
				_rdpClient.AdvancedSettings5.AuthenticationLevel = (uint)_connectionInfo.RDPAuthenticationLevel;
			}
			catch (Exception ex)
			{
				Runtime.MessageCollector.AddExceptionMessage(Language.strRdpSetAuthenticationLevelFailed, ex);
			}
		}
				
		private void SetLoadBalanceInfo()
		{
			if (string.IsNullOrEmpty(_connectionInfo.LoadBalanceInfo))
			{
				return;
			}
			try
			{
			    _rdpClient.AdvancedSettings2.LoadBalanceInfo = LoadBalanceInfoUseUtf8
                    ? new AzureLoadBalanceInfoEncoder().Encode(_connectionInfo.LoadBalanceInfo) 
                    : _connectionInfo.LoadBalanceInfo;
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

		    if (_rdpClient == null)
		    {
		        _leaveFullscreenEvent = null;
		        return;
		    }

		    // Before unadvising: if the session is still negotiating, re-advise minimal sinks that do
		    // nothing but record that it has settled. Otherwise the unadvise below would leave a
		    // control parked mid-logon with no way of ever learning that it had finished.
		    WatchForTheSessionToSettle();

		    // Unadvise the rest, so nothing below can call back into us.
		    try
		    {
		        _rdpClient.OnConnecting -= RDPEvent_OnConnecting;
		        _rdpClient.OnConnected -= RDPEvent_OnConnected;
		        _rdpClient.OnLoginComplete -= RDPEvent_OnLoginComplete;
		        _rdpClient.OnFatalError -= RDPEvent_OnFatalError;
		        _rdpClient.OnDisconnected -= RDPEvent_OnDisconnected;
		        _rdpClient.OnLeaveFullScreenMode -= RDPEvent_OnLeaveFullscreenMode;
		        _rdpClient.OnIdleTimeoutNotification -= RDPEvent_OnIdleTimeoutNotification;
		    }
		    catch (Exception ex)
		    {
		        Runtime.MessageCollector.AddMessage(MessageClass.DebugMsg,
		            $"Couldn't unsubscribe RDP event handlers: {ex.Message}");
		    }

		    var stopwatch = Stopwatch.StartNew();

		    var connectedState = QueryConnectedState();

		    // Stop the auto-reconnect machinery before the control is released. Connect() turns
		    // EnableAutoReconnect on with MaxReconnectAttempts = 5, and releasing a session that is
		    // mid-reconnect makes mstscax unwind every remaining attempt first - which is how
		    // closing one tab blocked the UI for 42 seconds while twelve others took ~120 ms.
		    try
		    {
		        _rdpClient.AdvancedSettings3.EnableAutoReconnect = false;
		        _rdpClient.AdvancedSettings3.MaxReconnectAttempts = 0;
		    }
		    catch (Exception ex)
		    {
		        Runtime.MessageCollector.AddMessage(MessageClass.DebugMsg,
		            $"Couldn't disable RDP auto-reconnect before close: {ex.Message}");
		    }

		    // Bound the graceful-shutdown wait as well. The tab is going away either way.
		    try { _rdpClient.AdvancedSettings2.shutdownTimeout = ShutdownTimeoutSecondsOnClose; }
		    catch (Exception) { /* not fatal */ }

		    WaitUntilTheControlIsReadyToBeDestroyed();

		    LogClose($"RDP client released (Connected was {connectedState}, now {QueryConnectedState()}) " +
		             $"at {stopwatch.ElapsedMilliseconds} ms");

		    _leaveFullscreenEvent = null;

		    // Keep the client only when the control is about to be parked: the probe below has to
		    // have something left to ask about the session. Otherwise the control is disposed in the
		    // next stage, so this is already the moment immediately before its release and the
		    // references can go now. On the parked path HostedControlPreDisposeHook does it instead.
		    if (_controlIsReadyToBeDestroyed)
		        ReleaseTheSessionsComReferences();
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
		    var disconnectAsked = false;

		    return () =>
		    {
		        if (QueryConnectedState() == 0)
		        {
		            StopWatchingForTheSessionToSettle();
		            return true;
		        }

		        if (!SessionHasSettled)
		            return false;

		        if (disconnectAsked)
		            return false;

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
		private MSTSCLib.IMsTscAxEvents_OnLoginCompleteEventHandler _settledOnLoginComplete;
		private MSTSCLib.IMsTscAxEvents_OnDisconnectedEventHandler _settledOnDisconnected;
		private MSTSCLib.IMsTscAxEvents_OnFatalErrorEventHandler _settledOnFatalError;

		/// <summary>
		/// Keeps the three sinks that say how a session ended advised past the teardown's unadvise,
		/// so a control parked mid-logon can still learn that it has settled.
		/// </summary>
		private void WatchForTheSessionToSettle()
		{
		    if (SessionHasSettled)
		        return;

		    _settledOnLoginComplete = () => _sessionHasSettled = true;
		    _settledOnDisconnected = _ => _sessionHasSettled = true;
		    _settledOnFatalError = _ => _sessionHasSettled = true;

		    try
		    {
		        _rdpClient.OnLoginComplete += _settledOnLoginComplete;
		        _rdpClient.OnDisconnected += _settledOnDisconnected;
		        _rdpClient.OnFatalError += _settledOnFatalError;
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
		        return;

		    try { _rdpClient.OnLoginComplete -= _settledOnLoginComplete; } catch (Exception) { /* already gone */ }
		    try { _rdpClient.OnDisconnected -= _settledOnDisconnected; } catch (Exception) { /* already gone */ }
		    try { _rdpClient.OnFatalError -= _settledOnFatalError; } catch (Exception) { /* already gone */ }

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
		    if (_rdpClient == null)
		        return;

		    // Must come first: these sinks are advised on the client, and unadvising through a
		    // released RCW would throw.
		    StopWatchingForTheSessionToSettle();

		    // Fetching each one hands back the RCW that already exists for it, so nothing here adds a
		    // reference that is not given back on the next line. A control that answers none of them -
		    // there is no such control at the v11 floor, but a teardown is not the place to find out -
		    // is logged and skipped.
		    foreach (var (name, fetch) in new (string, Func<object>)[]
		             {
		                 ("AdvancedSettings", () => _rdpClient.AdvancedSettings8),
		                 ("SecuredSettings", () => _rdpClient.SecuredSettings2),
		                 ("TransportSettings", () => _rdpClient.TransportSettings2)
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
		private void WaitUntilTheControlIsReadyToBeDestroyed()
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

		    var handshakeSatisfied = RunTheCloseHandshake(stopwatch);
		    var sessionIsDown = WaitForTheSessionToGoDown(stopwatch);

		    _controlIsReadyToBeDestroyed = handshakeSatisfied && sessionIsDown;
		}

		/// <summary>
		/// Asks the control's permission to destroy it, and waits if it says to.
		/// </summary>
		/// <returns>False only if it asked us to wait and then never came back.</returns>
		private bool RunTheCloseHandshake(Stopwatch stopwatch)
		{
		    ControlCloseStatus status;
		    try
		    {
		        status = _rdpClient.RequestClose();
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

		    var ready = false;
		    MSTSCLib.IMsTscAxEvents_OnDisconnectedEventHandler onDisconnected = _ => ready = true;
		    MSTSCLib.IMsTscAxEvents_OnConfirmCloseEventHandler onConfirmClose = () => ready = true;

		    try
		    {
		        _rdpClient.OnDisconnected += onDisconnected;
		        _rdpClient.OnConfirmClose += onConfirmClose;

		        PumpUntil(() => ready, TimeSpan.FromSeconds(ShutdownTimeoutSecondsOnClose), stopwatch);
		    }
		    catch (Exception ex)
		    {
		        LogClose($"Waiting for the control to be ready threw {ex.GetType().Name}: {ex.Message}");
		    }
		    finally
		    {
		        try { _rdpClient.OnDisconnected -= onDisconnected; } catch (Exception) { /* already gone */ }
		        try { _rdpClient.OnConfirmClose -= onConfirmClose; } catch (Exception) { /* already gone */ }
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
		        return true;

		    // Disconnect explicitly so the teardown happens here, where it is measured and where we
		    // can wait it out, rather than inside Dispose where it is neither. Only ever reached
		    // once the session has settled: this call is unbounded, and interrupting a handshake
		    // still in flight is what makes it take its time.
		    DisconnectTheSession();

		    var down = PumpUntil(() => QueryConnectedState() == 0, SessionDownTimeout, stopwatch);

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
		    var deadline = stopwatch.Elapsed + timeout;
		    while (stopwatch.Elapsed < deadline)
		    {
		        if (condition())
		            return true;

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
				return;

			Runtime.MessageCollector.AddMessage(MessageClass.DebugMsg,
				$"Connecting overlay [{SessionLabel}]: removed ({reason})", true);

			_connectingOverlay.Remove();
			_connectingOverlay = null;
		}

		private void SetEventHandlers()
		{
			try
			{
				_rdpClient.OnConnecting += RDPEvent_OnConnecting;
				_rdpClient.OnConnected += RDPEvent_OnConnected;
				_rdpClient.OnLoginComplete += RDPEvent_OnLoginComplete;
				_rdpClient.OnFatalError += RDPEvent_OnFatalError;
				_rdpClient.OnDisconnected += RDPEvent_OnDisconnected;
				_rdpClient.OnLeaveFullScreenMode += RDPEvent_OnLeaveFullscreenMode;
                _rdpClient.OnIdleTimeoutNotification += RDPEvent_OnIdleTimeoutNotification;
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
            // As in RDPEvent_OnDisconnected: leave the OCX's event sink before tearing the control
            // down. The message box matters too - pumping a modal loop from inside a COM callback
            // is its own hazard.
            PostToUiThread(() =>
            {
                Close(); //Simply close the RDP Session if the idle timeout has been triggered.

                string message = "The " + _connectionInfo.Name + " session was disconnected due to inactivity";
                Runtime.MessageCollector.AddMessage(MessageClass.InformationMsg, message, true);

                if (!_alertOnIdleDisconnect) return;

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
			Event_ErrorOccured(this, Convert.ToString(errorCode));
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
		private static string DescribeIntentionalDisconnect(ExtendedDisconnectReasonCode extendedReason)
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

		private void RDPEvent_OnDisconnected(int discReason)
		{
			_sessionConnected = false;

			const int UI_ERR_NORMAL_DISCONNECT = 0xB08;

			// First, whatever follows. A session that never came up must not be left behind a
			// permanent "Connecting...", and the reconnect UI below is drawn where the overlay is.
			HideTheConnectingOverlay($"disconnected, discReason={discReason}");

		    var extendedReason = ExtendedDisconnectReasonCode.exDiscReasonNoInfo;
		    try { extendedReason = _rdpClient.ExtendedDisconnectReason; }
		    catch (Exception) { /* the OCX may already be unusable */ }

		    // These two codes are the only way to tell a clean logoff from a real failure.
		    Runtime.MessageCollector.AddMessage(MessageClass.InformationMsg,
		        $"RDP disconnect: discReason={discReason} (0x{discReason:X}), " +
		        $"ExtendedDisconnectReason={(int)extendedReason} ({extendedReason})", true);

			if (discReason != UI_ERR_NORMAL_DISCONNECT)
			{
				var reason = DescribeIntentionalDisconnect(extendedReason)
				             ?? _rdpClient.GetErrorDescription((uint)discReason, (uint)extendedReason);
				Event_Disconnected(this, discReason + "\r\n" + reason);
			}

			if (Settings.Default.ReconnectOnDisconnect)
			{
				// Through ReplaceReconnectGroup: a flapping server arrives here again and again, and
				// the group being replaced owns a running animation timer.
				ReplaceReconnectGroup(new ReconnectGroup());
				ReconnectGroup.CloseClicked += Event_ReconnectGroupCloseClicked;
				ReconnectGroup.Left = (int) ((double) Control.Width / 2 - (double) ReconnectGroup.Width / 2);
				ReconnectGroup.Top = (int) ((double) Control.Height / 2 - (double) ReconnectGroup.Height / 2);
				ReconnectGroup.Parent = Control;
				ReconnectGroup.Show();
				tmrReconnect.Enabled = true;
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
			Event_Connecting(this);
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
			TryApplyDisplayScale(Control?.DeviceDpi ?? RdpDisplayScale.DefaultDpi,
			                     DisplayScalePass.SessionCreated);

			Event_Connected(this);
		}
				
		private void RDPEvent_OnLoginComplete()
		{
			_loginComplete = true;

			// Belt and braces. OnConnected should already have taken it away; a session that
			// somehow reached logon without it must not stay covered.
			HideTheConnectingOverlay("login complete");

			// Belt and braces here too. OnConnected has already asked for this scale, and for a
			// session this connection created that is the pass that counts. This one answers the two
			// cases it cannot: a reconnect into a session whose desktop was already up, and a host
			// that was not yet in a state to act on the earlier pass. It deliberately does not consult
			// the cache - the values are the same ones - and it does not nudge the resolution, because
			// by now there is a desktop that would visibly reflow twice.
			TryApplyDisplayScale(Control?.DeviceDpi ?? RdpDisplayScale.DefaultDpi,
			                     DisplayScalePass.LoginComplete);
		}
				
		private void RDPEvent_OnLeaveFullscreenMode()
		{
			Fullscreen = false;
            _leaveFullscreenEvent?.Invoke(this, new EventArgs());
        }
        #endregion
		
        #region Public Events & Handlers
		public delegate void LeaveFullscreenEventHandler(object sender, EventArgs e);
		private LeaveFullscreenEventHandler _leaveFullscreenEvent;
				
		public event LeaveFullscreenEventHandler LeaveFullscreen
		{
			add
			{
				_leaveFullscreenEvent = (LeaveFullscreenEventHandler)Delegate.Combine(_leaveFullscreenEvent, value);
			}
			remove
			{
				_leaveFullscreenEvent = (LeaveFullscreenEventHandler)Delegate.Remove(_leaveFullscreenEvent, value);
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
			[Description("strRDPDisableWallpaper")]DisableWallpaper = 0x1,
//			[Description("strRDPDisableFullWindowdrag")]DisableFullWindowDrag = 0x2,
//			[Description("strRDPDisableMenuAnimations")]DisableMenuAnimations = 0x4,
			[Description("strRDPDisableThemes")]DisableThemes = 0x8,
//			[Description("strRDPDisableCursorShadow")]DisableCursorShadow = 0x20,
//			[Description("strRDPDisableCursorblinking")]DisableCursorBlinking = 0x40,
            [Description("strRDPEnableFontSmoothing")]EnableFontSmoothing = 0x80,
			[Description("strRDPEnableDesktopComposition")]EnableDesktopComposition = 0x100
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
			string[] resolutionParts = null;
			if (resolution != RDPResolutions.FitToWindow & resolution != RDPResolutions.Fullscreen & resolution != RDPResolutions.SmartSize)
			{
				resolutionParts = resolution.ToString().Replace("Res", "").Split('x');
			}
			if (resolutionParts == null || resolutionParts.Length != 2)
			{
				return new Rectangle(0, 0, 0, 0);
			}
			else
			{
                return new Rectangle(0, 0, Convert.ToInt32(resolutionParts[0]), Convert.ToInt32(resolutionParts[1]));
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
		public void tmrReconnect_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
		{
		    try
		    {
			    var srvReady = IsPortOpen(_connectionInfo.Hostname, _connectionInfo.Port);

			    ReconnectGroup.ServerReady = srvReady;

			    if (ReconnectGroup.ReconnectWhenReady && srvReady)
			    {
				    tmrReconnect.Enabled = false;
				    ReconnectGroup.DisposeReconnectGroup();

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
				    SetDisplayScale();
				    _rdpClient.Connect();
			    }
		    }
		    catch (Exception ex)
		    {
                Runtime.MessageCollector.AddExceptionMessage(string.Format(Language.AutomaticReconnectError, _connectionInfo.Hostname),
                    ex, MessageClass.WarningMsg, false);
		    }
		}

        private static bool IsPortOpen(string hostname, int port)
        {
            try
            {
                using (var client = new TcpClient())
                {
                    var result = client.BeginConnect(hostname, port, null, null);
                    var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(500));
                    if (!success) return false;
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
}
