using mRemoteUG.Connection;
using mRemoteUG.Container;
using mRemoteUG.Tree;
using mRemoteUG.Tree.Root;

using NUnit.Framework;


namespace mRemoteUG.Tests.Tree;

public class NodeSearcherTests
{
    private readonly NodeSearcher _nodeSearcher;
    private readonly ContainerInfo _folder1 =
        new() { Name = "folder1", Description = "description1", Hostname = "hostname1" };
    private readonly ContainerInfo _folder2 =
        new() { Name = "folder2", Description = "description4", Hostname = "hostname4" };
    private readonly ConnectionInfo _con1 =
        new() { Name = "con1", Description = "description2", Hostname = "hostname2" };
    private readonly ConnectionInfo _con2 =
        new() { Name = "con2", Description = "description3", Hostname = "hostname3" };
    private readonly ConnectionInfo _con3 =
        new() { Name = "con3", Description = "description5", Hostname = "hostname5" };
    private readonly ConnectionInfo _con4 =
        new() { Name = "con4", Description = "description6", Hostname = "hostname6" };
    private readonly ConnectionInfo _con5 =
        new() { Name = "con5", Description = "description7", Hostname = "hostname7" };

    public NodeSearcherTests()
    {
        ConnectionTreeModel connectionTreeModel = SetupConnectionTreeModel();
        _nodeSearcher = new NodeSearcher(connectionTreeModel);
    }

    [Test]
    public void SearchByNameReturnsAllExpectedMatches()
    {
        IEnumerable<ConnectionInfo> matches = _nodeSearcher.SearchByName("folder");
        Assert.That(matches.ToList(), Is.EquivalentTo(new[] { _folder1, _folder2 }));
    }

    [Test]
    public void SearchByDescriptionReturnsAllExpectedMatches()
    {
        IEnumerable<ConnectionInfo> matches = _nodeSearcher.SearchByName("description");
        Assert.That(matches.ToList(), Is.EquivalentTo(new[] { _folder1, _folder2, _con1, _con2, _con3, _con4, _con5 }));
    }

    [Test]
    public void SearchByDescription1ReturnsAllExpectedMatches()
    {
        IEnumerable<ConnectionInfo> matches = _nodeSearcher.SearchByName("description1");
        Assert.That(matches.ToList(), Is.EquivalentTo(new[] { _folder1 }));
    }

    [Test]
    public void SearchByHostname1ReturnsAllExpectedMatches()
    {
        IEnumerable<ConnectionInfo> matches = _nodeSearcher.SearchByName("hostname1");
        Assert.That(matches.ToList(), Is.EquivalentTo(new[] { _folder1 }));
    }

    [Test]
    public void NextMatchAdvancesTheIterator()
    {
        _nodeSearcher.SearchByName("folder");
        ConnectionInfo? match1 = _nodeSearcher.CurrentMatch;
        ConnectionInfo? match2 = _nodeSearcher.NextMatch();
        Assert.That(match1, Is.Not.EqualTo(match2));
    }

    [Test]
    public void PreviousMatchRollsBackTheIterator()
    {
        _nodeSearcher.SearchByName("con");
        ConnectionInfo? match1 = _nodeSearcher.CurrentMatch;
        _nodeSearcher.NextMatch();
        ConnectionInfo? match2 = _nodeSearcher.PreviousMatch();
        Assert.That(match1, Is.EqualTo(match2));
    }

    [Test]
    public void SearchingWithEmptyStringReturnsNoMatches()
    {
        IEnumerable<ConnectionInfo> matches = _nodeSearcher.SearchByName("");
        Assert.That(matches.Count(), Is.EqualTo(0));
    }

    [Test]
    public void SearchingWithNoMatchReturnsNothingAndHasNoCurrentMatch()
    {
        IEnumerable<ConnectionInfo> matches = _nodeSearcher.SearchByName("nothing is called this");
        Assert.That(matches, Is.Empty);
        Assert.That(_nodeSearcher.CurrentMatch, Is.Null);
    }

    [Test]
    public void NextMatchAtTheLastMatchStaysThere()
    {
        _nodeSearcher.SearchByName("folder");
        ConnectionInfo? last = _nodeSearcher.NextMatch();
        Assert.That(_nodeSearcher.NextMatch(), Is.SameAs(last));
        Assert.That(_nodeSearcher.CurrentMatch, Is.SameAs(last));
    }

    [Test]
    public void PreviousMatchAtTheFirstMatchStaysThere()
    {
        _nodeSearcher.SearchByName("folder");
        ConnectionInfo? first = _nodeSearcher.CurrentMatch;
        Assert.That(_nodeSearcher.PreviousMatch(), Is.SameAs(first));
        Assert.That(_nodeSearcher.CurrentMatch, Is.SameAs(first));
    }

    [Test]
    public void NextAndPreviousMatchBeforeAnySearchAnswerNoMatch()
    {
        var searcher = new NodeSearcher(new ConnectionTreeModel());
        Assert.That(searcher.NextMatch(), Is.Null);
        Assert.That(searcher.PreviousMatch(), Is.Null);
    }

    private ConnectionTreeModel SetupConnectionTreeModel()
    {
        /*
         * Tree:
         * Root
         * |--- folder1 (description1, hostname1)
         * |    |--- con1 (description2, hostname2)
         * |    L--- con2 (description3, hostname3)
         * |--- folder2 (description4, hostname4)
         * |    |--- con3 (description5, hostname5)
         * |    L--- con4 (description6, hostname6)
         * L--- con5 (description7, hostname7)
         * 
         */
        var connectionTreeModel = new ConnectionTreeModel();
        var root = new RootNodeInfo(RootNodeType.Connection);
        _folder1.Inheritance.TurnOffInheritanceCompletely();
        _con1.Inheritance.TurnOffInheritanceCompletely();
        _con2.Inheritance.TurnOffInheritanceCompletely();
        _folder2.Inheritance.TurnOffInheritanceCompletely();
        _con3.Inheritance.TurnOffInheritanceCompletely();
        _con4.Inheritance.TurnOffInheritanceCompletely();
        _con5.Inheritance.TurnOffInheritanceCompletely();

        connectionTreeModel.AddRootNode(root);
        root.AddChildRange(new[] { _folder1, _folder2, _con5 });
        _folder1.AddChildRange(new[] { _con1, _con2 });
        _folder2.AddChildRange(new[] { _con3, _con4 });

        return connectionTreeModel;
    }
}
