using System.Configuration;

namespace mRemoteUG
{
    /// <summary>
    /// Hand-written half of the generated settings class. It exists only to carry the
    /// SettingsProvider attribute; the properties themselves are in Settings.Designer.cs.
    /// </summary>
    [SettingsProvider(typeof(mRemoteUG.Config.Settings.Providers.ChooseProvider))]
    internal sealed partial class Settings
    {
    }
}
