using mRemoteUG.Connection;
using mRemoteUG.Container;
using mRemoteUG.Tree;
using mRemoteUG.UI.Controls;

using NUnit.Framework;

namespace mRemoteUG.Tests.Tree;

[TestFixture]
public class ConnectionTreeFilterEvaluatorTests
{
    private readonly ContainerInfo _root = new() { Name = "root" };
    private readonly ContainerInfo _folder = new() { Name = "folder" };
    private readonly ConnectionInfo _matching = new() { Name = "needle" };
    private readonly ConnectionInfo _nonMatching = new() { Name = "haystack" };

    // Rebuilt per test in Setup: tests change the filter text and its inclusion list.
    private ConnectionTreeSearchTextFilter _filter = null!;

    public ConnectionTreeFilterEvaluatorTests()
    {
        _root.AddChild(_folder);
        _folder.AddChild(_matching);
        _folder.AddChild(_nonMatching);
    }

    [SetUp]
    public void Setup()
    {
        _filter = new ConnectionTreeSearchTextFilter { FilterText = "needle" };
    }

    private IEnumerable<ConnectionInfo> Roots => new ConnectionInfo[] { _root };

    [Test]
    public void MatchingNodeIsVisible()
    {
        HashSet<ConnectionInfo> visible = ConnectionTreeFilterEvaluator.VisibleNodes(Roots, _filter);
        Assert.That(visible, Does.Contain(_matching));
    }

    [Test]
    public void NonMatchingSiblingIsHidden()
    {
        HashSet<ConnectionInfo> visible = ConnectionTreeFilterEvaluator.VisibleNodes(Roots, _filter);
        Assert.That(visible, Does.Not.Contain(_nonMatching));
    }

    [Test]
    public void AncestorsOfAMatchAreRetained()
    {
        HashSet<ConnectionInfo> visible = ConnectionTreeFilterEvaluator.VisibleNodes(Roots, _filter);
        Assert.That(visible, Does.Contain(_folder).And.Contains(_root));
    }

    [Test]
    public void NothingIsVisibleWhenNothingMatches()
    {
        _filter.FilterText = "no such connection";
        HashSet<ConnectionInfo> visible = ConnectionTreeFilterEvaluator.VisibleNodes(Roots, _filter);
        Assert.That(visible, Is.Empty);
    }

    [Test]
    public void EmptyFilterTextMatchesEverything()
    {
        _filter.FilterText = "";
        HashSet<ConnectionInfo> visible = ConnectionTreeFilterEvaluator.VisibleNodes(Roots, _filter);
        Assert.That(visible, Is.EquivalentTo(new ConnectionInfo[] { _root, _folder, _matching, _nonMatching }));
    }

    [Test]
    public void SpecialInclusionListSurvivesAFilterItDoesNotMatch()
    {
        _filter.SpecialInclusionList.Add(_nonMatching);
        HashSet<ConnectionInfo> visible = ConnectionTreeFilterEvaluator.VisibleNodes(Roots, _filter);
        Assert.That(visible, Does.Contain(_nonMatching));
    }

    [Test]
    public void ANullFilterLeavesEveryNodeVisible()
    {
        HashSet<ConnectionInfo> visible = ConnectionTreeFilterEvaluator.VisibleNodes(Roots, null);
        Assert.That(visible.Count, Is.EqualTo(4));
    }

    [Test]
    public void MatchingFolderKeepsItsNonMatchingChildrenHidden()
    {
        _filter.FilterText = "folder";
        HashSet<ConnectionInfo> visible = ConnectionTreeFilterEvaluator.VisibleNodes(Roots, _filter);
        Assert.That(visible, Does.Contain(_folder));
        Assert.That(visible.Intersect(new[] { _matching, _nonMatching }), Is.Empty);
    }
}
