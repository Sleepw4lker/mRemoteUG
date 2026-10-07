using System.Configuration;

namespace mRemoteUG.Config.Settings.Providers
{
    /// <summary>
    /// The settings provider applied to <see cref="Settings"/> via
    /// <see cref="SettingsProviderAttribute"/>. Kept as a named type so the attribute on the
    /// partial class has something stable to point at.
    /// </summary>
    public class ChooseProvider : LocalFileSettingsProvider
    {
    }
}
