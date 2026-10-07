

namespace mRemoteUG.UI.Forms.OptionsPages
{
	
    public sealed partial class NotificationsPage : OptionsPage
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
            this.labelSwitchToErrorsAndInfos = new System.Windows.Forms.Label();
            this.chkSwitchToMCInformation = new System.Windows.Forms.CheckBox();
            this.chkSwitchToMCErrors = new System.Windows.Forms.CheckBox();
            this.chkSwitchToMCWarnings = new System.Windows.Forms.CheckBox();
            this.groupBoxNotifications = new System.Windows.Forms.GroupBox();
            this.labelNotificationsShowTypes = new System.Windows.Forms.Label();
            this.chkShowErrorInMC = new System.Windows.Forms.CheckBox();
            this.chkShowWarningInMC = new System.Windows.Forms.CheckBox();
            this.chkShowInfoInMC = new System.Windows.Forms.CheckBox();
            this.chkShowDebugInMC = new System.Windows.Forms.CheckBox();
            this.groupBoxLogging = new System.Windows.Forms.GroupBox();
            this.tblLogging = new System.Windows.Forms.TableLayoutPanel();
            this.chkLogDebugMsgs = new System.Windows.Forms.CheckBox();
            this.chkLogInfoMsgs = new System.Windows.Forms.CheckBox();
            this.chkLogWarningMsgs = new System.Windows.Forms.CheckBox();
            this.chkLogErrorMsgs = new System.Windows.Forms.CheckBox();
            this.chkLogToCurrentDir = new System.Windows.Forms.CheckBox();
            this.buttonRestoreDefaultLogPath = new System.Windows.Forms.Button();
            this.buttonOpenLogFile = new System.Windows.Forms.Button();
            this.buttonSelectLogPath = new System.Windows.Forms.Button();
            this.labelLogTheseMsgTypes = new System.Windows.Forms.Label();
            this.labelLogFilePath = new System.Windows.Forms.Label();
            this.textBoxLogPath = new System.Windows.Forms.TextBox();
            this.saveFileDialogLogging = new System.Windows.Forms.SaveFileDialog();
            this.groupBoxPopups = new System.Windows.Forms.GroupBox();
            this.tblPopups = new System.Windows.Forms.TableLayoutPanel();
            this.chkPopupDebug = new System.Windows.Forms.CheckBox();
            this.chkPopupError = new System.Windows.Forms.CheckBox();
            this.chkPopupInfo = new System.Windows.Forms.CheckBox();
            this.chkPopupWarning = new System.Windows.Forms.CheckBox();
            this.labelPopupShowTypes = new System.Windows.Forms.Label();
            this.groupBoxNotifications.SuspendLayout();
            this.groupBoxLogging.SuspendLayout();
            this.tblLogging.SuspendLayout();
            this.groupBoxPopups.SuspendLayout();
            this.tblPopups.SuspendLayout();
            this.SuspendLayout();
            // 
            // labelSwitchToErrorsAndInfos
            // 
            this.labelSwitchToErrorsAndInfos.AutoSize = true;
            this.labelSwitchToErrorsAndInfos.Location = new System.Drawing.Point(177, 25);
            this.labelSwitchToErrorsAndInfos.Name = "labelSwitchToErrorsAndInfos";
            this.labelSwitchToErrorsAndInfos.Size = new System.Drawing.Size(159, 13);
            this.labelSwitchToErrorsAndInfos.TabIndex = 5;
            this.labelSwitchToErrorsAndInfos.Text = "Switch to Notifications panel on:";
            // 
            // chkSwitchToMCInformation
            // 
            this.chkSwitchToMCInformation.AutoSize = true;
            this.chkSwitchToMCInformation.Location = new System.Drawing.Point(195, 67);
            this.chkSwitchToMCInformation.Name = "chkSwitchToMCInformation";
            this.chkSwitchToMCInformation.Size = new System.Drawing.Size(78, 17);
            this.chkSwitchToMCInformation.TabIndex = 6;
            this.chkSwitchToMCInformation.Text = "Information";
            this.chkSwitchToMCInformation.UseVisualStyleBackColor = true;
            // 
            // chkSwitchToMCErrors
            // 
            this.chkSwitchToMCErrors.AutoSize = true;
            this.chkSwitchToMCErrors.Location = new System.Drawing.Point(195, 113);
            this.chkSwitchToMCErrors.Name = "chkSwitchToMCErrors";
            this.chkSwitchToMCErrors.Size = new System.Drawing.Size(48, 17);
            this.chkSwitchToMCErrors.TabIndex = 8;
            this.chkSwitchToMCErrors.Text = "Error";
            this.chkSwitchToMCErrors.UseVisualStyleBackColor = true;
            // 
            // chkSwitchToMCWarnings
            // 
            this.chkSwitchToMCWarnings.AutoSize = true;
            this.chkSwitchToMCWarnings.Location = new System.Drawing.Point(195, 90);
            this.chkSwitchToMCWarnings.Name = "chkSwitchToMCWarnings";
            this.chkSwitchToMCWarnings.Size = new System.Drawing.Size(66, 17);
            this.chkSwitchToMCWarnings.TabIndex = 7;
            this.chkSwitchToMCWarnings.Text = "Warning";
            this.chkSwitchToMCWarnings.UseVisualStyleBackColor = true;
            // 
            // groupBoxNotifications
            // 
            this.groupBoxNotifications.Controls.Add(this.labelNotificationsShowTypes);
            this.groupBoxNotifications.Controls.Add(this.labelSwitchToErrorsAndInfos);
            this.groupBoxNotifications.Controls.Add(this.chkSwitchToMCErrors);
            this.groupBoxNotifications.Controls.Add(this.chkShowErrorInMC);
            this.groupBoxNotifications.Controls.Add(this.chkSwitchToMCInformation);
            this.groupBoxNotifications.Controls.Add(this.chkShowWarningInMC);
            this.groupBoxNotifications.Controls.Add(this.chkSwitchToMCWarnings);
            this.groupBoxNotifications.Controls.Add(this.chkShowInfoInMC);
            this.groupBoxNotifications.Controls.Add(this.chkShowDebugInMC);
            this.groupBoxNotifications.Location = new System.Drawing.Point(6, 2);
            this.groupBoxNotifications.Name = "groupBoxNotifications";
            this.groupBoxNotifications.Size = new System.Drawing.Size(601, 141);
            this.groupBoxNotifications.TabIndex = 0;
            this.groupBoxNotifications.TabStop = false;
            this.groupBoxNotifications.Text = "Notifications Panel";
            // 
            // labelNotificationsShowTypes
            // 
            this.labelNotificationsShowTypes.AutoSize = true;
            this.labelNotificationsShowTypes.Location = new System.Drawing.Point(6, 25);
            this.labelNotificationsShowTypes.Name = "labelNotificationsShowTypes";
            this.labelNotificationsShowTypes.Size = new System.Drawing.Size(139, 13);
            this.labelNotificationsShowTypes.TabIndex = 0;
            this.labelNotificationsShowTypes.Text = "Show these message types:";
            // 
            // chkShowErrorInMC
            // 
            this.chkShowErrorInMC.AutoSize = true;
            this.chkShowErrorInMC.Location = new System.Drawing.Point(20, 113);
            this.chkShowErrorInMC.Name = "chkShowErrorInMC";
            this.chkShowErrorInMC.Size = new System.Drawing.Size(48, 17);
            this.chkShowErrorInMC.TabIndex = 4;
            this.chkShowErrorInMC.Text = "Error";
            this.chkShowErrorInMC.UseVisualStyleBackColor = true;
            // 
            // chkShowWarningInMC
            // 
            this.chkShowWarningInMC.AutoSize = true;
            this.chkShowWarningInMC.Location = new System.Drawing.Point(20, 90);
            this.chkShowWarningInMC.Name = "chkShowWarningInMC";
            this.chkShowWarningInMC.Size = new System.Drawing.Size(66, 17);
            this.chkShowWarningInMC.TabIndex = 3;
            this.chkShowWarningInMC.Text = "Warning";
            this.chkShowWarningInMC.UseVisualStyleBackColor = true;
            // 
            // chkShowInfoInMC
            // 
            this.chkShowInfoInMC.AutoSize = true;
            this.chkShowInfoInMC.Location = new System.Drawing.Point(20, 67);
            this.chkShowInfoInMC.Name = "chkShowInfoInMC";
            this.chkShowInfoInMC.Size = new System.Drawing.Size(78, 17);
            this.chkShowInfoInMC.TabIndex = 2;
            this.chkShowInfoInMC.Text = "Information";
            this.chkShowInfoInMC.UseVisualStyleBackColor = true;
            // 
            // chkShowDebugInMC
            // 
            this.chkShowDebugInMC.AutoSize = true;
            this.chkShowDebugInMC.Location = new System.Drawing.Point(20, 44);
            this.chkShowDebugInMC.Name = "chkShowDebugInMC";
            this.chkShowDebugInMC.Size = new System.Drawing.Size(58, 17);
            this.chkShowDebugInMC.TabIndex = 1;
            this.chkShowDebugInMC.Text = "Debug";
            this.chkShowDebugInMC.UseVisualStyleBackColor = true;
            // 
            // groupBoxLogging
            // 
            this.groupBoxLogging.Controls.Add(this.tblLogging);
            this.groupBoxLogging.Controls.Add(this.chkLogToCurrentDir);
            this.groupBoxLogging.Controls.Add(this.buttonRestoreDefaultLogPath);
            this.groupBoxLogging.Controls.Add(this.buttonOpenLogFile);
            this.groupBoxLogging.Controls.Add(this.buttonSelectLogPath);
            this.groupBoxLogging.Controls.Add(this.labelLogTheseMsgTypes);
            this.groupBoxLogging.Controls.Add(this.labelLogFilePath);
            this.groupBoxLogging.Controls.Add(this.textBoxLogPath);
            this.groupBoxLogging.Location = new System.Drawing.Point(6, 149);
            this.groupBoxLogging.Name = "groupBoxLogging";
            this.groupBoxLogging.Size = new System.Drawing.Size(601, 158);
            this.groupBoxLogging.TabIndex = 1;
            this.groupBoxLogging.TabStop = false;
            this.groupBoxLogging.Text = "Logging";
            // 
            // tblLogging
            // 
            this.tblLogging.ColumnCount = 4;
            this.tblLogging.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tblLogging.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tblLogging.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tblLogging.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tblLogging.Controls.Add(this.chkLogDebugMsgs, 0, 0);
            this.tblLogging.Controls.Add(this.chkLogInfoMsgs, 1, 0);
            this.tblLogging.Controls.Add(this.chkLogWarningMsgs, 2, 0);
            this.tblLogging.Controls.Add(this.chkLogErrorMsgs, 3, 0);
            this.tblLogging.Location = new System.Drawing.Point(9, 124);
            this.tblLogging.Name = "tblLogging";
            this.tblLogging.RowCount = 1;
            this.tblLogging.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tblLogging.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tblLogging.Size = new System.Drawing.Size(585, 25);
            this.tblLogging.TabIndex = 7;
            // 
            // chkLogDebugMsgs
            // 
            this.chkLogDebugMsgs.AutoSize = true;
            this.chkLogDebugMsgs.Location = new System.Drawing.Point(3, 3);
            this.chkLogDebugMsgs.Name = "chkLogDebugMsgs";
            this.chkLogDebugMsgs.Size = new System.Drawing.Size(58, 17);
            this.chkLogDebugMsgs.TabIndex = 0;
            this.chkLogDebugMsgs.Text = "Debug";
            this.chkLogDebugMsgs.UseVisualStyleBackColor = true;
            // 
            // chkLogInfoMsgs
            // 
            this.chkLogInfoMsgs.AutoSize = true;
            this.chkLogInfoMsgs.Location = new System.Drawing.Point(149, 3);
            this.chkLogInfoMsgs.Name = "chkLogInfoMsgs";
            this.chkLogInfoMsgs.Size = new System.Drawing.Size(78, 17);
            this.chkLogInfoMsgs.TabIndex = 1;
            this.chkLogInfoMsgs.Text = "Information";
            this.chkLogInfoMsgs.UseVisualStyleBackColor = true;
            // 
            // chkLogWarningMsgs
            // 
            this.chkLogWarningMsgs.AutoSize = true;
            this.chkLogWarningMsgs.Location = new System.Drawing.Point(295, 3);
            this.chkLogWarningMsgs.Name = "chkLogWarningMsgs";
            this.chkLogWarningMsgs.Size = new System.Drawing.Size(66, 17);
            this.chkLogWarningMsgs.TabIndex = 2;
            this.chkLogWarningMsgs.Text = "Warning";
            this.chkLogWarningMsgs.UseVisualStyleBackColor = true;
            // 
            // chkLogErrorMsgs
            // 
            this.chkLogErrorMsgs.AutoSize = true;
            this.chkLogErrorMsgs.Location = new System.Drawing.Point(441, 3);
            this.chkLogErrorMsgs.Name = "chkLogErrorMsgs";
            this.chkLogErrorMsgs.Size = new System.Drawing.Size(48, 17);
            this.chkLogErrorMsgs.TabIndex = 3;
            this.chkLogErrorMsgs.Text = "Error";
            this.chkLogErrorMsgs.UseVisualStyleBackColor = true;
            // 
            // chkLogToCurrentDir
            // 
            this.chkLogToCurrentDir.AutoSize = true;
            this.chkLogToCurrentDir.Location = new System.Drawing.Point(9, 18);
            this.chkLogToCurrentDir.Name = "chkLogToCurrentDir";
            this.chkLogToCurrentDir.Size = new System.Drawing.Size(153, 17);
            this.chkLogToCurrentDir.TabIndex = 0;
            this.chkLogToCurrentDir.Text = "Log to application directory";
            this.chkLogToCurrentDir.UseVisualStyleBackColor = true;
            this.chkLogToCurrentDir.CheckedChanged += new System.EventHandler(this.chkLogToCurrentDir_CheckedChanged);
            // 
            // buttonRestoreDefaultLogPath
            // 
            this.buttonRestoreDefaultLogPath.Location = new System.Drawing.Point(495, 83);
            this.buttonRestoreDefaultLogPath.Name = "buttonRestoreDefaultLogPath";
            this.buttonRestoreDefaultLogPath.Size = new System.Drawing.Size(99, 23);
            this.buttonRestoreDefaultLogPath.TabIndex = 5;
            this.buttonRestoreDefaultLogPath.Text = "Use Default";
            this.buttonRestoreDefaultLogPath.UseVisualStyleBackColor = true;
            this.buttonRestoreDefaultLogPath.Click += new System.EventHandler(this.buttonRestoreDefaultLogPath_Click);
            // 
            // buttonOpenLogFile
            // 
            this.buttonOpenLogFile.Location = new System.Drawing.Point(273, 82);
            this.buttonOpenLogFile.Name = "buttonOpenLogFile";
            this.buttonOpenLogFile.Size = new System.Drawing.Size(105, 23);
            this.buttonOpenLogFile.TabIndex = 3;
            this.buttonOpenLogFile.Text = "Open File";
            this.buttonOpenLogFile.UseVisualStyleBackColor = true;
            this.buttonOpenLogFile.Click += new System.EventHandler(this.buttonOpenLogFile_Click);
            // 
            // buttonSelectLogPath
            // 
            this.buttonSelectLogPath.Location = new System.Drawing.Point(384, 82);
            this.buttonSelectLogPath.Name = "buttonSelectLogPath";
            this.buttonSelectLogPath.Size = new System.Drawing.Size(105, 23);
            this.buttonSelectLogPath.TabIndex = 4;
            this.buttonSelectLogPath.Text = "Choose Path";
            this.buttonSelectLogPath.UseVisualStyleBackColor = true;
            this.buttonSelectLogPath.Click += new System.EventHandler(this.buttonSelectLogPath_Click);
            // 
            // labelLogTheseMsgTypes
            // 
            this.labelLogTheseMsgTypes.AutoSize = true;
            this.labelLogTheseMsgTypes.Location = new System.Drawing.Point(6, 108);
            this.labelLogTheseMsgTypes.Name = "labelLogTheseMsgTypes";
            this.labelLogTheseMsgTypes.Size = new System.Drawing.Size(130, 13);
            this.labelLogTheseMsgTypes.TabIndex = 6;
            this.labelLogTheseMsgTypes.Text = "Log these message types:";
            // 
            // labelLogFilePath
            // 
            this.labelLogFilePath.AutoSize = true;
            this.labelLogFilePath.Location = new System.Drawing.Point(6, 38);
            this.labelLogFilePath.Name = "labelLogFilePath";
            this.labelLogFilePath.Size = new System.Drawing.Size(68, 13);
            this.labelLogFilePath.TabIndex = 1;
            this.labelLogFilePath.Text = "Log file path:";
            // 
            // textBoxLogPath
            // 
            this.textBoxLogPath.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.textBoxLogPath.Location = new System.Drawing.Point(9, 57);
            this.textBoxLogPath.Name = "textBoxLogPath";
            this.textBoxLogPath.ReadOnly = true;
            this.textBoxLogPath.Size = new System.Drawing.Size(585, 20);
            this.textBoxLogPath.TabIndex = 2;
            // 
            // groupBoxPopups
            // 
            this.groupBoxPopups.Controls.Add(this.tblPopups);
            this.groupBoxPopups.Controls.Add(this.labelPopupShowTypes);
            this.groupBoxPopups.Location = new System.Drawing.Point(6, 313);
            this.groupBoxPopups.Name = "groupBoxPopups";
            this.groupBoxPopups.Size = new System.Drawing.Size(601, 74);
            this.groupBoxPopups.TabIndex = 2;
            this.groupBoxPopups.TabStop = false;
            this.groupBoxPopups.Text = "Pop-ups";
            // 
            // tblPopups
            // 
            this.tblPopups.ColumnCount = 4;
            this.tblPopups.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tblPopups.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tblPopups.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tblPopups.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tblPopups.Controls.Add(this.chkPopupDebug, 0, 0);
            this.tblPopups.Controls.Add(this.chkPopupError, 3, 0);
            this.tblPopups.Controls.Add(this.chkPopupInfo, 1, 0);
            this.tblPopups.Controls.Add(this.chkPopupWarning, 2, 0);
            this.tblPopups.Location = new System.Drawing.Point(11, 40);
            this.tblPopups.Name = "tblPopups";
            this.tblPopups.RowCount = 1;
            this.tblPopups.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tblPopups.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tblPopups.Size = new System.Drawing.Size(585, 25);
            this.tblPopups.TabIndex = 1;
            // 
            // chkPopupDebug
            // 
            this.chkPopupDebug.AutoSize = true;
            this.chkPopupDebug.Location = new System.Drawing.Point(3, 3);
            this.chkPopupDebug.Name = "chkPopupDebug";
            this.chkPopupDebug.Size = new System.Drawing.Size(58, 17);
            this.chkPopupDebug.TabIndex = 0;
            this.chkPopupDebug.Text = "Debug";
            this.chkPopupDebug.UseVisualStyleBackColor = true;
            // 
            // chkPopupError
            // 
            this.chkPopupError.AutoSize = true;
            this.chkPopupError.Location = new System.Drawing.Point(441, 3);
            this.chkPopupError.Name = "chkPopupError";
            this.chkPopupError.Size = new System.Drawing.Size(48, 17);
            this.chkPopupError.TabIndex = 3;
            this.chkPopupError.Text = "Error";
            this.chkPopupError.UseVisualStyleBackColor = true;
            // 
            // chkPopupInfo
            // 
            this.chkPopupInfo.AutoSize = true;
            this.chkPopupInfo.Location = new System.Drawing.Point(149, 3);
            this.chkPopupInfo.Name = "chkPopupInfo";
            this.chkPopupInfo.Size = new System.Drawing.Size(78, 17);
            this.chkPopupInfo.TabIndex = 1;
            this.chkPopupInfo.Text = "Information";
            this.chkPopupInfo.UseVisualStyleBackColor = true;
            // 
            // chkPopupWarning
            // 
            this.chkPopupWarning.AutoSize = true;
            this.chkPopupWarning.Location = new System.Drawing.Point(295, 3);
            this.chkPopupWarning.Name = "chkPopupWarning";
            this.chkPopupWarning.Size = new System.Drawing.Size(66, 17);
            this.chkPopupWarning.TabIndex = 2;
            this.chkPopupWarning.Text = "Warning";
            this.chkPopupWarning.UseVisualStyleBackColor = true;
            // 
            // labelPopupShowTypes
            // 
            this.labelPopupShowTypes.AutoSize = true;
            this.labelPopupShowTypes.Location = new System.Drawing.Point(8, 24);
            this.labelPopupShowTypes.Name = "labelPopupShowTypes";
            this.labelPopupShowTypes.Size = new System.Drawing.Size(139, 13);
            this.labelPopupShowTypes.TabIndex = 0;
            this.labelPopupShowTypes.Text = "Show these message types:";
            // 
            // NotificationsPage
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.groupBoxPopups);
            this.Controls.Add(this.groupBoxLogging);
            this.Controls.Add(this.groupBoxNotifications);
            this.Name = "NotificationsPage";
            this.Size = new System.Drawing.Size(610, 489);
            this.groupBoxNotifications.ResumeLayout(false);
            this.groupBoxNotifications.PerformLayout();
            this.groupBoxLogging.ResumeLayout(false);
            this.groupBoxLogging.PerformLayout();
            this.tblLogging.ResumeLayout(false);
            this.tblLogging.PerformLayout();
            this.groupBoxPopups.ResumeLayout(false);
            this.groupBoxPopups.PerformLayout();
            this.tblPopups.ResumeLayout(false);
            this.tblPopups.PerformLayout();
            this.ResumeLayout(false);

		}

        internal System.Windows.Forms.Label labelSwitchToErrorsAndInfos;
        internal System.Windows.Forms.CheckBox chkSwitchToMCInformation;
        internal System.Windows.Forms.CheckBox chkSwitchToMCErrors;
        internal System.Windows.Forms.CheckBox chkSwitchToMCWarnings;
        private System.Windows.Forms.GroupBox groupBoxNotifications;
        private System.Windows.Forms.Label labelNotificationsShowTypes;
        private System.Windows.Forms.CheckBox chkShowErrorInMC;
        private System.Windows.Forms.CheckBox chkShowWarningInMC;
        private System.Windows.Forms.CheckBox chkShowInfoInMC;
        private System.Windows.Forms.CheckBox chkShowDebugInMC;
        private System.Windows.Forms.GroupBox groupBoxLogging;
        private System.Windows.Forms.SaveFileDialog saveFileDialogLogging;
        private System.Windows.Forms.Label labelLogFilePath;
        private System.Windows.Forms.TextBox textBoxLogPath;
        private System.Windows.Forms.Button buttonSelectLogPath;
        private System.Windows.Forms.Label labelLogTheseMsgTypes;
        private System.Windows.Forms.CheckBox chkLogErrorMsgs;
        private System.Windows.Forms.CheckBox chkLogWarningMsgs;
        private System.Windows.Forms.CheckBox chkLogInfoMsgs;
        private System.Windows.Forms.CheckBox chkLogDebugMsgs;
        private System.Windows.Forms.Button buttonOpenLogFile;
        private System.Windows.Forms.Button buttonRestoreDefaultLogPath;
        private System.Windows.Forms.GroupBox groupBoxPopups;
        private System.Windows.Forms.CheckBox chkPopupError;
        private System.Windows.Forms.Label labelPopupShowTypes;
        private System.Windows.Forms.CheckBox chkPopupWarning;
        private System.Windows.Forms.CheckBox chkPopupDebug;
        private System.Windows.Forms.CheckBox chkPopupInfo;
        private System.Windows.Forms.CheckBox chkLogToCurrentDir;
        private System.Windows.Forms.TableLayoutPanel tblLogging;
        private System.Windows.Forms.TableLayoutPanel tblPopups;
    }
}
