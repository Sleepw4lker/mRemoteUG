using System.Linq;
using System.Windows.Forms;
using mRemoteUG.UI.TaskDialog;
using NUnit.Framework;

namespace mRemoteUG.Tests.UI
{
    /// <summary>
    /// Covers what is left of the application's message box now that Windows draws it.
    /// </summary>
    /// <remarks>
    /// The layout, the sizing and the DPI behaviour all belong to comctl32 and are not ours to
    /// test. What is still ours is the translation: turning this application's strings and enums
    /// into a <see cref="TaskDialogPage"/>. That is what these assert.
    /// <para>
    /// <c>BuildPage</c> is separate from showing the dialog precisely so it can be tested - a
    /// shown task dialog is modal and would hang the run. That the dialog can actually be shown
    /// is checked by <c>--selftest</c>, which has a message loop and a real display.
    /// </para>
    /// </remarks>
    [TestFixture]
    public class TaskDialogPageTests
    {
        private static TaskDialogPage Build(string expandedInfo = "",
                                            string footer = "",
                                            string verificationText = "",
                                            string radioButtons = "",
                                            string commandButtons = "",
                                            ETaskDialogButtons buttons = ETaskDialogButtons.YesNo,
                                            int defaultIndex = 0)
        {
            return CTaskDialog.BuildPage("mRemoteUG", "Heading", "Content", expandedInfo, footer,
                                         verificationText, radioButtons, commandButtons, buttons,
                                         ESysIcons.Question, ESysIcons.Information, defaultIndex);
        }

        [Test]
        public void TheTextGoesWhereTheTaskDialogExpectsIt()
        {
            var page = Build();

            Assert.Multiple(() =>
            {
                Assert.That(page.Caption, Is.EqualTo("mRemoteUG"));
                Assert.That(page.Heading, Is.EqualTo("Heading"));
                Assert.That(page.Text, Is.EqualTo("Content"));
            });
        }

        /// <summary>
        /// The optional parts render nothing when their argument was empty.
        /// </summary>
        /// <remarks>
        /// Measured rather than assumed: a fresh <see cref="TaskDialogPage"/> already carries an
        /// expander, a footnote and a verification check box, so "did we leave them null" is the
        /// wrong question. What decides whether they appear is whether they have any text.
        /// </remarks>
        [Test]
        public void OptionalPartsHaveNoTextWhenTheyWereNotAskedFor()
        {
            var page = Build();

            Assert.Multiple(() =>
            {
                Assert.That(page.Expander?.Text, Is.Null.Or.Empty);
                Assert.That(page.Footnote?.Text, Is.Null.Or.Empty);
                Assert.That(page.Verification?.Text, Is.Null.Or.Empty);
                Assert.That(page.RadioButtons, Is.Empty);
            });
        }

        [Test]
        public void OptionalPartsAppearWhenTheyHaveText()
        {
            var page = Build(expandedInfo: "More detail", footer: "A note", verificationText: "Do not ask again");

            Assert.Multiple(() =>
            {
                Assert.That(page.Expander?.Text, Is.EqualTo("More detail"));
                Assert.That(page.Footnote?.Text, Is.EqualTo("A note"));
                Assert.That(page.Footnote?.Icon, Is.EqualTo(TaskDialogIcon.Information));
                Assert.That(page.Verification?.Text, Is.EqualTo("Do not ask again"));
            });
        }

        [TestCase(ETaskDialogButtons.YesNo, 2)]
        [TestCase(ETaskDialogButtons.YesNoCancel, 3)]
        [TestCase(ETaskDialogButtons.OkCancel, 2)]
        [TestCase(ETaskDialogButtons.Ok, 1)]
        [TestCase(ETaskDialogButtons.Close, 1)]
        [TestCase(ETaskDialogButtons.Cancel, 1)]
        [TestCase(ETaskDialogButtons.None, 0)]
        public void EveryButtonCombinationMapsToStandardButtons(ETaskDialogButtons buttons, int expected)
        {
            Assert.That(Build(buttons: buttons).Buttons, Has.Count.EqualTo(expected));
        }

        [Test]
        public void YesNoCancelMapsToTheMatchingStandardButtons()
        {
            var page = Build(buttons: ETaskDialogButtons.YesNoCancel);

            Assert.That(page.Buttons,
                        Is.EqualTo(new[] { TaskDialogButton.Yes, TaskDialogButton.No, TaskDialogButton.Cancel }));
        }

        /// <summary>
        /// Escape has to close the dialogs that offer a way out, and must not close the ones that
        /// insist on an answer.
        /// </summary>
        [TestCase(ETaskDialogButtons.YesNoCancel, true)]
        [TestCase(ETaskDialogButtons.OkCancel, true)]
        [TestCase(ETaskDialogButtons.Cancel, true)]
        [TestCase(ETaskDialogButtons.Close, true)]
        [TestCase(ETaskDialogButtons.YesNo, false)]
        [TestCase(ETaskDialogButtons.Ok, false)]
        public void CancellableDialogsAllowCancel(ETaskDialogButtons buttons, bool expected)
        {
            Assert.That(Build(buttons: buttons).AllowCancel, Is.EqualTo(expected));
        }

        /// <summary>
        /// Callers build their command links with <c>string.Join(" | ", ...)</c>, so the separator
        /// arrives padded with spaces that must not end up in the labels.
        /// </summary>
        [Test]
        public void CommandLinksAreSplitAndTrimmed()
        {
            var page = Build(commandButtons: "Create a new file | Open a different file | Exit",
                             buttons: ETaskDialogButtons.None);

            var links = page.Buttons.OfType<TaskDialogCommandLinkButton>().ToList();

            Assert.Multiple(() =>
            {
                Assert.That(links.Select(l => l.Text),
                            Is.EqualTo(new[] { "Create a new file", "Open a different file", "Exit" }));
                Assert.That(links.Select(l => l.DescriptionText), Is.All.Null);
            });
        }

        /// <summary>
        /// Each link carries its own index, which is how CommandButtonResult is recovered - the
        /// callers switch on it, so an off-by-one here silently picks the wrong action.
        /// </summary>
        [Test]
        public void EachCommandLinkCarriesItsIndex()
        {
            var page = Build(commandButtons: "First | Second | Third", buttons: ETaskDialogButtons.None);

            var links = page.Buttons.OfType<TaskDialogCommandLinkButton>().ToList();

            Assert.That(links.Select(l => l.Tag), Is.EqualTo(new object[] { 0, 1, 2 }));
        }

        [Test]
        public void ACommandLinkSplitsItsDescriptionOffTheFirstLine()
        {
            var page = Build(commandButtons: "Do this\nand here is what that means", buttons: ETaskDialogButtons.None);

            var link = page.Buttons.OfType<TaskDialogCommandLinkButton>().Single();

            Assert.Multiple(() =>
            {
                Assert.That(link.Text, Is.EqualTo("Do this"));
                Assert.That(link.DescriptionText, Is.EqualTo("and here is what that means"));
            });
        }

        [Test]
        public void CommandLinksAndStandardButtonsCanCoexist()
        {
            var page = Build(commandButtons: "Retry now", buttons: ETaskDialogButtons.Cancel);

            Assert.Multiple(() =>
            {
                Assert.That(page.Buttons.OfType<TaskDialogCommandLinkButton>().Count(), Is.EqualTo(1));
                Assert.That(page.Buttons, Does.Contain(TaskDialogButton.Cancel));
            });
        }

        [Test]
        public void TheDefaultIndexSelectsACommandLinkWhenThereAreSome()
        {
            var page = Build(commandButtons: "First | Second | Third",
                             buttons: ETaskDialogButtons.None,
                             defaultIndex: 1);

            Assert.That(page.DefaultButton, Is.SameAs(page.Buttons.OfType<TaskDialogCommandLinkButton>().ElementAt(1)));
        }

        /// <summary>
        /// Compared by value, not by reference: the standard button properties hand out a fresh
        /// instance on every read, so <c>Is.SameAs</c> can never hold here.
        /// </summary>
        [Test]
        public void TheDefaultIndexSelectsAStandardButtonWhenThereAreNoCommandLinks()
        {
            var page = Build(buttons: ETaskDialogButtons.YesNoCancel, defaultIndex: 1);

            Assert.That(page.DefaultButton, Is.EqualTo(TaskDialogButton.No));
        }

        /// <summary>
        /// A command link labelled "Cancel" must not be mistaken for the Cancel button.
        /// </summary>
        /// <remarks>
        /// This is not hypothetical: <c>DialogFactory.ShowLoadConnectionsFailedDialog</c> adds a
        /// command link whose label is <c>Language.strButtonCancel</c>. Because <c>==</c> on
        /// TaskDialogButton compares by value, the mapping has to identify command links by their
        /// tag before it compares anything against the standard buttons.
        /// </remarks>
        [Test]
        public void ACommandLinkLabelledCancelIsStillACommandLink()
        {
            var page = Build(commandButtons: "Create a new file | Cancel", buttons: ETaskDialogButtons.None);

            var cancelLink = page.Buttons.OfType<TaskDialogCommandLinkButton>().Single(b => b.Text == "Cancel");

            Assert.Multiple(() =>
            {
                Assert.That(cancelLink.Tag, Is.EqualTo(1));
                Assert.That(CTaskDialog.ResultFor(cancelLink), Is.EqualTo(DialogResult.OK));
            });
        }

        [Test]
        public void TheStandardButtonsMapToTheirDialogResults()
        {
            Assert.Multiple(() =>
            {
                Assert.That(CTaskDialog.ResultFor(TaskDialogButton.Yes), Is.EqualTo(DialogResult.Yes));
                Assert.That(CTaskDialog.ResultFor(TaskDialogButton.No), Is.EqualTo(DialogResult.No));
                Assert.That(CTaskDialog.ResultFor(TaskDialogButton.OK), Is.EqualTo(DialogResult.OK));
                Assert.That(CTaskDialog.ResultFor(TaskDialogButton.Cancel), Is.EqualTo(DialogResult.Cancel));
                Assert.That(CTaskDialog.ResultFor(TaskDialogButton.Close), Is.EqualTo(DialogResult.Cancel));
            });
        }

        [Test]
        public void RadioButtonsAreSplitAndTheDefaultOneIsChecked()
        {
            var page = Build(radioButtons: "First | Second | Third", defaultIndex: 2);

            Assert.Multiple(() =>
            {
                Assert.That(page.RadioButtons.Select(r => r.Text),
                            Is.EqualTo(new[] { "First", "Second", "Third" }));
                Assert.That(page.RadioButtons.Select(r => r.Checked),
                            Is.EqualTo(new[] { false, false, true }));
            });
        }

        [TestCase(ESysIcons.Information)]
        [TestCase(ESysIcons.Warning)]
        [TestCase(ESysIcons.Error)]
        [TestCase(ESysIcons.Question)]
        public void EveryIconMapsToSomething(ESysIcons icon)
        {
            var page = CTaskDialog.BuildPage("t", "h", "c", "", "", "", "", "",
                                             ETaskDialogButtons.Ok, icon, icon, 0);

            Assert.That(page.Icon, Is.Not.Null);
        }

        /// <summary>
        /// Windows dropped the question icon from the task dialog's standard set, so it is carried
        /// through as a custom icon. If that ever silently became the information "i", every
        /// confirmation in the application would change appearance without anyone noticing.
        /// </summary>
        [Test]
        public void TheQuestionIconIsNotSilentlyAnInformationIcon()
        {
            var question = CTaskDialog.BuildPage("t", "h", "c", "", "", "", "", "",
                                                 ETaskDialogButtons.Ok, ESysIcons.Question, ESysIcons.Question, 0);

            Assert.That(question.Icon, Is.Not.EqualTo(TaskDialogIcon.Information));
        }
    }
}
