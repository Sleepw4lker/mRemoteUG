using mRemoteUG.UI.Panels;
using mRemoteUG.UI.Window;

using NUnit.Framework;

namespace mRemoteUG.Tests.UI.Panels;

/// <summary>
/// Rename on a panel's tab in the documents strip: the dialog opens with the panel's current
/// title, as the user typed it, and only a confirmed, non-empty answer renames the panel.
/// </summary>
/// <remarks>
/// The dialog is modal, so the test stands in for it: <c>RenamePanel</c> takes the question as a
/// delegate that receives the prefill and returns the answer, or <see langword="null"/> for
/// Cancel.
/// </remarks>
[TestFixture]
[Apartment(ApartmentState.STA)]
public class PanelAdderRenameTests
{
    [Test]
    public void TheDialogOpensWithTheTitleAsTypedAndConfirmingItUnchangedKeepsIt()
    {
        using var panel = new ConnectionWindow();
        panel.SetFormText("R&&D");
        string? prefill = null;

        PanelAdder.RenamePanel(panel, current => prefill = current);

        Assert.Multiple(() =>
        {
            Assert.That(prefill, Is.EqualTo("R&D"), "The rename dialog did not open with the panel's title.");
            Assert.That(panel.TabText, Is.EqualTo("R&&D"), "Confirming the title unchanged changed it.");
            Assert.That(panel.Text, Is.EqualTo("R&&D"));
        });
    }

    [Test]
    public void ANewTitleIsEscapedOnTheWayIn()
    {
        using var panel = new ConnectionWindow();
        panel.SetFormText("Panel");

        PanelAdder.RenamePanel(panel, _ => "Ops & Test");

        Assert.That(panel.TabText, Is.EqualTo("Ops && Test"));
    }

    [TestCase(null, TestName = "Cancel keeps the old title")]
    [TestCase("", TestName = "An empty answer keeps the old title")]
    public void NoAnswerKeepsTheOldTitle(string? answer)
    {
        using var panel = new ConnectionWindow();
        panel.SetFormText("R&&D");

        PanelAdder.RenamePanel(panel, _ => answer);

        Assert.That(panel.TabText, Is.EqualTo("R&&D"));
    }
}
