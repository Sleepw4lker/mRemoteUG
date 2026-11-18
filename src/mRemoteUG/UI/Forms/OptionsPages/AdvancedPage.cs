using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using mRemoteUG.App;
using mRemoteUG.Config.Putty;
using mRemoteUG.Connection.Protocol;
using mRemoteUG.Messages;
using mRemoteUG.Tools;

namespace mRemoteUG.UI.Forms.OptionsPages;

public sealed partial class AdvancedPage
{
    public AdvancedPage()
    {
        InitializeComponent();
        txtPuttyPath.TextChanged += (sender, args) => RefreshPuttyStatus();
        btnBrowsePuttyPath.Click += BtnBrowsePuttyPath_Click;
        btnLaunchPutty.Click += BtnLaunchPutty_Click;
    }

    #region Public Methods

    public override string PageName
    {
        get => Language.strTabAdvanced;
        set { }
    }

    public override void ApplyLanguage()
    {
        base.ApplyLanguage();

        chkAutomaticReconnect.Text = Language.strCheckboxAutomaticReconnect;
        chkLoadBalanceInfoUseUtf8.Text = Language.strLoadBalanceInfoUseUtf8;
        lblPuttyExecutable.Text = Language.strLabelPuttyExecutable;
        lblConfigurePuttySessions.Text = Language.strLabelPuttySessionsConfig;
        lblPuttyWaitTime.Text = Language.strLabelPuttyTimeout;
        btnLaunchPutty.Text = Language.strButtonLaunchPutty;
    }

    public override void LoadSettings()
    {
        base.SaveSettings();

        chkAutomaticReconnect.Checked = Settings.Default.ReconnectOnDisconnect;
        chkLoadBalanceInfoUseUtf8.Checked = Settings.Default.RdpLoadBalanceInfoUseUtf8;
        txtPuttyPath.Text = Settings.Default.CustomPuttyPath;
        numPuttyWaitTime.Value = Settings.Default.MaxPuttyWaitTime;
        RefreshPuttyStatus();
    }

    public override void SaveSettings()
    {
        Settings.Default.ReconnectOnDisconnect = chkAutomaticReconnect.Checked;
        Settings.Default.RdpLoadBalanceInfoUseUtf8 = chkLoadBalanceInfoUseUtf8.Checked;

        bool puttyPathChanged = Settings.Default.CustomPuttyPath != txtPuttyPath.Text;
        Settings.Default.CustomPuttyPath = txtPuttyPath.Text;
        Settings.Default.MaxPuttyWaitTime = (int)numPuttyWaitTime.Value;

        Settings.Default.Save();

        if (!puttyPathChanged)
        {
            return;
        }
        // The user may have installed PuTTY since discovery last ran.
        PuttyPathProvider.InvalidateDiscoveryCache();
        PuttyBase.PuttyPath = PuttyPathProvider.ResolvedPath;
        // A different PuTTY means a different set of saved sessions.
        PuttySessionsManager.Instance.AddSessions();
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Reports whether PuTTY can actually be launched, so the user finds out here
    /// rather than when a connection fails.
    /// </summary>
    [SuppressMessage("Performance", "CA1863",
                     Justification = "The format is a localized resource read at run time; "
                                     + "a cached CompositeFormat would freeze the UI language.")]
    private void RefreshPuttyStatus()
    {
        string path = txtPuttyPath.Text.Trim();
        string status;
        bool usable;

        if (path.Length == 0)
        {
            // Nothing configured: discovery may still find one next to mRemoteUG or on PATH.
            string discovered = PuttyPathProvider.ResolvedPath;
            usable = discovered.Length > 0;
            status = usable
                ? string.Format(
                    CultureInfo.CurrentCulture,
                    Language.strPuttyPathDiscovered,
                    discovered,
                    DescribeType(discovered))
                : Language.strPuttyPathNotConfigured;
        }
        else if (!System.IO.File.Exists(path))
        {
            usable = false;
            status = string.Format(CultureInfo.CurrentCulture, Language.strPuttyPathNotFound, path);
        }
        else
        {
            usable = true;
            status = string.Format(CultureInfo.CurrentCulture, Language.strPuttyPathDetected, DescribeType(path));
        }

        lblPuttyStatus.Text = status;
        btnLaunchPutty.Enabled = usable;
    }

    private static string DescribeType(string path)
    {
        return PuttyTypeDetector.GetPuttyType(path).ToString();
    }

    private void BtnBrowsePuttyPath_Click(object? sender, EventArgs e)
    {
        using (var openFileDialog = new OpenFileDialog
        {
            CheckFileExists = true,
            FileName = "putty.exe",
            Filter = $"{Language.strFilterApplication}|*.exe|{Language.strFilterAll}|*.*"
        })
        {
            if (openFileDialog.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            txtPuttyPath.Text = openFileDialog.FileName;
        }
    }

    private void BtnLaunchPutty_Click(object? sender, EventArgs e)
    {
        try
        {
            // Save first so the launched PuTTY is the one shown in the textbox.
            SaveSettings();
            using var puttyProcessController = new PuttyProcessController();
            puttyProcessController.Start();
        }
        catch (Exception ex)
        {
            Runtime.MessageCollector.AddMessage(MessageClass.ErrorMsg,
                Language.strErrorCouldNotLaunchPutty + Environment.NewLine + ex.Message);
        }
    }

    #endregion
}
