using System;
using System.Windows.Forms;
using mRemoteUG.App;
using mRemoteUG.Tools;

namespace mRemoteUG.UI.Forms.OptionsPages
{
    public sealed partial class AppearancePage
    {
        /// <summary>
        /// The theme the application is actually running in, remembered so that the restart
        /// notice appears only when the choice would differ from it.
        /// </summary>
        private UiTheme _runningTheme;

        public AppearancePage()
        {
            InitializeComponent();
            cboTheme.SelectedIndexChanged += (sender, args) => UpdateThemeRestartNotice();
        }

        public override string PageName
        {
            get => Language.strTabAppearance;
            set { }
        }

        public override void ApplyLanguage()
        {
            base.ApplyLanguage();

            lblTheme.Text = Language.strTheme;
            lblThemeRestartRequired.Text = string.Format(Language.strThemeRestartRequired, Application.ProductName);
            chkShowDescriptionTooltipsInTree.Text = Language.strShowDescriptionTooltips;
            chkShowFullConnectionsFilePathInTitle.Text = Language.strShowFullConsFilePath;
            chkShowSystemTrayIcon.Text = Language.strAlwaysShowSysTrayIcon;
            chkMinimizeToSystemTray.Text = Language.strMinimizeToSysTray;
        }

        public override void LoadSettings()
        {
            base.LoadSettings();

            LoadThemes();

            chkShowDescriptionTooltipsInTree.Checked = Settings.Default.ShowDescriptionTooltipsInTree;
            chkShowFullConnectionsFilePathInTitle.Checked = Settings.Default.ShowCompleteConsPathInTitle;
            chkShowSystemTrayIcon.Checked = Settings.Default.ShowSystemTrayIcon;
            chkMinimizeToSystemTray.Checked = Settings.Default.MinimizeToTray;
        }

        /// <summary>
        /// Fills the theme list, in the order the options are worth reading rather than sorted.
        /// </summary>
        /// <remarks>
        /// The saved setting is the one that matters here, not the theme the process is running
        /// in: they are the same at every normal start, and where they differ - because the user
        /// changed it and has not restarted yet - it is the saved value that describes what the
        /// application will do next, which is what the page is editing.
        /// </remarks>
        private void LoadThemes()
        {
            cboTheme.Items.Clear();
            cboTheme.Items.AddRange(new object[]
            {
                new ThemeChoice(UiTheme.System, Language.strThemeSystem),
                new ThemeChoice(UiTheme.Light, Language.strThemeLight),
                new ThemeChoice(UiTheme.Dark, Language.strThemeDark)
            });

            _runningTheme = Settings.Default.UiTheme;

            foreach (ThemeChoice choice in cboTheme.Items)
            {
                if (choice.Theme != _runningTheme)
                    continue;

                cboTheme.SelectedItem = choice;
                break;
            }

            if (cboTheme.SelectedIndex == -1)
                cboTheme.SelectedIndex = 0;

            UpdateThemeRestartNotice();
        }

        private UiTheme SelectedTheme =>
            cboTheme.SelectedItem is ThemeChoice choice ? choice.Theme : UiTheme.System;

        private void UpdateThemeRestartNotice()
        {
            lblThemeRestartRequired.Visible = SelectedTheme != _runningTheme;
        }

        public override void SaveSettings()
        {
            // Saved but deliberately not applied. Application.SetColorMode re-points SystemColors
            // immediately, but every piece of native theming - the dark title bar, the tree and
            // list backgrounds, the scrollbars, the combo box chrome - is applied once, when a
            // window handle is created. Switching here would leave a half-themed application, so
            // the theme is read at startup and the page says a restart is needed instead.
            Settings.Default.UiTheme = SelectedTheme;

            Settings.Default.ShowDescriptionTooltipsInTree = chkShowDescriptionTooltipsInTree.Checked;
            Settings.Default.ShowCompleteConsPathInTitle = chkShowFullConnectionsFilePathInTitle.Checked;
            FrmMain.Default.ShowFullPathInTitle = chkShowFullConnectionsFilePathInTitle.Checked;

            Settings.Default.ShowSystemTrayIcon = chkShowSystemTrayIcon.Checked;
            if (Settings.Default.ShowSystemTrayIcon)
            {
                if (Runtime.NotificationAreaIcon == null)
                {
                    Runtime.NotificationAreaIcon = new NotificationAreaIcon();
                }
            }
            else
            {
                if (Runtime.NotificationAreaIcon != null)
                {
                    Runtime.NotificationAreaIcon.Dispose();
                    Runtime.NotificationAreaIcon = null;
                }
            }

            Settings.Default.MinimizeToTray = chkMinimizeToSystemTray.Checked;

            Settings.Default.Save();
        }

        /// <summary>
        /// One entry in the theme list: the value to save, and the words to show for it.
        /// </summary>
        /// <remarks>
        /// The enum cannot be bound directly because its names are not translatable and one of
        /// them ("System") means something quite different to a user than it does to the code.
        /// </remarks>
        private sealed class ThemeChoice
        {
            public ThemeChoice(UiTheme theme, string text)
            {
                Theme = theme;
                _text = text;
            }

            public UiTheme Theme { get; }

            private readonly string _text;

            public override string ToString() => _text;
        }
    }
}
