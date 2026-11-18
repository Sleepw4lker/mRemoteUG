using System.Reflection;
using System.Runtime.InteropServices;

using MSTSCLib;

using mRemoteUG.Connection.Protocol.RDP;

using NUnit.Framework;

namespace mRemoteUG.Tests.Connection.Protocol;

/// <summary>
/// An RDP session as far as the resize path can see one, on a machine with no server.
/// </summary>
/// <remarks>
/// The control set up by <c>Initialize</c> is real. What is stood in for is what only a live
/// session answers: the size the control reports, the window's DPI, and
/// <c>UpdateSessionDisplaySettings</c>, which can be told to refuse with <c>E_UNEXPECTED</c>
/// the way a session measured to do right after a connect or an automatic reconnect. The
/// control's events are raised by calling their handlers, as the control calls them.
/// </remarks>
internal sealed class RdpSessionStandIn : RdpProtocol
{
    private const int EUnexpected = unchecked((int)0x8000FFFF);

    /// <summary>The DPI of the monitor the window is on.</summary>
    public int Dpi { get; set; } = 192;

    /// <summary>What the control reports as the session's size.</summary>
    public Size Reported { get; set; } = new(3462, 1915);

    /// <summary>Whether the session refuses the next layouts with <c>E_UNEXPECTED</c>.</summary>
    public bool RefuseForNow { get; set; }

    /// <summary>Every layout the session was sent, refused or not.</summary>
    public List<SessionLayout> Attempts { get; } = [];

    /// <summary>Every layout the session took.</summary>
    public List<SessionLayout> Taken { get; } = [];

    /// <summary>How often the session was resized by reconnecting it.</summary>
    public int Reconnects { get; private set; }

    public Control? HostedControl => Control;

    public bool LoggedOn => LoginHasCompleted;

    protected override int CurrentDpi() => Dpi;

    protected override Size ReportedDesktopSize(MsRdpClient11NotSafeForScripting client) => Reported;

    protected override void SendSessionDisplaySettings(
        MsRdpClient11NotSafeForScripting client, uint width, uint height, uint desktopScale, uint deviceScale)
    {
        var layout = new SessionLayout(new Size((int)width, (int)height), desktopScale, deviceScale);
        Attempts.Add(layout);
        if (RefuseForNow)
        {
            // As the interop layer turns a failed HRESULT into an exception.
            Marshal.ThrowExceptionForHR(EUnexpected);
        }

        Taken.Add(layout);
    }

    protected override void ReconnectAtSize(MsRdpClient11NotSafeForScripting client, uint width, uint height)
    {
        Reconnects++;
    }

    /// <summary>Raises one of the control's events, by calling its handler.</summary>
    public void Raise(string handler, params object[] arguments)
    {
        MethodInfo method = typeof(RdpProtocol).GetMethod(handler, BindingFlags.Instance | BindingFlags.NonPublic)
                            ?? throw new AssertionException($"RdpProtocol has no {handler}.");
        method.Invoke(this, arguments);
    }

    /// <summary>What a resize of the window, or of the hosted control, asks of the session.</summary>
    public void Resize() => Raise("ReconnectForResize");

    /// <summary>Connects and logs on, the way the control reports a first logon.</summary>
    public void LogOn()
    {
        Raise("RDPEvent_OnConnected");
        Raise("RDPEvent_OnLoginComplete");
    }

    /// <summary>Forgets every call made so far.</summary>
    public void ForgetCalls()
    {
        Attempts.Clear();
        Taken.Clear();
        Reconnects = 0;
    }
}
