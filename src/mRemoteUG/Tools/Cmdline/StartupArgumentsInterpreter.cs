using mRemoteUG.App.Info;
using mRemoteUG.Messages;


namespace mRemoteUG.Tools.Cmdline;

public class StartupArgumentsInterpreter
{
    private readonly MessageCollector _messageCollector;

    public StartupArgumentsInterpreter(MessageCollector messageCollector)
    {
        ArgumentNullException.ThrowIfNull(messageCollector);

        _messageCollector = messageCollector;
    }

    public void ParseArguments(IEnumerable<string> cmdlineArgs)
    {
        _messageCollector.AddMessage(MessageClass.DebugMsg, "Parsing cmdline arguments");

        try
        {
            var args = new CmdArgumentsInterpreter(cmdlineArgs);

            ParseResetPositionArg(args);
            ParseResetPanelsArg(args);
            ParseResetToolbarArg(args);
            ParseNoReconnectArg(args);
            ParseCustomConnectionPathArg(args);
        }
        catch (Exception ex)
        {
            _messageCollector.AddExceptionMessage(Language.strCommandLineArgsCouldNotBeParsed, ex, logOnly: false);
        }
    }

    private void ParseResetPositionArg(CmdArgumentsInterpreter args)
    {
        if (args["resetpos"] == null && args["rp"] == null && args["reset"] == null)
        {
            return;
        }

        _messageCollector.AddMessage(MessageClass.DebugMsg, "Cmdline arg: Resetting window positions.");
        Settings.Default.MainFormKiosk = false;
        int newWidth = 900;
        int newHeight = 600;
        // Without a primary screen this threw a NullReferenceException at the same point, which
        // ParseArguments catches and reports; it still does, now saying what was missing.
        Rectangle workingArea = (Screen.PrimaryScreen
                                 ?? throw new InvalidOperationException("There is no primary screen to centre on."))
            .WorkingArea;
        int newX = workingArea.Width / 2 - newWidth / 2;
        int newY = workingArea.Height / 2 - newHeight / 2;
        Settings.Default.MainFormLocation = new Point(newX, newY);
        Settings.Default.MainFormSize = new Size(newWidth, newHeight);
        Settings.Default.MainFormState = FormWindowState.Normal;
    }

    private void ParseResetPanelsArg(CmdArgumentsInterpreter args)
    {
        if (args["resetpanels"] == null && args["rpnl"] == null && args["reset"] == null)
        {
            return;
        }

        _messageCollector.AddMessage(MessageClass.DebugMsg, "Cmdline arg: Resetting panels");
        Settings.Default.ResetPanels = true;
    }

    private void ParseResetToolbarArg(CmdArgumentsInterpreter args)
    {
        if (args["resettoolbar"] == null && args["rtbr"] == null && args["reset"] == null)
        {
            return;
        }

        _messageCollector.AddMessage(MessageClass.DebugMsg, "Cmdline arg: Resetting toolbar position");
        Settings.Default.ResetToolbars = true;
    }

    private void ParseNoReconnectArg(CmdArgumentsInterpreter args)
    {
        if (args["noreconnect"] == null && args["norc"] == null)
        {
            return;
        }

        _messageCollector.AddMessage(
            MessageClass.DebugMsg,
            "Cmdline arg: Disabling reconnection to previously connected hosts");
        Settings.Default.NoReconnect = true;
    }

    private void ParseCustomConnectionPathArg(CmdArgumentsInterpreter args)
    {
        // -c wins over --cons when both are given.
        string? consPath = args["c"] ?? args["cons"];
        if (consPath == null)
        {
            return;
        }

        _messageCollector.AddMessage(MessageClass.DebugMsg, "Cmdline arg: loading connections from a custom path");
        if (File.Exists(consPath) == false)
        {
            if (File.Exists(Path.Combine(GeneralAppInfo.HomePath, consPath)))
            {
                Settings.Default.LoadConsFromCustomLocation = true;
                Settings.Default.CustomConsPath = Path.Combine(GeneralAppInfo.HomePath, consPath);
                return;
            }
            if (!File.Exists(Path.Combine(ConnectionsFileInfo.DefaultConnectionsPath, consPath)))
            {
                return;
            }

            Settings.Default.LoadConsFromCustomLocation = true;
            Settings.Default.CustomConsPath = Path.Combine(ConnectionsFileInfo.DefaultConnectionsPath, consPath);
        }
        else
        {
            Settings.Default.LoadConsFromCustomLocation = true;
            Settings.Default.CustomConsPath = consPath;
        }
    }
}
