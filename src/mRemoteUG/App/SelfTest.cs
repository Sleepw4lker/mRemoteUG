using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using mRemoteUG.Connection.Protocol;
using mRemoteUG.Connection.Protocol.RDP;
using Microsoft.Win32;
using mRemoteUG.Config.Settings;
using mRemoteUG.Messages;
using mRemoteUG.Messages.MessageWriters;
using mRemoteUG.Messages.WriterDecorators;
using mRemoteUG.Tools;
using mRemoteUG.UI.Panels;
using mRemoteUG.UI.Window;
using MSTSCLib;

namespace mRemoteUG.App
{
    /// <summary>
    /// A non-interactive check that the application can actually start on the machine it was
    /// deployed to, run with <c>mRemoteUG.exe --selftest</c>.
    /// </summary>
    /// <remarks>
    /// The development machine cannot run mRemoteUG, so the feedback loop is: build here, copy the
    /// folder across, read a log back. Without this that loop returns "it did not work"; with it,
    /// it returns which step failed and why. Each check writes a PASS or FAIL line and the process
    /// exit code reflects the whole run, so it is also usable from a script.
    /// </remarks>
    public static class SelfTest
    {
        public const string CommandLineSwitch = "--selftest";

        private static readonly StringBuilder Report = new StringBuilder();
        private static int _failures;

        /// <summary>Runs the self-test with a deliberately larger UI font.</summary>
        public const string LargeFontSwitch = "--largefont";

        /// <summary>How much larger, as a multiple of whatever Windows reports.</summary>
        private const float LargeFontScale = 1.5f;

        public static bool IsRequested(IEnumerable<string> args)
        {
            return args != null &&
                   args.Any(a => string.Equals(a, CommandLineSwitch, StringComparison.OrdinalIgnoreCase));
        }

        public static bool LargeFontRequested(IEnumerable<string> args)
        {
            return args != null &&
                   args.Any(a => string.Equals(a, LargeFontSwitch, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Runs the self-test forced into the dark theme, whatever Windows is set to.</summary>
        public const string DarkSwitch = "--dark";

        /// <summary>Runs the self-test forced into the light theme, whatever Windows is set to.</summary>
        public const string LightSwitch = "--light";

        /// <summary>
        /// Which theme the command line asked for, defaulting to whatever the user has saved.
        /// </summary>
        /// <remarks>
        /// The theme has to be chosen out here rather than inside the check that measures it:
        /// Application.SetColorMode only reaches controls created after it, and none of the
        /// native re-theming happens on an existing window handle at all. Both switches at once
        /// is contradictory and resolves to dark, which is the interesting one.
        /// </remarks>
        public static UI.UiTheme RequestedTheme(IEnumerable<string> args)
        {
            if (HasSwitch(args, DarkSwitch)) return UI.UiTheme.Dark;
            if (HasSwitch(args, LightSwitch)) return UI.UiTheme.Light;
            return Settings.Default.UiTheme;
        }

        /// <summary>
        /// Whether <paramref name="args"/> contains the switch <paramref name="name"/>.
        /// </summary>
        /// <remarks>
        /// Shared with <see cref="ProgramRoot"/>, which has to recognise <c>--verbose</c> on the
        /// first line of Main - before anything has had a chance to log.
        /// </remarks>
        internal static bool HasSwitch(IEnumerable<string>? args, string name)
        {
            return args != null &&
                   args.Any(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// The system UI font, enlarged - the same family, so only the size is under test.
        /// </summary>
        public static Font LargeUiFont()
        {
            var system = SystemFonts.MessageBoxFont;
            return new Font(system.FontFamily, system.SizeInPoints * LargeFontScale, system.Style);
        }

        public static int Run()
        {
            Line("mRemoteUG self-test");
            Line("===================");
            Line("");

            ReportEnvironment();
            Check("DPI awareness", CheckDpiAwareness);
            Check("Log file deferred", CheckLogFileDeferred);
            Check("Logging", CheckLogging);
            Check("Message routing", CheckMessageRouting);
            Check("Message replay", CheckMessageReplay);
            Check("Settings", CheckSettings);
            Check("Panel layout", CheckPanelLayout);
            Check("Embedded resources", CheckEmbeddedResources);
            Check("Connection icons", CheckConnectionIcons);
            Check("Icon artwork", CheckGlyphs);
            Check("Forms and controls", CheckFormsConstruct);
            Check("Auto-scale baselines", CheckAutoScaleBaselines);
            Check("Toolbar metrics", CheckToolbarMetrics);
            Check("DPI change on hosted combo boxes", CheckDpiChangeOnHostedComboBoxes);
            Check("Colour theme", CheckColourTheme);
            Check("Task dialog", CheckTaskDialogAvailable);
            Check("PuTTY session watcher", CheckPuttySessionWatcher);
            Check("PuTTY hosting", CheckPuttyHosting);
            Check("RDP ActiveX control", CheckRdpControl);

            // Last: it reports on everything above it.
            Check("UI thread", CheckUiThread);

            Line("");
            Line(_failures == 0
                     ? "RESULT: PASS"
                     : $"RESULT: FAIL ({_failures} check(s) failed)");

            Emit();
            return _failures == 0 ? 0 : 1;
        }


        /// <summary>
        /// Whether anything failed on the UI thread while the checks above were running.
        /// </summary>
        /// <remarks>
        /// Last, so it covers every check before it. An exception raised inside a window procedure
        /// does not reach the <c>Check</c> that caused it - WinForms catches it and hands it to
        /// <see cref="Application.ThreadException"/> - so without this a run could raise one and
        /// still report RESULT: PASS. It used to do worse than that: with no handler installed,
        /// WinForms showed its own dialog, which during teardown could not create its window handle
        /// and killed the process. See platform-findings.
        /// </remarks>
        private static string CheckUiThread()
        {
            var raised = CrashLogger.UiThreadExceptionCount;
            if (raised > 0)
                throw new InvalidOperationException(
                    $"{raised} exception(s) reached the UI thread's handler during this run. " +
                    "They are in the log, with stack traces.");

            return "no exception reached Application.ThreadException; handler installed: " +
                   (CrashLogger.ThreadExceptionInstalled ? "yes" : "NO - a UI-thread failure would go to WinForms");
        }

        private static void ReportEnvironment()
        {
            Line("Environment");
            Line($"  Runtime         : {RuntimeInformation.FrameworkDescription}");
            Line($"  OS              : {RuntimeInformation.OSDescription}");
            Line($"  Process arch    : {RuntimeInformation.ProcessArchitecture}");
            Line($"  Base directory  : {AppContext.BaseDirectory}");
            Line($"  Entry assembly  : {Assembly.GetEntryAssembly()?.GetName().FullName}");
            // Measured, not assumed: this is the effective mode after Windows has applied the
            // manifest. It must read PerMonitorV2 -- see the note in Properties\app.manifest.
            Line($"  DPI mode        : {Application.HighDpiMode} (expected PerMonitorV2)");
            Line($"  Mixed hosting   : previous behaviour was {ProgramRoot.PreviousDpiHostingBehavior}" +
                 " (Default means the call worked, Invalid that it failed)");
            Line($"  Default font    : {Control.DefaultFont.Name} {Control.DefaultFont.SizeInPoints}pt");
            Line($"  Verbose logging : {(LogMessageTypeFilteringOptions.ForceDebugMessages ? "on (--verbose)" : "off")}");
            Line($"  Session         : {SessionDescription()}");
            foreach (var screen in Screen.AllScreens)
            {
                Line($"  Screen          : {screen.DeviceName} {screen.Bounds.Width}x{screen.Bounds.Height}" +
                     $" at {screen.Bounds.X},{screen.Bounds.Y} dpi {DpiForScreen(screen)}" +
                     (screen.Primary ? " (primary)" : ""));
            }
            Line("");
        }

        /// <summary>
        /// The <c>AutoScaleDimensions</c> a container using this font should declare, i.e. what
        /// the font measures at 96 DPI, whatever DPI this machine happens to run at.
        /// </summary>
        /// <remarks>
        /// This is the number that belongs in a designer file, and it cannot be read back off a
        /// constructed form: <c>PerformAutoScale</c> overwrites <c>AutoScaleDimensions</c> with
        /// <c>CurrentAutoScaleDimensions</c> once it has scaled, so afterwards every form reports
        /// a factor of exactly 1.000 no matter what it declared.
        /// <para>
        /// A font given in pixels is the same size on any DC, so measuring one sized for 96 DPI
        /// gives the 96 DPI metric while running at 120 or 144. WinForms' own
        /// <c>CurrentAutoScaleDimensions</c> does the measuring, so this cannot drift from the
        /// algorithm it has to match.
        /// </para>
        /// </remarks>
        private static System.Drawing.SizeF BaselineAt96Dpi(System.Drawing.Font font)
        {
            using (var at96 = new System.Drawing.Font(font.FontFamily,
                                                      font.SizeInPoints * 96f / 72f,
                                                      font.Style,
                                                      System.Drawing.GraphicsUnit.Pixel))
            using (var probe = new ContainerControl())
            {
                // Font before AutoScaleMode, and a real handle: CurrentAutoScaleDimensions is
                // cached the first time it is asked for, so setting the mode first measures the
                // default font and the answer comes back wrong but plausible.
                probe.Font = at96;
                probe.AutoScaleMode = AutoScaleMode.Font;
                probe.CreateControl();
                return probe.CurrentAutoScaleDimensions;
            }
        }

        /// <summary>
        /// The DPI of the monitor a screen sits on, asked of Windows rather than inferred.
        /// </summary>
        private static string DpiForScreen(Screen screen)
        {
            var point = new System.Drawing.Point(screen.Bounds.X + screen.Bounds.Width / 2,
                                                 screen.Bounds.Y + screen.Bounds.Height / 2);
            var monitor = NativeMethods.MonitorFromPoint(point, NativeMethods.MONITOR_DEFAULTTONEAREST);
            if (monitor == IntPtr.Zero)
                return "unknown";

            return NativeMethods.GetDpiForMonitor(monitor, NativeMethods.MonitorDpiType.Effective,
                                                  out var dpiX, out var dpiY) == 0
                ? $"{dpiX}x{dpiY} ({dpiX * 100 / 96}%)"
                : "unknown";
        }

        /// <summary>
        /// Proves Windows actually granted the DPI awareness the manifest asks for.
        /// </summary>
        /// <remarks>
        /// A manifest that fails to parse stops the application outright, so it cannot go
        /// unnoticed. A manifest that parses and grants something other than what it asks for is
        /// silent, and every other part of the HiDPI behaviour rests on this one value being
        /// right - so this fails the run rather than reporting a line.
        /// </remarks>
        /// <summary>
        /// The window station this process is attached to, or null if it cannot be asked.
        /// </summary>
        private static string WindowStationName()
        {
            try
            {
                var station = NativeMethods.GetProcessWindowStation();
                if (station == IntPtr.Zero)
                    return null;

                var name = new StringBuilder(256);
                return NativeMethods.GetUserObjectInformation(station, NativeMethods.UoiName,
                                                              name, name.Capacity * 2, out _)
                    ? name.ToString()
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// True when this process is running somewhere Windows treats as interactive.
        /// </summary>
        /// <remarks>
        /// Decided from the window station name, not from <c>Environment.UserInteractive</c>: that
        /// property reads true in an interactive session, which says nothing about what it does in a
        /// service, and .NET does not document it as being computed from the station. A Windows
        /// service is attached to a station of its own - <c>Service-0x0-3e7$</c> for LocalSystem -
        /// and only an interactive logon gets <c>WinSta0</c>.
        /// <para>
        /// An unknown station is treated as interactive on purpose: the only thing this decides is
        /// whether a check may be softened, and a check that softens itself because a probe failed
        /// is worse than one that fails loudly.
        /// </para>
        /// </remarks>
        private static bool RunningInteractively()
        {
            var station = WindowStationName();
            return station == null ||
                   station.StartsWith("WinSta0", StringComparison.OrdinalIgnoreCase);
        }

        private static string SessionDescription()
        {
            var station = WindowStationName() ?? "unknown";
            var id = "unknown";
            try { id = Process.GetCurrentProcess().SessionId.ToString(); }
            catch (Exception) { /* leave it unknown */ }

            return $"id {id}, window station {station} " +
                   (RunningInteractively() ? "(interactive)" : "(NOT interactive - a service session)");
        }

        /// <summary>
        /// The effective DPI awareness, which must be Per-Monitor V2 wherever Windows will grant it.
        /// </summary>
        /// <remarks>
        /// Measured after Windows has applied the manifest, so it answers "what did the process
        /// actually get" rather than "what did it ask for". The asking is pinned separately, by
        /// <c>BinaryFileTests.TheEmbeddedManifestAsksForPerMonitorV2</c>, which reads the manifest
        /// out of the built executable and can therefore be checked anywhere.
        /// <para>
        /// **A service session is not granted PerMonitorV2.** Measured on the Forgejo runner once it
        /// was started as a Windows service: the same binary that reports PerMonitorV2 on an
        /// interactive logon reports PerMonitor there, with a single 1024x768 96-DPI `WinDisc`
        /// placeholder for a display. So in a non-interactive session this reports what it got
        /// instead of failing - there is nothing the application could do about it, and failing
        /// would only make CI red for the environment rather than for the code. Anything below
        /// per-monitor still fails, because that would be the manifest being ignored outright.
        /// </para>
        /// <para>
        /// Worth knowing when reading a report from such a session: every DPI number in it comes
        /// from that placeholder display, so the toolbar metrics, the icon sizes and the auto-scale
        /// baselines are all measured at 96 DPI and prove nothing about real hardware.
        /// </para>
        /// </remarks>
        private static string CheckDpiAwareness()
        {
            var mode = Application.HighDpiMode;
            var interactive = RunningInteractively();

            if (mode != HighDpiMode.PerMonitorV2)
            {
                if (!interactive && mode == HighDpiMode.PerMonitor)
                {
                    return $"PerMonitor, not PerMonitorV2: {SessionDescription()}. A service session " +
                           "is not granted PerMonitorV2, so this is the environment and not the " +
                           "manifest - which BinaryFileTests pins separately. Every DPI number in " +
                           "this report therefore comes from a placeholder display.";
                }

                throw new InvalidOperationException(
                    $"Application.HighDpiMode is {mode}, expected PerMonitorV2. " +
                    "Properties\\app.manifest no longer asks for it, or Windows refused it. The " +
                    "dpiAwareness list has a single value, so there is nothing left to fall back to. " +
                    $"Session: {SessionDescription()}.");
            }

            // Not a failure: only the PuTTY protocols degrade without mixed-mode hosting, so the
            // rest of the run is still worth reporting.
            var previous = ProgramRoot.PreviousDpiHostingBehavior;
            var hosting = previous == NativeMethods.DpiHostingBehavior.Invalid
                ? "mixed-mode DPI hosting REFUSED - SetThreadDpiHostingBehavior returned Invalid, " +
                  "so a hosted PuTTY window may be misplaced or wrongly scaled"
                : $"mixed-mode DPI hosting on (previous behaviour {previous})";

            return $"PerMonitorV2; {hosting}";
        }

        /// <summary>
        /// Reports the auto-scale baseline every container is actually running with.
        /// </summary>
        /// <remarks>
        /// <c>AutoScaleDimensions</c> is the font metric a layout was authored against, and the
        /// runtime scale factor is <c>CurrentAutoScaleDimensions / AutoScaleDimensions</c>.
        /// Declaring the wrong baseline neither throws nor looks wrong on the machine it was typed
        /// on; it silently resizes every control on the form. The right value cannot be read off
        /// the source, so it is measured here and read back from the log.
        /// <para>
        /// The numeric check lives in <c>DpiScalingTests</c>, where the expected factor can be
        /// derived from the DPI. What fails here is the unambiguous case: a container that scales
        /// by nothing at all because it never declared a mode or a baseline.
        /// </para>
        /// </remarks>
        private static string CheckAutoScaleBaselines()
        {
            var lines = new List<string>();
            var unscaled = new List<string>();

            // BaseWindow is a base class that is never shown on its own - only the tests construct
            // one. It deliberately declares nothing, because its derived windows do not share a
            // font and a baseline set in its constructor is cached before they pick one; the
            // remarks on BaseWindow's constructor have the measurement.
            var exempt = new[] { typeof(UI.Window.BaseWindow) };

            foreach (var type in ConstructibleControls()
                                 .Where(t => typeof(ContainerControl).IsAssignableFrom(t))
                                 .Where(t => !exempt.Contains(t)))
            {
                ContainerControl container = null;
                try
                {
                    container = (ContainerControl)Activator.CreateInstance(type);

                    // Read the declared baseline before forcing a handle. PerformAutoScale
                    // overwrites AutoScaleDimensions with CurrentAutoScaleDimensions once it has
                    // scaled, so after that every container reports a factor of exactly 1.000
                    // whatever its designer actually said.
                    var declared = container.AutoScaleDimensions;

                    container.CreateControl();
                    var current = container.CurrentAutoScaleDimensions;
                    var baseline = BaselineAt96Dpi(container.Font);

                    var ok = !declared.IsEmpty &&
                             container.AutoScaleMode != AutoScaleMode.None &&
                             container.AutoScaleMode != AutoScaleMode.Inherit;

                    lines.Add($"{(ok ? " " : "!")} {type.Name,-26} dpi {container.DeviceDpi,-4} " +
                              $"font {container.Font.SizeInPoints}pt  " +
                              $"mode {container.AutoScaleMode,-8} " +
                              $"declared {declared.Width}x{declared.Height,-6} " +
                              $"current {current.Width}x{current.Height,-6} " +
                              $"baseline@96 {baseline.Width}x{baseline.Height,-6} " +
                              $"client {container.ClientSize.Width}x{container.ClientSize.Height}");

                    if (!ok)
                    {
                        unscaled.Add($"{type.FullName} (mode {container.AutoScaleMode}, " +
                                     $"declared {declared.Width}x{declared.Height}, " +
                                     $"font {container.Font.Name} {container.Font.SizeInPoints}pt " +
                                     $"-> should declare {baseline.Width}F, {baseline.Height}F)");
                    }
                }
                finally
                {
                    container?.Dispose();
                }
            }

            var table = string.Join("\n", lines);

            if (unscaled.Count > 0)
                throw new InvalidOperationException(
                    $"{unscaled.Count} container(s) do not auto-scale at all:\n" + string.Join("\n", unscaled) +
                    "\nEach needs AutoScaleMode.Font and the AutoScaleDimensions its own font measures " +
                    "at 96 DPI.\n\nMeasured on this machine:\n" + table);

            return table;
        }

        /// <summary>
        /// Reports what every toolbar measures on this machine, and what it would measure elsewhere.
        /// </summary>
        /// <remarks>
        /// This reports rather than fails, on the same grounds as the colour theme check: the unit
        /// tests already fail on the computation, and what cannot be had here is the observation.
        /// The machine this is developed on has one monitor, so a DPI change never happens and the
        /// numbers below are the only evidence of what the bar would do on hardware that has two.
        /// <para>
        /// The quick connect field's width is printed at five DPIs because it can be: the recompute
        /// takes its DPI as an argument rather than reading the monitor, so a single-monitor machine
        /// can still answer what it would choose at 192. A tester on real hardware can compare that
        /// column against what they see.
        /// </para>
        /// </remarks>
        private static string CheckToolbarMetrics()
        {
            var lines = new List<string>();

            foreach (var type in ConstructibleControls()
                                 .Where(t => typeof(ToolStrip).IsAssignableFrom(t)))
            {
                ToolStrip strip = null;
                try
                {
                    strip = (ToolStrip)Activator.CreateInstance(type);

                    // What DpiScaling.FollowDpiChange does in the running application, and the
                    // only way this check can see a larger system font at all: a ToolStrip reads
                    // ToolStripManager's process-wide default rather than Control.DefaultFont, so
                    // Application.SetDefaultFont under --largefont never reaches a bare strip.
                    // In the window it is the form's font that is handed over, which is this.
                    strip.Font = Control.DefaultFont;

                    lines.Add($"  {type.Name,-26} dpi {strip.DeviceDpi,-4} " +
                              $"font {strip.Font.Name} {strip.Font.SizeInPoints}pt  " +
                              $"glyphs {strip.ImageScalingSize.Width}x{strip.ImageScalingSize.Height}  " +
                              $"items {strip.Items.Count}");

                    foreach (ToolStripItem item in strip.Items)
                    {
                        lines.Add($"      {item.GetType().Name,-24} {item.Name,-22} " +
                                  $"autosize {item.AutoSize,-5} " +
                                  $"size {item.Size.Width}x{item.Size.Height,-6} " +
                                  $"margin {item.Margin.Left},{item.Margin.Top}," +
                                  $"{item.Margin.Right},{item.Margin.Bottom,-4} " +
                                  $"imagescaling {item.ImageScaling}");
                    }

                    if (strip is UI.Controls.QuickConnectToolStrip bar)
                    {
                        var widths = new List<string>();
                        foreach (var dpi in new[] { 96, 120, 144, 168, 192 })
                        {
                            bar.RefreshEntryFieldMetrics(dpi);
                            var field = bar.Items.OfType<UI.Controls.QuickConnectComboBox>().First();
                            widths.Add($"{dpi}:{field.Width}");
                        }

                        lines.Add($"      entry field width by dpi   {string.Join("  ", widths)}");

                        // Leave it measuring for the DPI it is actually on, not the last of the five.
                        bar.RefreshEntryFieldMetrics(bar.DeviceDpi);
                    }
                }
                finally
                {
                    strip?.Dispose();
                }
            }

            return string.Join("\n", lines);
        }

        /// <summary>
        /// Proves the task dialog Windows provides can actually be shown on this machine.
        /// </summary>
        /// <remarks>
        /// There is no layout left here to check - the OS owns it - but there is a deployment
        /// dependency. <c>TaskDialog.ShowDialog</c> throws unless visual styles are enabled and
        /// the Common Controls v6 assembly resolved, and the manifest that provides the second is
        /// exactly the kind of thing that breaks silently on a machine nobody can debug on.
        /// <para>
        /// The dialog closes itself the moment it exists, so this stays non-interactive: nothing
        /// is left on screen and nothing waits for a click.
        /// </para>
        /// </remarks>
        private static string CheckTaskDialogAvailable()
        {
            var page = UI.TaskDialog.CTaskDialog.BuildPage(
                "mRemoteUG",
                "Do you want to disconnect this session?",
                "The connection to the remote host will be closed.",
                "Some expanded detail.",
                "A footer note.",
                "Do not ask me this again",
                "",
                "Create a new file|Open a different file|Exit",
                UI.TaskDialog.ETaskDialogButtons.YesNoCancel,
                UI.TaskDialog.ESysIcons.Question,
                UI.TaskDialog.ESysIcons.Information,
                0);

            page.Created += (sender, args) => page.BoundDialog?.Close();

            var clicked = TaskDialog.ShowDialog(page);

            return $"shown and dismissed by comctl32; {page.Buttons.Count} buttons built " +
                   $"(closed as \"{clicked.Text}\"), with expander, footnote and verification check box";
        }

        // TreeView and ListView keep their own copy of the colours they paint with, set by
        // message at handle creation. Reading them back is how a control that was actually themed
        // is told apart from one that merely inherited a managed colour.
        private const int TvmGetBkColor = 0x111F;
        private const int TvmGetTextColor = 0x1122;
        private const int LvmGetBkColor = 0x1019;
        private const int LvmGetTextColor = 0x1023;

        /// <summary>
        /// Reports what the colour theme actually did, control by control.
        /// </summary>
        /// <remarks>
        /// The application is developed on a machine that cannot run it, so "does dark mode work"
        /// has to be answerable from a log file. Three things are worth measuring and none of them
        /// can be read off the source:
        /// <list type="number">
        /// <item>whether <c>SystemColors</c> re-pointed at all;</item>
        /// <item>whether each realized control took the native dark theme, which is a separate
        /// mechanism from the managed colours and happens only at handle creation - a control
        /// whose managed <c>BackColor</c> is dark while the window it owns still paints light is
        /// exactly the failure this catches;</item>
        /// <item>which controls set a colour of their own, because those are frozen at whatever
        /// they were typed as and are the only ones that need hand work.</item>
        /// </list>
        /// This reports rather than fails. The point of it is to turn "which controls still need
        /// explicit colouring" into a list read off a log, and failing on that list would be to
        /// assert the answer before measuring it. Run it as <c>mRemoteUG.exe --selftest --dark</c>
        /// and again with <c>--light</c>.
        /// </remarks>
        private static string CheckColourTheme()
        {
            var lines = new List<string>();

            lines.Add($"  mode requested  : {Application.ColorMode}");
            lines.Add($"  system is set to: {Application.SystemColorMode}");
            lines.Add($"  dark mode active: {Application.IsDarkModeEnabled}");
            // Reported as the renderer rather than as ToolStripManager.RenderMode, which
            // answers Professional even once the manager has been set to System and the
            // renderer it hands out is the system one. The renderer is what paints.
            lines.Add($"  manager renderer: {ToolStripManager.Renderer.GetType().Name}" +
                      $" (RenderMode reports {ToolStripManager.RenderMode})");

            foreach (var known in new[]
                     {
                         KnownColor.Control, KnownColor.ControlText, KnownColor.Window,
                         KnownColor.WindowText, KnownColor.Highlight, KnownColor.HighlightText,
                         KnownColor.GrayText, KnownColor.Info, KnownColor.InfoText
                     })
            {
                lines.Add($"  SystemColors.{known,-14}: {Hex(Color.FromKnownColor(known))}");
            }

            lines.Add($"  title bar dark  : {MeasureTitleBar()}");
            lines.Add("");

            var frozen = new List<string>();
            var stale = new List<string>();

            foreach (var type in ConstructibleControls()
                                 .Where(t => typeof(ContainerControl).IsAssignableFrom(t)))
            {
                Control container = null;
                try
                {
                    container = (Control)Activator.CreateInstance(type);
                    foreach (var control in new[] { container }.Concat(Descendants(container)))
                    {
                        // Touching Handle forces creation whether or not the control is visible,
                        // which CreateControl would skip. Handle creation is where the native
                        // theming happens, so without this there would be nothing to read back.
                        IntPtr handle;
                        try
                        {
                            handle = control.Handle;
                        }
                        catch (Exception)
                        {
                            continue;
                        }

                        var label = string.IsNullOrEmpty(control.Name) ? control.GetType().Name : control.Name;
                        var name = $"{type.Name}.{label}";

                        if (!control.BackColor.IsSystemColor && control.BackColor != Color.Transparent)
                            frozen.Add($"{name} BackColor {Hex(control.BackColor)}");
                        if (!control.ForeColor.IsSystemColor && control.ForeColor != Color.Transparent)
                            frozen.Add($"{name} ForeColor {Hex(control.ForeColor)}");

                        switch (control)
                        {
                            case TreeView tree:
                                Compare(stale, lines, $"{name} (TreeView)", handle,
                                        tree.BackColor, tree.ForeColor, TvmGetBkColor, TvmGetTextColor);
                                break;
                            case ListView list:
                                Compare(stale, lines, $"{name} (ListView {list.View})", handle,
                                        list.BackColor, list.ForeColor, LvmGetBkColor, LvmGetTextColor);
                                break;
                            case ToolStrip strip:
                                lines.Add($"  {name,-46} {strip.RenderMode} -> " +
                                          $"{strip.Renderer.GetType().Name}{OverrideOf(strip.Renderer)}");
                                break;
                            case TabControl tabs:
                                // The runtime type, because that is the whole fix: a stock
                                // TabControl leaves the strip around its tabs to the theme, which
                                // has no dark variant. A designer promotion is one line and is
                                // exactly the kind of thing a later designer round trip undoes, so
                                // it is reported rather than assumed.
                                lines.Add($"  {name,-46} {tabs.GetType().Name}, background " +
                                          $"{Hex(tabs.BackColor)}");
                                break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    lines.Add($"  {type.Name}: could not be measured ({ex.GetType().Name}: {ex.Message})");
                }
                finally
                {
                    container?.Dispose();
                }
            }

            if (stale.Count > 0)
            {
                lines.Add("");
                lines.Add($"  {stale.Count} control(s) whose native theming did not run:");
                lines.AddRange(stale.Select(d => "    " + d));
            }

            if (frozen.Count > 0)
            {
                lines.Add("");
                lines.Add($"  {frozen.Distinct().Count()} explicit colour(s), which do not follow the theme:");
                lines.AddRange(frozen.Distinct().Select(f => "    " + f));
            }

            return string.Join("\n", lines);
        }

        private static string Hex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

        /// <summary>
        /// Reports the colours a common control actually paints with, and flags the ones the dark
        /// theme failed to reach.
        /// </summary>
        /// <remarks>
        /// The obvious check - native colour equals managed colour - is wrong, and measuring both
        /// modes is what showed it. A ListView answers LVM_GETBKCOLOR with 0 and a TreeView
        /// answers TVM_GETTEXTCOLOR with CLR_NONE in a <em>light</em> run too, where both are
        /// plainly correct on screen. Those values mean "nobody told me, I am using my default",
        /// not "I am stale", so comparing them against the managed colour reports a problem on
        /// every run and would train the reader to ignore this section.
        /// <para>
        /// What is genuinely wrong is a control still painting a <em>light background</em> while
        /// the application is in dark mode - the signature of native theming that never happened,
        /// because every one of those code paths runs at handle creation and nowhere else. That is
        /// what this flags, and it stays quiet otherwise. The foreground is reported but not
        /// judged: a light foreground in dark mode is the correct answer, not a stale one.
        /// </para>
        /// </remarks>
        private static void Compare(List<string> stale, List<string> lines, string name,
                                    IntPtr handle, Color managedBack, Color managedFore,
                                    int backMessage, int foreMessage)
        {
            var nativeBack = (int)NativeMethods.SendMessage(handle, backMessage, IntPtr.Zero, IntPtr.Zero);
            var nativeFore = (int)NativeMethods.SendMessage(handle, foreMessage, IntPtr.Zero, IntPtr.Zero);

            var backIsStale = StillLight(nativeBack);

            lines.Add($"  {name,-46} back {Hex(managedBack)}/{ColorRef(nativeBack)} " +
                      $"fore {Hex(managedFore)}/{ColorRef(nativeFore)}" +
                      (backIsStale ? "   <- still light" : ""));

            if (backIsStale)
                stale.Add($"{name}: managed background is {Hex(managedBack)} but the window still " +
                          $"paints {ColorRef(nativeBack)}, so its native theming did not run");
        }

        /// <summary>
        /// Whether a COLORREF is a light colour while the application is running dark.
        /// </summary>
        /// <remarks>
        /// CLR_NONE and an untouched 0 both mean the control never had one set, which is not a
        /// failure - see the remarks on <see cref="Compare"/>. Anything else is judged on
        /// perceived luminance rather than against an exact value, because the question is "did
        /// this stay bright", not "is it the one shade I expected".
        /// </remarks>
        private static bool StillLight(int colorRef)
        {
            if (!Application.IsDarkModeEnabled || colorRef == -1 || colorRef == 0)
                return false;

            int r = colorRef & 0xFF, g = (colorRef >> 8) & 0xFF, b = (colorRef >> 16) & 0xFF;
            return (0.299 * r + 0.587 * g + 0.114 * b) > 128;
        }

        /// <summary>A COLORREF as written, which is BGR rather than RGB.</summary>
        private static string ColorRef(int value)
        {
            if (value == -1)
                return "unset";
            return $"#{value & 0xFF:X2}{(value >> 8) & 0xFF:X2}{(value >> 16) & 0xFF:X2}";
        }

        /// <summary>
        /// The dark renderer a system-rendered ToolStrip delegates to, which has no public name.
        /// </summary>
        /// <remarks>
        /// <c>ToolStripSystemDarkModeRenderer</c> is internal, so the only supported way to get it
        /// is to ask for system rendering and let the framework substitute it. Reflection here is
        /// reporting only - nothing depends on the name, and an absent property just prints
        /// nothing.
        /// </remarks>
        private static string OverrideOf(ToolStripRenderer renderer)
        {
            var property = renderer.GetType().GetProperty("RendererOverride",
                                                          BindingFlags.NonPublic | BindingFlags.Instance);
            var value = property?.GetValue(renderer);
            return value == null ? "" : $" (override {value.GetType().Name})";
        }

        /// <summary>
        /// Whether a window's title bar is actually drawn dark, read back from the compositor.
        /// </summary>
        /// <remarks>
        /// WinForms applies this from Control.SetVisibleCore, so a window that is never shown never
        /// gets it. Hence a real form, shown far enough off the desktop that nothing appears on
        /// screen, and closed again immediately.
        /// </remarks>
        private static string MeasureTitleBar()
        {
            try
            {
                using (var form = new Form
                       {
                           StartPosition = FormStartPosition.Manual,
                           Location = new Point(-32000, -32000),
                           Size = new Size(100, 100),
                           ShowInTaskbar = false
                       })
                {
                    form.Show();
                    var hr = NativeMethods.DwmGetWindowAttribute(
                        form.Handle, NativeMethods.DwmwaUseImmersiveDarkMode, out var value, sizeof(int));
                    form.Close();

                    if (hr != 0)
                        return $"could not be read (hr 0x{hr:X8})";
                    return value != 0 ? "yes" : "no";
                }
            }
            catch (Exception ex)
            {
                return $"could not be read ({ex.GetType().Name}: {ex.Message})";
            }
        }

        private static IEnumerable<Control> Descendants(Control root)
        {
            foreach (Control child in root.Controls)
            {
                yield return child;
                foreach (var descendant in Descendants(child))
                    yield return descendant;
            }
        }

        /// <summary>
        /// The same sweep <see cref="CheckFormsConstruct"/> makes, shared so the two cannot drift.
        /// </summary>
        private static List<Type> ConstructibleControls()
        {
            var skipped = new[] { typeof(UI.Forms.FrmMain) };
            return typeof(SelfTest).Assembly
                                   .GetTypes()
                                   .Where(t => typeof(Control).IsAssignableFrom(t))
                                   .Where(t => !t.IsAbstract && !t.IsGenericTypeDefinition)
                                   .Where(t => t.GetConstructor(Type.EmptyTypes) != null)
                                   .Where(t => !skipped.Contains(t))
                                   .OrderBy(t => t.FullName)
                                   .ToList();
        }

        private static void Check(string name, Func<string> check)
        {
            string detail;
            try
            {
                detail = check();
            }
            catch (Exception ex)
            {
                _failures++;
                Line($"FAIL {name}");
                Line($"       {ex.GetType().Name}: {ex.Message}");
                foreach (var frame in (ex.StackTrace ?? "").Split('\n').Take(5))
                    Line($"       {frame.TrimEnd()}");
                return;
            }

            Line($"PASS {name}");
            if (!string.IsNullOrEmpty(detail))
                foreach (var l in detail.Split('\n'))
                    Line($"       {l.TrimEnd()}");
        }

        /// <summary>
        /// Proves that starting the application does not create a log file on its own.
        /// </summary>
        /// <remarks>
        /// The appender used to be built in the Logger constructor, which opened its file before
        /// anything had been logged. A user who had switched every message type off under
        /// Options -> Notifications -> Logging still got a log file, and one at the default path
        /// even when a custom path was configured. This has to run before anything else touches
        /// the log, which is why it is registered first - and why it cannot be a unit test, the
        /// logger being a process-wide singleton that stays configured once anything has used it.
        /// </remarks>
        private static string CheckLogFileDeferred()
        {
            if (Logger.Instance.IsConfigured)
                throw new InvalidOperationException("The log appender was built before anything asked to log.");

            return $"no appender built yet; the log would go to {Logger.Instance.LogPath}";
        }

        /// <summary>
        /// Probes the file appender directly rather than through the message pipeline.
        /// </summary>
        /// <remarks>
        /// This is one of only two places outside
        /// <see cref="Messages.MessageWriters.TextLogMessageWriter"/> allowed to write to the log,
        /// and it is deliberate: the check exists to prove the sink reaches disk. Sending the
        /// probe through the Options -> Notifications filter would test the filter instead, and
        /// would report logging as broken to a user who had merely unticked Information. The filter
        /// is therefore reported rather than obeyed, so that a log arriving from a user says which
        /// categories were switched on when it was written.
        /// </remarks>
        private static string CheckLogging()
        {
            var log = Logger.Instance.Log;
            if (log == null) throw new InvalidOperationException("Logger.Instance.Log is null.");

            var marker = "self-test " + Guid.NewGuid().ToString("N");
            log.Info(marker, DateTime.Now);

            var path = Logger.Instance.LogPath;
            if (!File.Exists(path))
                throw new FileNotFoundException($"No log file was written to {path}.", path);

            var isDefault = string.Equals(path, Logger.DefaultLogPath, StringComparison.OrdinalIgnoreCase);
            return $"log file: {path} (default location: {(isDefault ? "yes" : "no")})\n" +
                   $"logging debug={OnOff(Settings.Default.TextLogMessageWriterWriteDebugMsgs)} " +
                   $"info={OnOff(Settings.Default.TextLogMessageWriterWriteInfoMsgs)} " +
                   $"warn={OnOff(Settings.Default.TextLogMessageWriterWriteWarningMsgs)} " +
                   $"error={OnOff(Settings.Default.TextLogMessageWriterWriteErrorMsgs)}";
        }

        private static string OnOff(bool value) => value ? "on" : "off";

        /// <summary>
        /// Proves, in the shipped binary, that Options -> Notifications -> Logging actually decides
        /// what reaches the log file.
        /// </summary>
        /// <remarks>
        /// Every message class is written twice, with its setting off and then on, and the file is
        /// read back each time. The settings are changed in memory only and put back afterwards.
        /// Save() is never called, so the choices the user made survive a self-test run.
        /// <para>
        /// Only the log writer is exercised. The popup writer shows a modal message box and the
        /// notification panel writer needs a window, neither of which belongs in a run that has to
        /// complete without anyone present.
        /// </para>
        /// </remarks>
        private static string CheckMessageRouting()
        {
            var options = new LogMessageTypeFilteringOptions();
            var writer = new MessageTypeFilterDecorator(options, new TextLogMessageWriter(Logger.Instance));

            var originalPath = Logger.Instance.LogPath;
            // --verbose forces debug through regardless of the setting, which is the whole point
            // of it - but this check is about the settings, so the override stands aside and is
            // put back afterwards. Without this, --selftest --verbose would fail here.
            var originalForceDebug = LogMessageTypeFilteringOptions.ForceDebugMessages;
            LogMessageTypeFilteringOptions.ForceDebugMessages = false;
            var originalDebug = Settings.Default.TextLogMessageWriterWriteDebugMsgs;
            var originalInfo = Settings.Default.TextLogMessageWriterWriteInfoMsgs;
            var originalWarning = Settings.Default.TextLogMessageWriterWriteWarningMsgs;
            var originalError = Settings.Default.TextLogMessageWriterWriteErrorMsgs;

            var directory = Path.Combine(Path.GetTempPath(), "mRemoteUG-selftest-" + Guid.NewGuid().ToString("N"));
            var results = new List<string>();
            try
            {
                Directory.CreateDirectory(directory);
                var probe = Path.Combine(directory, "routing.log");
                Logger.Instance.SetLogPath(probe);

                foreach (MessageClass messageClass in Enum.GetValues(typeof(MessageClass)))
                {
                    var suppressed = WritesMarker(writer, options, messageClass, probe, allow: false);
                    var written = WritesMarker(writer, options, messageClass, probe, allow: true);

                    if (suppressed)
                        throw new InvalidOperationException($"{messageClass} reached the log with its setting switched off.");
                    if (!written)
                        throw new InvalidOperationException($"{messageClass} did not reach the log with its setting switched on.");

                    results.Add($"{messageClass}: off -> suppressed, on -> written");
                }
            }
            finally
            {
                LogMessageTypeFilteringOptions.ForceDebugMessages = originalForceDebug;
                Settings.Default.TextLogMessageWriterWriteDebugMsgs = originalDebug;
                Settings.Default.TextLogMessageWriterWriteInfoMsgs = originalInfo;
                Settings.Default.TextLogMessageWriterWriteWarningMsgs = originalWarning;
                Settings.Default.TextLogMessageWriterWriteErrorMsgs = originalError;
                Logger.Instance.SetLogPath(originalPath);
                try { Directory.Delete(directory, true); } catch (IOException) { }
            }

            return string.Join("\n", results) +
                   (originalForceDebug ? "\n--verbose was on; it was stood aside for this check" : "");
        }

        /// <summary>
        /// Writes one marker of <paramref name="messageClass"/> with its filter set to
        /// <paramref name="allow"/>, and answers whether it reached the file.
        /// </summary>
        private static bool WritesMarker(IMessageWriter writer, IMessageTypeFilteringOptions options,
                                         MessageClass messageClass, string probe, bool allow)
        {
            switch (messageClass)
            {
                case MessageClass.DebugMsg: options.AllowDebugMessages = allow; break;
                case MessageClass.InformationMsg: options.AllowInfoMessages = allow; break;
                case MessageClass.WarningMsg: options.AllowWarningMessages = allow; break;
                case MessageClass.ErrorMsg: options.AllowErrorMessages = allow; break;
                default: throw new ArgumentOutOfRangeException(nameof(messageClass));
            }

            var marker = $"routing {messageClass} {(allow ? "on" : "off")} {Guid.NewGuid():N}";
            writer.Write(new Messages.Message(messageClass, marker, onlyLog: true));

            if (!File.Exists(probe)) return false;

            // The appender holds the file open, so read it sharing write access.
            using (var stream = new FileStream(probe, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream))
                return reader.ReadToEnd().Contains(marker);
        }

        /// <summary>
        /// Proves that messages collected before the writers exist are still handed over.
        /// </summary>
        /// <remarks>
        /// Startup reports a good deal before frmMain_Load builds the writers. That used to reach
        /// nothing at all, so a log configured to record it began part-way through startup instead.
        /// </remarks>
        private static string CheckMessageReplay()
        {
            var collector = new MessageCollector();
            collector.AddMessage(MessageClass.InformationMsg, "replay probe 1", true);
            collector.AddMessage(MessageClass.InformationMsg, "replay probe 2", true);

            var seen = new List<string>();
            collector.SubscribeAndReplay((o, args) =>
            {
                if (args.NewItems == null) return;
                foreach (IMessage message in args.NewItems)
                    seen.Add(message.Text);
            });

            if (seen.Count != 2)
                throw new InvalidOperationException($"Expected the 2 collected messages to be replayed, saw {seen.Count}.");

            collector.AddMessage(MessageClass.InformationMsg, "replay probe 3", true);

            if (seen.Count != 3)
                throw new InvalidOperationException($"Expected the later message to arrive as well, saw {seen.Count}.");
            if (seen.Distinct().Count() != 3)
                throw new InvalidOperationException("A message was handed over more than once: " + string.Join(", ", seen));

            return "backlog of 2 replayed, later message delivered once";
        }

        private static string CheckSettings()
        {
            // Touching a few settings of different types exercises the provider and the type
            // converters that turn the stored strings back into values.
            var singleInstance = Settings.Default.SingleInstance;
            var formSize = Settings.Default.MainFormSize;
            var puttyPath = Settings.Default.CustomPuttyPath ?? "";

            return $"SingleInstance={singleInstance}, MainFormSize={formSize}, " +
                   $"CustomPuttyPath={(puttyPath.Length == 0 ? "<unset>" : puttyPath)}";
        }

        /// <summary>
        /// Applies the saved panel arrangement to a scratch copy of the main window's splitters,
        /// and reports both what was stored and what it came out as.
        /// </summary>
        /// <remarks>
        /// The measurements are physical pixels and permille shares, and SplitterDistance throws
        /// outright when a value does not fit its container. On the real form that happens during
        /// Load, where it would take the whole window with it, so a saved layout that cannot be
        /// applied has to be found here instead - on the only machine that can open the window at
        /// all.
        /// <para>
        /// Read-only as far as the user is concerned: it restores onto throwaway controls and
        /// never calls Settings.Save().
        /// </para>
        /// </remarks>
        private static string CheckPanelLayout()
        {
            using var form = new Form { ClientSize = new Size(1000, 600) };

            var splitLeft = ScratchSplitter(Orientation.Horizontal, FixedPanel.None, 60, 60);
            var splitDocuments = ScratchSplitter(Orientation.Horizontal, FixedPanel.None, 100, 60);
            var splitMain = ScratchSplitter(Orientation.Vertical, FixedPanel.Panel1, 100, 200);

            var tabs = new DocumentTabControl { Dock = DockStyle.Fill };
            splitDocuments.Panel1.Controls.Add(tabs);
            splitMain.Panel1.Controls.Add(splitLeft);
            splitMain.Panel2.Controls.Add(splitDocuments);
            form.Controls.Add(splitMain);
            form.CreateControl();

            var layout = new MainLayout(splitMain, splitLeft, splitDocuments, tabs);
            layout.HostToolWindows(new BaseWindow(), new BaseWindow(), new BaseWindow());

            MainLayoutSettings.Restore(layout, form.DeviceDpi);
            var applied = layout.CaptureState();

            var settings = Settings.Default;
            var stored = settings.MainLayoutLeftColumnWidth <= 0
                ? "nothing saved yet"
                : $"{settings.MainLayoutLeftColumnWidth}px at {settings.MainFormDpi}dpi, " +
                  $"tree {settings.MainLayoutTreeSharePermille}/1000, " +
                  $"notifications {settings.MainLayoutNotificationsSharePermille}/1000";

            return $"stored: {stored}\n" +
                   $"applied to a 1000x600 window at {form.DeviceDpi}dpi: " +
                   $"left column {applied.LeftColumnWidth}px, tree {applied.TreeSharePermille}/1000, " +
                   $"notifications {applied.NotificationsSharePermille}/1000\n" +
                   $"visible: tree={applied.ShowTree}, config={applied.ShowConfig}, " +
                   $"notifications={applied.ShowNotifications}";
        }

        /// <summary>
        /// A splitter sized before its panel minimums are set, because a fresh SplitContainer is
        /// 150x100 and a minimum that leaves no room for the splitter throws.
        /// </summary>
        private static SplitContainer ScratchSplitter(Orientation orientation, FixedPanel fixedPanel,
                                                      int panel1Min, int panel2Min)
        {
            var split = new SplitContainer
            {
                Size = new Size(1000, 600),
                Orientation = orientation,
                FixedPanel = fixedPanel
            };
            split.Panel1MinSize = panel1Min;
            split.Panel2MinSize = panel2Min;
            split.Dock = DockStyle.Fill;
            return split;
        }

        private static string CheckEmbeddedResources()
        {
            // A renamed or missing manifest resource is the failure mode that survives a clean
            // build and only appears when a window opens.
            var assembly = typeof(SelfTest).Assembly;
            var names = assembly.GetManifestResourceNames();
            if (names.Length == 0)
                throw new InvalidOperationException("The assembly carries no manifest resources.");

            var language = Language.strAbout;
            if (string.IsNullOrEmpty(language))
                throw new InvalidOperationException("Language resources resolved to an empty string.");

            return $"{names.Length} manifest resources; Language lookup returned \"{language}\"";
        }

        private static string CheckConnectionIcons()
        {
            // The connection icons are embedded in the assembly rather than shipped as a
            // folder, so a rename or a dropped csproj glob is invisible until someone opens
            // the icon picker. Resolve every one of them here instead.
            var names = Connection.ConnectionIcon.Icons;
            if (names.Length == 0)
                throw new InvalidOperationException("No connection icons are embedded in the assembly.");

            var unresolved = names.Where(n => Connection.ConnectionIcon.FromString(n) == null).ToList();
            if (unresolved.Count > 0)
                throw new InvalidOperationException(
                    $"{unresolved.Count} icon name(s) did not resolve: {string.Join(", ", unresolved.Take(5))}");

            // Names from before the set was curated. A connections file still carries them, and
            // they heal on read rather than being rewritten, so nothing but this says they work.
            var legacy = Connection.ConnectionIcon.LegacyNames;
            var unhealed = legacy.Keys.Where(n => Connection.ConnectionIcon.FromString(n) == null).ToList();
            if (unhealed.Count > 0)
                throw new InvalidOperationException(
                    $"{unhealed.Count} legacy icon name(s) no longer heal: {string.Join(", ", unhealed.Take(5))}");

            // Every connection created without an explicit choice asks for this one. Resolved
            // rather than read straight off the setting, because a profile upgraded from a
            // pre-rename build still has the old product name stored in it - see
            // ConnectionIcon.DefaultIconName.
            var fallback = Connection.ConnectionIcon.DefaultIconName;
            if (Connection.ConnectionIcon.FromString(fallback) == null)
                throw new InvalidOperationException(
                    $"The default connection icon \"{fallback}\" is not embedded.");

            if (Connection.ConnectionIcon.FromString("no such icon") != null)
                throw new InvalidOperationException("An unknown icon name resolved to an icon.");

            return $"{names.Length} icons embedded, all resolved; " +
                   $"{legacy.Count} legacy name(s) heal; default \"{fallback}\"";
        }

        /// <summary>
        /// Every glyph resolves at every size, and the tint the theme resolved to.
        /// </summary>
        /// <remarks>
        /// The artwork is generated and committed, so nothing in the build checks that a manifest
        /// entry actually produced a file - a glyph that was never rendered at 48 is invisible
        /// until a window opens on a 300% monitor, which is not a monitor this is developed on.
        /// <para>
        /// The two tint values are reported rather than asserted. Glyphs are drawn on both
        /// Control and Window backgrounds, whose foregrounds are ControlText and WindowText, and
        /// are tinted with ControlText for both. Measured, those are <em>not</em> always the same:
        /// in dark mode on Windows 11 ControlText is pure white and WindowText is FFF0F0F0. The
        /// gap is far too small to see, which is why one tint is still the right call - but it is
        /// a gap, so both are printed. A theme that ever opens it up properly shows up here rather
        /// than as a washed-out glyph on one surface.
        /// </para>
        /// </remarks>
        private static string CheckGlyphs()
        {
            var names = mRemoteUG.Resources.GlyphNames;
            if (names.Length == 0)
                throw new InvalidOperationException("No glyphs are embedded in this assembly.");

            var missing = new List<string>();
            foreach (var name in names)
                foreach (var size in UI.Glyphs.Ladder)
                {
                    try
                    {
                        var glyph = UI.Glyphs.Get(name, size);
                        if (glyph.Width != size || glyph.Height != size)
                            missing.Add($"{name}@{size} is {glyph.Width}x{glyph.Height}");
                    }
                    catch (Exception ex)
                    {
                        missing.Add($"{name}@{size}: {ex.Message}");
                    }
                }

            if (missing.Count > 0)
                throw new InvalidOperationException(
                    $"{missing.Count} glyph frame(s) are wrong: {string.Join("; ", missing.Take(5))}");

            var controlText = SystemColors.ControlText.ToArgb();
            var windowText = SystemColors.WindowText.ToArgb();

            Line($"         glyphs        : {names.Length} at {UI.Glyphs.Ladder.Length} sizes " +
                 $"({string.Join(", ", UI.Glyphs.Ladder)})");
            Line($"         tint used     : ControlText {controlText:X8}");
            var gap = Math.Max(Math.Max(
                Math.Abs(SystemColors.ControlText.R - SystemColors.WindowText.R),
                Math.Abs(SystemColors.ControlText.G - SystemColors.WindowText.G)),
                Math.Abs(SystemColors.ControlText.B - SystemColors.WindowText.B));

            Line($"         also resolved : WindowText  {windowText:X8}" +
                 (gap == 0 ? "  (same)" : $"  (differs by {gap}/255 per channel)"));

            foreach (var screen in Screen.AllScreens)
            {
                // DpiForScreen formats for the log ("144x144 (150%)") and answers "unknown" when
                // the monitor cannot be found, so the number is taken off the front rather than
                // parsed whole. Getting that wrong reported "wants 16px" on a 150% monitor.
                var reported = DpiForScreen(screen);
                var digits = System.Text.RegularExpressions.Regex.Match(reported, @"^\d+");
                var dpi = digits.Success ? int.Parse(digits.Value) : UI.DpiScaling.DefaultDpi;
                var wanted = UI.DpiScaling.Scale(16, dpi);
                Line($"         {screen.DeviceName}: dpi {reported}, wants {wanted}px, " +
                     $"draws {UI.Glyphs.LadderSizeFor(wanted)}px");
            }

            return $"{names.Length} glyphs resolved at every ladder size";
        }

        private static string CheckFormsConstruct()
        {
            // Mirrors the FormConstructionTests fixture, but on the deployed machine.
            var types = ConstructibleControls();

            var failures = new List<string>();
            foreach (var type in types)
            {
                Control control = null;
                try
                {
                    control = (Control)Activator.CreateInstance(type);
                    control.CreateControl();
                    var _ = control.Handle;
                }
                catch (Exception ex)
                {
                    var actual = ex is TargetInvocationException tie && tie.InnerException != null
                        ? tie.InnerException
                        : ex;
                    failures.Add($"{type.FullName}: {actual.GetType().Name}: {actual.Message}");
                }
                finally
                {
                    control?.Dispose();
                }
            }

            if (failures.Count > 0)
                throw new InvalidOperationException(
                    $"{failures.Count} of {types.Count} failed to construct:\n" + string.Join("\n", failures));

            return $"{types.Count} forms and controls constructed";
        }

        /// <summary>
        /// Proves the registry change notification actually works here, by watching a scratch key
        /// and changing it. The PuTTY key itself is only reported on, never written to.
        /// </summary>
        /// <summary>
        /// Proves a registry change is noticed, that a burst of them never raises
        /// <see cref="RegistryKeyChangeWatcher.Changed"/> on two threads at once, and that a
        /// <see cref="CoalescingDispatcher"/> built here marshals onto this thread.
        /// </summary>
        /// <remarks>
        /// The burst and the marshalling are both here because of a crash: saving one profile in
        /// PuTTY killed the process with "Collection was modified; enumeration operation may not
        /// execute". One save writes dozens of registry values, each its own one-shot notification,
        /// and the registration is renewed before handlers run - so the refresh ran on several pool
        /// threads at once, all mutating and enumerating the same PuTTY Profile tree that the UI
        /// thread reads.
        /// <para>
        /// The marshalling probe uses the live <see cref="SynchronizationContext"/> rather than a
        /// stand-in, which is what the unit tests cannot do: whether a real
        /// WindowsFormsSynchronizationContext is installed at all on this path is the thing worth
        /// knowing, since without one the dispatcher silently runs inline on the watcher's thread
        /// and the crash comes back.
        /// </para>
        /// </remarks>
        private static string CheckPuttySessionWatcher()
        {
            const string scratchPath = @"Software\mRemoteUG\SelfTest";
            Registry.CurrentUser.CreateSubKey(scratchPath)?.Dispose();

            int burstRaises;
            int burstMaxConcurrent;

            try
            {
                using (var signalled = new ManualResetEventSlim(false))
                using (var watcher = new RegistryKeyChangeWatcher(Registry.CurrentUser, scratchPath))
                {
                    if (!watcher.Start())
                        throw new InvalidOperationException($"Could not watch HKCU\\{scratchPath}.");

                    watcher.Changed += (s, e) => signalled.Set();

                    using (var key = Registry.CurrentUser.OpenSubKey(scratchPath, writable: true))
                        key?.SetValue("probe", DateTime.UtcNow.Ticks);

                    if (!signalled.Wait(TimeSpan.FromSeconds(10)))
                        throw new TimeoutException("No registry change notification arrived within 10s.");
                }

                (burstRaises, burstMaxConcurrent) = ProbeWatcherBurst(scratchPath);
            }
            finally
            {
                try { Registry.CurrentUser.DeleteSubKeyTree(scratchPath, throwOnMissingSubKey: false); }
                catch (Exception) { /* leaving a scratch key behind is not a failure */ }
            }

            if (burstMaxConcurrent > 1)
                throw new InvalidOperationException(
                    $"A burst of registry writes raised Changed on {burstMaxConcurrent} threads at once. " +
                    "The PuTTY Profile refresh is not re-entrant; this is the crash in issue " +
                    "\"Collection was modified\".");

            var marshalling = ProbeRefreshMarshalling();

            using (var puttyKey = Registry.CurrentUser.OpenSubKey(@"Software\SimonTatham\PuTTY\Sessions"))
            {
                var puttyState = puttyKey == null
                    ? "PuTTY sessions key not present (PuTTY not installed)"
                    : $"PuTTY sessions key present with {puttyKey.SubKeyCount} session(s)";

                return $"notifications work; {puttyState}\n" +
                       $"burst of 40 writes: {burstRaises} handler run(s), " +
                       $"max {burstMaxConcurrent} concurrent (must be 1)\n" +
                       $"refresh marshalling: {marshalling}";
            }
        }

        /// <summary>
        /// Writes a burst the way PuTTY does when a profile is saved, and reports how many handler
        /// runs it caused and the most that ever ran at once.
        /// </summary>
        private static (int Raises, int MaxConcurrent) ProbeWatcherBurst(string scratchPath)
        {
            var raises = 0;
            var concurrent = 0;
            var maxConcurrent = 0;

            using (var watcher = new RegistryKeyChangeWatcher(Registry.CurrentUser, scratchPath))
            {
                if (!watcher.Start())
                    throw new InvalidOperationException($"Could not watch HKCU\\{scratchPath} for the burst probe.");

                watcher.Changed += (s, e) =>
                {
                    var now = Interlocked.Increment(ref concurrent);
                    int seen;
                    while ((seen = Volatile.Read(ref maxConcurrent)) < now)
                        if (Interlocked.CompareExchange(ref maxConcurrent, now, seen) == seen)
                            break;

                    // Stands in for the real handler: a registry re-read plus a tree rebuild.
                    Thread.Sleep(15);
                    Interlocked.Increment(ref raises);
                    Interlocked.Decrement(ref concurrent);
                };

                using (var key = Registry.CurrentUser.OpenSubKey(scratchPath, writable: true))
                    for (var i = 0; i < 40; i++)
                        key?.SetValue($"burst{i}", i);

                // Long enough for the burst to drain; the handler sleeps 15ms per run.
                var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
                while (DateTime.UtcNow < deadline &&
                       (Volatile.Read(ref raises) == 0 || Volatile.Read(ref concurrent) > 0))
                {
                    Thread.Sleep(10);
                }
            }

            return (Volatile.Read(ref raises), Volatile.Read(ref maxConcurrent));
        }

        /// <summary>
        /// Builds a dispatcher the way <c>PuttySessionsManager.StartWatcher</c> does and asks a
        /// background thread for a run, to show the work comes back to this thread.
        /// </summary>
        private static string ProbeRefreshMarshalling()
        {
            var context = SynchronizationContext.Current;
            var uiThreadId = Environment.CurrentManagedThreadId;
            var ranOn = 0;

            var dispatcher = new CoalescingDispatcher(() => ranOn = Environment.CurrentManagedThreadId, context);
            var requested = Task.Run(() => dispatcher.Request());
            requested.Wait(TimeSpan.FromSeconds(10));

            // Nothing is pumping messages during a self-test run, so drain the posted callback.
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
            while (Volatile.Read(ref ranOn) == 0 && DateTime.UtcNow < deadline)
            {
                Application.DoEvents();
                Thread.Sleep(5);
            }

            var contextName = context?.GetType().Name ?? "none";
            var landed = Volatile.Read(ref ranOn);

            if (context == null)
                throw new InvalidOperationException(
                    "No SynchronizationContext is installed on this thread, so the PuTTY Profile " +
                    "refresh would run on the registry watcher's thread pool thread.");

            if (landed != uiThreadId)
                throw new InvalidOperationException(
                    $"A refresh requested from a background thread ran on thread {landed}, not this " +
                    $"thread ({uiThreadId}). Context was {contextName}.");

            return $"requested off-thread, ran on this thread ({uiThreadId}) via {contextName}";
        }

        /// <summary>
        /// Hosts a real PuTTY window in a scratch panel the way a session does, and proves it
        /// ends up a chromeless, maximised child.
        /// </summary>
        /// <remarks>
        /// 890b245 stripped the caption and frame with SetWindowLongPtr + SWP_FRAMECHANGED and
        /// shipped with the title bar still showing: PuTTY puts it back on every SIZE_RESTORED
        /// (see HostedPuttyWindow.Adopt), and nothing here had ever hosted a PuTTY to notice.
        /// This runs the same Adopt against the configured PuTTY, so that class of failure is
        /// caught on this machine.
        /// <para>
        /// PuTTY is given a raw session to a listener on the loopback interface, which needs no
        /// network, no host key and no credentials, and pops no dialog. It is killed before the
        /// scratch form closes, so the form never destroys a window that belongs to another
        /// process. PuTTY updates its random-seed file when it runs; it writes no session.
        /// </para>
        /// <para>
        /// Skipped, not failed, when no PuTTY is configured or found; when the one found is a
        /// Chocolatey shim, which launches PuTTY in another process and exits, so there is never
        /// a window to host (a session pointed at it fails the same way); and for PuTTYNG, which
        /// is created as a child through -hwndparent and never adopted.
        /// </para>
        /// </remarks>
        private static string CheckPuttyHosting()
        {
            var path = PuttyPathProvider.ResolvedPath;
            if (!PuttyPathProvider.IsUsable)
                return "skipped: no PuTTY found; set CustomPuttyPath to test hosting";

            var version = FileVersionInfo.GetVersionInfo(path);
            if ((version.FileDescription ?? "").Contains("Chocolatey Shim"))
                return $"skipped: {path} is a Chocolatey shim that launches PuTTY in another process and exits;\n" +
                       "point CustomPuttyPath at the real executable (the shim prints it with --shimgen-noop)";

            var type = PuttyTypeDetector.GetPuttyType(path);
            if (type == PuttyTypeDetector.PuttyType.PuttyNg)
                return $"skipped: {path} is PuTTYNG, which is created as a child and never adopted";

            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var accepted = listener.AcceptTcpClientAsync();
            // Stopping the listener faults this task; observing the exception keeps it out of
            // the unhandled-exception path.
            accepted.ContinueWith(t => { _ = t.Exception; });

            var putty = new Process
            {
                StartInfo =
                {
                    FileName = path,
                    Arguments = $"-raw 127.0.0.1 -P {port}",
                    UseShellExecute = false
                }
            };

            using (var host = new Form { Width = 640, Height = 480, ShowInTaskbar = false })
            {
                var panel = new Panel { Dock = DockStyle.Fill };
                host.Controls.Add(panel);
                host.Show();
                try
                {
                    putty.Start();
                    putty.WaitForInputIdle(5000);
                    var hwnd = HostedPuttyWindow.WaitForHandle(() =>
                    {
                        putty.Refresh();
                        return putty.MainWindowHandle;
                    }, TimeSpan.FromSeconds(5));
                    if (hwnd == IntPtr.Zero)
                        throw new TimeoutException($"{path} showed no window within 5s (exited: {putty.HasExited}).");

                    var before = HostedPuttyWindow.Describe(hwnd);
                    HostedPuttyWindow.Adopt(hwnd, panel.Handle);
                    NativeMethods.MoveWindow(hwnd, 0, 0, panel.Width, panel.Height, true);
                    Application.DoEvents();
                    var after = HostedPuttyWindow.Describe(hwnd);

                    if (after.HasCaption || after.IsChild || !after.IsZoomed || after.Parent != panel.Handle)
                        throw new InvalidOperationException(
                            "PuTTY was not hosted as a chromeless, maximised, non-child window in the panel.\n" +
                            $"before: {before}\nafter:  {after}\npanel:  0x{panel.Handle.ToInt64():X}");

                    var keyboard = ProveKeyboardReachesPutty(hwnd, accepted);

                    return $"{path} ({version.ProductVersion}, {type})\n" +
                           $"before: {before}\nafter:  {after}\n" +
                           $"panel {panel.Width}x{panel.Height}, connection {(accepted.IsCompletedSuccessfully ? "accepted" : "pending")}\n" +
                           keyboard;
                }
                finally
                {
                    try { if (!putty.HasExited) putty.Kill(); }
                    catch (Exception) { /* nothing left to kill */ }
                    putty.Dispose();
                    listener.Stop();
                    host.Close();
                }
            }
        }

        /// <summary>
        /// Focuses the hosted PuTTY the way a session does and types into it, expecting the
        /// keystroke to come out of the raw connection on the other side.
        /// </summary>
        /// <remarks>
        /// This is the user-visible half of hosting: the first fix for the title bar made the
        /// window WS_CHILD, which cannot be foreground, and PuTTY stopped receiving keys. The
        /// keystroke is only sent once GetForegroundWindow is PuTTY's window - SendKeys goes to
        /// whatever is foreground, and if Windows refused to grant it (this process was not
        /// started by the foreground process) typing into some other window would be worse than
        /// not testing. That case is reported, not failed.
        /// </remarks>
        private static string ProveKeyboardReachesPutty(IntPtr hwnd, System.Threading.Tasks.Task<TcpClient> accepted)
        {
            HostedPuttyWindow.Focus(hwnd);
            Application.DoEvents();
            Thread.Sleep(100);

            var foreground = NativeMethods.GetForegroundWindow();
            if (foreground != hwnd)
            {
                // Two very different reasons. Windows refuses to grant the foreground to a process
                // that was not started by the foreground process, and then it stays with some other
                // application: not a hosting defect, and not testable from here. But if it landed on
                // a window of THIS process, the request was granted and went to the main form
                // instead of PuTTY - which is exactly what WS_CHILD does, and is the defect.
                NativeMethods.GetWindowThreadProcessId(foreground, out var owner);
                if (owner != (uint)Environment.ProcessId)
                    return $"keyboard: not tested - Windows did not grant this process the foreground (it is 0x{foreground.ToInt64():X}, another process); " +
                           "run --selftest from an interactive prompt to cover it";

                throw new InvalidOperationException(
                    $"SetForegroundWindow left the foreground on a window of this process (0x{foreground.ToInt64():X}) instead of PuTTY; " +
                    "no keystroke can reach PuTTY in this state.");
            }

            if (!HostedPuttyWindow.HasKeyboardFocus(hwnd))
                throw new InvalidOperationException("PuTTY is the foreground window but its thread does not have it as the focus window.");

            if (!accepted.Wait(TimeSpan.FromSeconds(3)))
                throw new TimeoutException("PuTTY never connected to the loopback listener.");

            using var client = accepted.Result;
            using var stream = client.GetStream();
            stream.ReadTimeout = 3000;

            // Enter as well as the letter: a raw session has local line editing on by default,
            // so nothing leaves PuTTY until the line is complete.
            SendKeys.SendWait("x{ENTER}");

            var received = new StringBuilder();
            var buffer = new byte[64];
            try
            {
                while (!received.ToString().Contains('x'))
                {
                    var count = stream.Read(buffer, 0, buffer.Length);
                    if (count == 0) break;
                    received.Append(Encoding.ASCII.GetString(buffer, 0, count));
                }
            }
            catch (IOException)
            {
                // read timeout: fall through to the check below
            }

            if (!received.ToString().Contains('x'))
                throw new InvalidOperationException(
                    $"Typed \"x\" + Enter into the focused PuTTY; the connection received {received.Length} byte(s): \"{received}\"");

            return $"keyboard: foreground and focused; typed x + Enter, connection received \"{received.ToString().TrimEnd()}\"";
        }

        /// <summary>A hosted control usually has no Name of its own; say something either way.</summary>
        private static string DescribeCombo(ComboBox combo) =>
            string.IsNullOrEmpty(combo.Name) ? combo.GetType().Name : combo.Name;

        /// <summary>
        /// Drives a real per-monitor DPI change at every ToolStrip-hosted ComboBox.
        /// </summary>
        /// <remarks>
        /// Moving the window from a 200% monitor to a 300% one killed the process. The measured
        /// chain: WM_DPICHANGED_BEFOREPARENT reaches the hosted ComboBox's own window; WinForms
        /// acts on it because ToolStripControlHost has given that control an explicitly set font;
        /// scaling that font raises ComboBox.OnFontChanged, which - with AutoCompleteMode set -
        /// recreates the control's window; and CreateWindowEx then failed with Win32 1400,
        /// ERROR_INVALID_WINDOW_HANDLE. Nothing of this application's own code is on that stack.
        /// <para>
        /// This cannot be a unit test. WinForms only takes that path when the thread is
        /// per-monitor V2 aware, which the test host is not and this process is - which is exactly
        /// the case ADR-0013 says --selftest exists for.
        /// </para>
        /// <para>
        /// One thing here is contrived, and only one: the window has not moved, so
        /// <c>GetDpiForWindow</c> still answers the current DPI and WinForms would take its
        /// "nothing changed" early return. <c>DeviceDpiInternal</c> is set to a different value
        /// first, which is the same inequality a monitor move produces. Everything after that is
        /// WinForms' own code running against a real window in a real per-monitor process.
        /// </para>
        /// <para>
        /// A failure inside a WndProc reached through SendMessage does not necessarily come back
        /// to the caller, so the count of exceptions that reached
        /// <see cref="Application.ThreadException"/> is compared across the call as well.
        /// </para>
        /// <para>
        /// **This does not reproduce the crash, and is not claimed to.** Measured: driving the path
        /// here scales the font and recreates the window exactly as on the failing machine, and
        /// then succeeds - so the recreation is necessary but not sufficient, and what the real
        /// monitor move adds is not known. What is checked instead is the recreation itself, which
        /// is where the crash happened: <c>RecreateHandleCore</c> was the frame that called
        /// <c>CreateWindowEx</c>. A hosted ComboBox that keeps its window through a DPI change
        /// cannot fail there at all, so that is the property worth holding onto.
        /// </para>
        /// </remarks>
        private static string CheckDpiChangeOnHostedComboBoxes()
        {
            const int WM_DPICHANGED_BEFOREPARENT = 0x02E2;

            var dpiProperty = typeof(Control).GetProperty(
                "DeviceDpiInternal", BindingFlags.Instance | BindingFlags.NonPublic);
            if (dpiProperty == null || !dpiProperty.CanWrite)
                throw new InvalidOperationException(
                    "Control.DeviceDpiInternal is gone, so a DPI change cannot be driven here any more.");

            var lines = new List<string>();
            var drove = 0;

            foreach (var type in ConstructibleControls()
                                 .Where(t => typeof(ToolStrip).IsAssignableFrom(t)))
            {
                using var host = new Form { Width = 900, Height = 200, ShowInTaskbar = false };
                ToolStrip strip = null;
                try
                {
                    strip = (ToolStrip)Activator.CreateInstance(type);
                }
                catch (Exception)
                {
                    continue; // CheckFormsConstruct is what reports a type that will not construct
                }

                host.Controls.Add(strip);
                host.Show();
                Application.DoEvents();

                foreach (var combo in strip.Items.OfType<ToolStripControlHost>()
                                           .Select(i => i.Control)
                                           .OfType<ComboBox>())
                {
                    if (!combo.IsHandleCreated)
                        continue;

                    var realDpi = combo.DeviceDpi;
                    var handleBefore = combo.Handle;
                    var fontBefore = combo.Font.SizeInPoints;
                    var exceptionsBefore = CrashLogger.UiThreadExceptionCount;

                    dpiProperty.SetValue(combo, realDpi == 96 ? 144 : 96);
                    NativeMethods.SendMessage(combo.Handle, WM_DPICHANGED_BEFOREPARENT,
                                              IntPtr.Zero, IntPtr.Zero);
                    Application.DoEvents();

                    var reached = CrashLogger.UiThreadExceptionCount - exceptionsBefore;
                    drove++;

                    var recreated = handleBefore != combo.Handle;

                    lines.Add($"  {type.Name}.{DescribeCombo(combo)}: " +
                              $"dpi {realDpi}, autocomplete {combo.AutoCompleteMode}, " +
                              $"font {fontBefore}pt -> {combo.Font.SizeInPoints}pt, " +
                              $"window {(recreated ? "RECREATED" : "kept")}");

                    if (reached > 0)
                        throw new InvalidOperationException(
                            $"A DPI change at {type.Name}.{DescribeCombo(combo)} put {reached} exception(s) " +
                            "on the UI thread. That is the crash itself, reproduced here.");

                    if (recreated)
                        throw new InvalidOperationException(
                            $"A DPI change made {type.Name}.{DescribeCombo(combo)} recreate its window " +
                            $"(autocomplete is {combo.AutoCompleteMode}). CreateWindowEx inside that " +
                            "recreation is what failed with Win32 1400 when the window was moved from a " +
                            "200% monitor to a 300% one.");
                }

                host.Close();
            }

            if (drove == 0)
                lines.Add("  no ToolStrip-hosted ComboBox was reachable, so this proved nothing");

            return $"drove a DPI change at {drove} hosted combo box(es)\n" + string.Join("\n", lines);
        }

        /// <summary>
        /// Creates the newest RDP ActiveX control this machine offers and proves it can actually
        /// be driven through its newest interface.
        /// </summary>
        /// <remarks>
        /// The candidate chain itself lives in <see cref="RdpClientCandidates"/> - this used to
        /// hand-copy it, with a comment claiming the two were the same and nothing enforcing it.
        /// <para>
        /// Only two things fail this check: no control at all, and a control that will not answer
        /// QueryInterface for IMsRdpClient10. v12 being unregistered is a supported outcome and is
        /// reported, not failed - Windows 11 builds differ on whether they carry it.
        /// </para>
        /// </remarks>
        private static string CheckRdpControl()
        {
            var attempts = new List<string>();
            using (var host = new Form { Width = 640, Height = 480, ShowInTaskbar = false })
            {
                host.Show();
                try
                {
                    var control = RdpClientCandidates.CreateNewest(host, "selftest", DockStyle.Fill, attempts);
                    if (control == null)
                    {
                        throw new InvalidOperationException(
                            "No RDP ActiveX control could be created:\n" + string.Join("\n", attempts));
                    }

                    try
                    {
                        // The assertion that matters: this is QueryInterface(IID_IMsRdpClient10),
                        // the same cast RdpProtocol makes on every connection.
                        var ocx = control.GetOcx();
                        var client = (MsRdpClient11NotSafeForScripting)ocx;

                        attempts.Add($"selected {control.GetType().Name}; mstscax {client.Version}");
                        attempts.Add(
                            $"IMsRdpClient10: yes   " +
                            $"NonScriptable8: {(ocx is IMsRdpClientNonScriptable8 ? "yes" : "no")}   " +
                            $"ExtendedSettings: {(ocx is IMsRdpExtendedSettings ? "yes" : "no")}   " +
                            $"PreferredRedirectionInfo: {(ocx is IMsRdpPreferredRedirectionInfo ? "yes" : "no")}");
                        attempts.Add(DescribeExtendedScaleProperties(ocx));

                        if (!control.GetType().Name.Contains("12"))
                            attempts.Add("v12 is not registered on this Windows build; v11 is a supported outcome.");

                        return string.Join("\n", attempts);
                    }
                    finally
                    {
                        host.Controls.Remove(control);
                        control.Dispose();
                    }
                }
                finally
                {
                    host.Close();
                }
            }
        }

        /// <summary>
        /// Whether this mstscax accepts the display-scale properties in the extended-settings
        /// bag, and in which variant type.
        /// </summary>
        /// <remarks>
        /// The bag is undocumented and an unknown name throws, so the only way to learn that a
        /// name is accepted is to set it. These are client-side property writes on a control that
        /// has not connected to anything, which is the whole reason this is measurable on a machine
        /// with no route to an RDP server: nothing about it needs a session. What it settles is
        /// whether a session can be told its scale before <c>Connect()</c> at all.
        /// <para>
        /// Both variant types are tried even when the first succeeds, because which one mstscax
        /// wants is not documented either, and a property it accepts but ignores looks exactly like
        /// one it honours. Reported rather than asserted: this is a fact about the machine, and a
        /// build on which it came out differently is something to read, not a failure.
        /// </para>
        /// </remarks>
        private static string DescribeExtendedScaleProperties(object ocx)
        {
            if (!(ocx is IMsRdpExtendedSettings extended))
                return "ExtendedSettings scale properties: no IMsRdpExtendedSettings to ask";

            return "ExtendedSettings scale properties:\n         " +
                   $"DesktopScaleFactor {ProbeExtendedProperty(extended, "DesktopScaleFactor", 150)}\n         " +
                   $"DeviceScaleFactor  {ProbeExtendedProperty(extended, "DeviceScaleFactor", 140)}";
        }

        private static string ProbeExtendedProperty(IMsRdpExtendedSettings extended, string name, uint value)
        {
            var outcomes = new List<string>();

            foreach (var boxed in new object[] { value, (int)value })
            {
                var variant = boxed.GetType().Name;
                var toSet = boxed;
                try
                {
                    extended.set_Property(name, ref toSet);
                }
                catch (Exception ex)
                {
                    outcomes.Add($"as {variant}: set threw {ex.GetType().Name} 0x{ex.HResult:X8}");
                    continue;
                }

                try
                {
                    var readBack = extended.get_Property(name);
                    outcomes.Add($"as {variant}: set, read back {readBack ?? "(null)"} " +
                                 $"({readBack?.GetType().Name ?? "none"})");
                }
                catch (Exception ex)
                {
                    outcomes.Add($"as {variant}: set, read threw {ex.GetType().Name} 0x{ex.HResult:X8}");
                }
            }

            return string.Join("; ", outcomes);
        }

        private static void Line(string text)
        {
            Report.AppendLine(text);
        }

        /// <summary>
        /// Writes the report next to the log, and to the console when one is attached, so the
        /// result can be read either by opening a file or by capturing stdout from a script.
        /// </summary>
        private static void Emit()
        {
            var text = Report.ToString();
            Console.Out.Write(text);
            Console.Out.Flush();

            try
            {
                var path = Path.Combine(Path.GetDirectoryName(Logger.Instance.LogPath) ?? AppContext.BaseDirectory,
                                        "mRemoteUG-selftest.log");
                Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
                File.WriteAllText(path, text);
                Console.Out.WriteLine($"(report written to {path})");
            }
            catch (Exception ex)
            {
                Console.Out.WriteLine($"(could not write the report to disk: {ex.Message})");
            }
        }
    }
}
