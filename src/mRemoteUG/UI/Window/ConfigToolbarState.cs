using mRemoteUG.Connection;

namespace mRemoteUG.UI.Window;

/// <summary>
/// What <see cref="ConfigWindow"/> shows for one selected object: which toolbar buttons are
/// enabled and checked, what goes into the property grid, and whose icon the icon button
/// shows.
/// </summary>
/// <param name="Enabled">The buttons that are enabled; the others are disabled.</param>
/// <param name="Checked">The view buttons that are checked; the others are unchecked.</param>
/// <param name="ReplacesGridObject">
/// Whether the grid gets <paramref name="GridObject"/>. When false, it keeps what it shows.
/// </param>
/// <param name="GridObject">The object the grid shows, when it gets a new one.</param>
/// <param name="IconSource">The connection whose icon the icon button shows, or null for none.</param>
internal sealed record ConfigToolbarState(
    ConfigToolbarButtons Enabled,
    ConfigToolbarButtons Checked,
    bool ReplacesGridObject,
    object? GridObject,
    ConnectionInfo? IconSource);
