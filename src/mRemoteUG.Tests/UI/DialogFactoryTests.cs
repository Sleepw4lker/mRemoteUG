using mRemoteUG.UI;

using NUnit.Framework;

namespace mRemoteUG.Tests.UI;

/// <summary>
/// The rule both startup dialogs ("load failed" and "file not found") use to decide whether to
/// ask again: they may only be left with a connection model loaded, or by choosing Exit.
/// Otherwise the application runs without a connection file, and the next import or export
/// ends it (#177).
/// </summary>
[TestFixture]
public class DialogFactoryTests
{
    [Test]
    public void ExitChosenAnswersTheDialog()
    {
        Assert.That(DialogFactory.StartupDialogIsAnswered(chosenExit: true, modelLoaded: false), Is.True);
    }

    [Test]
    public void ALoadedModelAnswersTheDialog()
    {
        Assert.That(DialogFactory.StartupDialogIsAnswered(chosenExit: false, modelLoaded: true), Is.True);
    }

    [Test]
    public void NeitherExitNorAModelAsksAgain()
    {
        Assert.That(DialogFactory.StartupDialogIsAnswered(chosenExit: false, modelLoaded: false), Is.False);
    }
}
