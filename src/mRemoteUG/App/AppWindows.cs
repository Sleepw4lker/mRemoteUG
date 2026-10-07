using mRemoteUG.UI.Window;

namespace mRemoteUG.App
{
    public static class AppWindows
    {
        private static ConnectionTreeWindow _treeForm;

        internal static ConnectionTreeWindow TreeForm
        {
            get => _treeForm ?? (_treeForm = new ConnectionTreeWindow());
            set => _treeForm = value;
        }

        internal static ConfigWindow ConfigForm { get; set; } = new ConfigWindow();
        internal static ErrorAndInfoWindow ErrorsForm { get; set; } = new ErrorAndInfoWindow();
    }
}
