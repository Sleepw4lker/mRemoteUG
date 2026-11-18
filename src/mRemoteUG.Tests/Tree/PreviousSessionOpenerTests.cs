using mRemoteUG.Connection;
using mRemoteUG.Tree;
using mRemoteUG.Tree.Root;
using mRemoteUG.UI.Controls;

using NSubstitute;

using NUnit.Framework;


namespace mRemoteUG.Tests.Tree;

public class PreviousSessionOpenerTests
{
    // Rebuilt per test in Setup: a substitute records every call it receives.
    private PreviousSessionOpener _previousSessionOpener = null!;
    private IConnectionInitiator _connectionInitiator = null!;
    private IConnectionTree _connectionTree = null!;

    [SetUp]
    public void Setup()
    {
        _connectionInitiator = Substitute.For<IConnectionInitiator>();
        _previousSessionOpener = new PreviousSessionOpener(_connectionInitiator);
        _connectionTree = Substitute.For<IConnectionTree>();
        _connectionTree.GetRootConnectionNode().Returns(BuildTree());
    }

    [Test]
    public void AllRequestedSessionsAreReopened()
    {
        _previousSessionOpener.Execute(_connectionTree);
        _connectionInitiator.ReceivedWithAnyArgs(2).OpenConnection(new ConnectionInfo());
    }

    [Test]
    public void ExceptionThrownWhenConstructorGivenNullArg()
    {
        // ReSharper disable once ObjectCreationAsStatement
        Assert.Throws<ArgumentNullException>(() => new PreviousSessionOpener(null!));
    }

    private static RootNodeInfo BuildTree()
    {
        var root = new RootNodeInfo(RootNodeType.Connection);
        root.AddChild(new ConnectionInfo { PleaseConnect = true });
        root.AddChild(new ConnectionInfo());
        root.AddChild(new ConnectionInfo { PleaseConnect = true });
        return root;
    }
}
