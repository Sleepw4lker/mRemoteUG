using mRemoteUG.UI.Window;

namespace mRemoteUG.App;

public static class AppWindows
{
    private static ConnectionTreeWindow? s_treeForm;

    internal static ConnectionTreeWindow TreeForm
    {
        get => s_treeForm ?? (s_treeForm = new ConnectionTreeWindow());
        set => s_treeForm = value;
    }

    internal static ConfigWindow ConfigForm { get; set; } = new ConfigWindow();
    internal static ErrorAndInfoWindow ErrorsForm { get; set; } = new ErrorAndInfoWindow();
}
