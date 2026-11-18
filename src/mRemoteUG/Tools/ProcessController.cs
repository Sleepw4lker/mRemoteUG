using System.Diagnostics;

using mRemoteUG.Tools.Cmdline;

namespace mRemoteUG.Tools;

public class ProcessController : IDisposable
{
    private readonly Process _process = new Process();

    public bool Start(string fileName, CommandLineArguments? arguments = null)
    {
        _process.StartInfo.UseShellExecute = false;
        _process.StartInfo.FileName = fileName;
        if (arguments != null)
        {
            _process.StartInfo.Arguments = arguments.ToString();
        }

        return _process.Start();
    }

    /// <summary>
    /// Releases this object's handle on the started process. The process itself keeps running.
    /// </summary>
    public void Dispose()
    {
        _process.Dispose();
        GC.SuppressFinalize(this);
    }
}
