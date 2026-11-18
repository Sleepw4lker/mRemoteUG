using mRemoteUG.Security.KeyDerivation;

namespace mRemoteUG.UI.Forms.OptionsPages;

public sealed partial class SecurityPage : OptionsPage
{
    public SecurityPage()
    {
        InitializeComponent();

        // The bounds come from the same constants the connection file reader checks against.
        numberBoxKdfIterations.Minimum = KeyDerivationIterationLimits.Minimum;
        numberBoxKdfIterations.Maximum = KeyDerivationIterationLimits.OptionsMaximum;
    }

    [System.ComponentModel.Browsable(false)]
    public override string PageName
    {
        get => Language.strTabSecurity;
        set { }
    }

    public override void ApplyLanguage()
    {
        base.ApplyLanguage();
        chkEncryptCompleteFile.Text = Language.strEncryptCompleteConnectionFile;
        labelKdfIterations.Text = Language.strEncryptionKeyDerivationIterations;
        groupAdvancedSecurityOptions.Text = Language.strAdvancedSecurityOptions;
    }

    public override void LoadSettings()
    {
        base.LoadSettings();
        chkEncryptCompleteFile.Checked = Settings.Default.EncryptCompleteConnectionsFile;
        numberBoxKdfIterations.Value = Settings.Default.EncryptionKeyDerivationIterations;
    }

    public override void SaveSettings()
    {
        Settings.Default.EncryptCompleteConnectionsFile = chkEncryptCompleteFile.Checked;
        Settings.Default.EncryptionKeyDerivationIterations = (int)numberBoxKdfIterations.Value;
    }

}
