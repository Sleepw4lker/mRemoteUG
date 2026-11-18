using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security;

using mRemoteUG.Security;

namespace mRemoteUG.UI.Forms;

public partial class PasswordForm
{
    private readonly string? _passwordName;
    private SecureString _password = new SecureString();

    /// <summary>
    /// Puts the dialog into the New Password mode. An extra
    /// password box is shown which must match the first password
    /// to continue.
    /// </summary>
    private bool NewPasswordMode { get; }

    /// <summary>
    /// Creates a new password form for entering or setting a password.
    /// </summary>
    /// <param name="passwordName"></param>
    /// <param name="newPasswordMode">
    /// Puts the dialog into the New Password mode. An extra
    /// password box is shown which must match the first password
    /// to continue.
    /// </param>
    public PasswordForm(string? passwordName = null, bool newPasswordMode = true)
    {
        InitializeComponent();
        _passwordName = passwordName;
        NewPasswordMode = newPasswordMode;
    }

    /// <summary>
    /// Dispaly a dialog box requesting that the user 
    /// enter their password.
    /// </summary>
    /// <returns>The password entered, or null if the dialog was cancelled.</returns>
    public SecureString? GetKey()
    {
        DialogResult dialog = ShowDialog();
        return dialog == DialogResult.OK
            ? _password
            : null;
    }

    #region Event Handlers
    [SuppressMessage("Style", "IDE1006", Justification = "The name is wired in PasswordForm.Designer.cs.")]
    private void frmPassword_Load(object sender, EventArgs e)
    {
        ApplyLanguage();

        if (NewPasswordMode)
        {
            return;
        }

        Height = Height - (txtVerify.Top - txtPassword.Top);
        lblVerify.Visible = false;
        txtVerify.Visible = false;
    }

    private void PasswordForm_FormClosed(object sender, FormClosedEventArgs e)
    {
        _password = txtPassword.Text.ConvertToSecureString();
        txtPassword.Text = "";
        txtVerify.Text = "";
        if (NewPasswordMode)
        {
            return;
        }

        Height = Height + (txtVerify.Top - txtPassword.Top);
    }

    [SuppressMessage("Style", "IDE1006", Justification = "The name is wired in PasswordForm.Designer.cs.")]
    private void btnCancel_Click(object sender, EventArgs e)
    {
        DialogResult = DialogResult.Cancel;
        Close();
    }

    [SuppressMessage("Style", "IDE1006", Justification = "The name is wired in PasswordForm.Designer.cs.")]
    private void btnOK_Click(object sender, EventArgs e)
    {
        // The dialog stays open, showing why, until the new password meets the rule.
        if (NewPasswordMode && !VerifyNewPassword())
        {
            return;
        }

        DialogResult = DialogResult.OK;
    }

    [SuppressMessage("Style", "IDE1006", Justification = "The name is wired in PasswordForm.Designer.cs.")]
    private void txtPassword_TextChanged(object sender, EventArgs e)
    {
        HideStatus();
    }
    #endregion

    #region Private Methods
    [SuppressMessage("Performance", "CA1863",
                     Justification = "The format is a localized resource read at run time; "
                                     + "a cached CompositeFormat would freeze the UI language.")]
    private void ApplyLanguage()
    {
        Text = string.IsNullOrEmpty(_passwordName)
            ? Language.strTitlePassword
            : string.Format(CultureInfo.CurrentCulture, Language.strTitlePasswordWithName, _passwordName);

        lblPassword.Text = Language.strLabelPassword;
        lblVerify.Text = Language.strLabelVerify;
        btnCancel.Text = Language.strButtonCancel;
        btnOK.Text = Language.strButtonOK;
    }

    private bool VerifyNewPassword()
    {
        string? problem = NewPasswordProblem(txtPassword.Text, txtVerify.Text);
        if (problem == null)
        {
            return true;
        }

        ShowStatus(problem);
        return false;
    }

    /// <summary>The rule a new password has to meet.</summary>
    /// <returns>The status message saying why it is refused, or null when it is accepted.</returns>
    internal static string? NewPasswordProblem(string password, string verification)
    {
        if (password.Length < 3)
        {
            return Language.strPasswordStatusTooShort;
        }

        // The encryption refuses a password of whitespace only, so the dialog must not hand one out.
        if (string.IsNullOrWhiteSpace(password))
        {
            return Language.strPasswordStatusWhitespace;
        }

        if (password != verification)
        {
            return Language.strPasswordStatusMustMatch;
        }

        return null;
    }

    private void ShowStatus(string status)
    {
        lblStatus.Visible = true;
        lblStatus.Text = status;
    }

    private void HideStatus()
    {
        lblStatus.Visible = false;
    }
    #endregion
}
