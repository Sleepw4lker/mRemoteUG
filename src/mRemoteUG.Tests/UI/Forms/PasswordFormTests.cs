using System.Threading;
using System.Windows.Forms;
using mRemoteUG.UI.Forms;
using NUnit.Framework;

namespace mRemoteUG.Tests.UI.Forms
{
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class PasswordFormTests
    {
        private PasswordForm _passwordForm;

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
            _passwordForm = null;
        }

        [Test]
        public void PasswordFormText()
        {
            Assert.That(_passwordForm.Text, Does.Match("Password"));
        }

        [Test]
        public void ClickingCancelClosesPasswordForm()
        {
            var eventFired = false;
            _passwordForm.FormClosed += (o, e) => eventFired = true;

            var cancelButton = FindButton("btnCancel");
            cancelButton.PerformClick();

            Assert.That(eventFired, Is.True);
        }

        private Button FindButton(string name)
        {
            var matches = _passwordForm.Controls.Find(name, true);
            Assert.That(matches, Is.Not.Empty, $"No control named '{name}' on {nameof(PasswordForm)}.");
            Assert.That(matches[0], Is.InstanceOf<Button>());
            return (Button)matches[0];
        }
    }
}
