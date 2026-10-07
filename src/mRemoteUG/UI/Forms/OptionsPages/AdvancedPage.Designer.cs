

namespace mRemoteUG.UI.Forms.OptionsPages
{
    public sealed partial class AdvancedPage : OptionsPage
	{
		//UserControl overrides dispose to clean up the component list.
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

		//Required by the Windows Form Designer
		private System.ComponentModel.Container components = null;

		//NOTE: The following procedure is required by the Windows Form Designer
		//It can be modified using the Windows Form Designer.
		//Do not modify it using the code editor.
		[System.Diagnostics.DebuggerStepThrough()]
        private void InitializeComponent()
		{
            this.chkAutomaticReconnect = new System.Windows.Forms.CheckBox();
            this.chkLoadBalanceInfoUseUtf8 = new System.Windows.Forms.CheckBox();
            this.lblPuttyExecutable = new System.Windows.Forms.Label();
            this.txtPuttyPath = new System.Windows.Forms.TextBox();
            this.btnBrowsePuttyPath = new System.Windows.Forms.Button();
            this.btnLaunchPutty = new System.Windows.Forms.Button();
            this.lblPuttyStatus = new System.Windows.Forms.Label();
            this.lblConfigurePuttySessions = new System.Windows.Forms.Label();
            this.lblPuttyWaitTime = new System.Windows.Forms.Label();
            this.numPuttyWaitTime = new System.Windows.Forms.NumericUpDown();
            ((System.ComponentModel.ISupportInitialize)(this.numPuttyWaitTime)).BeginInit();
            this.SuspendLayout();
            //
            // chkAutomaticReconnect
            //
            this.chkAutomaticReconnect.AutoSize = true;
            this.chkAutomaticReconnect.Location = new System.Drawing.Point(3, 3);
            this.chkAutomaticReconnect.Name = "chkAutomaticReconnect";
            this.chkAutomaticReconnect.Size = new System.Drawing.Size(399, 17);
            this.chkAutomaticReconnect.TabIndex = 0;
            this.chkAutomaticReconnect.Text = "Automatically try to reconnect when disconnected from server (RDP only)";
            this.chkAutomaticReconnect.UseVisualStyleBackColor = true;
            //
            // chkLoadBalanceInfoUseUtf8
            //
            this.chkLoadBalanceInfoUseUtf8.AutoSize = true;
            this.chkLoadBalanceInfoUseUtf8.Location = new System.Drawing.Point(3, 26);
            this.chkLoadBalanceInfoUseUtf8.Name = "chkLoadBalanceInfoUseUtf8";
            this.chkLoadBalanceInfoUseUtf8.Size = new System.Drawing.Size(304, 17);
            this.chkLoadBalanceInfoUseUtf8.TabIndex = 1;
            this.chkLoadBalanceInfoUseUtf8.Text = "Use UTF8 encoding for RDP Load Balance Info property";
            this.chkLoadBalanceInfoUseUtf8.UseVisualStyleBackColor = true;
            //
            // lblPuttyExecutable
            //
            this.lblPuttyExecutable.AutoSize = true;
            this.lblPuttyExecutable.Location = new System.Drawing.Point(3, 62);
            this.lblPuttyExecutable.Name = "lblPuttyExecutable";
            this.lblPuttyExecutable.Size = new System.Drawing.Size(101, 13);
            this.lblPuttyExecutable.TabIndex = 2;
            this.lblPuttyExecutable.Text = "PuTTY executable:";
            //
            // txtPuttyPath
            //
            this.txtPuttyPath.Location = new System.Drawing.Point(6, 78);
            this.txtPuttyPath.Name = "txtPuttyPath";
            this.txtPuttyPath.Size = new System.Drawing.Size(396, 20);
            this.txtPuttyPath.TabIndex = 3;
            //
            // btnBrowsePuttyPath
            //
            this.btnBrowsePuttyPath.Location = new System.Drawing.Point(408, 76);
            this.btnBrowsePuttyPath.Name = "btnBrowsePuttyPath";
            this.btnBrowsePuttyPath.Size = new System.Drawing.Size(30, 23);
            this.btnBrowsePuttyPath.TabIndex = 4;
            this.btnBrowsePuttyPath.Text = "...";
            this.btnBrowsePuttyPath.UseVisualStyleBackColor = true;
            //
            // btnLaunchPutty
            //
            this.btnLaunchPutty.Image = global::mRemoteUG.Resources.PuttyConfig;
            this.btnLaunchPutty.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.btnLaunchPutty.Location = new System.Drawing.Point(444, 76);
            this.btnLaunchPutty.Name = "btnLaunchPutty";
            this.btnLaunchPutty.Size = new System.Drawing.Size(130, 23);
            this.btnLaunchPutty.TabIndex = 5;
            this.btnLaunchPutty.Text = "Launch PuTTY";
            this.btnLaunchPutty.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.btnLaunchPutty.UseVisualStyleBackColor = true;
            //
            // lblPuttyStatus
            //
            this.lblPuttyStatus.AutoSize = true;
            this.lblPuttyStatus.Location = new System.Drawing.Point(3, 104);
            this.lblPuttyStatus.Name = "lblPuttyStatus";
            this.lblPuttyStatus.Size = new System.Drawing.Size(0, 13);
            this.lblPuttyStatus.TabIndex = 6;
            //
            // lblConfigurePuttySessions
            //
            this.lblConfigurePuttySessions.AutoSize = true;
            this.lblConfigurePuttySessions.Location = new System.Drawing.Point(3, 127);
            this.lblConfigurePuttySessions.Name = "lblConfigurePuttySessions";
            this.lblConfigurePuttySessions.Size = new System.Drawing.Size(300, 13);
            this.lblConfigurePuttySessions.TabIndex = 7;
            this.lblConfigurePuttySessions.Text = "Use the button above to create and configure PuTTY sessions.";
            //
            // lblPuttyWaitTime
            //
            this.lblPuttyWaitTime.AutoSize = true;
            this.lblPuttyWaitTime.Location = new System.Drawing.Point(3, 160);
            this.lblPuttyWaitTime.Name = "lblPuttyWaitTime";
            this.lblPuttyWaitTime.Size = new System.Drawing.Size(180, 13);
            this.lblPuttyWaitTime.TabIndex = 8;
            this.lblPuttyWaitTime.Text = "Maximum PuTTY wait time (seconds):";
            //
            // numPuttyWaitTime
            //
            this.numPuttyWaitTime.Location = new System.Drawing.Point(6, 176);
            this.numPuttyWaitTime.Maximum = new decimal(new int[] {
            60,
            0,
            0,
            0});
            this.numPuttyWaitTime.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numPuttyWaitTime.Name = "numPuttyWaitTime";
            this.numPuttyWaitTime.Size = new System.Drawing.Size(60, 20);
            this.numPuttyWaitTime.TabIndex = 9;
            this.numPuttyWaitTime.Value = new decimal(new int[] {
            5,
            0,
            0,
            0});
            //
            // AdvancedPage
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.chkAutomaticReconnect);
            this.Controls.Add(this.chkLoadBalanceInfoUseUtf8);
            this.Controls.Add(this.lblPuttyExecutable);
            this.Controls.Add(this.txtPuttyPath);
            this.Controls.Add(this.btnBrowsePuttyPath);
            this.Controls.Add(this.btnLaunchPutty);
            this.Controls.Add(this.lblPuttyStatus);
            this.Controls.Add(this.lblConfigurePuttySessions);
            this.Controls.Add(this.lblPuttyWaitTime);
            this.Controls.Add(this.numPuttyWaitTime);
            this.Name = "AdvancedPage";
            this.Size = new System.Drawing.Size(610, 489);
            ((System.ComponentModel.ISupportInitialize)(this.numPuttyWaitTime)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

		}
		internal System.Windows.Forms.CheckBox chkAutomaticReconnect;
        private System.Windows.Forms.CheckBox chkLoadBalanceInfoUseUtf8;
        private System.Windows.Forms.Label lblPuttyExecutable;
        private System.Windows.Forms.TextBox txtPuttyPath;
        private System.Windows.Forms.Button btnBrowsePuttyPath;
        private System.Windows.Forms.Button btnLaunchPutty;
        private System.Windows.Forms.Label lblPuttyStatus;
        private System.Windows.Forms.Label lblConfigurePuttySessions;
        private System.Windows.Forms.Label lblPuttyWaitTime;
        private System.Windows.Forms.NumericUpDown numPuttyWaitTime;
    }
}
