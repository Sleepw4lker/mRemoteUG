using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using mRemoteUG.App;
using mRemoteUG.Messages;
using mRemoteUG.Tools;
using mRemoteUG.UI;
using mRemoteUG.UI.Forms;


namespace mRemoteUG.Connection.Protocol;

[SuppressMessage("Design", "CA1001",
    Justification = "TmrReconnect is disposed by TearDown, the deferred teardown every session ends in "
        + "through Close() (ADR-0005). IDisposable would add a second way to end a session, one that "
        + "callers could reach around that teardown.")]
public abstract class ProtocolBase
{
    #region Private Variables

    private UI.Window.ConnectionWindow? _connectionWindow;
    private InterfaceControl? _interfaceControl;
    private IUiThreadInvoker? _uiThreadInvoker;
    private int _closeStarted;
    private static int s_sessionCounter;
    private readonly int _sessionNumber = Interlocked.Increment(ref s_sessionCounter);
    private string? _sessionLabel;
    private ConnectingHandler? _connectingEvent;
    private ConnectedHandler? _connectedEvent;
    private DisconnectedHandler? _disconnectedEvent;
    private ErrorOccuredHandler? _errorOccuredEvent;
    private ClosingHandler? _closingEvent;
    private ClosedHandler? _closedEvent;
    #endregion

    #region Public Properties
    #region Control
    private string? Name { get; }

    protected UI.Window.ConnectionWindow? ConnectionWindow
    {
        get { return _connectionWindow; }
        private set
        {
            DetachConnectionWindowHandlers();
            _connectionWindow = value;
            if (_connectionWindow == null)
            {
                return;
            }

            _connectionWindow.ResizeBegin += ResizeBegin;
            _connectionWindow.Resize += Resize;
            _connectionWindow.ResizeEnd += ResizeEnd;
        }
    }

    public InterfaceControl? InterfaceControl
    {
        get { return _interfaceControl; }
        set
        {
            _interfaceControl = value;
            ConnectionWindow = _interfaceControl?.GetContainerControl() as UI.Window.ConnectionWindow;

            // This setter runs on the UI thread, so it is the one reliable moment to capture
            // which thread that is. Doing it later - during teardown - is too late: the window
            // handles we would have to ask are exactly what is being destroyed. See
            // IUiThreadInvoker.
            Control? uiThreadAnchor = (Control?)_connectionWindow ?? _interfaceControl;
            if (_uiThreadInvoker == null && uiThreadAnchor != null)
            {
                _uiThreadInvoker = new ControlUiThreadInvoker(
                    uiThreadAnchor,
                    () => FrmMain.Default);
            }
        }
    }

    /// <summary>
    /// <see cref="InterfaceControl"/>, for the code that only runs while a session has one.
    /// </summary>
    /// <remarks>
    /// ConnectionInitiator assigns the panel before it calls <see cref="Initialize"/>, and the
    /// teardown clears it last, so initializing, connecting and every session event in between
    /// see one. Outside that window this code used to dereference null; it now throws the same
    /// failure, at the same point, with a message that says what was missing.
    /// </remarks>
    internal InterfaceControl AssignedInterfaceControl =>
        _interfaceControl
        ?? throw new InvalidOperationException($"{GetType().Name} has no InterfaceControl assigned.");

    /// <summary>
    /// Marshals teardown work onto the UI thread. Assigned automatically when
    /// <see cref="InterfaceControl"/> is set; settable so tests can supply a synchronous
    /// invoker instead of a real control.
    /// </summary>
    public IUiThreadInvoker? UiThreadInvoker
    {
        get { return _uiThreadInvoker; }
        set { _uiThreadInvoker = value; }
    }

    /// <summary>
    /// True when we are on the UI thread, or when no invoker was ever captured (in which case
    /// there is no UI to marshal to and running inline is correct).
    /// </summary>
    private bool OnUiThread => _uiThreadInvoker == null || _uiThreadInvoker.OnUiThread;

    /// <summary>
    /// Queues <paramref name="action"/> on the UI thread and returns immediately. Use this to
    /// leave a COM event callback before disposing the object that raised it.
    /// </summary>
    protected void PostToUiThread(Action action)
    {
        if (_uiThreadInvoker == null)
        {
            action();
        }
        else
        {
            _uiThreadInvoker.Post(action);
        }
    }

    private static readonly Queue<Action> s_idleWork = new Queue<Action>();
    private static bool s_idleHandlerAttached;

    /// <summary>
    /// Queues <paramref name="action"/> to run on the UI thread once the message loop is idle,
    /// one queued item per idle turn.
    /// </summary>
    /// <remarks>
    /// Posting is not enough on its own. COM pumps messages for any outgoing call from an STA,
    /// so a posted teardown is dispatched inside whatever mstscax call happens to be running -
    /// which is how a teardown ends up releasing one RDP control while several others are
    /// still unwinding their own disconnects. Disposing a control in that state is what
    /// blocks: a session torn down on its own always completes in well under 100 ms, while the
    /// same teardown during a burst of disconnects hangs the UI thread outright.
    /// <para>
    /// <see cref="Application.Idle"/> is raised only by WinForms' own message loop, never by
    /// COM's, so work queued here cannot start inside an mstscax call. Running one item per
    /// idle turn then keeps the teardowns from piling back up on each other.
    /// </para>
    /// </remarks>
    protected void PostToUiThreadWhenIdle(Action action)
    {
        // Queue synchronously and only hook the loop through the post: if the enqueue itself
        // were posted, a shutdown drain would run before it arrived and the teardown would be
        // lost altogether.
        lock (s_idleWork)
        {
            s_idleWork.Enqueue(action);
        }

        PostToUiThread(() =>
        {
            if (s_idleHandlerAttached)
            {
                return;
            }

            s_idleHandlerAttached = true;
            Application.Idle += RunOneQueuedItem;
        });
    }

    private static void RunOneQueuedItem(object? sender, EventArgs e)
    {
        Action? next = null;
        lock (s_idleWork)
        {
            if (s_idleWork.Count > 0)
            {
                next = s_idleWork.Dequeue();
            }
        }

        if (next == null)
        {
            Application.Idle -= RunOneQueuedItem;
            s_idleHandlerAttached = false;
            return;
        }

        next();
    }

    /// <summary>
    /// Forgets the queue and its message-loop hook. Tests only; the application has one UI
    /// thread for its lifetime, so it never needs this.
    /// </summary>
    internal static void ResetIdleQueueForTests()
    {
        lock (s_idleWork)
        {
            s_idleWork.Clear();
        }

        Application.Idle -= RunOneQueuedItem;
        s_idleHandlerAttached = false;
    }

    /// <summary>
    /// Tests only: whether <see cref="RunOneQueuedItem"/> is hooked to <see cref="Application.Idle"/>.
    /// </summary>
    internal static bool IdleHandlerAttachedForTests => s_idleHandlerAttached;

    /// <summary>
    /// Runs everything queued for idle immediately. Used when the message loop is about to
    /// stop, because then no further idle turn is coming.
    /// </summary>
    internal static void DrainQueuedTeardownsNow()
    {
        while (true)
        {
            Action next;
            lock (s_idleWork)
            {
                if (s_idleWork.Count == 0)
                {
                    return;
                }

                next = s_idleWork.Dequeue();
            }

            next();
        }
    }

    private void DetachConnectionWindowHandlers()
    {
        if (_connectionWindow == null)
        {
            return;
        }

        _connectionWindow.ResizeBegin -= ResizeBegin;
        _connectionWindow.Resize -= Resize;
        _connectionWindow.ResizeEnd -= ResizeEnd;
    }

    protected Control? Control { get; set; }

    #endregion

    public ConnectionInfo.Force Force { get; set; }

    public System.Timers.Timer TmrReconnect { get; } = new System.Timers.Timer(2000);
    protected ReconnectGroup? ActiveReconnectGroup { get; private set; }

    protected ProtocolBase(string name)
    {
        Name = name;
    }

    protected ProtocolBase()
    {
    }

    #endregion

    #region Methods
    /// <summary>
    /// True when <see cref="Focus"/> makes another window the foreground window, deactivating
    /// the main window: a hosted PuTTY, which is a separate process (ADR-0023). False for a
    /// session whose control is a child window in this process, such as RDP.
    /// </summary>
    public virtual bool FocusTakesTheForeground => false;

    public virtual void Focus()
    {
        try
        {
            Control?.Focus();
        }
        catch (Exception ex)
        {
            Runtime.MessageCollector.AddExceptionMessage("Couldn't focus Control (Connection.Protocol.Base)", ex);
        }
    }

    public virtual void ResizeBegin(object? sender, EventArgs e)
    {
    }

    public virtual void Resize(object? sender, EventArgs e)
    {
    }

    public virtual void ResizeEnd(object? sender, EventArgs e)
    {
    }

    /// <summary>
    /// The hosting panel is now being rendered at a different DPI.
    /// </summary>
    /// <remarks>
    /// Raised from <see cref="InterfaceControl"/>, which is the only part of the chain that
    /// hears about it: a per-monitor DPI change reaches child windows as
    /// WM_DPICHANGED_BEFOREPARENT/AFTERPARENT, never as a resize, so a protocol that only
    /// watches Resize sees nothing at all when a window moves between monitors.
    /// </remarks>
    public virtual void NotifyDpiChanged(int dpi)
    {
    }

    public virtual bool Initialize()
    {
        try
        {
            InterfaceControl interfaceControl = AssignedInterfaceControl;
            Control parent = interfaceControl.Parent
                ?? throw new InvalidOperationException("The InterfaceControl is not on a tab.");
            parent.Tag = interfaceControl;
            interfaceControl.Show();

            if (Control == null)
            {
                return true;
            }

            Control.Name = Name;
            Control.Parent = interfaceControl;

            // Dock rather than position by hand. Once an ActiveX control is in place, .NET 10
            // ignores explicit bounds assignments on its AxHost - Size, Bounds, SetBounds and
            // Width/Height all return without changing anything and without throwing, where
            // .NET Framework 4.8 honoured every one of them. The layout engine is the only
            // thing that still resizes such a control.
            Control.Dock = DockStyle.Fill;

            return true;
        }
        catch (Exception ex)
        {
            Runtime.MessageCollector.AddExceptionMessage("Couldn't SetProps (Connection.Protocol.Base)", ex);
            return false;
        }
    }

    public virtual bool Connect()
    {
        if (AssignedInterfaceControl.Info.Protocol == ProtocolType.RDP)
        {
            return false;
        }

        if (_connectedEvent == null)
        {
            return false;
        }

        _connectedEvent(this);
        return true;
    }

    public virtual void Disconnect()
    {
        Close();
    }

    /// <summary>
    /// Tears the session down. Safe to call more than once and from any thread; the teardown
    /// itself always runs on the UI thread.
    /// </summary>
    public virtual void Close()
    {
        if (Interlocked.Exchange(ref _closeStarted, 1) != 0)
        {
            LogClose($"Close() ignored, already closing (thread {Environment.CurrentManagedThreadId})");
            return;
        }

        // Run inline when we are already on the UI thread so that callers which are about to
        // destroy the window - Connection_FormClosing, most importantly - get a completed
        // teardown rather than a posted callback that the dying message loop would drop.
        if (OnUiThread)
        {
            LogClose($"Close() requested, running inline (thread {Environment.CurrentManagedThreadId})");
            CloseCore();
        }
        else
        {
            LogClose(
                $"Close() requested, posting to the UI thread (from thread {Environment.CurrentManagedThreadId})");
            PostToUiThread(CloseCore);
        }
    }

    /// <summary>
    /// Guards against one session's teardown starting inside another's.
    /// </summary>
    /// <remarks>
    /// Releasing an ActiveX control pumps messages - COM does that for any outgoing call from
    /// an STA - so a teardown posted by another tab can be dispatched in the middle of this
    /// one. Nesting two OCX teardowns is exactly what we are trying to avoid, so a close that
    /// arrives mid-teardown is queued and run once the outer one has finished.
    /// </remarks>
    [ThreadStatic]
    [SuppressMessage("Style", "IDE1006",
                     Justification = "[ThreadStatic] fields take t_, and no naming rule can select them.")]
    private static bool t_teardownInProgress;
    [ThreadStatic]
    [SuppressMessage("Style", "IDE1006",
                     Justification = "[ThreadStatic] fields take t_, and no naming rule can select them.")]
    private static Queue<ProtocolBase>? t_deferredCloses;

    private void CloseCore()
    {
        if (t_teardownInProgress)
        {
            LogClose("teardown deferred - another session is already tearing down on this thread");
            (t_deferredCloses ??= new Queue<ProtocolBase>()).Enqueue(this);
            return;
        }

        t_teardownInProgress = true;
        try
        {
            TearDown();
        }
        finally
        {
            t_teardownInProgress = false;
        }

        // Hand the queued teardowns back to the message loop one at a time rather than running
        // them here, so each one starts on a clean stack with the other sessions' disconnects
        // already delivered. On the way out there is no idle turn coming, so run them inline.
        while (t_deferredCloses != null && t_deferredCloses.Count > 0)
        {
            ProtocolBase next = t_deferredCloses.Dequeue();
            if (ApplicationIsClosing)
            {
                next.CloseCore();
            }
            else
            {
                next.PostToUiThreadWhenIdle(next.CloseCore);
            }
        }
    }

    /// <summary>
    /// The whole teardown, always on the UI thread.
    /// </summary>
    /// <remarks>
    /// Order is the point of this method. The hosted control is disposed <em>before</em>
    /// <see cref="Closed"/> fires, because that event removes the tab page, and removing the
    /// tab page destroys the handles of everything inside it. Disposing an ActiveX control
    /// after that point releases it outside its own apartment, and COM then has to marshal the
    /// release across apartments - which is where the multi-second stall on RDP logoff came
    /// from.
    /// </remarks>
    private void TearDown()
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            TeardownWatchdog.Enter($"[{SessionLabel}] detaching handlers");
            // 1. Stop anything that could re-enter while we tear down.
            try
            {
                TmrReconnect.Stop();
                TmrReconnect.Dispose();
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector?.AddExceptionMessage(
                    "Couldn't dispose the reconnect timer (Connection.Protocol.Base)",
                    ex);
            }

            try
            {
                // Same reason as the timer above, and it is a timer too: ReconnectGroup runs a
                // 200 ms animation. It is a child of the hosted control, so on the parked path
                // the control's own dispose is up to two minutes away and the ticks carry on
                // until then. Stop it here, while the control is still alive and parented.
                ReplaceReconnectGroup(null);
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector?.AddExceptionMessage(
                    "Couldn't dispose the reconnect group (Connection.Protocol.Base)",
                    ex);
            }

            DetachConnectionWindowHandlers();
            _connectionWindow = null;

            try
            {
                if (_interfaceControl?.Parent != null)
                {
                    _interfaceControl.Parent.Tag = null;
                }
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector?.AddExceptionMessage(
                    "Couldn't clear InterfaceControl.Parent.Tag (Connection.Protocol.Base)",
                    ex);
            }

            LogCloseStage("handlers detached", stopwatch);

            TeardownWatchdog.Enter($"[{SessionLabel}] releasing protocol resources");
            // 2. Protocol-specific native/COM cleanup, while the control is still alive.
            try
            {
                CleanupProtocolResources();
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector?.AddExceptionMessage(
                    "Couldn't clean up protocol resources (Connection.Protocol.Base)",
                    ex);
            }

            LogCloseStage("protocol resources released", stopwatch);

            TeardownWatchdog.Enter($"[{SessionLabel}] disposing the hosted control");
            // 3. Dispose the hosted control while it is still parented, its handle is alive and
            //    we are on its own apartment - unless the protocol says it is not safe to.
            if (Control != null && !HostedControlCanBeDisposed)
            {
                DeferredControlDisposal.Hold(Control, HostedControlSafeToDisposeProbe(), SessionLabel,
                                             HostedControlPreDisposeHook());
                LogCloseStage("hosted control parked", stopwatch);
            }
            else
            {
                try
                {
                    Control?.Dispose();
                }
                catch (Exception ex)
                {
                    Runtime.MessageCollector?.AddExceptionMessage(
                        "Couldn't dispose control (Connection.Protocol.Base)",
                        ex);
                }

                LogCloseStage("hosted control disposed", stopwatch);
            }

            TeardownWatchdog.Enter($"[{SessionLabel}] raising the Closed event");
            // 4. Only now tell the rest of the app: this removes the tab and updates
            //    OpenConnections. Must precede the InterfaceControl dispose below, because
            //    ConnectionWindow.OnProtocolClosed finds the tab via InterfaceControl.Parent.
            try
            {
                _closedEvent?.Invoke(this);
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector?.AddExceptionMessage(
                    "A Closed event handler threw (Connection.Protocol.Base)",
                    ex);
            }

            LogCloseStage("Closed event handled", stopwatch);

            TeardownWatchdog.Enter($"[{SessionLabel}] disposing the interface control");
            // 5. Finally the panel that hosted the control.
            try
            {
                _interfaceControl?.Dispose();
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector?.AddExceptionMessage(
                    "Couldn't dispose InterfaceControl (Connection.Protocol.Base)",
                    ex);
            }

            LogCloseStage("interface control disposed", stopwatch);

            // 6. Drop what stages 1-5 disposed. A protocol outlives its own teardown - the
            //    parked-control probe closes over it, and nothing ever detaches the four
            //    ConnectionInitiator subscriptions - so whatever is still pointed at from here
            //    stays reachable for as long as any of those roots does, disposed or not.
            //
            //    Safe only after stage 5, and only because SessionLabel was read in stage 1 and
            //    caches itself: it is derived from the InterfaceControl, so the log line below
            //    would otherwise stop naming the host.
            Control = null;
            _interfaceControl = null;
        }
        catch (Exception ex)
        {
            Runtime.MessageCollector?.AddExceptionMessage(
                "Couldn't close the connection (Connection.Protocol.Base)",
                ex);
        }
        finally
        {
            TeardownWatchdog.Leave();

            // The one line a default log keeps for a close. Everything above it is Debug.
            Runtime.MessageCollector?.AddMessage(MessageClass.InformationMsg,
                $"Close [{SessionLabel}] completed in {stopwatch.ElapsedMilliseconds} ms", true);
        }
    }

    /// <summary>
    /// Release protocol-specific native or COM resources. Called on the UI thread, before the
    /// hosted <see cref="Control"/> is disposed and before <see cref="Closed"/> fires.
    /// </summary>
    protected virtual void CleanupProtocolResources()
    {
    }

    /// <summary>
    /// True while the application is on its way out, when nothing is worth blocking for.
    /// </summary>
    private static bool ApplicationIsClosing => ApplicationLifecycle.IsShuttingDown;

    /// <summary>
    /// Work a protocol wants done immediately before its parked control is really disposed, or
    /// null if it has none.
    /// </summary>
    /// <remarks>
    /// Parking is the only thing that separates a close from the release of the hosted control,
    /// which is why this exists at all: stage 2 of the teardown cannot do this work, because on
    /// the parked path the control is still live and the probe still needs what stage 2 would
    /// have dropped. The non-parked path has no need of it - there the release happens in stage 3,
    /// so stage 2 is already immediately before it.
    /// </remarks>
    protected virtual Action? HostedControlPreDisposeHook() => null;

    /// <summary>
    /// Whether the hosted control can be disposed here and now, on the UI thread, without risk
    /// of blocking it. A control that cannot is parked by
    /// <see cref="DeferredControlDisposal"/> and disposed once it can be.
    /// </summary>
    /// <remarks>
    /// Never during shutdown. Each dispose during the close of the main window is one more
    /// chance to block with the UI still up; parking them all and disposing them once
    /// <c>Application.Run</c> has returned costs the same total time, but spends it with no
    /// message loop left to wedge - and by then every session has had the whole application
    /// close in which to go down, so in practice it costs nothing at all.
    /// </remarks>
    protected virtual bool HostedControlCanBeDisposed => !ApplicationIsClosing;

    /// <summary>
    /// A test, asked on each idle turn while a control is parked, of whether it has become safe
    /// to dispose. The default says yes: only a protocol with a session behind it needs to
    /// wait for anything.
    /// </summary>
    protected virtual Func<bool> HostedControlSafeToDisposeProbe() => () => true;

    private void LogCloseStage(string stage, Stopwatch stopwatch)
    {
        LogClose($"{stage} at {stopwatch.ElapsedMilliseconds} ms");
    }

    /// <summary>
    /// One step of a teardown, at Debug.
    /// </summary>
    /// <remarks>
    /// There are eighteen of these and they fire on every session close, which made closing a
    /// handful of tabs worth a hundred Information lines in everybody's log forever. The
    /// summary line in <see cref="TearDown"/> is what a normal log needs; the stages are what
    /// you want when a close misbehaves, and <c>mRemoteUG.exe --verbose</c> brings them back
    /// without anyone having to change a setting first. A stage that never returns is still
    /// reported at Warning by <see cref="TeardownWatchdog"/>, which is the case that cannot
    /// afford to be off by default.
    /// </remarks>
    protected void LogClose(string message)
    {
        Runtime.MessageCollector?.AddMessage(MessageClass.DebugMsg,
            $"Close [{SessionLabel}] {message}", true);
    }

    /// <summary>
    /// Identifies one session in the log. Without this the close timings from several tabs are
    /// indistinguishable, which is exactly what you need to read when closing several at once
    /// misbehaves.
    /// </summary>
    protected string SessionLabel
    {
        get
        {
            if (_sessionLabel != null)
            {
                return _sessionLabel;
            }

            string? host = _interfaceControl?.Info?.Hostname;
            if (string.IsNullOrEmpty(host))
            {
                host = _interfaceControl?.Info?.Name;
            }

            if (string.IsNullOrEmpty(host))
            {
                host = GetType().Name;
            }

            _sessionLabel = $"#{_sessionNumber}/{host}";
            return _sessionLabel;
        }
    }
    #endregion

    #region Events
    public delegate void ConnectingHandler(object sender);
    public event ConnectingHandler Connecting
    {
        add { _connectingEvent = (ConnectingHandler?)Delegate.Combine(_connectingEvent, value); }
        remove { _connectingEvent = (ConnectingHandler?)Delegate.Remove(_connectingEvent, value); }
    }

    public delegate void ConnectedHandler(object sender);
    public event ConnectedHandler Connected
    {
        add { _connectedEvent = (ConnectedHandler?)Delegate.Combine(_connectedEvent, value); }
        remove { _connectedEvent = (ConnectedHandler?)Delegate.Remove(_connectedEvent, value); }
    }

    public delegate void DisconnectedHandler(object sender, string disconnectedMessage);
    public event DisconnectedHandler Disconnected
    {
        add { _disconnectedEvent = (DisconnectedHandler?)Delegate.Combine(_disconnectedEvent, value); }
        remove { _disconnectedEvent = (DisconnectedHandler?)Delegate.Remove(_disconnectedEvent, value); }
    }

    public delegate void ErrorOccuredHandler(object sender, string errorMessage);
    public event ErrorOccuredHandler ErrorOccured
    {
        add { _errorOccuredEvent = (ErrorOccuredHandler?)Delegate.Combine(_errorOccuredEvent, value); }
        remove { _errorOccuredEvent = (ErrorOccuredHandler?)Delegate.Remove(_errorOccuredEvent, value); }
    }

    public delegate void ClosingHandler(object sender);
    public event ClosingHandler Closing
    {
        add { _closingEvent = (ClosingHandler?)Delegate.Combine(_closingEvent, value); }
        remove { _closingEvent = (ClosingHandler?)Delegate.Remove(_closingEvent, value); }
    }

    public delegate void ClosedHandler(object sender);
    public event ClosedHandler Closed
    {
        add { _closedEvent = (ClosedHandler?)Delegate.Combine(_closedEvent, value); }
        remove { _closedEvent = (ClosedHandler?)Delegate.Remove(_closedEvent, value); }
    }



    protected void OnClosed(object sender)
    {
        _closedEvent?.Invoke(sender);
    }

    protected void OnConnecting(object sender)
    {
        _connectingEvent?.Invoke(sender);
    }

    protected void OnConnected(object sender)
    {
        _connectedEvent?.Invoke(sender);
    }

    protected void OnDisconnected(object sender, string disconnectedMessage)
    {
        _disconnectedEvent?.Invoke(sender, disconnectedMessage);
    }

    protected void OnErrorOccured(object sender, string errorMsg)
    {
        _errorOccuredEvent?.Invoke(sender, errorMsg);
    }

    protected void OnReconnectGroupCloseClicked()
    {
        Close();
    }

    /// <summary>
    /// Puts <paramref name="next"/> in place of whatever reconnect group is up, disposing the
    /// one it replaces. Pass null to take the current one down.
    /// </summary>
    /// <remarks>
    /// A flapping server raises OnDisconnected over and over, and each one used to build a fresh
    /// group - a UserControl with a running 200 ms animation timer, parented onto the hosted
    /// control - on top of the last. Only the successful-reconnect path ever disposed one, so the
    /// rest stacked up with their timers still ticking.
    /// </remarks>
    protected void ReplaceReconnectGroup(ReconnectGroup? next)
    {
        if (ReferenceEquals(ActiveReconnectGroup, next))
        {
            return;
        }

        ReconnectGroup? previous = ActiveReconnectGroup;
        ActiveReconnectGroup = next;

        try { previous?.Dispose(); }
        catch (Exception ex)
        {
            Runtime.MessageCollector?.AddExceptionMessage(
                "Couldn't dispose the previous reconnect group (Connection.Protocol.Base)", ex);
        }
    }

    /// <summary>Tests only: the label every close line in the log is keyed on.</summary>
    internal string LabelForTests => SessionLabel;
    #endregion
}
