namespace mRemoteUG.UI.Forms.OptionsPages
{
    sealed partial class SecurityPage
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
            this.chkEncryptCompleteFile = new System.Windows.Forms.CheckBox();
            this.groupAdvancedSecurityOptions = new System.Windows.Forms.GroupBox();
            this.numberBoxKdfIterations = new System.Windows.Forms.NumericUpDown();
            this.labelKdfIterations = new System.Windows.Forms.Label();
            this.groupAdvancedSecurityOptions.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numberBoxKdfIterations)).BeginInit();
            this.SuspendLayout();
            // 
            // chkEncryptCompleteFile
            // 
            this.chkEncryptCompleteFile.AutoSize = true;
            this.chkEncryptCompleteFile.Location = new System.Drawing.Point(3, 3);
            this.chkEncryptCompleteFile.Name = "chkEncryptCompleteFile";
            this.chkEncryptCompleteFile.Size = new System.Drawing.Size(180, 17);
            this.chkEncryptCompleteFile.TabIndex = 0;
            this.chkEncryptCompleteFile.Text = "Encrypt complete connection file";
            this.chkEncryptCompleteFile.UseVisualStyleBackColor = true;
            //
            // groupAdvancedSecurityOptions
            //
            this.groupAdvancedSecurityOptions.Controls.Add(this.numberBoxKdfIterations);
            this.groupAdvancedSecurityOptions.Controls.Add(this.labelKdfIterations);
            this.groupAdvancedSecurityOptions.Location = new System.Drawing.Point(3, 30);
            this.groupAdvancedSecurityOptions.Name = "groupAdvancedSecurityOptions";
            this.groupAdvancedSecurityOptions.Size = new System.Drawing.Size(604, 60);
            this.groupAdvancedSecurityOptions.TabIndex = 1;
            this.groupAdvancedSecurityOptions.TabStop = false;
            this.groupAdvancedSecurityOptions.Text = "Advanced Security Options";
            //
            // numberBoxKdfIterations
            //
            this.numberBoxKdfIterations.Increment = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.numberBoxKdfIterations.Location = new System.Drawing.Point(191, 25);
            this.numberBoxKdfIterations.Maximum = new decimal(new int[] {
            50000,
            0,
            0,
            0});
            this.numberBoxKdfIterations.Minimum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.numberBoxKdfIterations.Name = "numberBoxKdfIterations";
            this.numberBoxKdfIterations.Size = new System.Drawing.Size(90, 20);
            this.numberBoxKdfIterations.TabIndex = 5;
            this.numberBoxKdfIterations.ThousandsSeparator = true;
            this.numberBoxKdfIterations.Value = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            //
            // labelKdfIterations
            //
            this.labelKdfIterations.AutoSize = true;
            this.labelKdfIterations.Location = new System.Drawing.Point(9, 28);
            this.labelKdfIterations.Name = "labelKdfIterations";
            this.labelKdfIterations.Size = new System.Drawing.Size(166, 13);
            this.labelKdfIterations.TabIndex = 4;
            this.labelKdfIterations.Text = "Key Derivation Function Iterations";
            // 
            // SecurityPage
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.chkEncryptCompleteFile);
            this.Controls.Add(this.groupAdvancedSecurityOptions);
            this.Name = "SecurityPage";
            this.Size = new System.Drawing.Size(610, 489);
            this.groupAdvancedSecurityOptions.ResumeLayout(false);
            this.groupAdvancedSecurityOptions.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numberBoxKdfIterations)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        internal System.Windows.Forms.CheckBox chkEncryptCompleteFile;
        private System.Windows.Forms.GroupBox groupAdvancedSecurityOptions;
        private System.Windows.Forms.NumericUpDown numberBoxKdfIterations;
        private System.Windows.Forms.Label labelKdfIterations;
    }
}
