using System;
using System.Diagnostics;
using System.Globalization;
using mRemoteUG.App.Initialization;
using mRemoteUG.Connection;
using mRemoteUG.Messages;
using mRemoteUG.Tools;
using mRemoteUG.Tools.Cmdline;


namespace mRemoteUG.App
{
    public class Startup
    {
        public static Startup Instance { get; } = new Startup();

        // Private, and kept empty on purpose: it is what stops anything but
        // Instance from constructing a Startup.
        private Startup()
        {
        }

        static Startup()
        {
        }

        public void InitializeProgram(MessageCollector messageCollector)
        {
            Debug.Print("---------------------------" + Environment.NewLine + "[START] - " + Convert.ToString(DateTime.Now, CultureInfo.InvariantCulture));
            var startupLogger = new StartupDataLogger(messageCollector);
            startupLogger.LogStartupData();
            CompatibilityChecker.CheckCompatibility(messageCollector);
            ParseCommandLineArgs(messageCollector);
            DefaultConnectionInfo.Instance.LoadFrom(Settings.Default, a=>"ConDefault"+a);
            DefaultConnectionInheritance.Instance.LoadFrom(Settings.Default, a=>"InhDefault"+a);
        }

        private static void ParseCommandLineArgs(MessageCollector messageCollector)
        {
            var interpreter = new StartupArgumentsInterpreter(messageCollector);
            interpreter.ParseArguments(Environment.GetCommandLineArgs());
        }

    }
}