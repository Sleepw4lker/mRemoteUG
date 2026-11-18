using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

using Microsoft.Win32;

using mRemoteUG.App;
using mRemoteUG.App.Info;
using mRemoteUG.App.Initialization;
using mRemoteUG.Config;
using mRemoteUG.Config.Connections;
using mRemoteUG.Config.DataProviders;
using mRemoteUG.Config.Putty;
using mRemoteUG.Config.Settings;
using mRemoteUG.Connection;
using mRemoteUG.Messages;
using mRemoteUG.Messages.MessageWriters;
using mRemoteUG.Tools;
using mRemoteUG.UI.Controls;
using mRemoteUG.UI.Menu;
using mRemoteUG.UI.Panels;
using mRemoteUG.UI.TaskDialog;
using mRemoteUG.UI.Window;

// ReSharper disable MemberCanBePrivate.Global

namespace mRemoteUG.UI.Forms;

public partial class FrmMain
{
    public static FrmMain Default { get; } = new FrmMain();

    private bool _inSizeMove;
    private bool _inMouseActivate;
    private int _activatingMouseMessage;
    // Set by a WM_WINDOWPOSCHANGED that held back focusing a session whose focus takes the
    // foreground (OnWindowPosChanged); cleared by a click activation or a deactivation.
    private bool _sessionFocusDeferred;
    private string? _connectionsFileName;
    private bool _showFullPathInTitle;
    private readonly ScreenSelectionSystemMenu _screenSystemMenu;
    private ConnectionInfo? _selectedConnection;
    private readonly IList<IMessageWriter> _messageWriters = new List<IMessageWriter>();

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal FullscreenHandler Fullscreen { get; set; }

    /// <summary>
    /// The main window static layout: the left tool column, the document area and
    /// the notifications strip.
    /// </summary>
    // Hides Control.Layout, the event, on purpose: nothing subscribes to it through FrmMain, and
    // every caller of this property means the window's layout.
    internal new MainLayout Layout { get; }

    /// <summary>
    /// The connection window whose selected Tab an activation focuses: the active document.
    /// Replaced by tests, which host a connection window outside the main window's layout.
    /// </summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal Func<ConnectionWindow?> ActiveConnectionWindow { get; set; }

    private FrmMain()
    {
        _showFullPathInTitle = Settings.Default.ShowCompleteConsPathInTitle;
        InitializeComponent();
        Fullscreen = new FullscreenHandler(this);

        Layout = new MainLayout(splitMain, splitLeft, splitDocuments, tabDocuments);
        Layout.ActiveDocumentChanged += Layout_ActiveDocumentChanged;
        ActiveConnectionWindow = () => Layout.ActiveDocument as ConnectionWindow;

        _screenSystemMenu = new ScreenSelectionSystemMenu(this);
    }

    #region Properties
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public FormWindowState PreviousWindowState { get; set; }

    public bool IsClosing { get; private set; }


    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? ConnectionsFileName
    {
        get => _connectionsFileName;
        set
        {
            if (_connectionsFileName == value)
            {
                return;
            }
            _connectionsFileName = value;
            UpdateWindowTitle();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ShowFullPathInTitle
    {
        get => _showFullPathInTitle;
        set
        {
            if (_showFullPathInTitle == value)
            {
                return;
            }
            _showFullPathInTitle = value;
            UpdateWindowTitle();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ConnectionInfo? SelectedConnection
    {
        get => _selectedConnection;
        set
        {
            if (_selectedConnection == value)
            {
                return;
            }
            _selectedConnection = value;
            UpdateWindowTitle();
        }
    }
    #endregion

    #region Startup & Shutdown
    [SuppressMessage("Style", "IDE1006", Justification = "The name is wired in FrmMain.Designer.cs.")]
    private void frmMain_Load(object sender, EventArgs e)
    {
        var startup = Stopwatch.StartNew();
        MessageCollector messageCollector = Runtime.MessageCollector;
        MessageCollectorSetup.SetupMessageCollector(messageCollector, _messageWriters);

        Startup.InitializeProgram(messageCollector);

        msMain.Location = Point.Empty;
        var settingsLoader = new SettingsLoader(this, messageCollector, _quickConnectToolStrip, msMain);
        settingsLoader.LoadSettings();

        SetMenuDependencies();

        // Has to follow HostToolWindows: the restore sets panel visibility, and there
        // is nothing to show or hide until the tool windows are in their panels. That
        // is also why it cannot live in SettingsLoader with the rest of the settings.
        Layout.HostToolWindows(AppWindows.TreeForm, AppWindows.ConfigForm, AppWindows.ErrorsForm);
        MainLayoutSettings.Restore(Layout, DeviceDpi);

        LockToolbarPositions(Settings.Default.LockToolbars);
        Settings.Default.PropertyChanged += OnApplicationSettingChanged;

        Runtime.WindowList = new WindowList();

        Runtime.ConnectionsService.ConnectionsLoaded += ConnectionsServiceOnConnectionsLoaded;
        Runtime.ConnectionsService.ConnectionsSaved += ConnectionsServiceOnConnectionsSaved;

        // The follow-up pass had only ever been reached from a DPI *change*, so a first launch
        // on a 144 DPI monitor left every ToolStrip on the process-wide default font and glyph
        // size until the window happened to be dragged somewhere else. It has to run after the
        // tool windows are in their panels, or the sweep cannot reach the strips they own, and
        // above the reveal below, or the toolbars visibly change size after the window appears.
        //
        // Called rather than posted, which is the opposite of what the two DPI hooks do. They
        // post because they fire mid-relayout with a font that is not settled yet; Load runs
        // after the auto-scale pass, so Font and DeviceDpi are already the final values here.
        ApplyDpiFollowUp(DeviceDpi);


        // The window is Opacity = 0 from the designer, so nothing above this line is
        // visible to the user. Everything that decides how the window *looks* has run by
        // now - settings, the tool windows, the restored layout, the menu text - so this
        // is the earliest point at which showing it is honest.
        //
        // What follows it does not shape the window, and some of it is slow in proportion
        // to the connection file: reading and parsing it, one PBKDF2 derivation per stored
        // password, enumerating PuTTY's saved sessions, and then PreviousSessionOpener,
        // which opens every connection that was open last time - RDP controls and all.
        // Run before the reveal, that is all black screen.
        Opacity = 1;
        startup.Stop();
        messageCollector.AddMessage(MessageClass.InformationMsg,
            $"Main window shown after {startup.ElapsedMilliseconds} ms; loading connections", true);

        // Posted rather than called: this returns to the message loop first, so the window
        // is painted before the load starts rather than after it.
        BeginInvoke(new Action(CompleteStartup));
    }

    /// <summary>
    /// The part of startup that runs once the main window is up.
    /// </summary>
    /// <remarks>
    /// Posted from <see cref="frmMain_Load"/>. It is on the UI thread like everything else
    /// here, so it still blocks interaction while it runs - what it no longer blocks is the
    /// window appearing at all. The try/catch is not decoration: an exception on a posted
    /// callback has no caller to reach, and the handler that would catch it - see
    /// <see cref="mRemoteUG.App.CrashLogger"/> - treats a UI-thread exception as fatal and ends
    /// the process. So without it a failure here would take the whole application down.
    /// </remarks>
    private void CompleteStartup()
    {
        var load = Stopwatch.StartNew();
        try
        {
            CredsAndConsSetup.LoadCredsAndCons();

            AppWindows.TreeForm.Focus();

            PuttySessionsManager.Instance.StartWatcher();

            _screenSystemMenu.BuildScreenList();
            SystemEvents.DisplaySettingsChanged += _screenSystemMenu.OnDisplayChanged;

            //Fix missing general panel at the first run
            if (Settings.Default.CreateEmptyPanelOnStartUp)
            {
                string panelName = !string.IsNullOrEmpty(Settings.Default.StartUpPanelName)
                    ? Settings.Default.StartUpPanelName
                    : Language.strNewPanel;

                if (!PanelAdder.DoesPanelExist(panelName))
                {
                    PanelAdder.AddPanel(panelName);
                }
            }

            Runtime.MessageCollector.AddMessage(MessageClass.InformationMsg,
                $"Startup complete in {load.ElapsedMilliseconds} ms", true);
        }
        catch (Exception ex)
        {
            Runtime.MessageCollector.AddExceptionMessage("Completing startup failed.", ex);
        }
    }


    private void OnApplicationSettingChanged(object? sender, PropertyChangedEventArgs propertyChangedEventArgs)
    {
        if (propertyChangedEventArgs.PropertyName != nameof(Settings.LockToolbars))
        {
            return;
        }

        LockToolbarPositions(Settings.Default.LockToolbars);
    }

    /// <summary>
    /// The hook a top-level window actually gets when it moves to a monitor at another scale.
    /// </summary>
    /// <remarks>
    /// Windows sends a top-level window <c>WM_DPICHANGED</c>, which WinForms raises here.
    /// <c>RescaleConstantsForDpi</c> is the child hook, driven by
    /// <c>WM_DPICHANGED_BEFOREPARENT</c>, and overriding it on this form left the sweep below
    /// never running at all - which is why the menus stayed at their old size through a first
    /// attempt at this fix. Both are overridden now: they route to the same idempotent pass, so
    /// it does not matter which of them fires, or whether both do.
    /// </remarks>
    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        ScheduleDpiFollowUp(e.DeviceDpiNew);
    }

    protected override void RescaleConstantsForDpi(int deviceDpiOld, int deviceDpiNew)
    {
        base.RescaleConstantsForDpi(deviceDpiOld, deviceDpiNew);
        ScheduleDpiFollowUp(deviceDpiNew);
    }

    /// <summary>
    /// Queues the DPI follow-up rather than running it now.
    /// </summary>
    /// <remarks>
    /// Both hooks fire while WinForms is part-way through its own relayout, and the pass below
    /// reads values that are not final yet - above all this form's font, which is what the
    /// toolbars are given. Posting it puts the work after the message that is being handled,
    /// by which point the framework has finished and everything read is the settled value.
    /// </remarks>
    private void ScheduleDpiFollowUp(int dpi)
    {
        if (!IsHandleCreated || IsDisposed)
        {
            return;
        }

        BeginInvoke(new Action(() => ApplyDpiFollowUp(dpi)));
    }

    /// <summary>
    /// Fixes up everything in this window that a DPI change does not reach by itself.
    /// </summary>
    /// <remarks>
    /// Swept from here, for the whole window, because none of these are reachable from where
    /// they are declared: drop-down menus are separate windows that never receive a DPI change,
    /// and the tool windows that own the other strips sit inside a tab page several parents
    /// away.
    /// <para>
    /// The font is the part that is easy to miss. A ToolStrip does <em>not</em> inherit its
    /// parent's font - measured, a form went from 9pt to 18pt and a Label on it followed while
    /// the MenuStrip stayed at 9pt with nothing of its own set, because ToolStrip reads a
    /// process-wide default instead. So the menu text kept its old size on every DPI change
    /// until it was assigned here.
    /// </para>
    /// </remarks>
    internal void ApplyDpiFollowUp(int dpi)
    {
        if (IsDisposed)
        {
            return;
        }

        // A failure in here is cosmetic, but the handler above this one ends the process, so
        // an exception thrown while relaying out for a new monitor would take every open
        // session with it. A window left looking wrong is the better of the two, and the
        // logged exception is what makes the cause findable. Win32Exception only: this covers
        // handle creation failing part-way through a DPI change, not a bug in the sweep.
        try
        {
            ApplyDpiFollowUpCore(dpi);
        }
        catch (Win32Exception ex)
        {
            Runtime.MessageCollector.AddExceptionMessage(
                $"The DPI follow-up for {dpi} DPI failed. The window may be laid out for the " +
                "monitor it came from until it is moved again.", ex);
        }
    }

    private void ApplyDpiFollowUpCore(int dpi)
    {
        // Captured before the sweep so the log can show both ends of it. The toolbar glyphs
        // were reported staying at their 200% size after a move back to a 100% monitor, and
        // that direction reproduces neither here nor in a headless round trip - so the next
        // round needs the numbers from the machine it happens on rather than another guess.
        var before = DpiScaling.ToolStripsOf(this)
                               .ToDictionary(strip => strip, Describe);

        FollowDpiChangeIn(this, dpi);

        // Logged because this cannot be watched where it is developed: the machine that builds
        // this has one monitor, so a DPI change only ever happens on someone else's desk. If a
        // toolbar is ever the wrong size again, this line says whether the pass even ran and
        // what it thought the font and DPI were.
        Runtime.MessageCollector.AddMessage(
            MessageClass.InformationMsg,
            $"DPI follow-up: dpi {dpi}, form font {Font.Name} {Font.SizeInPoints}pt, " +
            $"device dpi {DeviceDpi}, strips {DpiScaling.ToolStripsOf(this).Count()}",
            true);

        foreach (ToolStrip strip in DpiScaling.ToolStripsOf(this))
        {
            before.TryGetValue(strip, out string? was);
            Runtime.MessageCollector.AddMessage(
                MessageClass.InformationMsg,
                $"DPI follow-up strip {strip.Name}: was {was ?? "(new)"}, now {Describe(strip)}",
                true);
        }
    }

    /// <summary>
    /// The sweep of the DPI follow-up: everything under <paramref name="window"/> that a DPI
    /// change does not reach by itself.
    /// </summary>
    /// <remarks>
    /// Static, and taking the window, so that <c>--selftest</c> can run the same sweep on its
    /// stand-in for this form ("DPI change on a hosted RDP control"), which cannot be this form.
    /// </remarks>
    internal static void FollowDpiChangeIn(Form window, int dpi)
    {
        DpiScaling.FollowDpiChange(window, dpi, window.Font);

        // TabControl.ItemSize is measured from the font but does not follow it - measured, it
        // held 100x24 while the font doubled - so the connection tabs have to be told.
        foreach (ConnectionWindow connectionWindow in DpiScaling.DescendantsOf(window).OfType<ConnectionWindow>())
        {
            connectionWindow.RefreshTabMetrics(dpi);
        }

        // A hosted PuTTY is not a top-level window, so Windows never tells it the DPI changed and
        // it keeps the font size it started with (#262). The panel tells it too, in
        // NotifyDpiChanged; this is the second route, for a change the panel does not hear of.
        foreach (InterfaceControl interfaceControl in DpiScaling.DescendantsOf(window).OfType<InterfaceControl>())
        {
            (interfaceControl.Protocol as Connection.Protocol.PuttyBase)?.TellPuttyItsDpi(
                dpi, "main window's DPI follow-up");
        }

        // Same reason as the tabs: the quick connect entry field's width is computed from the
        // font and the DPI rather than inherited from anything, and this pass is the only place
        // that knows which DPI the window settled on.
        foreach (QuickConnectToolStrip quickConnect in DpiScaling.DescendantsOf(window).OfType<QuickConnectToolStrip>())
        {
            quickConnect.RefreshEntryFieldMetrics(dpi);
        }

        // And the same again for the connection tree's search row, whose height is measured
        // from the font it ends up with.
        foreach (ConnectionTreeWindow tree in DpiScaling.DescendantsOf(window).OfType<ConnectionTreeWindow>())
        {
            tree.RefreshSearchRowMetrics(dpi);
        }
    }

    /// <summary>Everything about a ToolStrip that a DPI change is supposed to move.</summary>
    private static string Describe(ToolStrip strip)
    {
        string items = string.Join(
            ",",
            strip.Items.OfType<ToolStripItem>().Select(item => $"{item.Size.Width}x{item.Size.Height}"));
        return $"dpi {strip.DeviceDpi} glyphs {strip.ImageScalingSize.Width} " +
               $"font {strip.Font.SizeInPoints}pt height {strip.Height} items [{items}]";
    }

    private void LockToolbarPositions(bool shouldBeLocked)
    {
        var toolbars = new ToolStrip[] { _quickConnectToolStrip, msMain };
        foreach (ToolStrip toolbar in toolbars)
        {
            toolbar.GripStyle = shouldBeLocked
                ? ToolStripGripStyle.Hidden
                : ToolStripGripStyle.Visible;
        }
    }

    private void ConnectionsServiceOnConnectionsLoaded(
        object? sender,
        ConnectionsLoadedEventArgs connectionsLoadedEventArgs)
    {
        UpdateWindowTitle();
    }

    private void ConnectionsServiceOnConnectionsSaved(
        object? sender,
        ConnectionsSavedEventArgs connectionsSavedEventArgs)
    {
        FileBackupPruner.PruneBackupFiles(
            connectionsSavedEventArgs.ConnectionFileName,
            Settings.Default.BackupFileKeepCount);
    }

    private void SetMenuDependencies()
    {
        var connectionInitiator = new ConnectionInitiator();
        fileMenu.TreeWindow = AppWindows.TreeForm;
        fileMenu.ConnectionInitiator = connectionInitiator;

        viewMenu.TsQuickConnect = _quickConnectToolStrip;
        viewMenu.FullscreenHandler = Fullscreen;
        viewMenu.MainForm = this;


        _quickConnectToolStrip.ConnectionInitiator = connectionInitiator;
    }

    [SuppressMessage("Style", "IDE1006", Justification = "The name is wired in FrmMain.Designer.cs.")]
    private void frmMain_FormClosing(object sender, FormClosingEventArgs e)
    {
        if (!(Runtime.WindowList == null || Runtime.WindowList.Count == 0))
        {
            int openConnections = 0;
            foreach (BaseWindow window in Runtime.WindowList)
            {
                var connectionWindow = window as ConnectionWindow;
                if (connectionWindow != null)
                {
                    openConnections = openConnections + connectionWindow.TabController.TabPages.Count;
                }
            }

            if (openConnections > 0
                && (Settings.Default.ConfirmCloseConnection == (int)ConfirmCloseMode.All
                    | (Settings.Default.ConfirmCloseConnection == (int)ConfirmCloseMode.Multiple & openConnections > 1)
                    || Settings.Default.ConfirmCloseConnection == (int)ConfirmCloseMode.Exit))
            {
                DialogResult result = CTaskDialog.MessageBox(
                    this,
                    GeneralAppInfo.ProductName,
                    Language.strConfirmExitMainInstruction,
                    "",
                    "",
                    "",
                    Language.strCheckboxDoNotShowThisMessageAgain,
                    ETaskDialogButtons.YesNo,
                    ESysIcons.Question,
                    ESysIcons.Question);
                if (CTaskDialog.VerificationChecked)
                {
                    Settings.Default.ConfirmCloseConnection--;
                }
                if (result == DialogResult.No)
                {
                    e.Cancel = true;
                    return;
                }
            }
        }

        Shutdown.Cleanup(_quickConnectToolStrip, this);

        IsClosing = true;
        ApplicationLifecycle.IsShuttingDown = true;

        if (Runtime.WindowList != null)
        {
            foreach (BaseWindow window in Runtime.WindowList)
            {
                window.Close();
            }
        }

        Debug.Print("[END] - " + Convert.ToString(DateTime.Now, CultureInfo.InvariantCulture));
    }
    #endregion

    #region Timer
    [SuppressMessage("Style", "IDE1006", Justification = "The name is wired in FrmMain.Designer.cs.")]
    private void tmrAutoSave_Tick(object sender, EventArgs e)
    {
        Runtime.MessageCollector.AddMessage(MessageClass.DebugMsg, "Doing AutoSave");
        Runtime.ConnectionsService.SaveConnectionsAsync();
    }
    #endregion

    #region Window Overrides
    [SuppressMessage("Style", "IDE1006", Justification = "The name is wired in FrmMain.Designer.cs.")]
    private void frmMain_ResizeBegin(object sender, EventArgs e)
    {
        _inSizeMove = true;
    }

    [SuppressMessage("Style", "IDE1006", Justification = "The name is wired in FrmMain.Designer.cs.")]
    private void frmMain_Resize(object sender, EventArgs e)
    {
        if (WindowState == FormWindowState.Minimized)
        {
            if (!Settings.Default.MinimizeToTray)
            {
                return;
            }

            if (Runtime.NotificationAreaIcon == null)
            {
                Runtime.NotificationAreaIcon = new NotificationAreaIcon();
            }
            Hide();
        }
        else
        {
            PreviousWindowState = WindowState;
        }
    }

    [SuppressMessage("Style", "IDE1006", Justification = "The name is wired in FrmMain.Designer.cs.")]
    private void frmMain_ResizeEnd(object sender, EventArgs e)
    {
        _inSizeMove = false;
        // This handles activations from clicks that started a size/move operation
        ActivateConnection("end of a size/move");
    }

    protected override void WndProc(ref System.Windows.Forms.Message m)
    {
        // What to log once the base class has answered the message (see the end of this method).
        bool logMouseActivateAnswer = false;
        bool logFocusAfterActivate = false;

        // Listen for and handle operating system messages
        try
        {
            // ReSharper disable once SwitchStatementMissingSomeCases
            switch (m.Msg)
            {
                case NativeMethods.WM_MOUSEACTIVATE:
                    _inMouseActivate = true;
                    // The mouse message that is activating the window: the only reliable record
                    // of which button it was, read below when the activation arrives.
                    _activatingMouseMessage = NativeMethods.HIWORD(m.LParam);
                    logMouseActivateAnswer = true;
                    break;
                case NativeMethods.WM_ACTIVATEAPP:
                    _inMouseActivate = false;
                    LogActivation($"WM_ACTIVATEAPP {(m.WParam != IntPtr.Zero ? "activating" : "deactivating")} "
                                  + "this application");
                    break;
                case NativeMethods.WM_ACTIVATE:
                    int activateState = NativeMethods.LOWORD(m.WParam);
                    LogActivation($"WM_ACTIVATE {ActivateStateName(activateState)} at the main "
                                  + $"window, the other window {DescribeWindow(m.LParam)}");
                    logFocusAfterActivate = activateState != NativeMethods.WA_INACTIVE;

                    Control? controlThatWasClicked = activateState == NativeMethods.WA_CLICKACTIVE
                        ? FromChildHandle(NativeMethods.WindowFromPoint(MousePosition))
                          ?? GetChildAtPoint(MousePosition)
                        : null;
                    OnActivate(activateState, controlThatWasClicked, MousePosition);

                    // Whatever the activation, the recorded click belongs to it and not to the next.
                    _activatingMouseMessage = 0;
                    break;
                case NativeMethods.WM_WINDOWPOSCHANGED:
                    NativeMethods.WINDOWPOS windowPos = Marshal.PtrToStructure<NativeMethods.WINDOWPOS>(m.LParam);
                    OnWindowPosChanged(windowPos.flags);
                    break;
                case NativeMethods.WM_SYSCOMMAND:
                    Screen? screen = _screenSystemMenu.GetScreenById(m.WParam.ToInt32());
                    if (screen != null)
                    {
                        Screens.SendFormToScreen(screen);
                    }

                    break;
            }
        }
        catch (Exception ex)
        {
            Runtime.MessageCollector.AddExceptionMessage("frmMain WndProc failed", ex);
        }

        base.WndProc(ref m);

        try
        {
            if (logMouseActivateAnswer)
            {
                LogActivation("WM_MOUSEACTIVATE at the main window: "
                              + $"{MouseMessageName(NativeMethods.HIWORD(m.LParam))}, "
                              + $"hit-test {NativeMethods.LOWORD(m.LParam)}, top-level {DescribeWindow(m.WParam)}, "
                              + $"answered {MouseActivateAnswerName(m.Result)}");
            }

            if (logFocusAfterActivate)
            {
                LogActivation($"after WM_ACTIVATE: focus {DescribeWindow(NativeMethods.GetFocus())}, "
                              + $"foreground {DescribeWindow(NativeMethods.GetForegroundWindow())}");
            }
        }
        catch (Exception ex)
        {
            Runtime.MessageCollector.AddExceptionMessage("frmMain WndProc activation logging failed", ex);
        }
    }

    /// <summary>
    /// A debug line about how a click reached the main window, for <c>--verbose</c> runs: the
    /// activation path cannot be exercised on the build machine, only read back from a log.
    /// </summary>
    internal static void LogActivation(string text)
    {
        Runtime.MessageCollector.AddMessage(MessageClass.DebugMsg, "Activation: " + text, true);
    }

    /// <summary>A window handle with the control it belongs to, when it is one of ours.</summary>
    internal static string DescribeWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return "none";
        }

        Control? control = FromHandle(hwnd);
        string what = control != null
            ? $"{control.GetType().Name} '{control.Name}'"
            : FromChildHandle(hwnd) is { } owner
                ? $"inside {owner.GetType().Name} '{owner.Name}'"
                : "not a control of this application";
        return $"0x{hwnd.ToInt64():X} ({what})";
    }

    private static string ActivateStateName(int state) => state switch
    {
        NativeMethods.WA_INACTIVE => "WA_INACTIVE",
        NativeMethods.WA_ACTIVE => "WA_ACTIVE",
        NativeMethods.WA_CLICKACTIVE => "WA_CLICKACTIVE",
        _ => $"state {state}"
    };

    private static string MouseMessageName(int message) => message switch
    {
        0 => "no mouse message recorded",
        NativeMethods.WM_LBUTTONDOWN => "WM_LBUTTONDOWN",
        NativeMethods.WM_RBUTTONDOWN => "WM_RBUTTONDOWN",
        NativeMethods.WM_MBUTTONDOWN => "WM_MBUTTONDOWN",
        NativeMethods.WM_XBUTTONDOWN => "WM_XBUTTONDOWN",
        _ => $"mouse message 0x{message:X}"
    };

    private static string MouseActivateAnswerName(IntPtr result) => result.ToInt64() switch
    {
        1 => "MA_ACTIVATE",
        2 => "MA_ACTIVATEANDEAT",
        3 => "MA_NOACTIVATE",
        4 => "MA_NOACTIVATEANDEAT",
        long other => $"{other}"
    };

    /// <summary>The main window's answer to <c>WM_WINDOWPOSCHANGED</c>.</summary>
    /// <param name="swpFlags">The <c>SWP_*</c> flags of the window's new position.</param>
    internal void OnWindowPosChanged(int swpFlags)
    {
        // Ignore this message if the window wasn't activated
        if ((swpFlags & NativeMethods.SWP_NOACTIVATE) != 0)
        {
            return;
        }

        if (_inMouseActivate)
        {
            LogActivation("WM_WINDOWPOSCHANGED during a mouse activation: session not focused");
            return;
        }

        if (_inSizeMove)
        {
            return;
        }

        if (SelectedSession(out _)?.Protocol.FocusTakesTheForeground == true)
        {
            // Held back until the WM_ACTIVATE that follows says whether a click is activating.
            _sessionFocusDeferred = true;
            LogActivation("WM_WINDOWPOSCHANGED with no WM_MOUSEACTIVATE before it, and the selected session "
                          + "takes the foreground when focused: focusing it waits for WM_ACTIVATE");
            BeginInvoke(FocusDeferredSession);
            return;
        }

        ActivateConnection("WM_WINDOWPOSCHANGED");
    }

    /// <summary>The main window's answer to <c>WM_ACTIVATE</c>.</summary>
    /// <param name="state">The activation state: <c>WA_INACTIVE</c>, <c>WA_ACTIVE</c> or <c>WA_CLICKACTIVE</c>.</param>
    /// <param name="controlThatWasClicked">For a click activation, the control under the mouse, if any.</param>
    /// <param name="mousePosition">The mouse position, in screen coordinates.</param>
    /// <remarks>
    /// <para>
    /// A session focus that <see cref="OnWindowPosChanged"/> held back is decided here. A click
    /// on a control of this window drops it: the click decides where focus goes, through
    /// <see cref="OnClickActivation"/>, which still focuses the session for a click on nothing
    /// that takes one. A deactivation drops it too, so the session never takes the foreground
    /// back from a window that has just taken it. Any other activation (Alt+Tab, the taskbar,
    /// a restore, or a click the window cannot place) leaves it to run once the activation is
    /// through.
    /// </para>
    /// <para>
    /// Why it is held back at all (#269): a click on the menu bar or a toolbar from another
    /// program sends no <c>WM_MOUSEACTIVATE</c> to this window - the strip answers it itself -
    /// so <c>WM_WINDOWPOSCHANGED</c> could not tell it from Alt+Tab, and focused the session.
    /// For a hosted PuTTY that is <c>SetForegroundWindow</c> on another process's window, which
    /// took the foreground away from the click before the click activation arrived; the menu
    /// bar needed a second click. A session in this process (RDP) takes only the focus, which
    /// the click activation then moves to the strip, so it is still focused at once.
    /// </para>
    /// </remarks>
    internal void OnActivate(int state, Control? controlThatWasClicked, Point mousePosition)
    {
        if (_sessionFocusDeferred)
        {
            if (state == NativeMethods.WA_INACTIVE)
            {
                _sessionFocusDeferred = false;
                LogActivation("deactivated before the held-back session focus ran: session not focused");
            }
            else if (state == NativeMethods.WA_CLICKACTIVE && controlThatWasClicked != null)
            {
                _sessionFocusDeferred = false;
                LogActivation($"click activation on {controlThatWasClicked.GetType().Name} "
                              + $"'{controlThatWasClicked.Name}': held-back session focus dropped, the session is "
                              + "not focused for it");
            }
        }

        // Only a click activation needs anything more here
        if (state == NativeMethods.WA_CLICKACTIVE && controlThatWasClicked != null)
        {
            OnClickActivation(controlThatWasClicked, mousePosition);
        }
    }

    /// <summary>
    /// Runs a session focus that <see cref="OnWindowPosChanged"/> held back, unless the
    /// activation after it dropped it. Posted, so it runs once the activation is through.
    /// </summary>
    private void FocusDeferredSession()
    {
        if (!_sessionFocusDeferred)
        {
            return;
        }

        _sessionFocusDeferred = false;
        ActivateConnection("WM_WINDOWPOSCHANGED, held back until the activation was through");
    }

    /// <summary>
    /// Passes on a click that activated the main window to the control it landed on.
    /// </summary>
    /// <param name="controlThatWasClicked">The control under the mouse.</param>
    /// <param name="mousePosition">The mouse position, in screen coordinates.</param>
    internal void OnClickActivation(Control controlThatWasClicked, Point mousePosition)
    {
        bool leftClick = ActivatingClickIsLeft(out string decidedFrom);
        string clicked = $"{controlThatWasClicked.GetType().Name} '{controlThatWasClicked.Name}'";
        if (controlThatWasClicked is TreeView ||
            controlThatWasClicked is ComboBox ||
            controlThatWasClicked is TextBox)
        {
            LogActivation($"click activation on {clicked}: {(leftClick ? "left" : "not left")} button "
                          + $"(from {decidedFrom}), focused it, no click simulated");
            controlThatWasClicked.Focus();
        }
        else if (controlThatWasClicked.CanSelect ||
                 controlThatWasClicked is MenuStrip ||
                 controlThatWasClicked is ToolStrip ||
                 controlThatWasClicked is TabControl)
        {
            // Simulate a mouse event since one wasn't generated by Windows. The simulated event is
            // a left button-down, so only for a left click: on the Tab strip a left button-down
            // selects the Tab under it, and a right-click on an inactive Tab while PuTTY or another
            // application had the foreground used to switch to that Tab this way.
            if (leftClick)
            {
                SimulateClick(controlThatWasClicked, mousePosition);
            }

            controlThatWasClicked.Focus();
            LogActivation($"click activation on {clicked}: {(leftClick ? "left" : "not left")} button "
                          + $"(from {decidedFrom}), "
                          + (leftClick ? "simulated a left button-down" : "no click simulated")
                          + $", focus now {DescribeWindow(NativeMethods.GetFocus())}");
        }
        else
        {
            LogActivation($"click activation on {clicked}: {(leftClick ? "left" : "not left")} button "
                          + $"(from {decidedFrom}), not a control that takes a click; focusing the session");
            // This handles activations from clicks that did not start a size/move operation
            ActivateConnection("click activation");
        }
    }

    /// <summary>
    /// Whether the click that is activating the main window gets the simulated left button-down:
    /// every click does, as it always has, except one known to be right, middle or X.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Not <see cref="Control.MouseButtons"/>: it reads the thread's input state, and when the
    /// activation arrives the button-down that caused it has not been taken from the queue yet,
    /// so a left click reads as no button and the first click on the menu bar was lost.
    /// </para>
    /// <para>
    /// The button comes from the <c>WM_MOUSEACTIVATE</c> recorded in <see cref="WndProc"/>.
    /// That message does not always get this far: it climbs the parent chain only while each
    /// window hands it to <c>DefWindowProc</c>, and <see cref="ConnectionWindow"/> answers it
    /// itself for a click inside a session. An unknown button is treated as left. Reading the
    /// physical button state instead (an earlier version of this) left those clicks without
    /// the button-down whenever the button was already up. With it the lab saw a menu bar that
    /// took two clicks and a click into a session that left the main window behind the other
    /// program, neither of which the build without this change showed; that gate was the only
    /// difference in behaviour between the two.
    /// </para>
    /// </remarks>
    private bool ActivatingClickIsLeft(out string decidedFrom)
    {
        int message = _activatingMouseMessage;
        _activatingMouseMessage = 0;
        if (message == 0)
        {
            decidedFrom = "no WM_MOUSEACTIVATE reached the main window, treated as left as before";
            return true;
        }

        decidedFrom = $"WM_MOUSEACTIVATE: {MouseMessageName(message)}";
        return !IsNonLeftButtonDown(message);
    }

    /// <summary>A right, middle or X button-down, in the client or the non-client area.</summary>
    private static bool IsNonLeftButtonDown(int mouseMessage) => mouseMessage is
        NativeMethods.WM_RBUTTONDOWN or NativeMethods.WM_MBUTTONDOWN or NativeMethods.WM_XBUTTONDOWN
        or WM_NCRBUTTONDOWN or WM_NCMBUTTONDOWN or WM_NCXBUTTONDOWN;

    private const int WM_NCRBUTTONDOWN = 0x00A4;
    private const int WM_NCMBUTTONDOWN = 0x00A7;
    private const int WM_NCXBUTTONDOWN = 0x00AB;

    private static void SimulateClick(Control control, Point mousePosition)
    {
        Point clientMousePosition = control.PointToClient(mousePosition);
        int temp_wLow = clientMousePosition.X;
        int temp_wHigh = clientMousePosition.Y;
        NativeMethods.SendMessage(
            control.Handle,
            NativeMethods.WM_LBUTTONDOWN,
            (IntPtr)NativeMethods.MK_LBUTTON,
            (IntPtr)NativeMethods.MAKELPARAM(ref temp_wLow, ref temp_wHigh));
        clientMousePosition.X = temp_wLow;
        clientMousePosition.Y = temp_wHigh;
    }

    /// <param name="reason">What asked for it, for the activation log.</param>
    private void ActivateConnection(string reason)
    {
        InterfaceControl? ifc = SelectedSession(out TabPage? tab);
        if (ifc == null || tab == null)
        {
            return;
        }

        // A hosted PuTTY takes focus by becoming the foreground window, which deactivates this one.
        LogActivation($"focusing the {ifc.Protocol.GetType().Name} session of the selected Tab '{tab.Text}' "
                      + $"({reason})");
        ifc.Protocol.Focus();
        ((ConnectionWindow?)ifc.FindForm())?.RefreshInterfaceController();
    }

    /// <summary>The session of the active connection window's selected Tab, if there is one.</summary>
    private InterfaceControl? SelectedSession(out TabPage? tab)
    {
        tab = ActiveConnectionWindow()?.TabController.SelectedTab;
        return (InterfaceControl?)tab?.Tag;
    }

    private void Layout_ActiveDocumentChanged(object? sender, EventArgs e)
    {
        ActivateConnection("active panel changed");
        var connectionWindow = Layout.ActiveDocument as ConnectionWindow;
        connectionWindow?.UpdateSelectedConnection();
    }

    internal void UpdateWindowTitle()
    {
        if (InvokeRequired)
        {
            Invoke(new MethodInvoker(UpdateWindowTitle));
            return;
        }

        var titleBuilder = new StringBuilder(Application.ProductName);
        const string separator = " - ";

        if (Runtime.ConnectionsService.IsConnectionsFileLoaded)
        {
            if (!string.IsNullOrEmpty(Runtime.ConnectionsService.ConnectionFileName))
            {
                titleBuilder.Append(separator);
                titleBuilder.Append(Settings.Default.ShowCompleteConsPathInTitle
                    ? Runtime.ConnectionsService.ConnectionFileName
                    : Path.GetFileName(Runtime.ConnectionsService.ConnectionFileName));
            }
        }

        if (!string.IsNullOrEmpty(SelectedConnection?.Name))
        {
            titleBuilder.Append(separator);
            titleBuilder.Append(SelectedConnection.Name);
        }

        Text = titleBuilder.ToString();
    }

    #endregion

    #region Screen Stuff
    public void SetDefaultLayout()
    {
        Layout.ResetToDefaults();
    }
    #endregion

    private void ViewMenu_Opening(object sender, EventArgs e)
    {
        viewMenu.MMenView_DropDownOpening(sender, e);
    }

    [SuppressMessage("Style", "IDE1006", Justification = "The name is wired in FrmMain.Designer.cs.")]
    private void mainFileMenu1_DropDownOpening(object sender, EventArgs e)
    {
        fileMenu.MMenFile_DropDownOpening(sender, e);
    }
}
