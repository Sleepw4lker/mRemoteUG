using System.Reflection;

using mRemoteUG.Connection;
using mRemoteUG.Connection.Protocol;
using mRemoteUG.Container;
using mRemoteUG.Tree.Root;

using NUnit.Framework;


namespace mRemoteUG.Tests.Connection;

public class ConnectionInfoTests
{
    // ProtocolBase has no abstract members; a minimal subclass avoids
    // RdpProtocol's dependency on the FrmMain singleton (STA/WinForms-only).
    private sealed class DummyProtocol : ProtocolBase
    {
    }

    // Rebuilt by [SetUp] before each test, so null! rather than an initializer here;
    // see ADR-0022 on when that is the right answer.
    private ConnectionInfo _connectionInfo = null!;
    private const string TestDomain = "somedomain";

    [SetUp]
    public void Setup()
    {
        _connectionInfo = new ConnectionInfo();
    }

    [Test]
    public void CopyCreatesMemberwiseCopy()
    {
        _connectionInfo.Domain = TestDomain;
        ConnectionInfo secondConnection = _connectionInfo.Clone();
        Assert.That(secondConnection.Domain, Is.EqualTo(_connectionInfo.Domain));
    }

    [Test]
    public void CloneDoesNotSetParentOfNewConnectionInfo()
    {
        _connectionInfo.SetParent(new ContainerInfo());
        ConnectionInfo clonedConnection = _connectionInfo.Clone();
        Assert.That(clonedConnection.Parent, Is.Null);
    }

    [Test]
    public void CloneAlsoCopiesInheritanceObject()
    {
        ConnectionInfo clonedConnection = _connectionInfo.Clone();
        Assert.That(clonedConnection.Inheritance, Is.Not.EqualTo(_connectionInfo.Inheritance));
    }

    [Test]
    public void CloneCorrectlySetsParentOfInheritanceObject()
    {
        ConnectionInfo clonedConnection = _connectionInfo.Clone();
        Assert.That(clonedConnection.Inheritance.Parent, Is.EqualTo(clonedConnection));
    }

    [Test]
    public void CopyFromCopiesProperties()
    {
        var secondConnection = new ConnectionInfo { Domain = TestDomain };
        _connectionInfo.CopyFrom(secondConnection);
        Assert.That(_connectionInfo.Domain, Is.EqualTo(secondConnection.Domain));
    }

    [Test]
    public void CopyingAConnectionInfoAlsoCopiesItsInheritance()
    {
        _connectionInfo.Inheritance.Username = true;
        var secondConnection = new ConnectionInfo { Inheritance = { Username = false } };
        secondConnection.CopyFrom(_connectionInfo);
        Assert.That(secondConnection.Inheritance.Username, Is.True);
    }

    [Test]
    public void PropertyChangedEventRaisedWhenOpenConnectionsChanges()
    {
        bool eventWasCalled = false;
        _connectionInfo.PropertyChanged += (sender, args) => eventWasCalled = true;
        _connectionInfo.OpenConnections.Add(new DummyProtocol());
        Assert.That(eventWasCalled);
    }

    [Test]
    public void PropertyChangedEventArgsAreCorrectWhenOpenConnectionsChanges()
    {
        string? nameOfModifiedProperty = "";
        _connectionInfo.PropertyChanged += (sender, args) => nameOfModifiedProperty = args.PropertyName;
        _connectionInfo.OpenConnections.Add(new DummyProtocol());
        Assert.That(nameOfModifiedProperty, Is.EqualTo("OpenConnections"));
    }

    [TestCaseSource(typeof(InheritancePropertyProvider), nameof(InheritancePropertyProvider.GetProperties))]
    public void MovingAConnectionUnderRootNodeDisablesInheritance(PropertyInfo property)
    {
        var rootNode = new RootNodeInfo(RootNodeType.Connection);
        _connectionInfo.Inheritance.EverythingInherited = true;
        _connectionInfo.SetParent(rootNode);
        object? propertyValue = property.GetValue(_connectionInfo.Inheritance);
        Assert.That(propertyValue, Is.False);
    }

    [TestCaseSource(typeof(InheritancePropertyProvider), nameof(InheritancePropertyProvider.GetProperties))]
    public void MovingAConnectionFromUnderRootNodeToUnderADifferentNodeEnablesInheritance(PropertyInfo property)
    {
        var rootNode = new RootNodeInfo(RootNodeType.Connection);
        var otherContainer = new ContainerInfo();
        _connectionInfo.Inheritance.EverythingInherited = true;
        _connectionInfo.SetParent(rootNode);
        _connectionInfo.SetParent(otherContainer);
        object? propertyValue = property.GetValue(_connectionInfo.Inheritance);
        Assert.That(propertyValue, Is.True);
    }

    [TestCase(ProtocolType.RDP, ExpectedResult = 3389)]
    public int GetDefaultPortReturnsCorrectPortForProtocol(ProtocolType protocolType)
    {
        _connectionInfo.Protocol = protocolType;
        return _connectionInfo.GetDefaultPort();
    }

    /// <summary>
    /// A null on the parent is not a value to inherit.
    /// </summary>
    /// <remarks>
    /// TryGetInheritedPropertyValue cast PropertyInfo.GetValue(Parent) to the property type and
    /// returned true whatever came back, so a null on the parent won over the child's own
    /// non-null field and was returned from a property declared to be a non-nullable string.
    /// For the getters that then Trim() it arrived as a NullReferenceException blamed on the
    /// child. The parent's field is nulled by reflection here because the setters no longer
    /// accept null -- which is the other half of the fix, and is covered separately below.
    /// </remarks>
    [TestCaseSource(typeof(InheritableStringPropertyProvider),
                    nameof(InheritableStringPropertyProvider.GetProperties))]
    public void ANullOnTheParentIsNotInherited(PropertyInfo property)
    {
        const string childValue = "the child's own value";
        var parent = new ContainerInfo();
        property.SetValue(_connectionInfo, childValue);
        _connectionInfo.SetParent(parent);
        InheritanceFlagFor(property).SetValue(_connectionInfo.Inheritance, true);

        BackingFieldFor(property).SetValue(parent, null);

        Assert.That(property.GetValue(_connectionInfo), Is.EqualTo(childValue));
    }

    /// <summary>
    /// The inherited value is still used when the parent actually has one.
    /// </summary>
    /// <remarks>
    /// The guard above must reject only null, not switch inheritance off. Without this the
    /// previous test passes against a TryGet that always returns false.
    /// </remarks>
    [TestCaseSource(typeof(InheritableStringPropertyProvider),
                    nameof(InheritableStringPropertyProvider.GetProperties))]
    public void AValueOnTheParentIsStillInherited(PropertyInfo property)
    {
        const string parentValue = "the parent value";
        var parent = new ContainerInfo();
        property.SetValue(_connectionInfo, "the child's own value");
        property.SetValue(parent, parentValue);
        _connectionInfo.SetParent(parent);
        InheritanceFlagFor(property).SetValue(_connectionInfo.Inheritance, true);

        Assert.That(property.GetValue(_connectionInfo), Is.EqualTo(parentValue));
    }

    /// <summary>
    /// No string property returns null, whatever is assigned to it.
    /// </summary>
    /// <remarks>
    /// All 17 are declared as a non-nullable string, but the setters stored what they were
    /// given -- seven of them as <c>value?.Trim()</c>, which stores null for a null input --
    /// and six getters then called Trim() on it. Assigning null and reading it back was an
    /// unguarded NullReferenceException. Empty is what absence already means here: the
    /// constructor assigns string.Empty to Hostname, and 68 call sites test these with
    /// IsNullOrEmpty.
    /// </remarks>
    [TestCaseSource(typeof(StringPropertyProvider), nameof(StringPropertyProvider.GetProperties))]
    public void NoStringPropertyReturnsNull(PropertyInfo property)
    {
        Assert.DoesNotThrow(() => property.SetValue(_connectionInfo, null),
                            $"assigning null to {property.Name} threw");

        object? value = null;
        Assert.DoesNotThrow(() => value = property.GetValue(_connectionInfo),
                            $"reading {property.Name} back after assigning null threw");
        Assert.That(value, Is.Empty, $"{property.Name} did not come back empty");
    }

    /// <summary>Whitespace is still trimmed away on the way in.</summary>
    /// <remarks>
    /// The six getters that used to Trim() were trimming a value the setter had already
    /// trimmed. Removing the redundant read-side Trim() is what makes those getters
    /// unconditionally safe, so the write-side one has to be covered.
    /// </remarks>
    [TestCaseSource(typeof(TrimmedStringPropertyProvider),
                    nameof(TrimmedStringPropertyProvider.GetProperties))]
    public void WhitespaceIsTrimmedOnAssignment(PropertyInfo property)
    {
        property.SetValue(_connectionInfo, "  padded  ");
        Assert.That(property.GetValue(_connectionInfo), Is.EqualTo("padded"));
    }

    /// <summary>
    /// A record whose Inheritance was set to null can still be read.
    /// </summary>
    /// <remarks>
    /// Inheritance has a public setter and is reassigned by Clone, CopyFrom,
    /// DefaultConnectionInfo and the XML deserializer. The GetType() call that read the
    /// inheritance flag sat outside the try/catch that guarded the rest of the lookup, so a
    /// null there took every inheritable getter on the record down with it.
    /// </remarks>
    [Test]
    public void ANullInheritanceObjectIsNotInherited()
    {
        _connectionInfo.Panel = "the child's own value";
        _connectionInfo.SetParent(new ContainerInfo());
        // Breaks the declared contract on purpose: this is the state the test is about.
        _connectionInfo.Inheritance = null!;

        Assert.That(_connectionInfo.Panel, Is.EqualTo("the child's own value"));
    }

    private static PropertyInfo InheritanceFlagFor(PropertyInfo property)
    {
        return typeof(ConnectionInfoInheritance).GetProperty(property.Name)
               ?? throw new AssertionException($"no inheritance flag matches {property.Name}");
    }

    private static FieldInfo BackingFieldFor(PropertyInfo property)
    {
        // IgnoreCase because the convention is not mechanical: RDGatewayHostname is backed
        // by _rdGatewayHostname, not _rDGatewayHostname.
        return typeof(AbstractConnectionRecord).GetField(
                   "_" + property.Name,
                   BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase)
               ?? throw new AssertionException($"no backing field matches {property.Name}");
    }

    /// <summary>Every string property on the record, discovered rather than listed.</summary>
    private sealed class StringPropertyProvider
    {
        public static IEnumerable<PropertyInfo> GetProperties()
        {
            return typeof(AbstractConnectionRecord)
                   .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                   .Where(p => p.PropertyType == typeof(string) && p.CanRead && p.CanWrite);
        }
    }

    /// <summary>The string properties that resolve through the inheritance lookup.</summary>
    private sealed class InheritableStringPropertyProvider
    {
        public static IEnumerable<PropertyInfo> GetProperties()
        {
            return StringPropertyProvider.GetProperties()
                   .Where(p => typeof(ConnectionInfoInheritance).GetProperty(p.Name) != null);
        }
    }

    /// <summary>The string properties whose setter trims.</summary>
    private sealed class TrimmedStringPropertyProvider
    {
        public static IEnumerable<PropertyInfo> GetProperties()
        {
            string[] trimmed = new[]
            {
                nameof(ConnectionInfo.Hostname), nameof(ConnectionInfo.Username),
                nameof(ConnectionInfo.Domain), nameof(ConnectionInfo.LoadBalanceInfo),
                nameof(ConnectionInfo.RDGatewayHostname), nameof(ConnectionInfo.RDGatewayUsername),
                nameof(ConnectionInfo.RDGatewayDomain)
            };
            return StringPropertyProvider.GetProperties().Where(p => trimmed.Contains(p.Name));
        }
    }

    private sealed class InheritancePropertyProvider
    {
        public static IEnumerable<PropertyInfo> GetProperties()
        {
            return new ConnectionInfoInheritance(new object()).GetProperties();
        }
    }
}
