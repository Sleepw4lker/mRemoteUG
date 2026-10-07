using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Forms;
using mRemoteUG.Config.Connections;
using mRemoteUG.Connection;
using mRemoteUG.Container;

namespace mRemoteUG.UI.Forms
{
    public partial class ExportForm
    {
        #region Public Properties
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string FileName
		{
			get
			{
				return txtFileName.Text;
			}
		}
			
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public SaveFormat SaveFormat
		{
			get
			{
			    var exportFormat = cboFileFormat.SelectedItem as ExportFormat;
			    return exportFormat?.Format ?? SaveFormat.mRXML;
			}
		}
			
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ExportScope Scope
		{
			get
			{
			    if (rdoExportSelectedFolder.Checked)
					return ExportScope.SelectedFolder;
			    if (rdoExportSelectedConnection.Checked)
			        return ExportScope.SelectedConnection;
			    return ExportScope.Everything;
			}
		}
			
		private ContainerInfo _selectedFolder;
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ContainerInfo SelectedFolder
		{
			get
			{
				return _selectedFolder;
			}
			set
			{
				_selectedFolder = value;
				lblSelectedFolder.Text = value?.Name;
				rdoExportSelectedFolder.Enabled = value != null;
			}
		}
			
		private ConnectionInfo _selectedConnection;
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ConnectionInfo SelectedConnection
		{
			get
			{
				return _selectedConnection;
			}
			set
			{
				_selectedConnection = value;
				lblSelectedConnection.Text = value?.Name;
				rdoExportSelectedConnection.Enabled = value != null;
			}
		}
			
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool IncludeUsername
		{
			get
			{
				return chkUsername.Checked;
			}
		}
			
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool IncludePassword
		{
			get
			{
				return chkPassword.Checked;
			}
		}
			
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool IncludeDomain
		{
			get
			{
				return chkDomain.Checked;
			}
		}

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool IncludeInheritance
		{
			get
			{
				return chkInheritance.Checked;
			}
		}
        #endregion
			
        #region Constructors
		public ExportForm()
		{
			InitializeComponent();
			SelectedFolder = null;
			SelectedConnection = null;
			btnOK.Enabled = false;
		}
        #endregion
			
        #region Private Methods
        #region Event Handlers
        private void ExportForm_Load(object sender, EventArgs e)
		{
			cboFileFormat.Items.Clear();
            cboFileFormat.Items.Add(new ExportFormat(SaveFormat.mRXML));
            cboFileFormat.Items.Add(new ExportFormat(SaveFormat.mRCSV));
			cboFileFormat.SelectedIndex = 0;
            ApplyLanguage();
		}

        private void txtFileName_TextChanged(object sender, EventArgs e)
		{
			btnOK.Enabled = !string.IsNullOrEmpty(txtFileName.Text);
		}

        private void btnBrowse_Click(object sender, EventArgs e)
		{
			using (var saveFileDialog = new SaveFileDialog())
			{
				saveFileDialog.CheckPathExists = true;
				saveFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
				saveFileDialog.OverwritePrompt = true;
				
				var fileTypes = new List<string>();
				fileTypes.AddRange(new[] {Language.strFiltermRemoteXML, "*.xml"});
				fileTypes.AddRange(new[] {Language.strFiltermRemoteCSV, "*.csv"});
				fileTypes.AddRange(new[] {Language.strFilterAll, "*.*"});
				
				saveFileDialog.Filter = string.Join("|", fileTypes.ToArray());
			    SelectFileTypeBasedOnSaveFormat(saveFileDialog);

                if (saveFileDialog.ShowDialog(this) != DialogResult.OK)
					return;
				
				txtFileName.Text = saveFileDialog.FileName;
            }
		}

        private void SelectFileTypeBasedOnSaveFormat(FileDialog saveFileDialog)
        {
            saveFileDialog.FilterIndex = SaveFormat == SaveFormat.mRCSV ? 2 : 1;
        }

        private void btnOK_Click(object sender, EventArgs e)
		{
			DialogResult = DialogResult.OK;
		}

        private void btnCancel_Click(object sender, EventArgs e)
		{
			DialogResult = DialogResult.Cancel;
		}

        #endregion
			

        private void ApplyLanguage()
		{
			Text = Language.strExport;
				
			grpFile.Text = Language.strExportFile;
			lblFileName.Text = Language.strLabelFilename;
			btnBrowse.Text = Language.strButtonBrowse;
			lblFileFormat.Text = Language.strFileFormatLabel;
				
			grpItems.Text = Language.strExportItems;
			rdoExportEverything.Text = Language.strExportEverything;
			rdoExportSelectedFolder.Text = Language.strExportSelectedFolder;
			rdoExportSelectedConnection.Text = Language.strExportSelectedConnection;
				
			grpProperties.Text = Language.strExportProperties;
			chkUsername.Text = Language.strCheckboxUsername;
			chkPassword.Text = Language.strCheckboxPassword;
			chkDomain.Text = Language.strCheckboxDomain;
			chkInheritance.Text = Language.strCheckboxInheritance;
			lblUncheckProperties.Text = Language.strUncheckProperties;
				
			btnOK.Text = Language.strButtonOK;
			btnCancel.Text = Language.strButtonCancel;
		}
        #endregion
			
        #region Public Enumerations
		public enum ExportScope
		{
			Everything,
			SelectedFolder,
			SelectedConnection
		}
        #endregion
			
        #region Private Classes
		[ImmutableObject(true)]
        private class ExportFormat
		{
            #region Public Properties

		    public SaveFormat Format { get; }

		    #endregion
				
            #region Constructors
			public ExportFormat(SaveFormat format)
			{
				Format = format;
			}
            #endregion
				
            #region Public Methods
			public override string ToString()
			{
				switch (Format)
				{
					case SaveFormat.mRXML:
						return Language.strMremoteUgXml;
                    case SaveFormat.mRCSV:
						return Language.strMremoteUgCsv;
					default:
						return Format.ToString();
				}
			}
            #endregion
		}
        #endregion
	}
}
