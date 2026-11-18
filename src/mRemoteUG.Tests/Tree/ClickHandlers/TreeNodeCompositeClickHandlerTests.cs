using mRemoteUG.Connection;
using mRemoteUG.Tree;

using NSubstitute;

using NUnit.Framework;


namespace mRemoteUG.Tests.Tree;

public class TreeNodeCompositeClickHandlerTests
{
    // Rebuilt per test in Setup: a test replaces its handlers.
    private TreeNodeCompositeClickHandler _clickHandler = null!;
    private readonly ConnectionInfo _connectionInfo = new();

    [SetUp]
    public void Setup()
    {
        _clickHandler = new TreeNodeCompositeClickHandler();
    }

    [Test]
    public void ExecutesAllItsHandlers()
    {
        ITreeNodeClickHandler<ConnectionInfo> handler1 = Substitute.For<ITreeNodeClickHandler<ConnectionInfo>>();
        ITreeNodeClickHandler<ConnectionInfo> handler2 = Substitute.For<ITreeNodeClickHandler<ConnectionInfo>>();
        _clickHandler.ClickHandlers = new[] { handler1, handler2 };
        _clickHandler.Execute(_connectionInfo);
        handler1.Received().Execute(_connectionInfo);
        handler2.Received().Execute(_connectionInfo);
    }

    [Test]
    public void ThrowWhenExecuteGivenNullArg()
    {
        Assert.Throws<ArgumentNullException>(() => _clickHandler.Execute(null!));
    }
}
