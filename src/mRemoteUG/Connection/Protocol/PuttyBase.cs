using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using mRemoteUG.App;
using mRemoteUG.Messages;
using mRemoteUG.Security;
using mRemoteUG.Tools;
using mRemoteUG.Tools.Cmdline;
// ReSharper disable ArrangeAccessorOwnerBody

namespace mRemoteUG.Connection.Protocol;

public class PuttyBase : ProtocolBase
{
    private const int IDM_RECONF = 0x50; // PuTTY Settings Menu ID
    private bool _isPuttyNg;

    #region Public Properties

    protected PuttyProtocolKind PuttyProtocol { private get; set; }

    protected PuttySshVersionKind PuttySSHVersion { private get; set; }

    public IntPtr PuttyHandle { get; set; }

    private Process? PuttyProcess { get; set; }

    public static string? PuttyPath { get; set; }

    public bool Focused
    {
        get { return NativeMethods.GetForegroundWindow() == PuttyHandle; }
    }

    #endregion

    #region Private Events & Handlers
    private void ProcessExited(object? sender, EventArgs e)
    {
        // Raising Closed directly used to close the tab while leaving everything else behind:
        // the hosted control and InterfaceControl were never disposed and the reconnect timer
        // kept running. Go through Close() so a session that ends on its own is torn down the
        // same way as one the user closes. Close() is idempotent and marshals to the UI thread,
        // which matters here because this runs on a Process.Exited thread-pool callback.
        Close();
    }
    #endregion

    #region Public Methods
    [SuppressMessage("Performance", "CA1863",
                     Justification = "The format is a localized resource read at run time; "
                                     + "a cached CompositeFormat would freeze the UI language.")]
    public override bool Connect()
    {
        try
        {
            string? unusableReason = PuttyPathProvider.GetUnusableReason();
            if (unusableReason != null)
            {
                Runtime.MessageCollector.AddMessage(MessageClass.ErrorMsg,
                    Language.strPuttyConnectionFailed + Environment.NewLine + unusableReason +
                    Environment.NewLine + Language.strPuttyConfigureHint);
                return false;
            }

            _isPuttyNg = PuttyTypeDetector.GetPuttyType() == PuttyTypeDetector.PuttyType.PuttyNg;

            var puttyProcess = new Process
            {
                StartInfo =
                {
                    UseShellExecute = false,
                    FileName = PuttyPath
                }
            };
            PuttyProcess = puttyProcess;

            InterfaceControl interfaceControl = AssignedInterfaceControl;
            var arguments = new CommandLineArguments();

            arguments.Add("-load", interfaceControl.Info.PuttySession);

            if (!(interfaceControl.Info is PuttySessionInfo))
            {
                arguments.Add("-" + PuttyProtocol);

                if (PuttyProtocol == PuttyProtocolKind.ssh)
                {
                    string username = "";
                    string password = "";

                    if (!string.IsNullOrEmpty(interfaceControl.Info.Username))
                    {
                        username = interfaceControl.Info.Username;
                    }
                    else
                    {
                        // ReSharper disable once SwitchStatementMissingSomeCases
                        switch (Settings.Default.EmptyCredentials)
                        {
                            case "windows":
                                username = Environment.UserName;
                                break;
                            case "custom":
                                username = Settings.Default.DefaultUsername;
                                break;
                        }
                    }

                    if (!string.IsNullOrEmpty(interfaceControl.Info.Password))
                    {
                        password = interfaceControl.Info.Password;
                    }
                    else
                    {
                        if (Settings.Default.EmptyCredentials == "custom")
                        {
                            password = DefaultCredentials.Password;
                        }
                    }

                    arguments.Add("-" + (int)PuttySSHVersion);

                    if (((int)Force & (int)ConnectionInfo.Force.NoCredentials)
                        != (int)ConnectionInfo.Force.NoCredentials)
                    {
                        if (!string.IsNullOrEmpty(username))
                        {
                            arguments.Add("-l", username);
                        }
                        if (!string.IsNullOrEmpty(password))
                        {
                            arguments.Add("-pw", password);
                        }
                    }
                }

                arguments.Add("-P", interfaceControl.Info.Port.ToString(CultureInfo.InvariantCulture));
                arguments.Add(interfaceControl.Info.Hostname);
            }

            if (_isPuttyNg)
            {
                arguments.Add("-hwndparent", interfaceControl.Handle.ToString(CultureInfo.InvariantCulture));
            }

            puttyProcess.StartInfo.Arguments = arguments.ToString();

            puttyProcess.EnableRaisingEvents = true;
            puttyProcess.Exited += ProcessExited;

            puttyProcess.Start();
            puttyProcess.WaitForInputIdle(Settings.Default.MaxPuttyWaitTime * 1000);

            // The probe differs by build: PuTTYNG creates its window as a child of the panel
            // named in -hwndparent, stock PuTTY as a top-level window of its own.
            Func<IntPtr> probe;
            if (_isPuttyNg)
            {
                probe = () => NativeMethods.FindWindowEx(interfaceControl.Handle, IntPtr.Zero, null, null);
            }
            else
            {
                probe = () =>
                {
                    puttyProcess.Refresh();
                    return puttyProcess.MainWindowHandle;
                };
            }

            PuttyHandle = HostedPuttyWindow.WaitForHandle(
                probe, TimeSpan.FromSeconds(Settings.Default.MaxPuttyWaitTime));

            if (PuttyHandle == IntPtr.Zero)
            {
                // This used to fall through: SetParent(0, ...) failed silently and the tab
                // opened empty while PuTTY ran on its own somewhere.
                throw new TimeoutException(
                    $"PuTTY showed no window within {Settings.Default.MaxPuttyWaitTime}s " +
                    "(Options > Advanced > PuTTY wait time).");
            }

            if (!_isPuttyNg)
            {
                // Stock PuTTY arrives as a top-level window with a caption and a frame, and it
                // puts them back on every SIZE_RESTORED - see HostedPuttyWindow.Adopt for why
                // the answer is to maximise it rather than to strip them.
                HostedPuttyWindow.Adopt(PuttyHandle, interfaceControl.Handle);
            }

            // One line rather than four: these are four parts of the same fact, and they cost
            // four Information entries per PuTTY session in every log.
            Runtime.MessageCollector.AddMessage(MessageClass.InformationMsg,
                string.Join(", ",
                    Language.strPuttyStuff,
                    string.Format(CultureInfo.CurrentCulture, Language.strPuttyHandle, PuttyHandle),
                    string.Format(CultureInfo.CurrentCulture, Language.strPuttyTitle, puttyProcess.MainWindowTitle),
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Language.strPuttyParentHandle,
                        interfaceControl.Parent?.Handle)),
                true);

            Resize(this, new EventArgs());
            LogHostedWindow();
            base.Connect();
            return true;
        }
        catch (Exception ex)
        {
            Runtime.MessageCollector.AddMessage(
                MessageClass.ErrorMsg,
                Language.strPuttyConnectionFailed + Environment.NewLine + ex.Message);
            return false;
        }
    }

    /// <summary>True: <see cref="HostedPuttyWindow.Focus"/> makes PuTTY's window the foreground window.</summary>
    public override bool FocusTakesTheForeground => true;

    public override void Focus()
    {
        try
        {
            if (ConnectionWindow is { InTabDrag: true })
            {
                return;
            }
            HostedPuttyWindow.Focus(PuttyHandle);
        }
        catch (Exception ex)
        {
            Runtime.MessageCollector.AddMessage(
                MessageClass.ErrorMsg,
                Language.strPuttyFocusFailed + Environment.NewLine + ex.Message,
                true);
        }
    }

    /// <summary>
    /// Records how Windows is hosting the PuTTY window: its DPI awareness, whether it is
    /// still the chromeless, maximised child it was made, and whether the window Windows
    /// actually produced is the size that was asked for.
    /// </summary>
    /// <remarks>
    /// PuTTY's own manifest decides the awareness and we do not control it. An unaware or
    /// system-aware build hosted inside a per-monitor-aware window is bitmap-scaled by Windows,
    /// which is correct in size and soft in appearance; that is inherent to mixed-mode hosting
    /// and is not a fault to be fixed by making this process unaware again.
    /// <para>
    /// The chrome readback runs after the first Resize, which is the first chance PuTTY has
    /// had to put its title bar back. A caption at that point is a Warning: it is the defect
    /// 890b245 shipped, and this line is the only way to see it from a log file on another
    /// machine.
    /// </para>
    /// <para>
    /// The actual-rect readback exists for one unreproduced report: after undocking a laptop
    /// from a 200% external monitor onto its own 150% panel, a newly opened stock PuTTY session
    /// filled only about two thirds of the panel, while sessions already open at the time of
    /// the undock stayed correctly sized. <see cref="mRemoteUG.App.NativeMethods.GetWindowRect"/>, called
    /// from this (per-monitor-aware) thread, is not DPI-virtualized, so comparing it against
    /// the panel size told <see cref="HostedPuttyWindow.Adopt"/>'s <c>MoveWindow</c> distinguishes
    /// the two possible faults: Windows not honouring the requested size for a lower-than-PMv2
    /// -aware child (actual rect smaller than the panel), versus the panel itself already being
    /// wrong when this ran (actual rect matches the panel, both wrong). Nothing here fixes
    /// either - there is no multi-monitor, mixed-DPI hardware on this machine to reproduce it
    /// on - this is only the readback that will say which one it was, the next time it happens.
    /// </para>
    /// </remarks>
    private void LogHostedWindow()
    {
        try
        {
            if (PuttyHandle == IntPtr.Zero)
            {
                return;
            }

            InterfaceControl interfaceControl = AssignedInterfaceControl;
            NativeMethods.DpiAwareness awareness = NativeMethods.GetAwarenessFromDpiAwarenessContext(
                NativeMethods.GetWindowDpiAwarenessContext(PuttyHandle));
            HostedWindowState state = HostedPuttyWindow.Describe(PuttyHandle);

            string actualRectDescription;
            if (NativeMethods.GetWindowRect(PuttyHandle, out NativeMethods.RECT actualRect))
            {
                int actualWidth = actualRect.right - actualRect.left;
                int actualHeight = actualRect.bottom - actualRect.top;
                var screen = Screen.FromHandle(PuttyHandle);
                actualRectDescription =
                    $"actual {actualWidth}x{actualHeight} on screen {screen.DeviceName} " +
                    $"(bounds {screen.Bounds.Width}x{screen.Bounds.Height}, " +
                    $"work area {screen.WorkingArea.Width}x{screen.WorkingArea.Height})";

                if (actualWidth != interfaceControl.Width || actualHeight != interfaceControl.Height)
                {
                    Runtime.MessageCollector.AddMessage(MessageClass.WarningMsg,
                        "PuTTY window size does not match the panel it was asked to fill: " +
                        $"asked for {interfaceControl.Width}x{interfaceControl.Height}, got " +
                        actualRectDescription + ".", true);
                }
            }
            else
            {
                actualRectDescription = "actual rect unavailable (GetWindowRect failed)";
            }

            Runtime.MessageCollector.AddMessage(MessageClass.DebugMsg,
                $"PuTTY window hosting: child awareness {awareness}, child dpi " +
                $"{NativeMethods.GetDpiForWindow(PuttyHandle)}, host dpi " +
                $"{NativeMethods.GetDpiForWindow(interfaceControl.Handle)}, " +
                $"panel {interfaceControl.Width}x{interfaceControl.Height}, {actualRectDescription}, " +
                $"{state}.", true);

            if (!_isPuttyNg && state.HasCaption)
            {
                Runtime.MessageCollector.AddMessage(MessageClass.WarningMsg,
                    "PuTTY reinstated its title bar; the hosted window is not maximised. " + state, true);
            }
        }
        catch (Exception ex)
        {
            // A failure, not a fact: reporting it as Information is why nobody ever noticed it
            // happening.
            Runtime.MessageCollector.AddMessage(MessageClass.WarningMsg,
                "Could not read back the PuTTY window's hosting state" + Environment.NewLine + ex.Message, true);
        }
    }

    /// <summary>The window and the DPI PuTTY was last told, so that two routes send it once.</summary>
    private (IntPtr Window, int Dpi) _dpiToldToPutty;

    /// <summary>
    /// Tells the PuTTY window the DPI its Tab is now shown at, so it rebuilds its fonts for it.
    /// </summary>
    /// <remarks>
    /// Called from both places that hear of a DPI change: the panel's own
    /// <see cref="NotifyDpiChanged"/>, and <c>FrmMain</c>'s follow-up once the change has
    /// settled. Either may be the one that arrives, so whichever comes second for the same DPI
    /// sends nothing: PuTTY would only rebuild the fonts it already has. Nothing is known about
    /// the DPI PuTTY started with, so the first change for a window is always sent.
    /// <para>
    /// Logged, because the font size is visible only on a machine with two monitors at
    /// different scales, and the development machine has one.
    /// </para>
    /// </remarks>
    internal void TellPuttyItsDpi(int dpi, string route)
    {
        try
        {
            if (PuttyHandle == IntPtr.Zero || dpi <= 0 || _dpiToldToPutty == (PuttyHandle, dpi))
            {
                return;
            }

            string? failure = HostedPuttyWindow.TellDpi(PuttyHandle, dpi);
            if (failure == null)
            {
                _dpiToldToPutty = (PuttyHandle, dpi);
            }

            Runtime.MessageCollector.AddMessage(
                failure == null ? MessageClass.DebugMsg : MessageClass.WarningMsg,
                $"PuTTY DPI [{SessionLabel}]: told PuTTY {dpi} DPI from the {route}" +
                (failure == null ? "." : $", and it did not take it: {failure}."),
                true);
        }
        catch (Exception ex)
        {
            Runtime.MessageCollector.AddMessage(MessageClass.WarningMsg,
                $"Could not tell PuTTY its new DPI ({dpi})" + Environment.NewLine + ex.Message, true);
        }
    }

    /// <summary>
    /// A DPI change resizes nothing by itself, so the hosted window has to be repositioned,
    /// and PuTTY has to be told, because Windows does not tell it (see <see cref="TellPuttyItsDpi"/>).
    /// </summary>
    public override void NotifyDpiChanged(int dpi)
    {
        base.NotifyDpiChanged(dpi);
        Resize(this, EventArgs.Empty);
        TellPuttyItsDpi(dpi, "connection window");
    }

    /// <summary>Set between the main window's <c>ResizeBegin</c> and <c>ResizeEnd</c>.</summary>
    private bool _inResizeDrag;

    /// <summary>Set while a <see cref="InterfaceControl_SizeChanged"/> follow-up is queued.</summary>
    private bool _panelResizePending;

    public override bool Initialize()
    {
        if (!base.Initialize())
        {
            return false;
        }

        // No unsubscribe: the panel is disposed in the teardown, and holds this protocol anyway.
        AssignedInterfaceControl.SizeChanged += InterfaceControl_SizeChanged;
        return true;
    }

    public override void ResizeBegin(object? sender, EventArgs e)
    {
        _inResizeDrag = true;
    }

    /// <summary>
    /// Catches up with a panel size change that <see cref="InterfaceControl_SizeChanged"/>
    /// left alone during the drag.
    /// </summary>
    public override void ResizeEnd(object? sender, EventArgs e)
    {
        _inResizeDrag = false;
        if (PuttyHandle != IntPtr.Zero && InterfaceControl is not null)
        {
            Resize(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Moves the PuTTY window to its panel's size once the current message is done with it.
    /// </summary>
    /// <remarks>
    /// The connection window's <c>Resize</c> and the DPI change are not enough. Measured for RDP
    /// on a dock from 150% to 200% (see <c>RdpProtocol.FollowHostedControlSize</c>): the DPI
    /// follow-up re-measures the tab strip for the larger font after the DPI change has been
    /// handled, 12 px taller, so the Tab page shrinks while the connection window keeps its size.
    /// PuTTY's window kept the panel size from before, clipping its last row (an uncovered strip,
    /// undocking) until the connection window was next resized.
    /// <para>
    /// Posted, so the in-between sizes of one layout pass collapse into the one it settles at.
    /// Nothing is done during a drag: the connection window's <c>Resize</c> already moves PuTTY
    /// at every step, and <see cref="ResizeEnd"/> catches up with anything it missed.
    /// </para>
    /// </remarks>
    private void InterfaceControl_SizeChanged(object? sender, EventArgs e)
    {
        if (_panelResizePending || _inResizeDrag || sender is not Control panel || !panel.IsHandleCreated)
        {
            return;
        }

        _panelResizePending = true;
        panel.BeginInvoke(() =>
        {
            _panelResizePending = false;
            if (panel.IsDisposed || _inResizeDrag || InterfaceControl is null || PuttyHandle == IntPtr.Zero)
            {
                return;
            }

            Size connectionWindow = ConnectionWindow?.ClientSize ?? Size.Empty;
            Runtime.MessageCollector.AddMessage(MessageClass.DebugMsg,
                $"Resize [{SessionLabel}]: following the PuTTY panel at {panel.Width}x{panel.Height} " +
                $"(connection window {connectionWindow.Width}x{connectionWindow.Height}).", true);
            Resize(this, EventArgs.Empty);
        });
    }

    public override void Resize(object? sender, EventArgs e)
    {
        try
        {
            InterfaceControl interfaceControl = AssignedInterfaceControl;
            if (interfaceControl.Size == Size.Empty)
            {
                return;
            }

            // Both branches are the same. Stock PuTTY used to be positioned at negative
            // offsets so its caption and frame fell outside the panel, with the offsets
            // computed from SystemInformation.CaptionHeight, FrameBorderSize and the resize
            // border thicknesses. Every one of those wraps GetSystemMetrics, which under a
            // per-monitor-aware thread answers for the *system* DPI - so on any monitor that is
            // not at the system scale the chrome was mispositioned by the difference.
            //
            // Now there is no chrome to hide: PuTTYNG never had any, and stock PuTTY is hosted
            // maximised (HostedPuttyWindow.Adopt), which is the one state in which it leaves
            // its caption off. Moving a maximised window is allowed and does not restore it.
            NativeMethods.MoveWindow(PuttyHandle, 0, 0, interfaceControl.Width, interfaceControl.Height, true);
        }
        catch (Exception ex)
        {
            Runtime.MessageCollector.AddMessage(
                MessageClass.ErrorMsg,
                Language.strPuttyResizeFailed + Environment.NewLine + ex.Message,
                true);
        }
    }

    /// <summary>
    /// Kills and releases the PuTTY process.
    /// </summary>
    /// <remarks>
    /// This used to be a Close() override that ran before base.Close(). Hanging it on the
    /// cleanup hook instead means it is covered by the base class's run-once guard and happens
    /// at the right point in the teardown - after handlers are detached, before the hosted
    /// control is disposed. Killing the process raises Exited, which calls Close() again; the
    /// guard absorbs that.
    /// </remarks>
    protected override void CleanupProtocolResources()
    {
        try
        {
            if (PuttyProcess?.HasExited == false)
            {
                PuttyProcess.Kill();
            }
        }
        catch (Exception ex)
        {
            Runtime.MessageCollector.AddMessage(
                MessageClass.ErrorMsg,
                Language.strPuttyKillFailed + Environment.NewLine + ex.Message,
                true);
        }

        try
        {
            if (PuttyProcess != null)
            {
                PuttyProcess.Exited -= ProcessExited;
                PuttyProcess.Dispose();
            }
        }
        catch (Exception ex)
        {
            Runtime.MessageCollector.AddMessage(
                MessageClass.ErrorMsg,
                Language.strPuttyDisposeFailed + Environment.NewLine + ex.Message,
                true);
        }

        PuttyProcess = null;
    }

    public void ShowSettingsDialog()
    {
        try
        {
            NativeMethods.PostMessage(PuttyHandle, NativeMethods.WM_SYSCOMMAND, (IntPtr)IDM_RECONF, (IntPtr)0);
            NativeMethods.SetForegroundWindow(PuttyHandle);
        }
        catch (Exception ex)
        {
            Runtime.MessageCollector.AddMessage(
                MessageClass.ErrorMsg,
                Language.strPuttyShowSettingsDialogFailed + Environment.NewLine + ex.Message,
                true);
        }
    }
    #endregion

    #region Enums

    protected enum PuttyProtocolKind
    {
        ssh = 0,
        telnet = 1,
        rlogin = 2,
        raw = 3,
        serial = 4
    }

    protected enum PuttySshVersionKind
    {
        ssh1 = 1,
        ssh2 = 2
    }
    #endregion
}
