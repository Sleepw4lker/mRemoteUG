using mRemoteUG.Tools.Cmdline;

namespace mRemoteUG.Tools
{
	public class PuttyProcessController : ProcessController
	{
		public bool Start(CommandLineArguments arguments = null)
		{
			return Start(PuttyPathProvider.ResolvedPath, arguments);
		}
	}
}
