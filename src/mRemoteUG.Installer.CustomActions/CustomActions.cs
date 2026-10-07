using WixToolset.Dtf.WindowsInstaller;

namespace mRemoteUG.Installer.CustomActions
{
    public class CustomActions
    {
        /// <summary>
        /// mRemoteUG targets net10.0-windows and is deployed framework-dependent, so the .NET 10
        /// Windows Desktop Runtime has to be present. Replaces the old .NET Framework 4.0 check.
        /// </summary>
        [CustomAction]
        public static ActionResult IsDotNetDesktopRuntimeInstalled(Session session)
        {
            session.Log("Begin IsDotNetDesktopRuntimeInstalled");
            var required = int.Parse(session["REQUIREDDOTNETMAJORVERSION"]);
            new DesktopRuntimeChecker(session).Execute(required, "DOTNET_DESKTOP_RUNTIME_INSTALLED");
            return ActionResult.Success;
        }
    }
}