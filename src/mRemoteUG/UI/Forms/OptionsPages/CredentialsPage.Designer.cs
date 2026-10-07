namespace mRemoteUG.UI.Forms.OptionsPages
{
    sealed partial class CredentialsPage
    {
        /// <summary> 
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.pnlDefaultCredentials = new System.Windows.Forms.Panel();
            this.radCredentialsCustom = new System.Windows.Forms.RadioButton();
            this.lblDefaultCredentials = new System.Windows.Forms.Label();
            this.radCredentialsNoInfo = new System.Windows.Forms.RadioButton();
            this.radCredentialsWindows = new System.Windows.Forms.RadioButton();
            this.txtCredentialsDomain = new System.Windows.Forms.TextBox();
            this.lblCredentialsUsername = new System.Windows.Forms.Label();
            this.txtCredentialsPassword = new System.Windows.Forms.TextBox();
            this.lblCredentialsPassword = new System.Windows.Forms.Label();
            this.txtCredentialsUsername = new System.Windows.Forms.TextBox();
            this.lblCredentialsDomain = new System.Windows.Forms.Label();
            this.pnlDefaultCredentials.SuspendLayout();
            this.SuspendLayout();
            // 
            // pnlDefaultCredentials
            // 
            this.pnlDefaultCredentials.Controls.Add(this.radCredentialsCustom);
            this.pnlDefaultCredentials.Controls.Add(this.lblDefaultCredentials);
            this.pnlDefaultCredentials.Controls.Add(this.radCredentialsNoInfo);
            this.pnlDefaultCredentials.Controls.Add(this.radCredentialsWindows);
            this.pnlDefaultCredentials.Controls.Add(this.txtCredentialsDomain);
            this.pnlDefaultCredentials.Controls.Add(this.lblCredentialsUsername);
            this.pnlDefaultCredentials.Controls.Add(this.txtCredentialsPassword);
            this.pnlDefaultCredentials.Controls.Add(this.lblCredentialsPassword);
            this.pnlDefaultCredentials.Controls.Add(this.txtCredentialsUsername);
            this.pnlDefaultCredentials.Controls.Add(this.lblCredentialsDomain);
            this.pnlDefaultCredentials.Location = new System.Drawing.Point(3, 3);
            this.pnlDefaultCredentials.Name = "pnlDefaultCredentials";
            this.pnlDefaultCredentials.Size = new System.Drawing.Size(596, 210);
            this.pnlDefaultCredentials.TabIndex = 0;
            //
            // radCredentialsCustom
            // 
            this.radCredentialsCustom.AutoSize = true;
            this.radCredentialsCustom.BackColor = System.Drawing.Color.Transparent;
            this.radCredentialsCustom.Location = new System.Drawing.Point(16, 69);
            this.radCredentialsCustom.Name = "radCredentialsCustom";
            this.radCredentialsCustom.Size = new System.Drawing.Size(87, 17);
            this.radCredentialsCustom.TabIndex = 3;
            this.radCredentialsCustom.Text = "the following:";
            this.radCredentialsCustom.UseVisualStyleBackColor = false;
            this.radCredentialsCustom.CheckedChanged += new System.EventHandler(this.radCredentialsCustom_CheckedChanged);
            // 
            // lblDefaultCredentials
            // 
            this.lblDefaultCredentials.AutoSize = true;
            this.lblDefaultCredentials.Location = new System.Drawing.Point(3, 9);
            this.lblDefaultCredentials.Name = "lblDefaultCredentials";
            this.lblDefaultCredentials.Size = new System.Drawing.Size(257, 13);
            this.lblDefaultCredentials.TabIndex = 0;
            this.lblDefaultCredentials.Text = "For empty Username, Password or Domain fields use:";
            // 
            // radCredentialsNoInfo
            // 
            this.radCredentialsNoInfo.AutoSize = true;
            this.radCredentialsNoInfo.BackColor = System.Drawing.Color.Transparent;
            this.radCredentialsNoInfo.Checked = true;
            this.radCredentialsNoInfo.Location = new System.Drawing.Point(16, 31);
            this.radCredentialsNoInfo.Name = "radCredentialsNoInfo";
            this.radCredentialsNoInfo.Size = new System.Drawing.Size(91, 17);
            this.radCredentialsNoInfo.TabIndex = 1;
            this.radCredentialsNoInfo.TabStop = true;
            this.radCredentialsNoInfo.Text = "no information";
            this.radCredentialsNoInfo.UseVisualStyleBackColor = false;
            // 
            // radCredentialsWindows
            // 
            this.radCredentialsWindows.AutoSize = true;
            this.radCredentialsWindows.BackColor = System.Drawing.Color.Transparent;
            this.radCredentialsWindows.Location = new System.Drawing.Point(16, 50);
            this.radCredentialsWindows.Name = "radCredentialsWindows";
            this.radCredentialsWindows.Size = new System.Drawing.Size(227, 17);
            this.radCredentialsWindows.TabIndex = 2;
            this.radCredentialsWindows.Text = "my current credentials (windows logon info)";
            this.radCredentialsWindows.UseVisualStyleBackColor = false;
            // 
            // txtCredentialsDomain
            // 
            this.txtCredentialsDomain.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtCredentialsDomain.Enabled = false;
            this.txtCredentialsDomain.Location = new System.Drawing.Point(140, 172);
            this.txtCredentialsDomain.Name = "txtCredentialsDomain";
            this.txtCredentialsDomain.Size = new System.Drawing.Size(150, 20);
            this.txtCredentialsDomain.TabIndex = 9;
            // 
            // lblCredentialsUsername
            // 
            this.lblCredentialsUsername.Enabled = false;
            this.lblCredentialsUsername.Location = new System.Drawing.Point(37, 95);
            this.lblCredentialsUsername.Name = "lblCredentialsUsername";
            this.lblCredentialsUsername.Size = new System.Drawing.Size(97, 13);
            this.lblCredentialsUsername.TabIndex = 4;
            this.lblCredentialsUsername.Text = "Username:";
            this.lblCredentialsUsername.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // txtCredentialsPassword
            // 
            this.txtCredentialsPassword.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtCredentialsPassword.Enabled = false;
            this.txtCredentialsPassword.Location = new System.Drawing.Point(140, 120);
            this.txtCredentialsPassword.Name = "txtCredentialsPassword";
            this.txtCredentialsPassword.Size = new System.Drawing.Size(150, 20);
            this.txtCredentialsPassword.TabIndex = 7;
            this.txtCredentialsPassword.UseSystemPasswordChar = true;
            // 
            // lblCredentialsPassword
            // 
            this.lblCredentialsPassword.Enabled = false;
            this.lblCredentialsPassword.Location = new System.Drawing.Point(34, 123);
            this.lblCredentialsPassword.Name = "lblCredentialsPassword";
            this.lblCredentialsPassword.Size = new System.Drawing.Size(100, 13);
            this.lblCredentialsPassword.TabIndex = 6;
            this.lblCredentialsPassword.Text = "Password:";
            this.lblCredentialsPassword.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // txtCredentialsUsername
            // 
            this.txtCredentialsUsername.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtCredentialsUsername.Enabled = false;
            this.txtCredentialsUsername.Location = new System.Drawing.Point(140, 93);
            this.txtCredentialsUsername.Name = "txtCredentialsUsername";
            this.txtCredentialsUsername.Size = new System.Drawing.Size(150, 20);
            this.txtCredentialsUsername.TabIndex = 5;
            // 
            // lblCredentialsDomain
            // 
            this.lblCredentialsDomain.Enabled = false;
            this.lblCredentialsDomain.Location = new System.Drawing.Point(34, 175);
            this.lblCredentialsDomain.Name = "lblCredentialsDomain";
            this.lblCredentialsDomain.Size = new System.Drawing.Size(100, 13);
            this.lblCredentialsDomain.TabIndex = 8;
            this.lblCredentialsDomain.Text = "Domain:";
            this.lblCredentialsDomain.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // CredentialsPage
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.pnlDefaultCredentials);
            this.Name = "CredentialsPage";
            this.Size = new System.Drawing.Size(610, 489);
            this.pnlDefaultCredentials.ResumeLayout(false);
            this.pnlDefaultCredentials.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion
        internal System.Windows.Forms.Panel pnlDefaultCredentials;
        internal System.Windows.Forms.RadioButton radCredentialsCustom;
        internal System.Windows.Forms.Label lblDefaultCredentials;
        internal System.Windows.Forms.RadioButton radCredentialsNoInfo;
        internal System.Windows.Forms.RadioButton radCredentialsWindows;
        internal System.Windows.Forms.TextBox txtCredentialsDomain;
        internal System.Windows.Forms.Label lblCredentialsUsername;
        internal System.Windows.Forms.TextBox txtCredentialsPassword;
        internal System.Windows.Forms.Label lblCredentialsPassword;
        internal System.Windows.Forms.TextBox txtCredentialsUsername;
        internal System.Windows.Forms.Label lblCredentialsDomain;
    }
}
