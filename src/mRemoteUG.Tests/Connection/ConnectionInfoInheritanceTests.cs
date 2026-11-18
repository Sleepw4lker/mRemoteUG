using mRemoteUG.Connection;
using mRemoteUG.Container;

using NUnit.Framework;

namespace mRemoteUG.Tests.Connection;

[TestFixture]
public class ConnectionInfoInheritanceTests
{
    // Rebuilt by [SetUp] before each test, so null! rather than an initializer here;
    // see ADR-0022 on when that is the right answer.
    private ConnectionInfo _connectionInfo = null!;
    private ConnectionInfoInheritance _inheritance = null!;


    [SetUp]
    public void Setup()
    {
        _connectionInfo = new ConnectionInfo();
        _inheritance = new ConnectionInfoInheritance(_connectionInfo);
    }

    [Test]
    public void TurnOffInheritanceCompletely()
    {
        _inheritance.Username = true;
        _inheritance.TurnOffInheritanceCompletely();
        Assert.That(AllInheritancePropertiesAreFalse(), Is.True);
    }

    [Test]
    public void TurnOnInheritanceCompletely()
    {
        _inheritance.TurnOnInheritanceCompletely();
        Assert.That(AllInheritancePropertiesAreTrue(), Is.True);
    }

    [Test]
    public void DisableInheritanceTurnsOffAllInheritance()
    {
        _inheritance.Username = true;
        _inheritance.DisableInheritance();
        Assert.That(AllInheritancePropertiesAreFalse(), Is.True);
    }

    [Test]
    public void EnableInheritanceRestoresPreviousInheritanceValues()
    {
        _inheritance.Username = true;
        _inheritance.DisableInheritance();
        _inheritance.EnableInheritance();
        Assert.That(_inheritance.Username, Is.True);
    }

    [Test]
    public void GetPropertiesReturnsListOfSettableProperties()
    {
        bool hasIconProperty = _inheritance
            .GetProperties()
            .Contains(typeof(ConnectionInfoInheritance).GetProperty("Icon"));
        Assert.That(hasIconProperty, Is.True);
    }

    [Test]
    public void GetPropertiesExludesPropertiesThatShouldNotBeSet()
    {
        bool hasEverythingInheritedProperty = _inheritance
            .GetProperties()
            .Contains(typeof(ConnectionInfoInheritance).GetProperty("EverythingInherited"));
        Assert.That(hasEverythingInheritedProperty, Is.False);
    }

    [Test]
    public void AlwaysReturnInheritedValueIfRequested()
    {
        bool expectedSetting = false;

        var container = new ContainerInfo { AutomaticResize = expectedSetting };
        var con1 = new ConnectionInfo
        {
            AutomaticResize = true,
            Inheritance = { AutomaticResize = true }
        };
        container.AddChild(con1);

        Assert.That(con1.AutomaticResize, Is.EqualTo(expectedSetting));
    }

    /// <remarks>
    /// These used to filter on <c>new ArrayList { "IsDefault" }.Contains(property)</c>, a list of
    /// strings asked about a PropertyInfo, which never matched. So no flag was ever checked and
    /// both assertions passed whatever the methods under test did. The flags now come from
    /// <see cref="ConnectionInfoInheritance.GetProperties"/>, the same list the methods walk.
    /// </remarks>
    private bool AllInheritancePropertiesAreTrue()
    {
        return FlagsSetTo(false).Count == 0 && FlagsSetTo(true).Count > 0;
    }

    private bool AllInheritancePropertiesAreFalse()
    {
        return FlagsSetTo(true).Count == 0 && FlagsSetTo(false).Count > 0;
    }

    private List<string> FlagsSetTo(bool value)
    {
        return _inheritance.GetProperties()
                           .Where(p => p.PropertyType == typeof(bool)
                                       && p.GetValue(_inheritance) is bool flag && flag == value)
                           .Select(p => p.Name)
                           .ToList();
    }
}
