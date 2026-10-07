namespace mRemoteUG.UI.Forms
{
	public partial class FrmMain : System.Windows.Forms.Form
	{
		
		//Form overrides dispose to clean up the component list.
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
		
		//NOTE: The following procedure is required by the Windows Form Designer
		//It can be modified using the Windows Form Designer.
		//Do not modify it using the code editor.
		[System.Diagnostics.DebuggerStepThrough()]
        private void InitializeComponent()
		{
            this.components = new System.ComponentModel.Container();
            mRemoteUG.Connection.ConnectionInitiator connectionInitiator1 = new mRemoteUG.Connection.ConnectionInitiator();
            this.splitMain = new System.Windows.Forms.SplitContainer();
            this.splitLeft = new System.Windows.Forms.SplitContainer();
            this.splitDocuments = new System.Windows.Forms.SplitContainer();
            this.tabDocuments = new mRemoteUG.UI.Panels.DocumentTabControl();
            this.msMain = new System.Windows.Forms.MenuStrip();
            this.fileMenu = new mRemoteUG.UI.Menu.MainFileMenu();
            this.viewMenu = new mRemoteUG.UI.Menu.ViewMenu();
            this.toolsMenu = new mRemoteUG.UI.Menu.ToolsMenu();
            this.helpMenu = new mRemoteUG.UI.Menu.HelpMenu();
            this.mMenSep3 = new System.Windows.Forms.ToolStripSeparator();
            this.tsContainer = new System.Windows.Forms.ToolStripContainer();
            this._quickConnectToolStrip = new mRemoteUG.UI.Controls.QuickConnectToolStrip();
            this.tmrAutoSave = new System.Windows.Forms.Timer(this.components);
            this.msMain.SuspendLayout();
            this.tsContainer.ContentPanel.SuspendLayout();
            this.tsContainer.TopToolStripPanel.SuspendLayout();
            this.tsContainer.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitMain)).BeginInit();
            this.splitMain.Panel1.SuspendLayout();
            this.splitMain.Panel2.SuspendLayout();
            this.splitMain.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitLeft)).BeginInit();
            this.splitLeft.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitDocuments)).BeginInit();
            this.splitDocuments.Panel1.SuspendLayout();
            this.splitDocuments.SuspendLayout();
            this.SuspendLayout();
            //
            // splitMain
            //
            this.splitMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitMain.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this.splitMain.Location = new System.Drawing.Point(0, 0);
            this.splitMain.Name = "splitMain";
            this.splitMain.Panel1.Controls.Add(this.splitLeft);
            this.splitMain.Panel1MinSize = 100;
            this.splitMain.Panel2.Controls.Add(this.splitDocuments);
            this.splitMain.Panel2MinSize = 200;
            this.splitMain.Size = new System.Drawing.Size(1129, 472);
            this.splitMain.SplitterDistance = 230;
            this.splitMain.TabIndex = 13;
            //
            // splitLeft
            //
            this.splitLeft.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitLeft.Location = new System.Drawing.Point(0, 0);
            this.splitLeft.Name = "splitLeft";
            this.splitLeft.Orientation = System.Windows.Forms.Orientation.Horizontal;
            this.splitLeft.Panel1MinSize = 60;
            this.splitLeft.Panel2MinSize = 60;
            this.splitLeft.Size = new System.Drawing.Size(230, 472);
            this.splitLeft.SplitterDistance = 280;
            this.splitLeft.TabIndex = 0;
            //
            // splitDocuments
            //
            this.splitDocuments.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitDocuments.Location = new System.Drawing.Point(0, 0);
            this.splitDocuments.Name = "splitDocuments";
            this.splitDocuments.Orientation = System.Windows.Forms.Orientation.Horizontal;
            this.splitDocuments.Panel1.Controls.Add(this.tabDocuments);
            this.splitDocuments.Panel1MinSize = 100;
            this.splitDocuments.Panel2Collapsed = true;
            this.splitDocuments.Panel2MinSize = 60;
            this.splitDocuments.Size = new System.Drawing.Size(895, 472);
            this.splitDocuments.SplitterDistance = 350;
            this.splitDocuments.TabIndex = 0;
            //
            // tabDocuments
            //
            this.tabDocuments.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabDocuments.Location = new System.Drawing.Point(0, 0);
            this.tabDocuments.Name = "tabDocuments";
            this.tabDocuments.SelectedIndex = 0;
            this.tabDocuments.Size = new System.Drawing.Size(895, 472);
            this.tabDocuments.TabIndex = 0;
            //
            // msMain
            // 
            this.msMain.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.msMain.Dock = System.Windows.Forms.DockStyle.None;
            this.msMain.GripMargin = new System.Windows.Forms.Padding(0);
            this.msMain.GripStyle = System.Windows.Forms.ToolStripGripStyle.Visible;
            this.msMain.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileMenu,
            this.viewMenu,
            this.toolsMenu,
            this.helpMenu});
            this.msMain.Location = new System.Drawing.Point(3, 50);
            this.msMain.Name = "msMain";
            this.msMain.Padding = new System.Windows.Forms.Padding(2, 2, 0, 2);
            this.msMain.Size = new System.Drawing.Size(176, 24);
            this.msMain.Stretch = false;
            this.msMain.TabIndex = 0;
            this.msMain.Text = "Main Toolbar";
            // 
            // fileMenu
            // 
            this.fileMenu.ConnectionInitiator = null;
            this.fileMenu.Name = "mMenFile";
            this.fileMenu.Size = new System.Drawing.Size(37, 20);
            this.fileMenu.Text = Language.strMenuFile;
            this.fileMenu.TreeWindow = null;
            this.fileMenu.DropDownOpening += new System.EventHandler(this.mainFileMenu1_DropDownOpening);
            // 
            // viewMenu
            // 
            this.viewMenu.FullscreenHandler = null;
            this.viewMenu.MainForm = null;
            this.viewMenu.Name = "mMenView";
            this.viewMenu.Size = new System.Drawing.Size(44, 20);
            this.viewMenu.Text = Language.strMenuView;
            this.viewMenu.TsQuickConnect = null;
            this.viewMenu.DropDownOpening += new System.EventHandler(this.ViewMenu_Opening);
            // 
            // toolsMenu
            // 
            this.toolsMenu.Name = "mMenTools";
            this.toolsMenu.Size = new System.Drawing.Size(47, 20);
            this.toolsMenu.Text = Language.strMenuTools;
            // 
            // helpMenu
            // 
            this.helpMenu.Name = "mMenInfo";
            this.helpMenu.Size = new System.Drawing.Size(44, 20);
            this.helpMenu.Text = Language.strMenuHelp;
            this.helpMenu.TextDirection = System.Windows.Forms.ToolStripTextDirection.Horizontal;
            // 
            // mMenSep3
            // 
            this.mMenSep3.Name = "mMenSep3";
            this.mMenSep3.Size = new System.Drawing.Size(211, 6);
            // 
            // tsContainer
            // 
            // 
            // tsContainer.ContentPanel
            // 
            this.tsContainer.ContentPanel.Controls.Add(this.splitMain);
            this.tsContainer.ContentPanel.Size = new System.Drawing.Size(1129, 472);
            this.tsContainer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tsContainer.Location = new System.Drawing.Point(0, 0);
            this.tsContainer.Name = "tsContainer";
            this.tsContainer.Size = new System.Drawing.Size(1129, 571);
            this.tsContainer.TabIndex = 17;
            this.tsContainer.Text = "ToolStripContainer1";
            // 
            // tsContainer.TopToolStripPanel
            // 
            this.tsContainer.TopToolStripPanel.Controls.Add(this._quickConnectToolStrip);
            this.tsContainer.TopToolStripPanel.Controls.Add(this.msMain);
            //
            // _quickConnectToolStrip
            //
            this._quickConnectToolStrip.BackColor = System.Drawing.SystemColors.Control;
            this._quickConnectToolStrip.ConnectionInitiator = connectionInitiator1;
            this._quickConnectToolStrip.Dock = System.Windows.Forms.DockStyle.None;
            this._quickConnectToolStrip.ForeColor = System.Drawing.SystemColors.ControlText;
            this._quickConnectToolStrip.Location = new System.Drawing.Point(3, 0);
            // No MaximumSize here either - see the note in QuickConnectToolStrip itself.
            this._quickConnectToolStrip.Name = "_quickConnectToolStrip";
            this._quickConnectToolStrip.Size = new System.Drawing.Size(364, 25);
            this._quickConnectToolStrip.TabIndex = 18;
            //
            // tmrAutoSave
            // 
            this.tmrAutoSave.Interval = 10000;
            this.tmrAutoSave.Tick += new System.EventHandler(this.tmrAutoSave_Tick);
            // 
            // 
            // 
            // FrmMain
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1129, 571);
            this.Controls.Add(this.tsContainer);
            this.MainMenuStrip = this.msMain;
            this.MinimumSize = new System.Drawing.Size(400, 400);
            this.Name = "FrmMain";
            this.Opacity = 0D;
            this.Text = "mRemoteUG";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.frmMain_FormClosing);
            this.Load += new System.EventHandler(this.frmMain_Load);
            this.ResizeBegin += new System.EventHandler(this.frmMain_ResizeBegin);
            this.ResizeEnd += new System.EventHandler(this.frmMain_ResizeEnd);
            this.Resize += new System.EventHandler(this.frmMain_Resize);
            this.msMain.ResumeLayout(false);
            this.msMain.PerformLayout();
            this.splitMain.Panel1.ResumeLayout(false);
            this.splitMain.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitMain)).EndInit();
            this.splitMain.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitLeft)).EndInit();
            this.splitLeft.ResumeLayout(false);
            this.splitDocuments.Panel1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitDocuments)).EndInit();
            this.splitDocuments.ResumeLayout(false);
            this.tsContainer.ContentPanel.ResumeLayout(false);
            this.tsContainer.TopToolStripPanel.ResumeLayout(false);
            this.tsContainer.TopToolStripPanel.PerformLayout();
            this.tsContainer.ResumeLayout(false);
            this.tsContainer.PerformLayout();
            this.ResumeLayout(false);

		}
		internal System.Windows.Forms.SplitContainer splitMain;
		internal System.Windows.Forms.SplitContainer splitLeft;
		internal System.Windows.Forms.SplitContainer splitDocuments;
		internal mRemoteUG.UI.Panels.DocumentTabControl tabDocuments;
		internal System.Windows.Forms.MenuStrip msMain;
		internal System.Windows.Forms.ToolStripContainer tsContainer;
		internal System.Windows.Forms.Timer tmrAutoSave;
		internal System.Windows.Forms.ToolStripSeparator mMenSep3;
        private System.ComponentModel.IContainer components;
        private Menu.MainFileMenu fileMenu;
        private Menu.ViewMenu viewMenu;
        private Menu.ToolsMenu toolsMenu;
        private Menu.HelpMenu helpMenu;
        internal mRemoteUG.UI.Controls.QuickConnectToolStrip _quickConnectToolStrip;
        //theming support
    }
}
