using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using mRemoteUG.App.Info;
using mRemoteUG.Messages;

namespace mRemoteUG.App.Initialization
{
    public class StartupDataLogger
    {
        private readonly MessageCollector _messageCollector;

        public StartupDataLogger(MessageCollector messageCollector)
        {
            if (messageCollector == null)
                throw new ArgumentNullException(nameof(messageCollector));

            _messageCollector = messageCollector;
        }

        public void LogStartupData()
        {
            LogApplicationData();
            LogCmdLineArgs();
            LogSystemData();
            LogClrData();
            LogCultureData();
        }

        /// <summary>
        /// Logs the operating system and its architecture.
        /// </summary>
        /// <remarks>
        /// This d to run two WMI queries, against Win32_OperatingSystem and Win32_Processor,
        /// which put a WMI round trip -- slow, and prone to hanging on a machine whose WMI
        /// repository is unhealthy -- directly on the startup path, to produce one line of log.
        /// RuntimeInformation reports the same facts from the BCL, at no cost.
        ///
        /// The service pack level is no longer reported: it has not been meaningful since
        /// Windows 8, which is three releases below the supported baseline.
        /// </remarks>
        private void LogSystemData()
        {
            var parts = new[]
            {
                RuntimeInformation.OSDescription.Trim(),
                $"{RuntimeInformation.OSArchitecture} OS",
                $"{RuntimeInformation.ProcessArchitecture} process"
            };

            var data = string.Join(", ", Array.FindAll(parts, s => !string.IsNullOrEmpty(s)));
            _messageCollector.AddMessage(MessageClass.InformationMsg, data, true);
        }


        private void LogApplicationData()
        {
            // The full informational version, commit hash and all - not the trimmed one the
            // About dialog shows. This line is how a binary running on a machine that cannot be
            // reached from here is identified from its log alone (ADR-0030).
            var data = $"{Application.ProductName} {GeneralAppInfo.ApplicationVersionFull}";
            data += " starting.";
            _messageCollector.AddMessage(MessageClass.InformationMsg, data, true);
        }

        private void LogCmdLineArgs()
        {
            var data = $"Command Line: {string.Join(" ", Environment.GetCommandLineArgs())}";
            _messageCollector.AddMessage(MessageClass.InformationMsg, data, true);
        }

        private void LogClrData()
        {
            var data = $"Microsoft .NET CLR {Environment.Version}";
            _messageCollector.AddMessage(MessageClass.InformationMsg, data, true);
        }

        private void LogCultureData()
        {
            var data = $"System Culture: {Thread.CurrentThread.CurrentUICulture.Name}/{Thread.CurrentThread.CurrentUICulture.NativeName}";
            _messageCollector.AddMessage(MessageClass.InformationMsg, data, true);
        }
    }
}