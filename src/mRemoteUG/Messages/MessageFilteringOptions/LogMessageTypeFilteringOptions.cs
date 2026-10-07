#nullable enable
namespace mRemoteUG.Messages
{
    public class LogMessageTypeFilteringOptions : IMessageTypeFilteringOptions
    {
        /// <summary>
        /// Turns debug logging on for this run only, without touching what the user has saved.
        /// Set by the <c>--verbose</c> command line switch.
        /// </summary>
        /// <remarks>
        /// This exists to make Debug a usable level. Debug is off by default, so for a long time
        /// the only way to be sure a line would reach the log was to report it as Information -
        /// which is how routine per-stage and per-resize detail ended up in every log
        /// permanently. Asking someone to run mRemoteUG.exe --verbose and reproduce the problem
        /// costs them nothing and gets that detail back, so it no longer has to live at
        /// Information in order to be reachable.
        /// </remarks>
        public static bool ForceDebugMessages { get; set; }

        public bool AllowDebugMessages
        {
            get { return ForceDebugMessages || Settings.Default.TextLogMessageWriterWriteDebugMsgs; }
            set { Settings.Default.TextLogMessageWriterWriteDebugMsgs = value; }
        }

        public bool AllowInfoMessages
        {
            get { return Settings.Default.TextLogMessageWriterWriteInfoMsgs; }
            set { Settings.Default.TextLogMessageWriterWriteInfoMsgs = value; }
        }

        public bool AllowWarningMessages
        {
            get { return Settings.Default.TextLogMessageWriterWriteWarningMsgs; }
            set { Settings.Default.TextLogMessageWriterWriteWarningMsgs = value; }
        }

        public bool AllowErrorMessages
        {
            get { return Settings.Default.TextLogMessageWriterWriteErrorMsgs; }
            set { Settings.Default.TextLogMessageWriterWriteErrorMsgs = value; }
        }
    }
}