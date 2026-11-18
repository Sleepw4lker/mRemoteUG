using System.Diagnostics.CodeAnalysis;

using mRemoteUG.UI.Forms;

using NUnit.Framework;

namespace mRemoteUG.Tests.UI.Forms;

[TestFixture]
[Apartment(ApartmentState.STA)]
[SuppressMessage("Design", "CA1001",
    Justification = "[TearDown] disposes _passwordForm after every test. "
        + "NUnit would dispose an IDisposable fixture once, after its last test.")]
public class PasswordFormTests
{
    // Rebuilt by [SetUp] before each test, so null! rather than an initializer here;
    // see ADR-0022 on when that is the right answer.
    private PasswordForm _passwordForm = null!;

    [SetUp]
    public void Setup()
    {
        _passwordForm = new PasswordForm();
        _passwordForm.Show();
    }

    [TearDown]
    public void Teardown()
    {
        _passwordForm.Dispose();
    }

    [Test]
    public void PasswordFormText()
    {
        Assert.That(_passwordForm.Text, Does.Match("Password"));
    }

    [Test]
    public void ClickingCancelClosesPasswordForm()
    {
        bool eventFired = false;
        _passwordForm.FormClosed += (o, e) => eventFired = true;

        Button cancelButton = FindButton("btnCancel");
        cancelButton.PerformClick();

        Assert.That(eventFired, Is.True);
    }

    [Test]
    public void ANewPasswordOfSpacesOnlyIsRefused()
    {
        Assert.That(PasswordForm.NewPasswordProblem("   ", "   "), Is.EqualTo(Language.strPasswordStatusWhitespace));
    }

    [Test]
    public void ANewPasswordThatIsTooShortIsRefused()
    {
        Assert.That(PasswordForm.NewPasswordProblem("ab", "ab"), Is.EqualTo(Language.strPasswordStatusTooShort));
    }

    [Test]
    public void ANewPasswordWhoseTwoEntriesDifferIsRefused()
    {
        Assert.That(
            PasswordForm.NewPasswordProblem("secret", "secreT"),
            Is.EqualTo(Language.strPasswordStatusMustMatch));
    }

    [Test]
    public void AValidNewPasswordIsAccepted()
    {
        Assert.That(PasswordForm.NewPasswordProblem(" s e c r e t ", " s e c r e t "), Is.Null);
    }

    [Test]
    public void OkWithARefusedNewPasswordKeepsTheDialogOpenAndSaysWhy()
    {
        FindControl<System.Windows.Forms.TextBox>("txtPassword").Text = "ab";
        FindControl<System.Windows.Forms.TextBox>("txtVerify").Text = "ab";

        FindButton("btnOK").PerformClick();

        Assert.That(_passwordForm.DialogResult, Is.Not.EqualTo(DialogResult.OK));
        Assert.That(FindControl<Label>("lblStatus").Text, Is.EqualTo(Language.strPasswordStatusTooShort));
    }

    [Test]
    public void OkWithAValidNewPasswordAccepts()
    {
        FindControl<System.Windows.Forms.TextBox>("txtPassword").Text = "secret";
        FindControl<System.Windows.Forms.TextBox>("txtVerify").Text = "secret";

        FindButton("btnOK").PerformClick();

        Assert.That(_passwordForm.DialogResult, Is.EqualTo(DialogResult.OK));
    }

    private Button FindButton(string name)
    {
        return FindControl<Button>(name);
    }

    private T FindControl<T>(string name)
        where T : Control
    {
        Control[] matches = _passwordForm.Controls.Find(name, true);
        Assert.That(matches, Is.Not.Empty, $"No control named '{name}' on {nameof(PasswordForm)}.");
        Assert.That(matches[0], Is.InstanceOf<T>());
        return (T)matches[0];
    }
}
