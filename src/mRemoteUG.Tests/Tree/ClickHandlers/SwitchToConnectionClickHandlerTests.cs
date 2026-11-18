using mRemoteUG.Connection;
using mRemoteUG.Container;
using mRemoteUG.Tree;

using NSubstitute;

using NUnit.Framework;


namespace mRemoteUG.Tests.Tree;

public class SwitchToConnectionClickHandlerTests
{
    // Rebuilt per test in Setup: a substitute records every call it receives.
    private SwitchToConnectionClickHandler _clickHandler = null!;
    private IConnectionInitiator _connectionInitiator = null!;

    [SetUp]
    public void Setup()
    {
        _connectionInitiator = Substitute.For<IConnectionInitiator>();
        _clickHandler = new SwitchToConnectionClickHandler(_connectionInitiator);
    }

    [Test]
    public void SwitchesToConnection()
    {
        var connectionInfo = new ConnectionInfo();
        _clickHandler.Execute(connectionInfo);
        _connectionInitiator.Received().SwitchToOpenConnection(connectionInfo);
    }

    [Test]
    public void DoesNothingWhenGivenContainerInfo()
    {
        _clickHandler.Execute(new ContainerInfo());
        _connectionInitiator.DidNotReceiveWithAnyArgs().SwitchToOpenConnection(new ConnectionInfo());
    }

    [Test]
    public void ExceptionThrownWhenConstructorGivenNullArg()
    {
        // ReSharper disable once ObjectCreationAsStatement
        Assert.Throws<ArgumentNullException>(() => new SwitchToConnectionClickHandler(null!));
    }

    [Test]
    public void ThrowWhenExecuteGivenNullArg()
    {
        Assert.Throws<ArgumentNullException>(() => _clickHandler.Execute(null!));
    }
}
