using mRemoteUG.Config.Connections;
using mRemoteUG.Connection;

namespace mRemoteUG.App.Initialization;

public class CredsAndConsSetup
{
    public static void LoadCredsAndCons()
    {
        _ = new SaveConnectionsOnEdit(Runtime.ConnectionsService);

        if (Settings.Default.FirstStart
            && !Settings.Default.LoadConsFromCustomLocation
            && !File.Exists(ConnectionsService.GetStartupConnectionFileName()))
        {
            Runtime.ConnectionsService.NewConnectionsFile(ConnectionsService.GetStartupConnectionFileName());
        }

        Runtime.LoadConnections();
    }
}
