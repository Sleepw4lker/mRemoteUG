using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;
using mRemoteUG.Config.Serializers.Xml;
using mRemoteUG.Connection;
using mRemoteUG.Connection.Protocol;
using mRemoteUG.Container;
using mRemoteUG.Security;
using mRemoteUG.Security.Factories;
using mRemoteUG.Tree;
using mRemoteUG.Tree.Root;
using NUnit.Framework;


namespace mRemoteUG.Tests.IntegrationTests
{
	public class XmlSerializationLifeCycleTests
    {
        private XmlConnectionsSerializer _serializer;
        private XmlConnectionsDeserializer _deserializer;
        private ConnectionTreeModel _originalModel;
        private readonly ICryptoProviderFactory _cryptoFactory = new CryptoProviderFactory(BlockCipherEngines.AES , BlockCipherModes.GCM);

        [SetUp]
        public void Setup()
        {
            _originalModel = SetupConnectionTreeModel();
            var cryptoProvider = _cryptoFactory.Build();
            var nodeSerializer = new XmlConnectionNodeSerializer(
                cryptoProvider, 
                _originalModel.RootNodes.OfType<RootNodeInfo>().First().PasswordString.ConvertToSecureString(),
                new SaveFilter());
            _serializer = new XmlConnectionsSerializer(cryptoProvider, nodeSerializer);
            _deserializer = new XmlConnectionsDeserializer();
        }

        [TearDown]
        public void Teardown()
        {
            _serializer = null;
        }

        [Test]
        public void SerializeThenDeserialize()
        {
            var serializedContent = _serializer.Serialize(_originalModel);
            var deserializedModel = _deserializer.Deserialize(serializedContent);
            var nodeNamesFromDeserializedModel = deserializedModel.GetRecursiveChildList().Select(node => node.Name);
            var nodeNamesFromOriginalModel = _originalModel.GetRecursiveChildList().Select(node => node.Name);
            Assert.That(nodeNamesFromDeserializedModel, Is.EquivalentTo(nodeNamesFromOriginalModel));
        }

        [Test]
        public void SerializeThenDeserializeWithFullEncryption()
        {
            _serializer.UseFullEncryption = true;
            var serializedContent = _serializer.Serialize(_originalModel);
            var deserializedModel = _deserializer.Deserialize(serializedContent);
            var nodeNamesFromDeserializedModel = deserializedModel.GetRecursiveChildList().Select(node => node.Name);
            var nodeNamesFromOriginalModel = _originalModel.GetRecursiveChildList().Select(node => node.Name);
            Assert.That(nodeNamesFromDeserializedModel, Is.EquivalentTo(nodeNamesFromOriginalModel));
        }

        [Test]
        public void SerializeAndDeserializePropertiesWithInternationalCharacters()
        {
            var originalConnectionInfo = new ConnectionInfo {Name = "con1", Description = "£°úg¶┬ä" };
            var serializedContent = _serializer.Serialize(originalConnectionInfo);
            var deserializedModel = _deserializer.Deserialize(serializedContent);
            var deserializedConnectionInfo = deserializedModel.GetRecursiveChildList().First(node => node.Name == originalConnectionInfo.Name);
            Assert.That(deserializedConnectionInfo.Description, Is.EqualTo(originalConnectionInfo.Description));
        }


        [Test]
        public void SerializeAndDeserializeWithCustomKdfIterationsValue()
        {
            var cryptoProvider = _cryptoFactory.Build();
            cryptoProvider.KeyDerivationIterations = 5000;
            var nodeSerializer = new XmlConnectionNodeSerializer(
                cryptoProvider, 
                _originalModel.RootNodes.OfType<RootNodeInfo>().First().PasswordString.ConvertToSecureString(),
                new SaveFilter());
            _serializer = new XmlConnectionsSerializer(cryptoProvider, nodeSerializer);
            var serializedContent = _serializer.Serialize(_originalModel);
            var deserializedModel = _deserializer.Deserialize(serializedContent);
            var nodeNamesFromDeserializedModel = deserializedModel.GetRecursiveChildList().Select(node => node.Name);
            var nodeNamesFromOriginalModel = _originalModel.GetRecursiveChildList().Select(node => node.Name);
            Assert.That(nodeNamesFromDeserializedModel, Is.EquivalentTo(nodeNamesFromOriginalModel));
        }

        [Test]
        public void GuidCreatedIfNonExistedInXml()
        {
            var originalConnectionInfo = new ConnectionInfo { Name = "con1" };
            var serializedContent = _serializer.Serialize(originalConnectionInfo);

            // remove GUID from connection xml
            serializedContent = serializedContent.Replace(originalConnectionInfo.ConstantID, "");

            var deserializedModel = _deserializer.Deserialize(serializedContent);
            var deserializedConnectionInfo = deserializedModel.GetRecursiveChildList().First(node => node.Name == originalConnectionInfo.Name);
            Assert.That(Guid.TryParse(deserializedConnectionInfo.ConstantID, out var guid));
        }


        [Test]
        public void PuttySessionSurvivesARoundTrip()
        {
            var originalConnectionInfo = new ConnectionInfo
            {
                Name = "con1",
                Protocol = ProtocolType.SSH2,
                PuttySession = "SomeSavedSession"
            };
            var serializedContent = _serializer.Serialize(originalConnectionInfo);

            var deserializedModel = _deserializer.Deserialize(serializedContent);

            var deserializedConnectionInfo = deserializedModel.GetRecursiveChildList()
                                                              .First(node => node.Name == originalConnectionInfo.Name);
            Assert.Multiple(() =>
            {
                Assert.That(deserializedConnectionInfo.Protocol, Is.EqualTo(ProtocolType.SSH2));
                Assert.That(deserializedConnectionInfo.PuttySession, Is.EqualTo("SomeSavedSession"));
            });
        }

        [Test]
        public void LoadsAFileWrittenWhilePuttySupportWasAbsent()
        {
            // Files saved between the RDP-only strip and the restoration of PuTTY carry a
            // current ConfVersion but no PuttySession/InheritPuttySession attributes.
            // Reading them must not throw.
            var originalConnectionInfo = new ConnectionInfo { Name = "con1" };
            var serializedContent = Regex.Replace(_serializer.Serialize(originalConnectionInfo),
                                                   " (Inherit)?PuttySession=\"[^\"]*\"", "");
            Assert.That(serializedContent, Does.Not.Contain("PuttySession"));

            var deserializedModel = _deserializer.Deserialize(serializedContent);

            var deserializedConnectionInfo = deserializedModel.GetRecursiveChildList()
                                                              .First(node => node.Name == originalConnectionInfo.Name);
            Assert.Multiple(() =>
            {
                Assert.That(deserializedConnectionInfo.PuttySession, Is.Empty);
                Assert.That(deserializedConnectionInfo.Inheritance.PuttySession, Is.False);
            });
        }


        /// <summary>
        /// Every persisted field survives a serialize/deserialize round trip.
        /// </summary>
        /// <remarks>
        /// This is the net for changes to the deserializer. Two things about it are deliberate,
        /// and both were measured rather than assumed.
        /// <para>
        /// <b>Every field is set to a non-default value first.</b> The committed fixtures cannot do
        /// this job: confCons_v2_9.xml holds 82 attributes across 18 nodes, and 39 of them are
        /// constant on every node at exactly the value the constructor would have assigned anyway.
        /// Against such a file, "the deserializer read this" and "the deserializer never read it and
        /// the field kept its default" are indistinguishable, so whole blocks of assignments can be
        /// deleted with every test still green.
        /// </para>
        /// <para>
        /// <b>It compares private backing fields, not properties.</b> ConnectionInfo overrides
        /// GetPropertyValue to return the parent value whenever the matching inheritance flag is
        /// set, and several getters also Trim(). A property comparison therefore reports the
        /// effective value and can mask a read that never happened. The fields are the only
        /// parent-independent answer to the question "did the deserializer assign this?".
        /// </para>
        /// <para>
        /// The field list is discovered by reflection rather than written out, so a newly added
        /// persisted field cannot silently escape coverage.
        /// </para>
        /// </remarks>
        [Test]
        public void EveryPersistedFieldSurvivesARoundTrip()
        {
            var original = new ConnectionInfo();
            SetEveryFieldToANonDefaultValue(original);

            var serialized = _serializer.Serialize(original);
            var deserialized = _deserializer.Deserialize(serialized)
                                            .GetRecursiveChildList()
                                            .First(node => node.ConstantID == original.ConstantID);

            Assert.Multiple(() =>
            {
                foreach (var field in PersistedFields())
                {
                    Assert.That(Format(field.GetValue(deserialized)),
                                Is.EqualTo(Format(field.GetValue(original))),
                                $"{field.Name} did not survive the round trip");
                }
            });
        }

        /// <summary>The backing field of every persisted property, ordered so failures read consistently.</summary>
        private static IEnumerable<FieldInfo> PersistedFields()
        {
            return typeof(AbstractConnectionRecord)
                   .GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
                   .Where(field => field.Name.StartsWith("_", StringComparison.Ordinal))
                   .OrderBy(field => field.Name, StringComparer.Ordinal);
        }

        /// <summary>
        /// Assigns each persisted property a value the constructor would not have produced.
        /// </summary>
        /// <remarks>
        /// Values are derived from the property itself rather than randomised, so a failure
        /// reproduces. ConnectionInfoHelpers.GetRandomizedConnectionInfo is deliberately not used:
        /// it draws from an unseeded static Random.
        /// <para>
        /// Protocol is assigned first and from the supported set. Its setter resets Port, and an
        /// unsupported protocol would make AddNodesFromXmlRecursive skip the node entirely, so the
        /// connection would not come back at all.
        /// </para>
        /// </remarks>
        private static void SetEveryFieldToANonDefaultValue(ConnectionInfo connectionInfo)
        {
            // Inheritance off first, and explicitly. DefaultConnectionInheritance.Instance is a
            // process-wide mutable singleton that DefaultConnectionInheritanceTests calls
            // TurnOnInheritanceCompletely on, permanently, for whatever runs after it. A
            // connection that inherits its password is serialized with an empty one, so without
            // this the test passes alone and fails in the full suite depending on run order.
            connectionInfo.Inheritance.TurnOffInheritanceCompletely();

            connectionInfo.Protocol = ProtocolTypes.Supported.First(p => p != connectionInfo.Protocol);

            foreach (var field in PersistedFields())
            {
                var property = typeof(ConnectionInfo).GetProperty(
                    field.Name.Substring(1),
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

                Assert.That(property, Is.Not.Null, $"no public property matches backing field {field.Name}");
                if (property.Name == nameof(ConnectionInfo.Protocol)) continue;

                property.SetValue(connectionInfo, NonDefaultValue(property, property.GetValue(connectionInfo)));
            }
        }

        private static object NonDefaultValue(PropertyInfo property, object current)
        {
            var type = property.PropertyType;

            if (type == typeof(string)) return property.Name + "-roundtrip";
            if (type == typeof(bool)) return !(bool)current;
            if (type == typeof(int)) return (int)current + 4242;
            if (type.IsEnum) return Enum.GetValues(type).Cast<object>().First(value => !value.Equals(current));

            throw new NotSupportedException($"No non-default rule for {type} ({property.Name})");
        }

        /// <summary>Invariant, and numeric for enums: several RDP enums share values, so ToString is not specified.</summary>
        private static string Format(object value)
        {
            if (value == null) return "<null>";
            if (value is Enum) return Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }


        /// <summary>
        /// A collapsed folder comes back collapsed.
        /// </summary>
        /// <remarks>
        /// Replaces ExpandedPropertyGetsDeserialized, which asserted IsExpanded is true on a
        /// fixture folder. Every container in every committed fixture is saved expanded, and
        /// ContainerInfo defaults IsExpanded to true, so that test passed whether or not the
        /// attribute was ever read. Only the collapsed case can fail.
        /// </remarks>
        [Test]
        public void ACollapsedContainerSurvivesARoundTrip()
        {
            var folder = new ContainerInfo { Name = "collapsed", IsExpanded = false };
            var rootNode = new RootNodeInfo(RootNodeType.Connection);
            rootNode.AddChild(folder);
            var model = new ConnectionTreeModel();
            model.AddRootNode(rootNode);

            var deserialized = _deserializer.Deserialize(_serializer.Serialize(model));
            var roundTripped = deserialized.GetRecursiveChildList()
                                           .OfType<ContainerInfo>()
                                           .First(node => node.Name == folder.Name);

            Assert.That(roundTripped.IsExpanded, Is.False);
        }

        /// <summary>
        /// A file missing any one attribute still loads the rest of that connection.
        /// </summary>
        /// <remarks>
        /// XmlAttributeCollection's string indexer returns null for an absent attribute, and the
        /// deserializer read all but a handful as <c>xmlnode.Attributes["X"].Value</c>, so one
        /// missing attribute threw. What made it hard to notice is that the throw was caught and
        /// logged per connection and the half-populated connection was still returned: every
        /// property after the failure point silently kept its constructor default. A file written
        /// by any version with a different attribute set therefore loaded, looked plausible, and
        /// quietly lost most of each connection.
        /// <para>
        /// LoadsAFileWrittenWhilePuttySupportWasAbsent covers the two attributes that happened to
        /// be guarded already. This covers every attribute the serializer writes, and asserts the
        /// loss is confined to the attribute actually removed rather than everything after it.
        /// </para>
        /// </remarks>
        [Test]
        public void AnyOneAbsentAttributeCostsOnlyItsOwnField()
        {
            var original = new ConnectionInfo();
            SetEveryFieldToANonDefaultValue(original);
            var serialized = _serializer.Serialize(original);

            var attributeNames = ConnectionAttributeNames(serialized, original.ConstantID);
            Assert.That(attributeNames, Has.Count.GreaterThan(50),
                        "expected the serializer to be writing the full attribute set");

            Assert.Multiple(() =>
            {
                foreach (var attributeName in attributeNames)
                {
                    // Id has its own test, GuidCreatedIfNonExistedInXml. Protocol is excluded
                    // because falling back to a default protocol can make the node unsupported,
                    // and AddNodesFromXmlRecursive then skips it on purpose, so the connection
                    // legitimately would not come back at all.
                    if (attributeName == "Id" || attributeName == "Protocol") continue;

                    var stripped = WithoutConnectionAttribute(serialized, original.ConstantID, attributeName);

                    ConnectionInfo loaded = null;
                    Assert.DoesNotThrow(
                        () => loaded = _deserializer.Deserialize(stripped)
                                                    ?.GetRecursiveChildList()
                                                    .FirstOrDefault(node => node.ConstantID == original.ConstantID),
                        $"a file with no {attributeName} attribute threw");

                    Assert.That(loaded, Is.Not.Null,
                                $"the connection did not load from a file with no {attributeName} attribute");
                    if (loaded == null) continue;

                    var lost = PersistedFields()
                               .Where(field => Format(field.GetValue(loaded)) != Format(field.GetValue(original)))
                               .Select(field => field.Name)
                               .ToList();

                    Assert.That(lost, Has.Count.LessThanOrEqualTo(1),
                                $"removing {attributeName} also lost {string.Join(", ", lost)}");
                }
            });
        }

        /// <summary>The attribute names the serializer wrote onto one connection's element.</summary>
        private static List<string> ConnectionAttributeNames(string xml, string connectionId)
        {
            var node = ConnectionNode(xml, connectionId, out _);
            return node.Attributes.Cast<XmlAttribute>().Select(attribute => attribute.Name).ToList();
        }

        /// <summary>
        /// The same document with one attribute removed from one connection's element.
        /// </summary>
        /// <remarks>
        /// Done through XmlDocument rather than a regular expression over the text because several
        /// of these names -- Name and Id among them -- are also written on the root element, and a
        /// textual strip would take those too and fail for an unrelated reason.
        /// </remarks>
        private static string WithoutConnectionAttribute(string xml, string connectionId, string attributeName)
        {
            var node = ConnectionNode(xml, connectionId, out var document);
            node.Attributes.RemoveNamedItem(attributeName);
            return document.OuterXml;
        }

        private static XmlNode ConnectionNode(string xml, string connectionId, out XmlDocument document)
        {
            document = new XmlDocument();
            document.LoadXml(xml);
            var node = document.SelectSingleNode($"//Node[@Id='{connectionId}']");
            Assert.That(node?.Attributes, Is.Not.Null, "the serialized connection element was not found");
            return node;
        }

        private ConnectionTreeModel SetupConnectionTreeModel()
        {
            /*
             * Root
             * |--- con0
             * |--- folder1
             * |    L--- con1
             * L--- folder2
             *      |--- con2
             *      L--- folder3
             *           |--- con3
             *           L--- con4
             */
            var connectionTreeModel = new ConnectionTreeModel();
            var rootNode = new RootNodeInfo(RootNodeType.Connection);
            var folder1 = new ContainerInfo { Name = "folder1" };
            var folder2 = new ContainerInfo { Name = "folder2" };
            var folder3 = new ContainerInfo { Name = "folder3" };
            var con0 = new ConnectionInfo { Name = "con0" };
            var con1 = new ConnectionInfo { Name = "con1" };
            var con2 = new ConnectionInfo { Name = "con2" };
            var con3 = new ConnectionInfo { Name = "con3" };
            var con4 = new ConnectionInfo { Name = "con4" };
            rootNode.AddChild(folder1);
            rootNode.AddChild(folder2);
            rootNode.AddChild(con0);
            folder1.AddChild(con1);
            folder2.AddChild(con2);
            folder2.AddChild(folder3);
            folder3.AddChild(con3);
            folder3.AddChild(con4);
            connectionTreeModel.AddRootNode(rootNode);
            return connectionTreeModel;
        }
    }
}