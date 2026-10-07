using mRemoteUG.UI.Forms.OptionsPages;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace mRemoteUG.UI.Forms
{
    /// <summary>
    /// The application options, one page per tab.
    /// </summary>
    /// <remarks>
    /// The tab strip carries no icons and <see cref="TabControl.ItemSize"/> is left unset on
    /// purpose. Both are DPI traps: an <see cref="ImageList"/> has to have its slot size rebuilt
    /// by hand on every DPI change, and a pinned <c>ItemSize</c> is a 96-DPI pixel literal that
    /// nothing rescales. Left alone, the native control measures the strip from its own font, and
    /// the font is what the auto-scale pass already scales. That is the whole of the DPI handling
    /// this dialog needs.
    /// </remarks>
    public partial class OptionsForm : Form
    {
        public OptionsForm()
        {
            InitializeComponent();
        }

        /// <summary>
        /// The options pages, in the order their tabs appear.
        /// </summary>
        private static OptionsPage[] CreateOptionsPages()
        {
            return new OptionsPage[]
            {
                new StartupExitPage(),
                new AppearancePage(),
                new TabsPanelsPage(),
                new NotificationsPage(),
                new ConnectionsPage(),
                new CredentialsPage(),
                new SecurityPage(),
                new AdvancedPage()
            };
        }

        /// <summary>
        /// The page each tab hosts. Empty until <see cref="OptionsForm_Load"/> has run, which it
        /// always has by the time a button on the dialog can be clicked.
        /// </summary>
        private IEnumerable<OptionsPage> Pages =>
            tabOptions.TabPages.Cast<TabPage>().Select(tab => (OptionsPage)tab.Tag);

        private void OptionsForm_Load(object sender, EventArgs e)
        {
            foreach (var page in CreateOptionsPages())
            {
                page.LoadSettings();
                page.Dock = DockStyle.Fill;

                var tab = new TabPage {Tag = page, UseVisualStyleBackColor = true};
                tab.Controls.Add(page);
                tabOptions.TabPages.Add(tab);
            }

            ApplyLanguage();

            // After ApplyLanguage, because the tab captions are what the strip is measured from.
            SizeToLargestPage();
        }

        /// <summary>
        /// Shrinks the dialog to the largest page it actually has to show.
        /// </summary>
        /// <remarks>
        /// The pages are absolutely positioned and all declare the same 610x489 designer canvas,
        /// which is bigger than any of them fills - the deepest, Notifications, uses about 80% of
        /// the width and 83% of the height, and the rest use far less. The designer size is
        /// therefore a poor description of what the dialog has to show, so it is measured here
        /// instead. Sized once, to the largest page, rather than per tab: a dialog that resized
        /// itself every time you clicked a tab would be worse than one that is slightly too big.
        /// </remarks>
        private void SizeToLargestPage()
        {
            var content = Size.Empty;

            foreach (var page in Pages)
            {
                var controls = page.Controls.Cast<Control>().ToList();
                if (controls.Count == 0)
                    continue;

                // The page was drawn nearly flush - the insets run 4 to 7 pixels left and 2 to 3
                // top - so the larger of the two is mirrored onto the right and bottom rather than
                // each axis onto itself. Notifications starts 7 from the left but has one control
                // 2 from the top, and mirroring that would leave its deepest group box 2 pixels
                // off the bottom edge while sitting 7 off the left.
                var gutter = Math.Max(controls.Min(c => c.Bounds.Left), controls.Min(c => c.Bounds.Top));

                content = new Size(
                    Math.Max(content.Width, controls.Max(c => c.Bounds.Right) + gutter),
                    Math.Max(content.Height, controls.Max(c => c.Bounds.Bottom) + gutter));
            }

            if (content.IsEmpty)
                return;

            // What the tab control spends on its border and its strip, which is height the pages
            // do not get and width they do not get either.
            var chrome = tabOptions.Size - tabOptions.DisplayRectangle.Size;

            // The strip is measured, not derived from the pages: a dialog narrow enough to wrap it
            // onto a second row would claw back the height this is trying to save.
            var strip = 0;
            for (var index = 0; index < tabOptions.TabCount; index++)
                strip += tabOptions.GetTabRect(index).Width;

            ClientSize = new Size(
                Math.Max(content.Width + chrome.Width, strip + chrome.Width),
                content.Height + chrome.Height + pnlBottom.Height);

            // ClientSize is set after the dialog has already been placed, so it has to be put back
            // in the middle of its owner.
            if (StartPosition == FormStartPosition.CenterParent)
                CenterToParent();
        }

        private void ApplyLanguage()
        {
            Text = Language.strOptionsPageTitle;

            foreach (TabPage tab in tabOptions.TabPages)
            {
                var page = (OptionsPage)tab.Tag;
                page.ApplyLanguage();

                // A tab caption goes through DrawText without DT_NOPREFIX, so an ampersand is
                // eaten as a mnemonic marker. PageName is the unescaped display text -
                // "Tabs & Panels" would otherwise render as "Tabs  Panels" with P underlined.
                tab.Text = page.PageName.Replace("&", "&&");
            }
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            foreach (var page in Pages)
            {
                page.SaveSettings();
            }

            Settings.Default.Save();
        }
    }
}
