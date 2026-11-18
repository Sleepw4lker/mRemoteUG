using mRemoteUG.Connection;
using mRemoteUG.Container;
using mRemoteUG.Tree;
using mRemoteUG.UI.Controls;

using NSubstitute;

using NUnit.Framework;


namespace mRemoteUG.Tests.Tree;

public class ExpandNodeClickHandlerTests
{
    // Rebuilt per test in Setup: a substitute records every call it receives.
    private ExpandNodeClickHandler _clickHandler = null!;
    private IConnectionTree _connectionTree = null!;

    [SetUp]
    public void Setup()
    {
        _connectionTree = Substitute.For<IConnectionTree>();
        _clickHandler = new ExpandNodeClickHandler(_connectionTree);
    }

    [Test]
    public void TargetedNodeIsExpanded()
    {
        var folder = new ContainerInfo();
        _clickHandler.Execute(folder);
        _connectionTree.Received().ToggleExpansion(folder);
    }

    [Test]
    public void NothingHappensWhenConnectionInfoProvided()
    {
        _clickHandler.Execute(new ConnectionInfo());
        _connectionTree.DidNotReceiveWithAnyArgs().ToggleExpansion(new ConnectionInfo());
    }

    [Test]
    public void ExceptionThrownOnConstructorNullArg()
    {
        // ReSharper disable once ObjectCreationAsStatement
        Assert.Throws<ArgumentNullException>(() => new ExpandNodeClickHandler(null!));
    }
}
