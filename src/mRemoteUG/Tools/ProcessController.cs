using System.Diagnostics;
using mRemoteUG.Tools.Cmdline;

namespace mRemoteUG.Tools
{
	public class ProcessController
	{
		private readonly Process Process = new Process();

		public bool Start(string fileName, CommandLineArguments arguments = null)
		{
			Process.StartInfo.UseShellExecute = false;
			Process.StartInfo.FileName = fileName;
			if (arguments != null)
				Process.StartInfo.Arguments = arguments.ToString();

			return Process.Start();
		}
	}
}
