using System.Globalization;

using mRemoteUG.Connection;
using mRemoteUG.Connection.Protocol;
using mRemoteUG.Container;
using mRemoteUG.Messages;
using mRemoteUG.Tests.TestHelpers;

using NUnit.Framework;

namespace mRemoteUG.Tests.Connection;

/// <summary>
/// The parts of opening a Connection that are decided before any window is touched: refusing
/// a Connection with no host name, and which panel a Session goes into when the Connection
/// names one itself.
/// </summary>
/// <remarks>
/// Switching to an open Session and choosing the window need a seam over <c>FrmMain.Default</c>
/// and <c>Runtime.WindowList</c> (#122); the panel chooser is a modal dialog. Neither is
/// reached here. These change Settings.Default in memory only and put it back afterwards.
/// </remarks>
[TestFixture]
public class ConnectionInitiatorTests
{
    /// <summary>A protocol no build supports, so the factory refuses it before any UI exists.</summary>
    private const ProtocolType UnsupportedProtocol = (ProtocolType)int.MaxValue;

    private bool _alwaysShowPanelSelectionDlg;

    [SetUp]
    public void CaptureSettings()
    {
        _alwaysShowPanelSelectionDlg = mRemoteUG.Settings.Default.AlwaysShowPanelSelectionDlg;
        mRemoteUG.Settings.Default.AlwaysShowPanelSelectionDlg = false;
    }

    [TearDown]
    public void RestoreSettings()
    {
        mRemoteUG.Settings.Default.AlwaysShowPanelSelectionDlg = _alwaysShowPanelSelectionDlg;
    }

    private static IReadOnlyList<IMessage> NoHostnameWarnings(MessageRecorder recorder)
    {
        return recorder.Matching(MessageClass.WarningMsg, Language.strConnectionOpenFailedNoHostname);
    }

    [TestCase("")]
    [TestCase(null)]
    [TestCase("   ")]
    public void AConnectionWithoutAHostNameIsRefused(string hostname)
    {
        var initiator = new ConnectionInitiator();
        var connection = new ConnectionInfo { Hostname = hostname };
        using var recorder = new MessageRecorder();

        initiator.OpenConnection(connection);

        Assert.Multiple(() =>
        {
            Assert.That(NoHostnameWarnings(recorder), Has.Count.EqualTo(1));
            Assert.That(recorder.Messages, Has.Count.EqualTo(1));
            Assert.That(connection.OpenConnections, Is.Empty);
            Assert.That(initiator.ActiveConnections, Is.Empty);
        });
    }

    [TestCase(ConnectionInfo.Force.None)]
    [TestCase(ConnectionInfo.Force.DoNotJump)]
    [TestCase(ConnectionInfo.Force.OverridePanel)]
    public void TheRefusalComesBeforeAnyForceIsLookedAt(ConnectionInfo.Force force)
    {
        var initiator = new ConnectionInitiator();
        var connection = new ConnectionInfo { Hostname = "" };
        using var recorder = new MessageRecorder();

        initiator.OpenConnection(connection, force);

        Assert.Multiple(() =>
        {
            Assert.That(NoHostnameWarnings(recorder), Has.Count.EqualTo(1));
            Assert.That(recorder.Messages, Has.Count.EqualTo(1));
            Assert.That(initiator.ActiveConnections, Is.Empty);
        });
    }

    /// <summary>
    /// The other side of the refusal: a Connection with a host name goes on to the protocol.
    /// An unsupported protocol stops it there, before any window is needed.
    /// </summary>
    [Test]
    public void AConnectionWithAHostNameGoesOnToItsProtocol()
    {
        var initiator = new ConnectionInitiator();
        var connection = new ConnectionInfo { Hostname = "server01", Protocol = UnsupportedProtocol };
        using var recorder = new MessageRecorder();

        initiator.OpenConnection(connection);

        Assert.Multiple(() =>
        {
            Assert.That(NoHostnameWarnings(recorder), Is.Empty);
            Assert.That(recorder.Matching(MessageClass.ErrorMsg, NotSupported(UnsupportedProtocol)),
                        Has.Count.EqualTo(1));
            Assert.That(recorder.Messages, Has.Count.EqualTo(1));
            Assert.That(connection.OpenConnections, Is.Empty);
            Assert.That(initiator.ActiveConnections, Is.Empty);
        });
    }

    /// <summary>The protocol-not-supported error names the protocol, here only by its number.</summary>
    private static string NotSupported(ProtocolType protocol)
    {
        return ((int)protocol).ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Nothing to switch to: a Connection without an open Session is answered without looking
    /// for its window at all.
    /// </summary>
    [Test]
    public void ThereIsNothingToSwitchToForAConnectionWithoutASession()
    {
        var initiator = new ConnectionInitiator();
        var connection = new ConnectionInfo { Hostname = "server01" };

        bool switched = initiator.SwitchToOpenConnection(connection);

        Assert.That(switched, Is.False);
    }

    [Test]
    public void OpeningAFolderOpensEveryConnectionInItAndInItsSubfolders()
    {
        var initiator = new ConnectionInitiator();
        var root = new ContainerInfo();
        var subfolder = new ContainerInfo();
        root.AddChild(new ConnectionInfo { Hostname = "" });
        root.AddChild(subfolder);
        subfolder.AddChild(new ConnectionInfo { Hostname = "" });
        subfolder.AddChild(new ConnectionInfo { Hostname = "" });
        using var recorder = new MessageRecorder();

        initiator.OpenConnection(root);

        Assert.Multiple(() =>
        {
            Assert.That(NoHostnameWarnings(recorder), Has.Count.EqualTo(3));
            Assert.That(recorder.Messages, Has.Count.EqualTo(3));
        });
    }

    [Test]
    public void OpeningAnEmptyFolderDoesNothing()
    {
        var initiator = new ConnectionInitiator();
        using var recorder = new MessageRecorder();

        initiator.OpenConnection(new ContainerInfo(), ConnectionInfo.Force.None);

        Assert.That(recorder.Messages, Is.Empty);
    }

    [TestCase("Servers", ConnectionInfo.Force.None)]
    [TestCase("Servers", ConnectionInfo.Force.DoNotJump)]
    [TestCase("Servers", ConnectionInfo.Force.Fullscreen | ConnectionInfo.Force.NoCredentials)]
    [TestCase("General", ConnectionInfo.Force.None)]
    public void AConnectionWithItsOwnPanelOpensInIt(string panel, ConnectionInfo.Force force)
    {
        var connection = new ConnectionInfo { Panel = panel };

        string? chosen = ConnectionInitiator.SetConnectionPanel(connection, force);

        Assert.That(chosen, Is.EqualTo(panel));
    }

    [Test]
    public void ANewConnectionOpensInTheDefaultPanel()
    {
        var connection = new ConnectionInfo();

        string? chosen = ConnectionInitiator.SetConnectionPanel(connection, ConnectionInfo.Force.None);

        Assert.That(chosen, Is.EqualTo(Language.strGeneral));
    }

    [Test]
    public void AnInheritedPanelIsTheOneUsed()
    {
        var folder = new ContainerInfo { Panel = "From the folder" };
        var connection = new ConnectionInfo { Panel = "Own" };
        folder.AddChild(connection);
        connection.Inheritance.Panel = true;

        string? chosen = ConnectionInitiator.SetConnectionPanel(connection, ConnectionInfo.Force.None);

        Assert.That(chosen, Is.EqualTo("From the folder"));
    }
}
