using mRemoteUG.Connection;
using mRemoteUG.Connection.Protocol;
using mRemoteUG.Container;
using mRemoteUG.UI.Controls;

using NUnit.Framework;

namespace mRemoteUG.Tests.UI.Controls;

public class ConnectionContextMenuDisconnectTests
{
    // Stands in for a PuTTY session: ProtocolBase.Disconnect closes inline on the UI thread, and
    // ConnectionInitiator's Closed handler takes the session out of OpenConnections before
    // Disconnect returns. This does the same without hosting anything.
    private sealed class InlineClosingSession : ProtocolBase
    {
        private readonly ConnectionInfo _owner;
        private readonly List<ProtocolBase> _disconnected;

        public InlineClosingSession(ConnectionInfo owner, List<ProtocolBase> disconnected)
        {
            _owner = owner;
            _disconnected = disconnected;
            owner.OpenConnections.Add(this);
        }

        public override void Disconnect()
        {
            _disconnected.Add(this);
            _owner.OpenConnections.Remove(this);
        }
    }

    [Test]
    public void EverySessionOfOneConnectionIsDisconnected()
    {
        var disconnected = new List<ProtocolBase>();
        var connection = new ConnectionInfo { Name = "ssh" };
        var first = new InlineClosingSession(connection, disconnected);
        var duplicate = new InlineClosingSession(connection, disconnected);

        ConnectionContextMenu.DisconnectConnection(connection);

        Assert.That(disconnected, Is.EquivalentTo(new[] { first, duplicate }));
        Assert.That(connection.OpenConnections, Is.Empty);
    }

    [Test]
    public void DisconnectingAFolderReachesSessionsInSubFolders()
    {
        // folder
        // |- direct
        // |- sub
        //    |- nested (two sessions)
        var disconnected = new List<ProtocolBase>();
        var folder = new ContainerInfo { Name = "folder" };
        var sub = new ContainerInfo { Name = "sub" };
        var direct = new ConnectionInfo { Name = "direct" };
        var nested = new ConnectionInfo { Name = "nested" };
        folder.AddChild(direct);
        folder.AddChild(sub);
        sub.AddChild(nested);
        var directSession = new InlineClosingSession(direct, disconnected);
        var nestedSession = new InlineClosingSession(nested, disconnected);
        var nestedDuplicate = new InlineClosingSession(nested, disconnected);

        ConnectionContextMenu.DisconnectConnection(folder);

        Assert.That(disconnected, Is.EquivalentTo(new[] { directSession, nestedSession, nestedDuplicate }));
    }

    [Test]
    [Apartment(ApartmentState.STA)]
    public void DisconnectIsOfferedOnAFolderWhoseOnlySessionIsInASubFolder()
    {
        var folder = new ContainerInfo { Name = "folder" };
        var sub = new ContainerInfo { Name = "sub" };
        var nested = new ConnectionInfo { Name = "nested" };
        folder.AddChild(sub);
        sub.AddChild(nested);
        _ = new InlineClosingSession(nested, new List<ProtocolBase>());

        using var tree = new ConnectionTree();
        using var menu = new ConnectionContextMenu(tree);
        menu.ShowHideMenuItemsForContainer(folder);

        ToolStripItem disconnect = menu.Items.Find("_cMenTreeDisconnect", true).Single();
        Assert.That(disconnect.Enabled, Is.True);
    }
}
