using mRemoteUG.App;
using mRemoteUG.Messages;
using mRemoteUG.UI.Forms;
using mRemoteUG.UI.Forms.Input;
using mRemoteUG.UI.Window;

namespace mRemoteUG.UI.Panels;

public class PanelAdder
{
    public static Form? AddPanel(string title = "", bool noTabber = false)
    {
        try
        {
            var connectionForm = new ConnectionWindow();
            BuildConnectionWindowContextMenu(connectionForm);
            SetConnectionWindowTitle(title, connectionForm);
            ShowConnectionWindow(connectionForm);
            PrepareTabControllerSupport(noTabber, connectionForm);
            return connectionForm;
        }
        catch (Exception ex)
        {
            Runtime.MessageCollector.AddMessage(
                MessageClass.ErrorMsg,
                "Couldn\'t add panel" + Environment.NewLine + ex.Message);
            return null;
        }
    }

    public static bool DoesPanelExist(string panelName)
    {
        return Runtime.WindowList?.OfType<ConnectionWindow>().Any(w => w.TabText == panelName)
            ?? false;
    }

    private static void ShowConnectionWindow(ConnectionWindow connectionForm)
    {
        FrmMain.Default.Layout.AddDocument(connectionForm);
    }

    private static void PrepareTabControllerSupport(bool noTabber, ConnectionWindow connectionForm)
    {
        if (noTabber)
        {
            connectionForm.TabController.Dispose();
        }
        else
        {
            (Runtime.WindowList ?? throw new InvalidOperationException("The main form has no window list yet."))
                .Add(connectionForm);
        }
    }

    private static void SetConnectionWindowTitle(string title, ConnectionWindow connectionForm)
    {
        if (title == "")
        {
            title = Language.strNewPanel;
        }

        connectionForm.SetFormText(title.Replace("&", "&&"));
    }

    /// <summary>
    /// Menu shown when a connection panel tab is right-clicked. MainLayout resolves
    /// the tab under the cursor and shows this menu.
    /// </summary>
    private static void BuildConnectionWindowContextMenu(ConnectionWindow pnlcForm)
    {
        var cMen = new ContextMenuStrip();
        cMen.Items.Add(CreateRenameMenuItem(pnlcForm));
        pnlcForm.OwnContextMenu(cMen);
    }

    private static ToolStripMenuItem CreateRenameMenuItem(ConnectionWindow pnlcForm)
    {
        var cMenRen = new ToolStripMenuItem
        {
            Text = Language.strRename,
            Image = Resources.Rename,
            Tag = pnlcForm
        };
        cMenRen.Click += CMenConnectionPanelRename_Click;
        return cMenRen;
    }

    private static void CMenConnectionPanelRename_Click(object? sender, EventArgs e)
    {
        try
        {
            // Only the rename item built above is wired here, and it is tagged with its window.
            if (sender is not ToolStripMenuItem { Tag: ConnectionWindow conW })
            {
                return;
            }

            RenamePanel(conW, AskForTitle);
        }
        catch (Exception ex)
        {
            Runtime.MessageCollector.AddExceptionMessage("cMenConnectionPanelRename_Click: Caught Exception: ", ex);
        }
    }

    /// <summary>
    /// Asks for a panel's new title, offering its current one as the user typed it, and applies
    /// a non-empty answer. <paramref name="askForTitle"/> gets the prefill and returns the answer,
    /// or <see langword="null"/> for Cancel; it is a parameter so a test can stand in for the
    /// modal dialog.
    /// </summary>
    internal static void RenamePanel(ConnectionWindow panel, Func<string, string?> askForTitle)
    {
        // The tab text carries '&' doubled, so the strip does not read it as a mnemonic.
        string? newTitle = askForTitle(panel.TabText.Replace("&&", "&", StringComparison.Ordinal));
        if (!string.IsNullOrEmpty(newTitle))
        {
            panel.SetFormText(newTitle.Replace("&", "&&", StringComparison.Ordinal));
        }
    }

    private static string? AskForTitle(string current)
    {
        // Cancel must answer null: the text box now starts filled, and an edit the user then
        // cancelled would otherwise still rename the panel.
        string title = current;
        return InputDialog.InputBox(Language.strNewTitle, Language.strNewTitle + ":", ref title) == DialogResult.OK
            ? title
            : null;
    }
}
