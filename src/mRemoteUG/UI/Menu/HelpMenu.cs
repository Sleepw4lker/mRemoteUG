using System;
using System.Windows.Forms;
using mRemoteUG.App.Info;
using mRemoteUG.UI.Forms;
using mRemoteUG.UI.TaskDialog;

namespace mRemoteUG.UI.Menu
{
    /// <summary>
    /// The Help menu, which is now one item: About.
    /// </summary>
    /// <remarks>
    /// It used to carry Website, Donate, Support Forum and Report a Bug as well, and every one of
    /// them had been broken since this fork dropped the HTTP and HTTPS protocols. They did not
    /// open a browser: they built a connection whose protocol was HTTP and handed it to the
    /// connection initiator, which has no case for it any more and answers "protocol not
    /// supported". Four menu items that could only produce an error message.
    /// <para>
    /// Help Contents went earlier, with the window and the IE control that displayed it.
    /// </para>
    /// </remarks>
    public class HelpMenu : ToolStripMenuItem
    {
        private ToolStripMenuItem _mMenInfoAbout;

        public HelpMenu()
        {
            Initialize();
        }

        private void Initialize()
        {
            _mMenInfoAbout = new ToolStripMenuItem();

            //
            // mMenInfo
            //
            DropDownItems.AddRange(new ToolStripItem[] { _mMenInfoAbout });
            Name = "mMenInfo";
            Size = new System.Drawing.Size(44, 20);
            Text = Language.strMenuHelp;
            TextDirection = ToolStripTextDirection.Horizontal;
            //
            // mMenInfoAbout
            //
            _mMenInfoAbout.Image = Resources.About;
            _mMenInfoAbout.Name = "mMenInfoAbout";
            _mMenInfoAbout.Size = new System.Drawing.Size(190, 22);
            _mMenInfoAbout.Text = Language.strMenuAbout;
            _mMenInfoAbout.Click += mMenInfoAbout_Click;
        }


        /// <summary>
        /// Shows what this is, which version it is, and whose work it is built on.
        /// </summary>
        /// <remarks>
        /// This used to be a window of its own - a black logo banner, four labels and two
        /// scrolling document viewers reading CREDITS.TXT and CHANGELOG.TXT, laid out by hand in
        /// a 1117x705 design. Both files are still installed beside the executable for anyone who
        /// wants them; what the dialog itself needs to say is a few lines and an expander, and
        /// the task dialog Windows provides says them without a layout to maintain.
        /// <para>
        /// The text is <see cref="AboutText"/> rather than literals here, so it can be asserted
        /// without showing a modal dialog.
        /// </para>
        /// </remarks>
        private void mMenInfoAbout_Click(object? sender, EventArgs e)
        {
            CTaskDialog.MessageBox(
                FrmMain.Default,
                Language.strAbout,
                GeneralAppInfo.ProductName,
                AboutText.Content,
                AboutText.Credits,
                Language.strLabelReleasedUnderGPL,
                "",
                ETaskDialogButtons.Ok,
                ESysIcons.Information,
                ESysIcons.Information);
        }
    }
}
