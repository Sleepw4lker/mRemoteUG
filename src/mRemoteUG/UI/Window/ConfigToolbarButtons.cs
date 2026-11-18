namespace mRemoteUG.UI.Window;

/// <summary>
/// The six buttons <see cref="ConfigWindow"/> adds to its property grid's toolbar.
/// </summary>
/// <remarks>
/// The first four pick what the grid shows, and at most one of them is checked at a time.
/// The last two are never checked, only enabled or not.
/// </remarks>
[Flags]
internal enum ConfigToolbarButtons
{
    None = 0,
    Properties = 1,
    Inheritance = 2,
    DefaultProperties = 4,
    DefaultInheritance = 8,
    Icon = 16,
    HostStatus = 32,
}
