using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using mRemoteUG.App;

namespace mRemoteUG.UI.Forms.OptionsPages;

public sealed partial class NotificationsPage
{
    /// <summary>
    /// The path the user last chose for themselves, kept while the default location is in use
    /// so that turning the default back off restores it.
    /// </summary>
    private string _customLogPath = string.Empty;

    /// <summary>
    /// Set while LoadSettings is populating the controls, so that the handlers those setters
    /// fire do not treat them as edits made by the user.
    /// </summary>
    private bool _loadingSettings;

    public NotificationsPage()
    {
        InitializeComponent();
    }

    public override string PageName
    {
        get => Language.strMenuNotifications;
        set { }
    }

    public override void ApplyLanguage()
    {
        base.ApplyLanguage();

        // notifications panel
        groupBoxNotifications.Text = Language.strMenuNotifications;
        labelNotificationsShowTypes.Text = Language.strShowTheseMessageTypes;
        chkShowDebugInMC.Text = Language.strDebug;
        chkShowInfoInMC.Text = Language.strInformations;
        chkShowWarningInMC.Text = Language.strWarnings;
        chkShowErrorInMC.Text = Language.strErrors;
        labelSwitchToErrorsAndInfos.Text = Language.strSwitchToErrorsAndInfos;
        chkSwitchToMCInformation.Text = Language.strInformations;
        chkSwitchToMCWarnings.Text = Language.strWarnings;
        chkSwitchToMCErrors.Text = Language.strErrors;

        // logging
        groupBoxLogging.Text = Language.strLogging;
        chkLogDebugMsgs.Text = Language.strDebug;
        chkLogInfoMsgs.Text = Language.strInformations;
        chkLogWarningMsgs.Text = Language.strWarnings;
        chkLogErrorMsgs.Text = Language.strErrors;
        chkLogToCurrentDir.Text = Language.strLogToAppDir;
        labelLogFilePath.Text = Language.strLogFilePath;
        labelLogTheseMsgTypes.Text = Language.strLogTheseMessageTypes;
        buttonOpenLogFile.Text = Language.strOpenFile;
        buttonSelectLogPath.Text = Language.strChoosePath;
        buttonRestoreDefaultLogPath.Text = Language.strUseDefault;

        // popups
        groupBoxPopups.Text = Language.strPopups;
        labelPopupShowTypes.Text = Language.strShowTheseMessageTypes;
        chkPopupDebug.Text = Language.strDebug;
        chkPopupInfo.Text = Language.strInformations;
        chkPopupWarning.Text = Language.strWarnings;
        chkPopupError.Text = Language.strErrors;
    }

    public override void LoadSettings()
    {
        base.LoadSettings();
        LoadNotificationPanelSettings();
        LoadLoggingSettings();
        LoadPopupSettings();
    }

    public override void SaveSettings()
    {
        SaveNotificationPanelSettings();
        SaveLoggingSettings();
        SavePopupSettings();
        Settings.Default.Save();
    }

    private void LoadNotificationPanelSettings()
    {
        chkShowDebugInMC.Checked = Settings.Default.NotificationPanelWriterWriteDebugMsgs;
        chkShowInfoInMC.Checked = Settings.Default.NotificationPanelWriterWriteInfoMsgs;
        chkShowWarningInMC.Checked = Settings.Default.NotificationPanelWriterWriteWarningMsgs;
        chkShowErrorInMC.Checked = Settings.Default.NotificationPanelWriterWriteErrorMsgs;
        chkSwitchToMCInformation.Checked = Settings.Default.SwitchToMCOnInformation;
        chkSwitchToMCWarnings.Checked = Settings.Default.SwitchToMCOnWarning;
        chkSwitchToMCErrors.Checked = Settings.Default.SwitchToMCOnError;
    }

    private void LoadLoggingSettings()
    {
        // The checkbox is set last, and the whole block is guarded, because its CheckedChanged
        // handler rewrites the path box. Setting it first - as this used to - meant the handler
        // overwrote the stored path and the following line quietly put it back: the ordering
        // was load bearing without saying so.
        _loadingSettings = true;
        try
        {
            _customLogPath = Settings.Default.LogFilePath ?? string.Empty;
            textBoxLogPath.Text = Logger.EffectiveLogPath;
            chkLogToCurrentDir.Checked = Settings.Default.LogToApplicationDirectory;
        }
        finally
        {
            _loadingSettings = false;
        }

        // CheckedChanged does not fire when the stored value already matches the control default,
        // so the enabled state cannot be left to the handler alone.
        UpdateLogPathControlsEnabledState();

        chkLogDebugMsgs.Checked = Settings.Default.TextLogMessageWriterWriteDebugMsgs;
        chkLogInfoMsgs.Checked = Settings.Default.TextLogMessageWriterWriteInfoMsgs;
        chkLogWarningMsgs.Checked = Settings.Default.TextLogMessageWriterWriteWarningMsgs;
        chkLogErrorMsgs.Checked = Settings.Default.TextLogMessageWriterWriteErrorMsgs;
    }

    private void LoadPopupSettings()
    {
        chkPopupDebug.Checked = Settings.Default.PopupMessageWriterWriteDebugMsgs;
        chkPopupInfo.Checked = Settings.Default.PopupMessageWriterWriteInfoMsgs;
        chkPopupWarning.Checked = Settings.Default.PopupMessageWriterWriteWarningMsgs;
        chkPopupError.Checked = Settings.Default.PopupMessageWriterWriteErrorMsgs;
    }

    private void SaveNotificationPanelSettings()
    {
        Settings.Default.NotificationPanelWriterWriteDebugMsgs = chkShowDebugInMC.Checked;
        Settings.Default.NotificationPanelWriterWriteInfoMsgs = chkShowInfoInMC.Checked;
        Settings.Default.NotificationPanelWriterWriteWarningMsgs = chkShowWarningInMC.Checked;
        Settings.Default.NotificationPanelWriterWriteErrorMsgs = chkShowErrorInMC.Checked;
        Settings.Default.SwitchToMCOnInformation = chkSwitchToMCInformation.Checked;
        Settings.Default.SwitchToMCOnWarning = chkSwitchToMCWarnings.Checked;
        Settings.Default.SwitchToMCOnError = chkSwitchToMCErrors.Checked;

    }

    private void SaveLoggingSettings()
    {
        Settings.Default.LogToApplicationDirectory = chkLogToCurrentDir.Checked;
        // The custom path is kept even while the default location is in use, so that turning
        // the default off later gives the user their path back instead of nothing.
        Settings.Default.LogFilePath = chkLogToCurrentDir.Checked
            ? _customLogPath
            : textBoxLogPath.Text;
        // EffectiveLogPath rather than LogFilePath: applying the path box unconditionally
        // pointed the running log at somewhere the next startup would not have chosen.
        Logger.Instance.SetLogPath(Logger.EffectiveLogPath);
        Settings.Default.TextLogMessageWriterWriteDebugMsgs = chkLogDebugMsgs.Checked;
        Settings.Default.TextLogMessageWriterWriteInfoMsgs = chkLogInfoMsgs.Checked;
        Settings.Default.TextLogMessageWriterWriteWarningMsgs = chkLogWarningMsgs.Checked;
        Settings.Default.TextLogMessageWriterWriteErrorMsgs = chkLogErrorMsgs.Checked;
    }

    private void SavePopupSettings()
    {
        Settings.Default.PopupMessageWriterWriteDebugMsgs = chkPopupDebug.Checked;
        Settings.Default.PopupMessageWriterWriteInfoMsgs = chkPopupInfo.Checked;
        Settings.Default.PopupMessageWriterWriteWarningMsgs = chkPopupWarning.Checked;
        Settings.Default.PopupMessageWriterWriteErrorMsgs = chkPopupError.Checked;
    }

    [SuppressMessage("Style", "IDE1006", Justification = "The name is wired in NotificationsPage.Designer.cs.")]
    private void buttonSelectLogPath_Click(object sender, System.EventArgs e)
    {
        string currentFile = textBoxLogPath.Text;
        string? currentDirectory = Path.GetDirectoryName(currentFile);
        saveFileDialogLogging.Title = Language.strChooseLogPath;
        saveFileDialogLogging.Filter = @"Log file|*.log";
        saveFileDialogLogging.InitialDirectory = currentDirectory;
        saveFileDialogLogging.FileName = currentFile;
        DialogResult dialogResult = saveFileDialogLogging.ShowDialog();
        if (dialogResult != DialogResult.OK)
        {
            return;
        }

        textBoxLogPath.Text = saveFileDialogLogging.FileName;
    }

    [SuppressMessage("Style", "IDE1006", Justification = "The name is wired in NotificationsPage.Designer.cs.")]
    private void buttonRestoreDefaultLogPath_Click(object sender, System.EventArgs e)
    {
        textBoxLogPath.Text = Logger.DefaultLogPath;
    }

    [SuppressMessage("Style", "IDE1006", Justification = "The name is wired in NotificationsPage.Designer.cs.")]
    [SuppressMessage("Performance", "CA1863",
                     Justification = "The format is a localized resource read at run time; "
                                     + "a cached CompositeFormat would freeze the UI language.")]
    private void buttonOpenLogFile_Click(object sender, System.EventArgs e)
    {
        // Where the log is actually going, not what the box shows: the box can hold an edit
        // that has not been saved, or the default path while a custom one is stored.
        string path = Logger.Instance.LogPath;

        // The file is not created until something is written to it, so with every message type
        // switched off there is legitimately nothing to open.
        if (!File.Exists(path))
        {
            MessageBox.Show(string.Format(CultureInfo.CurrentCulture, Language.strLogFileNotFound, path),
                            Language.strLogging,
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        // UseShellExecute has to be explicit on .NET: the default changed from true on .NET
        // Framework to false, and Process.Start on a data file fails without it.
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    [SuppressMessage("Style", "IDE1006", Justification = "The name is wired in NotificationsPage.Designer.cs.")]
    private void chkLogToCurrentDir_CheckedChanged(object sender, System.EventArgs e)
    {
        UpdateLogPathControlsEnabledState();
        if (_loadingSettings)
        {
            return;
        }

        if (chkLogToCurrentDir.Checked)
        {
            // Remember what the user had. Both transitions used to overwrite the box with the
            // default path, so unticking silently discarded the path they had chosen.
            _customLogPath = textBoxLogPath.Text;
            textBoxLogPath.Text = Logger.DefaultLogPath;
        }
        else if (!string.IsNullOrWhiteSpace(_customLogPath))
        {
            textBoxLogPath.Text = _customLogPath;
        }
    }

    private void UpdateLogPathControlsEnabledState()
    {
        buttonSelectLogPath.Enabled = !chkLogToCurrentDir.Checked;
        buttonRestoreDefaultLogPath.Enabled = !chkLogToCurrentDir.Checked;
    }
}
