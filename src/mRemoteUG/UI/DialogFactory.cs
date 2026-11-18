using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using mRemoteUG.App;
using mRemoteUG.App.Info;
using mRemoteUG.Messages;
using mRemoteUG.UI.TaskDialog;

namespace mRemoteUG.UI;

public class DialogFactory
{
    public static OpenFileDialog BuildLoadConnectionsDialog()
    {
        return new OpenFileDialog
        {
            Title = "",
            CheckFileExists = true,
            InitialDirectory = ConnectionsFileInfo.DefaultConnectionsPath,
            Filter = Language.strFiltermRemoteXML + @"|*.xml|" + Language.strFilterAll + @"|*.*"
        };
    }

    /// <summary>
    /// Creates and shows a dialog to either create a new connections file, load a different one,
    /// exit, or optionally cancel the operation.
    /// </summary>
    /// <param name="connectionFileName"></param>
    /// <param name="messageText"></param>
    /// <param name="showCancelButton"></param>
    [SuppressMessage("Performance", "CA1863",
                     Justification = "The format is a localized resource read at run time; "
                                     + "a cached CompositeFormat would freeze the UI language.")]
    public static void ShowLoadConnectionsFailedDialog(
        string connectionFileName,
        string messageText,
        bool showCancelButton)
    {
        var commandButtons = new List<string>
        {
            Language.ConfigurationCreateNew,
            Language.strOpenADifferentFile,
            Language.strMenuExit
        };

        if (showCancelButton)
        {
            commandButtons.Add(Language.strButtonCancel);
        }

        bool answered = false;
        while (!answered)
        {
            try
            {
                CTaskDialog.ShowTaskDialogBox(
                    GeneralAppInfo.ProductName,
                    messageText,
                    "", "", "", "", "",
                    string.Join(" | ", commandButtons),
                    ETaskDialogButtons.None,
                    ESysIcons.Question,
                    ESysIcons.Question);

                bool chosenExit = false;
                switch (CTaskDialog.CommandButtonResult)
                {
                    case 0: // New
                        using (SaveFileDialog saveAsDialog = ConnectionsSaveAsDialog())
                        {
                            if (saveAsDialog.ShowDialog() == DialogResult.OK)
                            {
                                Runtime.ConnectionsService.NewConnectionsFile(saveAsDialog.FileName);
                            }
                        }
                        break;
                    case 1: // Load
                        Runtime.LoadConnections(true);
                        break;
                    case 2: // Exit
                        Application.Exit();
                        chosenExit = true;
                        break;
                    case 3: // Cancel, only offered when a file is already loaded, so the model is there
                        break;
                }

                answered = StartupDialogIsAnswered(
                    chosenExit,
                    Runtime.ConnectionsService.ConnectionTreeModel != null);
            }
            catch (Exception exception)
            {
                Runtime.MessageCollector.AddExceptionMessage(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Language.strConnectionsFileCouldNotBeLoadedNew,
                        connectionFileName),
                    exception,
                    MessageClass.WarningMsg);
            }
        }
    }

    /// <summary>
    /// Whether a dialog that asks what to do about an unusable connection file may close. It
    /// may only once a connection model is loaded, or the user chose Exit: a cancelled open or
    /// save dialog, or a failed create or load, must ask again, because after startup the
    /// application never runs without a connection model (the File menu's import and export
    /// commands rely on it).
    /// </summary>
    internal static bool StartupDialogIsAnswered(bool chosenExit, bool modelLoaded)
    {
        return chosenExit || modelLoaded;
    }

    /// <summary>
    /// Creates a new dialog that allows the user to select an mRemoteUG
    /// connections file path. Don't forget to dispose the dialog when you
    /// are done!
    /// </summary>
    public static SaveFileDialog ConnectionsSaveAsDialog()
    {
        return new SaveFileDialog
        {
            CheckPathExists = true,
            InitialDirectory = ConnectionsFileInfo.DefaultConnectionsPath,
            FileName = ConnectionsFileInfo.DefaultConnectionsFile,
            OverwritePrompt = true,
            Filter = Language.strFiltermRemoteXML + @"|*.xml|" + Language.strFilterAll + @"|*.*"
        };
    }
}
