using mRemoteUG.UI;

using NUnit.Framework;

namespace mRemoteUG.Tests.UI;

/// <summary>
/// <see cref="DpiScaling.WindowDpi"/> answers the DPI the window is on, not the one a control
/// last heard about.
/// </summary>
/// <remarks>
/// Measured in a lab log: the process started on a 150% panel, the window was docked to a 200%
/// monitor, and a session opened afterwards read <c>Control.DeviceDpi</c> 144 from its brand-new
/// hosted control, so it was created at 150% on a 200% screen. <c>DeviceDpi</c> is a cached value
/// that only DPI-change messages update; a control created after the move never got one.
/// <para>
/// The stale case itself is the self-test's ("Window DPI"): outside a per-monitor V2 process,
/// <c>DeviceDpi</c> always answers the system DPI and cannot be made stale. These pin the fallbacks.
/// </para>
/// </remarks>
[TestFixture]
[Apartment(ApartmentState.STA)]
public class WindowDpiTests
{
    [Test]
    public void WithoutAWindowTheCachedDpiIsAllThereIs()
    {
        using var control = new Control();

        Assert.That(DpiScaling.WindowDpi(control), Is.EqualTo(control.DeviceDpi));
    }

    [Test]
    public void WithNoControlItIsTheDefault()
    {
        Assert.That(DpiScaling.WindowDpi(null), Is.EqualTo(DpiScaling.DefaultDpi));
    }
}
