using System;
using System.Drawing;
using System.Windows.Forms;


namespace mRemoteUG.UI.Window
{
    public partial class ConnectionWindow
    {
        internal ContextMenuStrip cmenTab;
        private System.ComponentModel.Container components;
        private ToolStripMenuItem cmenTabFullscreen;
        private ToolStripMenuItem cmenTabTransferFile;
        private ToolStripMenuItem cmenTabSendSpecialKeys;
        private ToolStripSeparator cmenTabSep1;
        private ToolStripMenuItem cmenTabRenameTab;
        private ToolStripMenuItem cmenTabDuplicateTab;
        private ToolStripMenuItem cmenTabDisconnect;
        private ToolStripMenuItem cmenTabSmartSize;
        private ToolStripMenuItem cmenTabSendSpecialKeysCtrlAltDel;
        private ToolStripMenuItem cmenTabSendSpecialKeysCtrlEsc;
        private ToolStripMenuItem cmenTabViewOnly;
        internal ToolStripMenuItem cmenTabReconnect;
        internal ToolStripMenuItem cmenTabExternalApps;
        private ToolStripMenuItem cmenTabStartChat;
        private ToolStripMenuItem cmenTabRefreshScreen;
        private ToolStripSeparator ToolStripSeparator1;
        private ToolStripMenuItem cmenTabPuttySettings;


        // Form overrides dispose to clean up the component list. Every other designer type in this
        // tree has this; this one did not, so cmenTab - a ContextMenuStrip with fifteen
        // image-bearing items - and everything else in the container outlived the panel. Form.Dispose
        // does not reach the container on its own.
        [System.Diagnostics.DebuggerNonUserCode()]
        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing && components != null)
                {
                    components.Dispose();
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        /// <summary>
        /// Takes ownership of a context menu built for this panel, so it goes when the panel does.
        /// </summary>
        /// <remarks>
        /// Assigning to <see cref="System.Windows.Forms.Control.ContextMenuStrip"/> does not transfer
        /// ownership - Control.Dispose leaves the menu alone - so the panel context menu PanelAdder
        /// builds leaked once per panel opened. The components container is the thing that is disposed
        /// above, so the menu goes in there.
        /// </remarks>
        internal void OwnContextMenu(ContextMenuStrip menu)
        {
            ContextMenuStrip = menu;
            if (menu != null)
                components.Add(menu);
        }

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            // The session strip paints its own background; a stock TabControl leaves the band
            // beside the tabs to the theme, which has no dark variant. See SessionTabControl.
            TabController = new mRemoteUG.UI.Panels.SessionTabControl();
            cmenTab = new ContextMenuStrip(components);
            cmenTabFullscreen = new ToolStripMenuItem();
            cmenTabSmartSize = new ToolStripMenuItem();
            cmenTabViewOnly = new ToolStripMenuItem();
            ToolStripSeparator1 = new ToolStripSeparator();
            cmenTabStartChat = new ToolStripMenuItem();
            cmenTabTransferFile = new ToolStripMenuItem();
            cmenTabRefreshScreen = new ToolStripMenuItem();
            cmenTabSendSpecialKeys = new ToolStripMenuItem();
            cmenTabSendSpecialKeysCtrlAltDel = new ToolStripMenuItem();
            cmenTabSendSpecialKeysCtrlEsc = new ToolStripMenuItem();
            cmenTabExternalApps = new ToolStripMenuItem();
            cmenTabSep1 = new ToolStripSeparator();
            cmenTabRenameTab = new ToolStripMenuItem();
            cmenTabDuplicateTab = new ToolStripMenuItem();
            cmenTabReconnect = new ToolStripMenuItem();
            cmenTabDisconnect = new ToolStripMenuItem();
            cmenTabPuttySettings = new ToolStripMenuItem();
            cmenTab.SuspendLayout();
            SuspendLayout();
            //
            //TabController
            //
            TabController.Anchor = ((AnchorStyles.Top | AnchorStyles.Bottom)
                | AnchorStyles.Left)
                | AnchorStyles.Right;
            TabController.Cursor = Cursors.Hand;
            TabController.DrawMode = TabDrawMode.OwnerDrawFixed;
            TabController.Location = new Point(0, -1);
            TabController.Name = "TabController";
            TabController.Size = new Size(632, 454);
            TabController.SizeMode = TabSizeMode.Fixed;
            TabController.TabIndex = 0;
            //
            //cmenTab
            //
            cmenTab.Items.AddRange(new ToolStripItem[]
            {
                cmenTabFullscreen,
                cmenTabSmartSize,
                cmenTabViewOnly,
                ToolStripSeparator1,
                cmenTabStartChat,
                cmenTabTransferFile,
                cmenTabRefreshScreen,
                cmenTabSendSpecialKeys,
                cmenTabPuttySettings,
                cmenTabExternalApps,
                cmenTabSep1,
                cmenTabRenameTab,
                cmenTabDuplicateTab,
                cmenTabReconnect,
                cmenTabDisconnect
            });
            cmenTab.Name = "cmenTab";
            cmenTab.Size = new Size(202, 346);
            //
            //cmenTabFullscreen
            //
            cmenTabFullscreen.Image = Resources.arrow_out;
            cmenTabFullscreen.Name = "cmenTabFullscreen";
            cmenTabFullscreen.Size = new Size(201, 22);
            cmenTabFullscreen.Text = @"Fullscreen (RDP)";
            //
            //cmenTabSmartSize
            //
            cmenTabSmartSize.Image = Resources.SmartSize;
            cmenTabSmartSize.Name = "cmenTabSmartSize";
            cmenTabSmartSize.Size = new Size(201, 22);
            cmenTabSmartSize.Text = @"SmartSize (RDP/VNC)";
            //
            //cmenTabViewOnly
            //
            cmenTabViewOnly.Name = "cmenTabViewOnly";
            cmenTabViewOnly.Size = new Size(201, 22);
            cmenTabViewOnly.Text = @"View Only (VNC)";
            //
            //ToolStripSeparator1
            //
            ToolStripSeparator1.Name = "ToolStripSeparator1";
            ToolStripSeparator1.Size = new Size(198, 6);
            //
            //cmenTabStartChat
            //
            cmenTabStartChat.Image = Resources.Chat;
            cmenTabStartChat.Name = "cmenTabStartChat";
            cmenTabStartChat.Size = new Size(201, 22);
            cmenTabStartChat.Text = @"Start Chat (VNC)";
            cmenTabStartChat.Visible = false;
            //
            //cmenTabTransferFile
            //
            cmenTabTransferFile.Image = Resources.SSHTransfer;
            cmenTabTransferFile.Name = "cmenTabTransferFile";
            cmenTabTransferFile.Size = new Size(201, 22);
            cmenTabTransferFile.Text = @"Transfer File (SSH)";
            //
            //cmenTabRefreshScreen
            //
            cmenTabRefreshScreen.Image = Resources.Refresh;
            cmenTabRefreshScreen.Name = "cmenTabRefreshScreen";
            cmenTabRefreshScreen.Size = new Size(201, 22);
            cmenTabRefreshScreen.Text = @"Refresh Screen (VNC)";
            //
            //cmenTabSendSpecialKeys
            //
            cmenTabSendSpecialKeys.DropDownItems.AddRange(new ToolStripItem[]
            {
                cmenTabSendSpecialKeysCtrlAltDel,
                cmenTabSendSpecialKeysCtrlEsc
            });
            cmenTabSendSpecialKeys.Image = Resources.Keyboard;
            cmenTabSendSpecialKeys.Name = "cmenTabSendSpecialKeys";
            cmenTabSendSpecialKeys.Size = new Size(201, 22);
            cmenTabSendSpecialKeys.Text = @"Send special Keys (VNC)";
            //
            //cmenTabSendSpecialKeysCtrlAltDel
            //
            cmenTabSendSpecialKeysCtrlAltDel.Name = "cmenTabSendSpecialKeysCtrlAltDel";
            cmenTabSendSpecialKeysCtrlAltDel.Size = new Size(141, 22);
            cmenTabSendSpecialKeysCtrlAltDel.Text = @"Ctrl+Alt+Del";
            //
            //cmenTabSendSpecialKeysCtrlEsc
            //
            cmenTabSendSpecialKeysCtrlEsc.Name = "cmenTabSendSpecialKeysCtrlEsc";
            cmenTabSendSpecialKeysCtrlEsc.Size = new Size(141, 22);
            cmenTabSendSpecialKeysCtrlEsc.Text = @"Ctrl+Esc";
            //
            //cmenTabExternalApps
            //
            cmenTabExternalApps.Image = Resources.ExternalApps;
            cmenTabExternalApps.Name = "cmenTabExternalApps";
            cmenTabExternalApps.Size = new Size(201, 22);
            cmenTabExternalApps.Text = @"External Applications";
            //
            //cmenTabSep1
            //
            cmenTabSep1.Name = "cmenTabSep1";
            cmenTabSep1.Size = new Size(198, 6);
            //
            //cmenTabRenameTab
            //
            cmenTabRenameTab.Image = Resources.Rename;
            cmenTabRenameTab.Name = "cmenTabRenameTab";
            cmenTabRenameTab.Size = new Size(201, 22);
            cmenTabRenameTab.Text = @"Rename Tab";
            //
            //cmenTabDuplicateTab
            //
            cmenTabDuplicateTab.Name = "cmenTabDuplicateTab";
            cmenTabDuplicateTab.Size = new Size(201, 22);
            cmenTabDuplicateTab.Text = @"Duplicate Tab";
            //
            //cmenTabReconnect
            //
            cmenTabReconnect.Image = Resources.Refresh;
            cmenTabReconnect.Name = "cmenTabReconnect";
            cmenTabReconnect.Size = new Size(201, 22);
            cmenTabReconnect.Text = @"Reconnect";
            //
            //cmenTabDisconnect
            //
            cmenTabDisconnect.Image = Resources.Pause;
            cmenTabDisconnect.Name = "cmenTabDisconnect";
            cmenTabDisconnect.Size = new Size(201, 22);
            cmenTabDisconnect.Text = @"Disconnect";
            //
            //cmenTabPuttySettings
            //
            cmenTabPuttySettings.Name = "cmenTabPuttySettings";
            cmenTabPuttySettings.Size = new Size(201, 22);
            cmenTabPuttySettings.Text = @"PuTTY Settings";
            //
            //Connection
            //
            // Segoe UI 8.25pt measures 6x13 at 96 DPI, which is the baseline this layout was
            // authored against, so the factor is 1.0 at 100% and the DPI ratio above it.
            AutoScaleDimensions = new SizeF(6F, 13F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(632, 453);
            Controls.Add(TabController);
            Name = "Connection";
            TabText = @"UI.Window.Connection";
            Text = @"UI.Window.Connection";
            cmenTab.ResumeLayout(false);
            ResumeLayout(false);
        }
    }
}