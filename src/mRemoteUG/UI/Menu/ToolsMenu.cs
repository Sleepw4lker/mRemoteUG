using System;
using System.Windows.Forms;
using mRemoteUG.App;
using mRemoteUG.UI.Forms;

namespace mRemoteUG.UI.Menu
{
	public class ToolsMenu : ToolStripMenuItem
    {
        private ToolStripMenuItem _mMenToolsOptions;

        public ToolsMenu()
        {
            Initialize();
        }

        private void Initialize()
        {
            _mMenToolsOptions = new ToolStripMenuItem();

            //
            // mMenTools
            //
            DropDownItems.AddRange(new ToolStripItem[] {
            _mMenToolsOptions});
            Name = "mMenTools";
            Size = new System.Drawing.Size(48, 20);
            Text = Language.strMenuTools;
            //
            // mMenToolsOptions
            //
            _mMenToolsOptions.Image = Resources.Options;
            _mMenToolsOptions.Name = "mMenToolsOptions";
            _mMenToolsOptions.Size = new System.Drawing.Size(184, 22);
            _mMenToolsOptions.Text = Language.strMenuOptions;
            _mMenToolsOptions.Click += mMenToolsOptions_Click;
        }


        #region Tools
        private void mMenToolsOptions_Click(object? sender, EventArgs e)
        {
            try
            {
                using (var optionsForm = new OptionsForm())
                {
                    optionsForm.ShowDialog(FrmMain.Default);
                }
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("Showing the options dialog failed.", ex);
            }
        }
        #endregion
    }
}
