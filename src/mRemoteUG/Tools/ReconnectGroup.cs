using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace mRemoteUG.Tools;

public partial class ReconnectGroup
{
    public ReconnectGroup()
    {
        InitializeComponent();
    }
    private bool _serverReady;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ServerReady
    {
        get
        {
            return _serverReady;
        }
        set
        {
            SetStatusImage(value ? Resources.HostStatus_On : Resources.HostStatus_Off);

            _serverReady = value;
        }
    }

    private delegate void SetStatusImageCB(Image img);
    private void SetStatusImage(Image img)
    {
        if (pbServerStatus.InvokeRequired)
        {
            var d = new SetStatusImageCB(SetStatusImage);
            ParentForm?.Invoke(d, new object[] { img });
        }
        else
        {
            pbServerStatus.Image = img;
        }
    }

    [SuppressMessage("Style", "IDE1006", Justification = "The name is wired in ReconnectGroup.Designer.cs.")]
    private void chkReconnectWhenReady_CheckedChanged(object? sender, EventArgs e)
    {
        _reconnectWhenReady = chkReconnectWhenReady.Checked;
    }

    private bool _reconnectWhenReady;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ReconnectWhenReady
    {
        get
        {
            return _reconnectWhenReady;
        }
        set
        {
            _reconnectWhenReady = value;
            SetCheckbox(value);
        }
    }

    private delegate void SetCheckboxCB(bool val);
    private void SetCheckbox(bool val)
    {
        if (chkReconnectWhenReady.InvokeRequired)
        {
            var d = new SetCheckboxCB(SetCheckbox);
            ParentForm?.Invoke(d, new object[] { val });
        }
        else
        {
            chkReconnectWhenReady.Checked = val;
        }
    }

    public delegate void CloseClickedHandler();
    private CloseClickedHandler? _closeClickedEvent;

    public event CloseClickedHandler CloseClicked
    {
        add
        {
            _closeClickedEvent = (CloseClickedHandler?)Delegate.Combine(_closeClickedEvent, value);
        }
        remove
        {
            _closeClickedEvent = (CloseClickedHandler?)Delegate.Remove(_closeClickedEvent, value);
        }
    }


    [SuppressMessage("Style", "IDE1006", Justification = "The name is wired in ReconnectGroup.Designer.cs.")]
    private void btnClose_Click(object? sender, EventArgs e)
    {
        _closeClickedEvent?.Invoke();
    }

    [SuppressMessage("Style", "IDE1006", Justification = "The name is wired in ReconnectGroup.Designer.cs.")]
    private void tmrAnimation_Tick(object? sender, EventArgs e)
    {
        switch (lblAnimation.Text)
        {
            case "":
                lblAnimation.Text = "»";
                break;
            case "»":
                lblAnimation.Text = "»»";
                break;
            case "»»":
                lblAnimation.Text = "»»»";
                break;
            case "»»»":
                lblAnimation.Text = "";
                break;
        }
    }

    private delegate void DisposeReconnectGroupCB();
    public void DisposeReconnectGroup()
    {
        if (InvokeRequired)
        {
            var d = new DisposeReconnectGroupCB(DisposeReconnectGroup);
            ParentForm?.Invoke(d);
        }
        else
        {
            Dispose();
        }
    }

    public void ReconnectGroupLoad(object? sender, EventArgs e)
    {
        ApplyLanguage();
    }

    private void ApplyLanguage()
    {
        grpAutomaticReconnect.Text = Language.strGroupboxAutomaticReconnect;
        btnClose.Text = Language.strButtonClose;
        lblServerStatus.Text = Language.strLabelServerStatus;
        chkReconnectWhenReady.Text = Language.strCheckboxReconnectWhenReady;
    }
}
