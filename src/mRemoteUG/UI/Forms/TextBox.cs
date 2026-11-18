using System.ComponentModel;

// Adapted from http://stackoverflow.com/a/3678888/2101395

namespace mRemoteUG.UI.Forms;

public class TextBox : System.Windows.Forms.TextBox
{
    #region Public Properties
    [Category("Behavior"),
        DefaultValue(false)]
    private bool _selectAllOnFocus;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool SelectAllOnFocus
    {
        get
        {
            return _selectAllOnFocus;
        }
        set
        {
            _selectAllOnFocus = value;
        }
    }
    #endregion

    #region Protected Methods
    protected override void OnEnter(EventArgs e)
    {
        base.OnEnter(e);

        if (MouseButtons == MouseButtons.None)
        {
            SelectAll();
            _focusHandled = true;
        }
    }

    protected override void OnLeave(EventArgs e)
    {
        base.OnLeave(e);

        _focusHandled = false;
    }

    protected override void OnMouseUp(MouseEventArgs mevent)
    {
        base.OnMouseUp(mevent);

        if (!_focusHandled)
        {
            if (SelectionLength == 0)
            {
                SelectAll();
            }
            _focusHandled = true;
        }
    }
    #endregion

    #region Private Fields
    private bool _focusHandled;
    #endregion
}
