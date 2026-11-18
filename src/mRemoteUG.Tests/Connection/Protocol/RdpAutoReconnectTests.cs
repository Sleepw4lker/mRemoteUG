using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using mRemoteUG.App;
using mRemoteUG.Connection;
using mRemoteUG.Connection.Protocol.RDP;
using mRemoteUG.UI.Window;

using NUnit.Framework;

namespace mRemoteUG.Tests.Connection.Protocol;

/// <summary>
/// The RDP control's own reconnect reaches the log, and nothing else.
/// </summary>
/// <remarks>
/// Reported from a lab run: a session dropped, the control showed its own "trying to reconnect"
/// screen, and the log had nothing about it at all. The control raises
/// <c>OnAutoReconnecting2</c> and <c>OnAutoReconnected</c>; nothing here could produce them
/// without a server, so the handlers are called the way the control calls them, against a real
/// RDP control set up by <c>Initialize</c>.
/// </remarks>
[TestFixture]
[Apartment(ApartmentState.STA)]
[SuppressMessage("Design", "CA1001",
    Justification = "[TearDown] disposes _window after every test. "
        + "NUnit would dispose an IDisposable fixture once, after its last test.")]
public class RdpAutoReconnectTests
{
    private ConnectionWindow _window = null!;
    private RdpSessionStandIn _protocol = null!;

    [SetUp]
    public void Setup()
    {
        _window = new ConnectionWindow("auto-reconnect panel");
        _window.Show();
        var info = new ConnectionInfo { Name = "server3", Hostname = "127.0.0.4" };
        TabPage tabPage = _window.AddConnectionTab(info)
            ?? throw new AssertionException("AddConnectionTab returned no tab page.");
        _protocol = new RdpSessionStandIn();
        _protocol.InterfaceControl = new InterfaceControl(tabPage, _protocol, info);
        Assert.That(_protocol.Initialize(), Is.True,
                    "RdpProtocol.Initialize() failed - no RDP ActiveX control could be created.");
    }

    [TearDown]
    public void TearDown()
    {
        _window?.Dispose();
    }

    [Test]
    public void AReconnectAttemptIsLoggedWithItsReasonAndCount()
    {
        string[] logged = Logged(() => Raise("RDPEvent_OnAutoReconnecting2", 264, true, 2, 20));

        Assert.That(logged, Has.Some.Contains("auto-reconnect").And.Contains("discReason=264")
                                    .And.Contains("attempt 2 of 20").And.Contains("network available"));
    }

    /// <summary>
    /// A successful reconnect is logged, and sends nothing to the session.
    /// </summary>
    /// <remarks>
    /// Measured: sending size and scale from here ran before the display channel was open, and
    /// the reconnect it fell back to dropped the session again, more than thirty times. The logon
    /// that follows does the scale.
    /// </remarks>
    [Test]
    public void ASuccessfulReconnectIsLoggedAndSendsNothing()
    {
        // Logged on first: before logon every resize stops at the logon guard, and this test
        // passed whatever the handler did.
        _protocol.LogOn();
        Assert.That(_protocol.LoggedOn, Is.True, "The session did not reach a completed logon.");
        _protocol.ForgetCalls();

        string[] logged = Logged(() => Raise("RDPEvent_OnAutoReconnected"));

        Assert.Multiple(() =>
        {
            Assert.That(_protocol.Attempts, Is.Empty, "The reconnect sent a layout to the session.");
            Assert.That(_protocol.Reconnects, Is.Zero, "The reconnect was answered with another one.");
            Assert.That(logged, Has.Some.Contains("auto-reconnect").And.Contains("reconnected"));
            Assert.That(logged, Has.None.Contains("Resiz").Or.Contains("Display scale"));
        });
    }

    private void Raise(string handler, params object[] arguments)
    {
        MethodInfo method = typeof(RdpProtocol).GetMethod(handler, BindingFlags.Instance | BindingFlags.NonPublic)
                            ?? throw new AssertionException($"RdpProtocol has no {handler}.");
        method.Invoke(_protocol, arguments);
    }

    private static string[] Logged(Action action)
    {
        int before = Runtime.MessageCollector.Messages.Count();
        action();
        return Runtime.MessageCollector.Messages.Skip(before).Select(m => m.Text).ToArray();
    }
}
