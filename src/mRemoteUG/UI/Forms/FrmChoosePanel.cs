using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

using mRemoteUG.App;
using mRemoteUG.UI.Forms.Input;
using mRemoteUG.UI.Panels;

namespace mRemoteUG.UI.Forms;

public partial class FrmChoosePanel
{

    public FrmChoosePanel()
    {
        InitializeComponent();
    }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Panel
    {
        // A DropDownList whose OK button is enabled only while it has items always has one
        // selected, so the empty fallback is not expected to be seen.
        get => cbPanels.SelectedItem?.ToString() ?? string.Empty;
        set => cbPanels.SelectedItem = value;
    }

    [SuppressMessage("Style", "IDE1006", Justification = "The name is wired in FrmChoosePanel.Designer.cs.")]
    private void frmChoosePanel_Load(object sender, System.EventArgs e)
    {
        ApplyLanguage();
        AddAvailablePanels();
    }

    private void ApplyLanguage()
    {
        btnOK.Text = Language.strButtonOK;
        lblDescription.Text = Language.strLabelSelectPanel;
        btnNew.Text = Language.strButtonNew;
        btnCancel.Text = Language.strButtonCancel;
        Text = Language.strTitleSelectPanel;
    }

    private void AddAvailablePanels()
    {
        cbPanels.Items.Clear();

        // FrmMain builds the window list before any connection can ask for a panel.
        WindowList? windows = Runtime.WindowList;
        for (int i = 0; windows != null && i <= windows.Count - 1; i++)
        {
            if (windows[i] is { } window)
            {
                cbPanels.Items.Add(window.Text.Replace("&&", "&"));
            }
        }

        if (cbPanels.Items.Count > 0)
        {
            cbPanels.SelectedItem = cbPanels.Items[0];
            cbPanels.Enabled = true;
            btnOK.Enabled = true;
        }
        else
        {
            cbPanels.Enabled = false;
            btnOK.Enabled = false;
        }
    }

    [SuppressMessage("Style", "IDE1006", Justification = "The name is wired in FrmChoosePanel.Designer.cs.")]
    private void btnNew_Click(object sender, System.EventArgs e)
    {
        string pnlName = Language.strNewPanel;

        if (InputDialog.InputBox(Language.strNewPanel, Language.strPanelName + ":", ref pnlName) != DialogResult.OK
            || string.IsNullOrEmpty(pnlName))
        {
            return;
        }

        PanelAdder.AddPanel(pnlName);
        AddAvailablePanels();
        cbPanels.SelectedItem = pnlName;
        cbPanels.Focus();
    }

    [SuppressMessage("Style", "IDE1006", Justification = "The name is wired in FrmChoosePanel.Designer.cs.")]
    private void btnOK_Click(object sender, System.EventArgs e)
    {
        DialogResult = DialogResult.OK;
    }
}
