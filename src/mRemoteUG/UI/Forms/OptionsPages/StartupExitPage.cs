using System;

namespace mRemoteUG.UI.Forms.OptionsPages
{
    public sealed partial class StartupExitPage
    {
        public StartupExitPage()
        {
            InitializeComponent();
        }

        public override string PageName
        {
            get => Language.strStartupExit;
            set { }
        }

        public override void ApplyLanguage()
        {
            base.ApplyLanguage();

            chkSaveConsOnExit.Text = Language.strSaveConsOnExit;
            chkReconnectOnStart.Text = Language.strReconnectAtStartup;
            chkSingleInstance.Text = Language.strAllowOnlySingleInstance;
        }

        public override void SaveSettings()
        {
            base.SaveSettings();

            Settings.Default.SaveConsOnExit = chkSaveConsOnExit.Checked;
            Settings.Default.OpenConsFromLastSession = chkReconnectOnStart.Checked;
            Settings.Default.SingleInstance = chkSingleInstance.Checked;

            Settings.Default.Save();
        }

        private void StartupExitPage_Load(object sender, EventArgs e)
        {
            chkSaveConsOnExit.Checked = Settings.Default.SaveConsOnExit;
            chkReconnectOnStart.Checked = Settings.Default.OpenConsFromLastSession;
            chkSingleInstance.Checked = Settings.Default.SingleInstance;
        }
    }
}
