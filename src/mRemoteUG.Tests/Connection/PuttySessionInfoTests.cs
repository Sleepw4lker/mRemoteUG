using System.ComponentModel;

using mRemoteUG.Connection;
using mRemoteUG.Connection.Protocol;
using mRemoteUG.Container;
using mRemoteUG.Tree;

using NUnit.Framework;

namespace mRemoteUG.Tests.Connection;

/// <summary>
/// What a PuTTY Profile shows in the tree and the property grid, and what it ignores.
/// </summary>
[TestFixture]
public class PuttySessionInfoTests
{
    [Test]
    public void ItIsAPuttySessionNode()
    {
        Assert.That(new PuttySessionInfo().GetTreeNodeType(), Is.EqualTo(TreeNodeType.PuttySession));
    }

    [Test]
    public void ItAlwaysShowsThePuttyIcon()
    {
        var session = new PuttySessionInfo { Icon = "Linux" };

        Assert.That(session.Icon, Is.EqualTo("PuTTY"));
    }

    [Test]
    public void ItOpensInItsParentsPanel()
    {
        var parent = new ContainerInfo { Panel = "Servers" };
        var session = new PuttySessionInfo();
        parent.AddChild(session);

        session.Panel = "Elsewhere";

        Assert.That(session.Panel, Is.EqualTo("Servers"));
    }

    [Test]
    public void ItHasNoPanelWithoutAParent()
    {
        Assert.That(new PuttySessionInfo().Panel, Is.Null);
    }

    [Test]
    public void ItKeepsWhatItIsGiven()
    {
        var session = new PuttySessionInfo
        {
            PuttySession = "profile",
            Name = "name",
            Description = "description",
            Hostname = "host.example",
            Username = "user",
            Password = "password",
            Protocol = ProtocolType.Telnet,
            Port = 2323,
            MacAddress = "00-11-22-33-44-55",
            UserField = "field"
        };

        Assert.Multiple(() =>
        {
            Assert.That(session.PuttySession, Is.EqualTo("profile"));
            Assert.That(session.Name, Is.EqualTo("name"));
            Assert.That(session.Description, Is.EqualTo("description"));
            Assert.That(session.Hostname, Is.EqualTo("host.example"));
            Assert.That(session.Username, Is.EqualTo("user"));
            Assert.That(session.Password, Is.EqualTo("password"));
            Assert.That(session.Protocol, Is.EqualTo(ProtocolType.Telnet));
            Assert.That(session.Port, Is.EqualTo(2323));
            Assert.That(session.MacAddress, Is.EqualTo("00-11-22-33-44-55"));
            Assert.That(session.UserField, Is.EqualTo("field"));
        });
    }

    /// <summary>
    /// Everything taken over from PuTTY is read-only, because PuTTY owns it; what PuTTY does not
    /// store is hidden.
    /// </summary>
    [TestCase(nameof(PuttySessionInfo.PuttySession), true)]
    [TestCase(nameof(PuttySessionInfo.Name), true)]
    [TestCase(nameof(PuttySessionInfo.Hostname), true)]
    [TestCase(nameof(PuttySessionInfo.Username), true)]
    [TestCase(nameof(PuttySessionInfo.Protocol), true)]
    [TestCase(nameof(PuttySessionInfo.Port), true)]
    [TestCase(nameof(PuttySessionInfo.Description), false)]
    [TestCase(nameof(PuttySessionInfo.Icon), false)]
    [TestCase(nameof(PuttySessionInfo.Panel), false)]
    [TestCase(nameof(PuttySessionInfo.Password), false)]
    [TestCase(nameof(PuttySessionInfo.MacAddress), false)]
    [TestCase(nameof(PuttySessionInfo.UserField), false)]
    public void PropertiesAreReadOnlyAndShownOnlyWhenPuttyStoresThem(string property, bool browsable)
    {
        PropertyDescriptor descriptor = TypeDescriptor.GetProperties(typeof(PuttySessionInfo))[property]
                                        ?? throw new AssertionException($"PuttySessionInfo has no {property}");

        Assert.Multiple(() =>
        {
            Assert.That(descriptor.IsReadOnly, Is.True);
            Assert.That(descriptor.IsBrowsable, Is.EqualTo(browsable));
        });
    }
}
