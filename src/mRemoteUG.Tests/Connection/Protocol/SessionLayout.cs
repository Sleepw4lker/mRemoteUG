namespace mRemoteUG.Tests.Connection.Protocol;

/// <summary>One monitor layout sent to an RDP session: its size and its two scale factors.</summary>
internal readonly record struct SessionLayout(Size Size, uint DesktopScale, uint DeviceScale);
