using System.Collections;
using System.Collections.Generic;
using System.Linq;
using mRemoteUG.Config.Serializers.Xml;
using mRemoteUG.Connection;
using mRemoteUG.Container;
using mRemoteUG.Security;
using mRemoteUG.Tree;
// Aliased rather than imported: the product assembly has a Resources class in namespace
// mRemoteUG, and a member of an enclosing namespace is found before a file-level using, so
// inside mRemoteUG.Tests the bare name Resources binds to that one instead of this one.
using Fixtures = mRemoteUG.Tests.Properties.Resources;
using NUnit.Framework;

namespace mRemoteUG.Tests.Config.Serializers.ConnectionSerializers.Xml
{
    public class XmlConnectionsDeserializerTests
    {
        private XmlConnectionsDeserializer _xmlConnectionsDeserializer;
        private ConnectionTreeModel _connectionTreeModel;

        public void Setup(string confCons, string password)
        {
            _xmlConnectionsDeserializer = new XmlConnectionsDeserializer(() => password.ConvertToSecureString());
            _connectionTreeModel = _xmlConnectionsDeserializer.Deserialize(confCons);
        }

        [TearDown]
        public void Teardown()
        {
            _xmlConnectionsDeserializer = null;
            _connectionTreeModel = null;
        }

        [TestCaseSource(typeof(XmlConnectionsDeserializerFixtureData), nameof(XmlConnectionsDeserializerFixtureData.FixtureParams))]
        public void DeserializingCreatesRootNode(Datagram testData)
        {
            Setup(testData.ConfCons, testData.Password);
            Assert.That(_connectionTreeModel.RootNodes, Is.Not.Empty);
        }

        [TestCaseSource(typeof(XmlConnectionsDeserializerFixtureData), nameof(XmlConnectionsDeserializerFixtureData.FixtureParams))]
        public void RootNodeHasThreeChildren(Datagram testData)
        {
            Setup(testData.ConfCons, testData.Password);
            var connectionRoot = _connectionTreeModel.RootNodes[0];
            Assert.That(connectionRoot.Children.Count, Is.EqualTo(3));
        }

        [TestCaseSource(typeof(XmlConnectionsDeserializerFixtureData), nameof(XmlConnectionsDeserializerFixtureData.FixtureParams))]
        public void RootContainsFolder1(Datagram testData)
        {
            Setup(testData.ConfCons, testData.Password);
            var connectionRoot = _connectionTreeModel.RootNodes[0];
            Assert.That(ContainsNodeNamed("Folder1", connectionRoot.Children), Is.True);
        }

        [TestCaseSource(typeof(XmlConnectionsDeserializerFixtureData), nameof(XmlConnectionsDeserializerFixtureData.FixtureParams))]
        public void Folder1ContainsThreeConnections(Datagram testData)
        {
            // Folder1's three connections include one using SSH2, which this build
            // supports again, so all three load.
            Setup(testData.ConfCons, testData.Password);
            var connectionRoot = _connectionTreeModel.RootNodes[0];
            var folder1 = GetFolderNamed("Folder1", connectionRoot.Children);
            var folder1ConnectionCount = folder1?.Children.Count(node => !(node is ContainerInfo));
            Assert.That(folder1ConnectionCount, Is.EqualTo(3));
        }

        [TestCaseSource(typeof(XmlConnectionsDeserializerFixtureData), nameof(XmlConnectionsDeserializerFixtureData.FixtureParams))]
        public void Folder2ContainsThreeNodes(Datagram testData)
        {
            Setup(testData.ConfCons, testData.Password);
            var connectionRoot = _connectionTreeModel.RootNodes[0];
            var folder2 = GetFolderNamed("Folder2", connectionRoot.Children);
            var folder1Count = folder2?.Children.Count();
            Assert.That(folder1Count, Is.EqualTo(3));
        }

        [TestCaseSource(typeof(XmlConnectionsDeserializerFixtureData), nameof(XmlConnectionsDeserializerFixtureData.FixtureParams))]
        public void Folder21HasTwoNodes(Datagram testData)
        {
            Setup(testData.ConfCons, testData.Password);
            var connectionRoot = _connectionTreeModel.RootNodes[0];
            var folder2 = GetFolderNamed("Folder2", connectionRoot.Children);
            var folder21 = GetFolderNamed("Folder2.1", folder2.Children);
            Assert.That(folder21.Children.Count, Is.EqualTo(2));
        }

        [TestCaseSource(typeof(XmlConnectionsDeserializerFixtureData), nameof(XmlConnectionsDeserializerFixtureData.FixtureParams))]
        public void Folder211HasOneConnection(Datagram testData)
        {
            Setup(testData.ConfCons, testData.Password);
            var connectionRoot = _connectionTreeModel.RootNodes[0];
            var folder2 = GetFolderNamed("Folder2", connectionRoot.Children);
            var folder21 = GetFolderNamed("Folder2.1", folder2.Children);
            var folder211 = GetFolderNamed("Folder2.1.1", folder21.Children);
            var connectionCount = folder211.Children.Count(node => !(node is ContainerInfo));
            Assert.That(connectionCount, Is.EqualTo(1));
        }

        [TestCaseSource(typeof(XmlConnectionsDeserializerFixtureData), nameof(XmlConnectionsDeserializerFixtureData.FixtureParams))]
        public void Folder22InheritsUsername(Datagram testData)
        {
            Setup(testData.ConfCons, testData.Password);
            var connectionRoot = _connectionTreeModel.RootNodes[0];
            var folder2 = GetFolderNamed("Folder2", connectionRoot.Children);
            var folder22 = GetFolderNamed("Folder2.2", folder2.Children);
            Assert.That(folder22.Inheritance.Username, Is.True);
        }

        private bool ContainsNodeNamed(string name, IEnumerable<ConnectionInfo> list)
        {
            return list.Any(node => node.Name == name);
        }

        private ContainerInfo GetFolderNamed(string name, IEnumerable<ConnectionInfo> list)
        {
            var folder = list.First(node => (node is ContainerInfo && node.Name == name)) as ContainerInfo;
            return folder;
        }
    }

    public class XmlConnectionsDeserializerFixtureData
    {
        public static IEnumerable FixtureParams
        {
            get
            {
                yield return new TestCaseData(new Datagram("confCons v2.9", Fixtures.confCons_v2_9, "mR3m"));
                yield return new TestCaseData(new Datagram("confCons v2.9 5k Iterations", Fixtures.confCons_v2_9_5k_iterations, "mR3m"));
                yield return new TestCaseData(new Datagram("confCons v2.9 fullencryption", Fixtures.confCons_v2_9_fullencryption, "mR3m"));
                yield return new TestCaseData(new Datagram("confCons v2.9 custompassword", Fixtures.confCons_v2_9_passwordis_Password, "Password"));
                yield return new TestCaseData(new Datagram("confCons v2.9 custompassword,fullencryption", Fixtures.confCons_v2_9_passwordis_Password_fullencryption, "Password"));
            }
        }
    }

    public class Datagram
    {
        private readonly string _testName;

        public string ConfCons { get; set; }
        public string Password { get; set; }

        public Datagram(string testName, string confCons, string password)
        {
            _testName = testName;
            ConfCons = confCons;
            Password = password;
        }

        public override string ToString()
        {
            return _testName;
        }
    }
}