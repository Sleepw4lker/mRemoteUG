using System.Collections.Generic;
using System.Linq;
using mRemoteUG.Connection;
using mRemoteUG.Container;
using mRemoteUG.Tree;
using mRemoteUG.UI.Controls;
using NUnit.Framework;

namespace mRemoteUG.Tests.Tree
{
    [TestFixture]
    public class ConnectionTreeFilterEvaluatorTests
    {
        private ContainerInfo _root;
        private ContainerInfo _folder;
        private ConnectionInfo _matching;
        private ConnectionInfo _nonMatching;
        private ConnectionTreeSearchTextFilter _filter;

        [SetUp]
        public void Setup()
        {
            _root = new ContainerInfo { Name = "root" };
            _folder = new ContainerInfo { Name = "folder" };
            _matching = new ConnectionInfo { Name = "needle" };
            _nonMatching = new ConnectionInfo { Name = "haystack" };

            _root.AddChild(_folder);
            _folder.AddChild(_matching);
            _folder.AddChild(_nonMatching);

            _filter = new ConnectionTreeSearchTextFilter { FilterText = "needle" };
        }

        private IEnumerable<ConnectionInfo> Roots => new ConnectionInfo[] { _root };

        [Test]
        public void MatchingNodeIsVisible()
        {
            var visible = ConnectionTreeFilterEvaluator.VisibleNodes(Roots, _filter);
            Assert.That(visible, Does.Contain(_matching));
        }

        [Test]
        public void NonMatchingSiblingIsHidden()
        {
            var visible = ConnectionTreeFilterEvaluator.VisibleNodes(Roots, _filter);
            Assert.That(visible, Does.Not.Contain(_nonMatching));
        }

        [Test]
        public void AncestorsOfAMatchAreRetained()
        {
            var visible = ConnectionTreeFilterEvaluator.VisibleNodes(Roots, _filter);
            Assert.That(visible, Does.Contain(_folder).And.Contains(_root));
        }

        [Test]
        public void NothingIsVisibleWhenNothingMatches()
        {
            _filter.FilterText = "no such connection";
            var visible = ConnectionTreeFilterEvaluator.VisibleNodes(Roots, _filter);
            Assert.That(visible, Is.Empty);
        }

        [Test]
        public void EmptyFilterTextMatchesEverything()
        {
            _filter.FilterText = "";
            var visible = ConnectionTreeFilterEvaluator.VisibleNodes(Roots, _filter);
            Assert.That(visible, Is.EquivalentTo(new ConnectionInfo[] { _root, _folder, _matching, _nonMatching }));
        }

        [Test]
        public void SpecialInclusionListSurvivesAFilterItDoesNotMatch()
        {
            _filter.SpecialInclusionList.Add(_nonMatching);
            var visible = ConnectionTreeFilterEvaluator.VisibleNodes(Roots, _filter);
            Assert.That(visible, Does.Contain(_nonMatching));
        }

        [Test]
        public void ANullFilterLeavesEveryNodeVisible()
        {
            var visible = ConnectionTreeFilterEvaluator.VisibleNodes(Roots, null);
            Assert.That(visible.Count, Is.EqualTo(4));
        }

        [Test]
        public void MatchingFolderKeepsItsNonMatchingChildrenHidden()
        {
            _filter.FilterText = "folder";
            var visible = ConnectionTreeFilterEvaluator.VisibleNodes(Roots, _filter);
            Assert.That(visible, Does.Contain(_folder));
            Assert.That(visible.Intersect(new[] { _matching, _nonMatching }), Is.Empty);
        }
    }
}
