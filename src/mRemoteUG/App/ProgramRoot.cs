using mRemoteUG.UI;
using mRemoteUG.UI.Forms;
using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;

namespace mRemoteUG.App
{
    public static class ProgramRoot
    {
        private static Mutex mutex;

        /// <summary>
        /// Logs debug messages for this run without changing what the user has saved.
        /// </summary>
        /// <remarks>
        /// Recognised here rather than in <see cref="Tools.Cmdline.StartupArgumentsInterpreter"/>,
        /// which does not run until frmMain_Load - by which point the startup detail this is
        /// wanted for has already been reported.
        /// </remarks>
        public const string VerboseSwitch = "--verbose";

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        public static int Main(string[] args)
        {
            EnableMixedModeDpiHosting();

            // Installed before anything that could fail. Both handlers go on under --selftest too:
            // the non-interactive path logs and carries on rather than showing anything, and leaving
            // Application.ThreadException unsubscribed would hand the dialog to WinForms instead of
            // avoiding it.
            CrashLogger.Install(interactive: !SelfTest.IsRequested(args));

            // Before anything can log, and on both paths out of here: a switch that only took
            // effect once the UI was up would miss everything it is most wanted for.
            Messages.LogMessageTypeFilteringOptions.ForceDebugMessages =
                SelfTest.HasSwitch(args, VerboseSwitch);

            if (SelfTest.IsRequested(args))
            {
                // --largefont runs the whole self-test with a UI font half again as large, which
                // is what a user who has turned up Windows' text size gives us. It has to happen
                // here, before any control exists: a font change after that point does not
                // re-run the auto-scale pass, so the only way to see a layout built for a
                // different font is to start the process with one.
                if (SelfTest.LargeFontRequested(args))
                    Application.SetDefaultFont(SelfTest.LargeUiFont());

                ConfigureApplication(SelfTest.RequestedTheme(args));
                return SelfTest.Run();
            }

            if (Settings.Default.SingleInstance)
                StartApplicationAsSingleInstance();
            else
                StartApplication();

            return 0;
        }


        /// <summary>
        /// What <c>SetThreadDpiHostingBehavior</c> returned, for <c>--selftest</c> to report.
        /// </summary>
        /// <remarks>
        /// The API answers the <em>previous</em> behaviour, so <c>Default</c> here means the call
        /// worked and <c>Invalid</c> means it failed.
        /// </remarks>
        internal static NativeMethods.DpiHostingBehavior PreviousDpiHostingBehavior { get; private set; }
            = NativeMethods.DpiHostingBehavior.Invalid;

        /// <summary>
        /// Allows this thread's windows to host children of a different DPI awareness.
        /// </summary>
        /// <remarks>
        /// PuTTY is a separate process with its own manifest, and PuttyBase reparents its window
        /// into ours. Once this process is per-monitor aware that is a cross-awareness reparent,
        /// which the default hosting behaviour forbids outright.
        /// <para>
        /// This must run before any window handle exists: the behaviour is captured when a window
        /// is created, not when a child is attached to it, and WinForms recreates handles at times
        /// of its own choosing. Hence the first line of Main rather than somewhere near SetParent.
        /// </para>
        /// <para>
        /// Unguarded: the export has existed since Windows 10 1803 and the baseline is Windows 11,
        /// so it always resolves. The API reports a value it will not accept by returning
        /// <c>Invalid</c> rather than by throwing, which is why there is
        /// nothing to catch and why that value is still worth reporting.
        /// </para>
        /// </remarks>
        private static void EnableMixedModeDpiHosting()
        {
            PreviousDpiHostingBehavior =
                NativeMethods.SetThreadDpiHostingBehavior(NativeMethods.DpiHostingBehavior.Mixed);
        }

        /// <summary>
        /// The application-wide settings that have to be in place before any control exists.
        /// </summary>
        /// <remarks>
        /// Both ways into this program - <c>--selftest</c> and the real UI - come through here,
        /// because every one of these is a process-wide switch read at control construction. A
        /// self-test that configured itself differently from the application would be measuring
        /// something the user never sees.
        /// <para>
        /// DPI awareness is <em>not</em> here. It is set to Per-Monitor V2 in
        /// Properties/app.manifest, which Windows applies before any managed code runs, making it
        /// both the more reliable place and the authoritative one: once the process DPI context is
        /// set, <c>Application.SetHighDpiMode</c> is a no-op. Calling it as well earns warning
        /// WFO0003 and reads as though it were doing something. <c>--selftest</c> fails the run
        /// unless the effective mode reads PerMonitorV2.
        /// </para>
        /// </remarks>
        private static void ConfigureApplication(UiTheme theme)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // The theme is applied once, here, and never again while the process runs. SetColorMode
            // re-points the whole SystemColors table and every control that reads it, but it does
            // no native re-theming: SetWindowTheme, DwmSetWindowAttribute for the title bar, and
            // the TVM_/LVM_SETBKCOLOR messages that colour a TreeView or ListView all run at
            // handle creation and nowhere else. Switching at run time would therefore recolour the
            // managed surface and leave the title bar, the tree, the lists and the scrollbars
            // light. Changing this setting asks the user to restart instead.
            Application.SetColorMode(theme.ToColorMode());

            // ToolStrips are the one family of controls that SystemColors does not reach - they
            // are painted by a renderer. Only ToolStripSystemRenderer has a dark variant to fall
            // back on (ToolStripSystemDarkModeRenderer, which is internal and reachable no other
            // way); ToolStripProfessionalRenderer has no dark branch at all. This is a thread
            // static, so it has to be set on the UI thread - which this is.
            ToolStripManager.RenderMode = ToolStripManagerRenderMode.System;
        }

        private static void StartApplication()
        {
            ConfigureApplication(Settings.Default.UiTheme);
            Application.Run(FrmMain.Default);

            // Nothing may be left alive here. An RDP control that is still undisposed when the
            // process exits crashes it, and so does one that has merely been unparented - both
            // isolated down to a single variable. Anything parked during the close is disposed
            // now, with the message loop already finished and no UI left to block.
            Connection.Protocol.DeferredControlDisposal.DisposeEverythingNow();
        }

        public static void CloseSingletonInstanceMutex()
        {
            mutex?.Close();
        }

        private static void StartApplicationAsSingleInstance()
        {
            const string mutexID = "mRemoteUG_SingleInstanceMutex";
            bool newInstanceCreated;
            mutex = new Mutex(false, mutexID, out newInstanceCreated);
            if (!newInstanceCreated)
            {
                SwitchToCurrentInstance();
                return;
            }

            StartApplication();
            GC.KeepAlive(mutex);
        }

        private static void SwitchToCurrentInstance()
        {
            var singletonInstanceWindowHandle = GetRunningSingletonInstanceWindowHandle();
            if (singletonInstanceWindowHandle == IntPtr.Zero) return;
            if (NativeMethods.IsIconic(singletonInstanceWindowHandle) != 0)
                NativeMethods.ShowWindow(singletonInstanceWindowHandle, (int)NativeMethods.SW_RESTORE);
            NativeMethods.SetForegroundWindow(singletonInstanceWindowHandle);
        }

        private static IntPtr GetRunningSingletonInstanceWindowHandle()
        {
            var windowHandle = IntPtr.Zero;
            var currentProcess = Process.GetCurrentProcess();

            string currentFileName;
            try
            {
                currentFileName = currentProcess.MainModule?.FileName;
            }
            catch (Exception)
            {
                return IntPtr.Zero;
            }

            foreach (var enumeratedProcess in Process.GetProcessesByName(currentProcess.ProcessName))
            {
                try
                {
                    if (enumeratedProcess.Id != currentProcess.Id &&
                        enumeratedProcess.MainModule?.FileName == currentFileName &&
                        enumeratedProcess.MainWindowHandle != IntPtr.Zero)
                        windowHandle = enumeratedProcess.MainWindowHandle;
                }
                catch (Exception)
                {
                    // Reading MainModule needs to open the other process, which fails for anything
                    // running as another user or at a higher integrity level. Skipping such a
                    // process is correct -- it is not an instance we could switch to anyway -- and
                    // it must not take the whole launch down before the logger exists.
                }
            }
            return windowHandle;
        }
    }
}