using System;
using mRemoteUG.App;
using mRemoteUG.Security;

namespace mRemoteUG.UI.Forms.OptionsPages
{
	public sealed partial class CredentialsPage : OptionsPage
    {
        public CredentialsPage()
        {
            InitializeComponent();
        }

        public override string PageName {
            get => Language.Credentials;
            set { }
        }

        public override void ApplyLanguage()
        {
            base.ApplyLanguage();
            lblDefaultCredentials.Text = Language.strEmptyUsernamePasswordDomainFields;
            radCredentialsNoInfo.Text = Language.strNoInformation;
            radCredentialsWindows.Text = Language.strMyCurrentWindowsCreds;
            radCredentialsCustom.Text = Language.strTheFollowing;
            lblCredentialsUsername.Text = Language.strLabelUsername;
            lblCredentialsPassword.Text = Language.strLabelPassword;
            lblCredentialsDomain.Text = Language.strLabelDomain;
        }

        public override void LoadSettings()
        {
            base.SaveSettings();

            // ReSharper disable once SwitchStatementMissingSomeCases
            switch (Settings.Default.EmptyCredentials)
            {
                case "noinfo":
                    radCredentialsNoInfo.Checked = true;
                    break;
                case "windows":
                    radCredentialsWindows.Checked = true;
                    break;
                case "custom":
                case "admpwd":
                    radCredentialsCustom.Checked = true;
                    break;
            }

            txtCredentialsUsername.Text = Settings.Default.DefaultUsername;
            txtCredentialsPassword.Text = DefaultCredentials.Password;
            txtCredentialsDomain.Text = Settings.Default.DefaultDomain;
        }

        public override void SaveSettings()
        {
            if (radCredentialsNoInfo.Checked)
            {
                Settings.Default.EmptyCredentials = "noinfo";
            }
            else if (radCredentialsWindows.Checked)
            {
                Settings.Default.EmptyCredentials = "windows";
            }
            else if (radCredentialsCustom.Checked)
            {
                Settings.Default.EmptyCredentials = "custom";
            }

            Settings.Default.DefaultUsername = txtCredentialsUsername.Text;
            DefaultCredentials.Password = txtCredentialsPassword.Text;
            Settings.Default.DefaultDomain = txtCredentialsDomain.Text;

            Settings.Default.Save();
        }

        private void radCredentialsCustom_CheckedChanged(object sender, EventArgs e)
        {
            lblCredentialsUsername.Enabled = radCredentialsCustom.Checked;
            lblCredentialsPassword.Enabled = radCredentialsCustom.Checked;
            lblCredentialsDomain.Enabled = radCredentialsCustom.Checked;
            txtCredentialsUsername.Enabled = radCredentialsCustom.Checked;
            txtCredentialsPassword.Enabled = radCredentialsCustom.Checked;
            txtCredentialsDomain.Enabled = radCredentialsCustom.Checked;
        }
    }
}
