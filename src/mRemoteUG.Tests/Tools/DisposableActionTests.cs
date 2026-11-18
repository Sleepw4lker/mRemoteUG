using mRemoteUG.Tools;

using NUnit.Framework;

namespace mRemoteUG.Tests.Tools;

public class DisposableActionTests
{
    [Test]
    public void InitializerActionRunsWhenObjectIsCreated()
    {
        bool initializerRan = false;
        _ = new DisposableAction(() => initializerRan = true, () => { });

        Assert.That(initializerRan);
    }

    [Test]
    public void DisposalActionRunsWhenDisposeIsCalled()
    {
        bool disposeActionRan = false;
        var action = new DisposableAction(() => { }, () => disposeActionRan = true);

        Assert.That(disposeActionRan, Is.False);
        action.Dispose();
        Assert.That(disposeActionRan, Is.True);
    }

    [Test]
    public void DisposeActionOnlyExecutedOnceWhenCallingDisposeMultipleTimes()
    {
        int invokeCount = 0;
        var action = new DisposableAction(() => { }, () => invokeCount++);

        action.Dispose();
        action.Dispose();
        action.Dispose();
        action.Dispose();
        action.Dispose();
        Assert.That(invokeCount, Is.EqualTo(1));
    }
}
