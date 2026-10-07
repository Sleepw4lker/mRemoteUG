

namespace mRemoteUG.UI.Forms.OptionsPages
{
	
    public sealed partial class AppearancePage : OptionsPage
	{
			
		//UserControl overrides dispose to clean up the component list.
		[System.Diagnostics.DebuggerNonUserCode()]protected override void Dispose(bool disposing)
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
			
		//Required by the Windows Form Designer
		private System.ComponentModel.Container components = null;
			
		//NOTE: The following procedure is required by the Windows Form Designer
		//It can be modified using the Windows Form Designer.
		//Do not modify it using the code editor.
		[System.Diagnostics.DebuggerStepThrough()]private void InitializeComponent()
		{
            this.lblTheme = new System.Windows.Forms.Label();
            this.cboTheme = new System.Windows.Forms.ComboBox();
            this.lblThemeRestartRequired = new System.Windows.Forms.Label();
            this.chkShowFullConnectionsFilePathInTitle = new System.Windows.Forms.CheckBox();
            this.chkShowDescriptionTooltipsInTree = new System.Windows.Forms.CheckBox();
            this.chkShowSystemTrayIcon = new System.Windows.Forms.CheckBox();
            this.chkMinimizeToSystemTray = new System.Windows.Forms.CheckBox();
            this.SuspendLayout();
            //
            // lblTheme
            //
            this.lblTheme.AutoSize = true;
            this.lblTheme.Location = new System.Drawing.Point(3, 3);
            this.lblTheme.Name = "lblTheme";
            this.lblTheme.Size = new System.Drawing.Size(70, 13);
            this.lblTheme.TabIndex = 0;
            this.lblTheme.Text = "Colour theme";
            //
            // cboTheme
            //
            this.cboTheme.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboTheme.FormattingEnabled = true;
            this.cboTheme.Location = new System.Drawing.Point(3, 24);
            this.cboTheme.Name = "cboTheme";
            this.cboTheme.Size = new System.Drawing.Size(304, 21);
            this.cboTheme.TabIndex = 1;
            //
            // lblThemeRestartRequired
            //
            this.lblThemeRestartRequired.AutoSize = true;
            this.lblThemeRestartRequired.Location = new System.Drawing.Point(3, 56);
            this.lblThemeRestartRequired.Name = "lblThemeRestartRequired";
            this.lblThemeRestartRequired.Size = new System.Drawing.Size(380, 13);
            this.lblThemeRestartRequired.TabIndex = 2;
            this.lblThemeRestartRequired.Text = "mRemoteUG must be restarted before the new theme takes effect.";
            this.lblThemeRestartRequired.Visible = false;
            // 
            // chkShowFullConnectionsFilePathInTitle
            // 
            this.chkShowFullConnectionsFilePathInTitle.AutoSize = true;
            this.chkShowFullConnectionsFilePathInTitle.Location = new System.Drawing.Point(3, 102);
            this.chkShowFullConnectionsFilePathInTitle.Name = "chkShowFullConnectionsFilePathInTitle";
            this.chkShowFullConnectionsFilePathInTitle.Size = new System.Drawing.Size(239, 17);
            this.chkShowFullConnectionsFilePathInTitle.TabIndex = 4;
            this.chkShowFullConnectionsFilePathInTitle.Text = "Show full connections file path in window title";
            this.chkShowFullConnectionsFilePathInTitle.UseVisualStyleBackColor = true;
            // 
            // chkShowDescriptionTooltipsInTree
            // 
            this.chkShowDescriptionTooltipsInTree.AutoSize = true;
            this.chkShowDescriptionTooltipsInTree.Location = new System.Drawing.Point(3, 79);
            this.chkShowDescriptionTooltipsInTree.Name = "chkShowDescriptionTooltipsInTree";
            this.chkShowDescriptionTooltipsInTree.Size = new System.Drawing.Size(231, 17);
            this.chkShowDescriptionTooltipsInTree.TabIndex = 3;
            this.chkShowDescriptionTooltipsInTree.Text = "Show description tooltips in connection tree";
            this.chkShowDescriptionTooltipsInTree.UseVisualStyleBackColor = true;
            // 
            // chkShowSystemTrayIcon
            // 
            this.chkShowSystemTrayIcon.AutoSize = true;
            this.chkShowSystemTrayIcon.Location = new System.Drawing.Point(3, 148);
            this.chkShowSystemTrayIcon.Name = "chkShowSystemTrayIcon";
            this.chkShowSystemTrayIcon.Size = new System.Drawing.Size(172, 17);
            this.chkShowSystemTrayIcon.TabIndex = 5;
            this.chkShowSystemTrayIcon.Text = "Always show System Tray Icon";
            this.chkShowSystemTrayIcon.UseVisualStyleBackColor = true;
            // 
            // chkMinimizeToSystemTray
            // 
            this.chkMinimizeToSystemTray.AutoSize = true;
            this.chkMinimizeToSystemTray.Location = new System.Drawing.Point(3, 171);
            this.chkMinimizeToSystemTray.Name = "chkMinimizeToSystemTray";
            this.chkMinimizeToSystemTray.Size = new System.Drawing.Size(139, 17);
            this.chkMinimizeToSystemTray.TabIndex = 6;
            this.chkMinimizeToSystemTray.Text = "Minimize to System Tray";
            this.chkMinimizeToSystemTray.UseVisualStyleBackColor = true;
            // 
            // AppearancePage
            // 
            this.Controls.Add(this.lblTheme);
            this.Controls.Add(this.cboTheme);
            this.Controls.Add(this.lblThemeRestartRequired);
            this.Controls.Add(this.chkShowFullConnectionsFilePathInTitle);
            this.Controls.Add(this.chkShowDescriptionTooltipsInTree);
            this.Controls.Add(this.chkShowSystemTrayIcon);
            this.Controls.Add(this.chkMinimizeToSystemTray);
            // All eight options pages were authored against MS Sans Serif 8.25pt, which measures
            // 6x13 at 96 DPI, and declaring that is what makes their literals scale up to whatever
            // the ambient font is - Segoe UI 9pt, 7x15, since .NET Core changed the default.
            // This page used to declare 7x15, which meant it never scaled: its Locations stayed at
            // MS Sans Serif spacing while its eight AutoSize controls grew to fit the larger font,
            // so the designed whitespace was eaten from both ends. Three pairs ended up touching
            // at a 0-pixel gap. Scaling it like its siblings puts the gaps back.
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Name = "AppearancePage";
            this.Size = new System.Drawing.Size(610, 489);
            this.ResumeLayout(false);
            this.PerformLayout();

		}
		internal System.Windows.Forms.Label lblTheme;
		internal System.Windows.Forms.ComboBox cboTheme;
		internal System.Windows.Forms.Label lblThemeRestartRequired;
		internal System.Windows.Forms.CheckBox chkShowFullConnectionsFilePathInTitle;
		internal System.Windows.Forms.CheckBox chkShowDescriptionTooltipsInTree;
		internal System.Windows.Forms.CheckBox chkShowSystemTrayIcon;
		internal System.Windows.Forms.CheckBox chkMinimizeToSystemTray;
			
	}
}
