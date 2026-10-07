using System;
using System.Drawing;
using System.Windows.Forms;
using mRemoteUG.UI.Panels;
using mRemoteUG.UI.Window;

namespace mRemoteUG.Tests.UI.Panels
{
    /// <summary>
    /// The three splitters wired the way frmMain.Designer.cs wires them, hosted on a real form
    /// so that the sizes under test are the ones WinForms actually computes.
    /// </summary>
    internal sealed class MainLayoutHarness : IDisposable
    {
        public Form Form { get; }
        public SplitContainer SplitMain { get; }
        public SplitContainer SplitLeft { get; }
        public SplitContainer SplitDocuments { get; }
        public MainLayout Layout { get; }
        public BaseWindow TreeWindow { get; }
        public BaseWindow ConfigWindow { get; }
        public BaseWindow NotificationsWindow { get; }

        public MainLayoutHarness(int width = 1000, int height = 600)
        {
            SplitLeft = NewSplit(width, height, Orientation.Horizontal, FixedPanel.None, 60, 60);
            SplitDocuments = NewSplit(width, height, Orientation.Horizontal, FixedPanel.None, 100, 60);
            SplitMain = NewSplit(width, height, Orientation.Vertical, FixedPanel.Panel1, 100, 200);

            var tabs = new DocumentTabControl { Dock = DockStyle.Fill };
            SplitDocuments.Panel1.Controls.Add(tabs);
            SplitMain.Panel1.Controls.Add(SplitLeft);
            SplitMain.Panel2.Controls.Add(SplitDocuments);

            Form = new Form { ClientSize = new Size(width, height) };
            Form.Controls.Add(SplitMain);
            Form.CreateControl();
            Form.PerformLayout();

            TreeWindow = new BaseWindow();
            ConfigWindow = new BaseWindow();
            NotificationsWindow = new BaseWindow();

            Layout = new MainLayout(SplitMain, SplitLeft, SplitDocuments, tabs);
            Layout.HostToolWindows(TreeWindow, ConfigWindow, NotificationsWindow);
        }

        /// <summary>
        /// Builds one splitter the way the designer does, and for the same reason: a fresh
        /// SplitContainer is 150x100, and assigning a panel minimum that leaves no room for
        /// the splitter throws. The designer gets around it with BeginInit/EndInit; sizing it
        /// first is the same trick without the ceremony.
        /// </summary>
        private static SplitContainer NewSplit(int width, int height, Orientation orientation,
                                               FixedPanel fixedPanel, int panel1Min, int panel2Min)
        {
            var split = new SplitContainer
            {
                Size = new Size(width, height),
                Orientation = orientation,
                FixedPanel = fixedPanel
            };
            split.Panel1MinSize = panel1Min;
            split.Panel2MinSize = panel2Min;
            split.Dock = DockStyle.Fill;
            return split;
        }

        public void Resize(int width, int height)
        {
            Form.ClientSize = new Size(width, height);
            Form.PerformLayout();
        }

        public void Dispose()
        {
            Form.Dispose();
        }
    }
}
