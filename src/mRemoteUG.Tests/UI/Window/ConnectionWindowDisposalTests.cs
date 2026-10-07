using System.Threading;
using System.Windows.Forms;
using mRemoteUG.Connection;
using mRemoteUG.Connection.Protocol;
using mRemoteUG.UI.Window;
using NUnit.Framework;

namespace mRemoteUG.Tests.UI.Window
{
    /// <summary>
    /// What a Panel and its Tabs have to give back when they go.
    /// </summary>
    /// <remarks>
    /// Neither of these is a large number of bytes. Both are Windows handles, which are the kind
    /// of resource a long-running session manager runs out of rather than merely wastes:
    /// <c>TabPageCollection.Remove</c> only unparents, so a Tab closed without a dispose keeps its
    /// window handle, and <c>Form.Dispose</c> does not reach into a designer's
    /// <c>components</c> container by itself.
    /// </remarks>
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class ConnectionWindowDisposalTests
    {
        private sealed class QuietProtocol : ProtocolBase
        {
            public QuietProtocol() : base("quiet")
            {
                Control = new Panel();
            }

            protected override void CleanupProtocolResources()
            {
            }
        }

        [Test]
        public void ClosingATabDisposesItsPage()
        {
            // Removing a page from the collection does not dispose it - WinForms reparents the
            // page's live HWND to the thread's parking window instead, where it stays until the
            // finalizer gets to it. One per Session closed, for the lifetime of the process.
            using var window = new ConnectionWindow("disposal panel");
            window.Show();

            var closing = HostATab(window, "server1");

            // A second Tab, so the close does not take the whole Panel with it and the assertion
            // is about the page rather than about the form's cascade.
            HostATab(window, "server2");

            var page = closing.InterfaceControl.Parent as TabPage;
            Assert.That(page, Is.Not.Null, "The protocol was not hosted in a tab page.");

            window.Prot_Event_Closed(closing);
            Pump();

            Assert.Multiple(() =>
            {
                Assert.That(window.TabController.TabPages, Does.Not.Contain(page),
                            "The page was not removed from the strip at all.");
                Assert.That(page.IsDisposed, Is.True,
                            "The closed tab page was removed but never disposed, so its window "
                            + "handle outlives the Session it was showing.");
            });
        }

        [Test]
        public void ClosingATabDoesNotDisposeTheSharedTabIcon()
        {
            // The icon comes from ConnectionIcon's (name, size) cache, which exists precisely so
            // there is no HICON per Tab. Disposing the page must not reach it, or the second Tab
            // to use that icon draws nothing.
            using var window = new ConnectionWindow("icon panel");
            window.Show();

            var closing = HostATab(window, "server1");
            HostATab(window, "server2");

            var page = (ConnectionTabPage)closing.InterfaceControl.Parent;
            var icon = page.Icon;

            window.Prot_Event_Closed(closing);
            Pump();

            Assume.That(icon, Is.Not.Null, "No icon was assigned, so there is nothing to protect.");
            Assert.That(icon.Handle, Is.Not.EqualTo(System.IntPtr.Zero),
                        "The shared tab icon was disposed along with the page.");
        }

        [Test]
        public void DisposingThePanelDisposesItsContextMenu()
        {
            // cmenTab lives in the designer's components container, and Form.Dispose does not
            // dispose that container unless the generated Dispose(bool) override does it. The
            // menu carries fifteen image-bearing items.
            var window = new ConnectionWindow("menu panel");
            var menu = window.cmenTab;
            Assume.That(menu, Is.Not.Null);

            window.Dispose();

            Assert.That(menu.IsDisposed, Is.True,
                        "The panel's context menu survived the panel, so its items and their "
                        + "images leak once per Panel opened.");
        }


        [Test]
        public void AContextMenuThePanelOwnsGoesWhenThePanelDoes()
        {
            // PanelAdder builds a rename menu per panel and used to assign it straight to
            // ContextMenuStrip, which does not transfer ownership - Control.Dispose leaves it alone.
            var window = new ConnectionWindow("owned menu panel");
            var menu = new ContextMenuStrip();
            menu.Items.Add(new ToolStripMenuItem("Rename"));

            window.OwnContextMenu(menu);
            Assume.That(window.ContextMenuStrip, Is.SameAs(menu), "The menu was not assigned.");

            window.Dispose();

            Assert.That(menu.IsDisposed, Is.True,
                        "The panel's own context menu survived it.");
        }

        private static QuietProtocol HostATab(ConnectionWindow window, string name)
        {
            var connectionInfo = new ConnectionInfo { Name = name };
            var tabPage = window.AddConnectionTab(connectionInfo);
            Assert.That(tabPage, Is.Not.Null, "AddConnectionTab returned no tab page.");

            var protocol = new QuietProtocol();
            protocol.InterfaceControl = new InterfaceControl(tabPage, protocol, connectionInfo);
            Assert.That(protocol.Initialize(), Is.True, "ProtocolBase.Initialize() failed.");
            return protocol;
        }

        private static void Pump()
        {
            for (var i = 0; i < 40; i++)
            {
                Application.DoEvents();
                Thread.Sleep(5);
            }
        }
    }
}
