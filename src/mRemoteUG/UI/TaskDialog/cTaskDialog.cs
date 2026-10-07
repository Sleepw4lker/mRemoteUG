using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace mRemoteUG.UI.TaskDialog
{
    #region Enums

    public enum ESysIcons
    {
        Information,
        Question,
        Warning,
        Error
    }

    public enum ETaskDialogButtons
    {
        YesNo,
        YesNoCancel,
        OkCancel,
        Ok,
        Close,
        Cancel,
        None
    }

    #endregion

    /// <summary>
    /// The application's message box, on top of the task dialog Windows itself provides.
    /// </summary>
    /// <remarks>
    /// This used to be a hand-built WinForms window - <c>frmTaskDialog</c>, its designer, its resx
    /// and a custom-painted <c>CommandButton</c>, about 1,450 lines - that positioned every control
    /// by hand and accumulated its own height from pixel literals. That is the only kind of layout
    /// that can put two controls on top of each other, and under per-monitor DPI it did: the
    /// literals scaled with the DPI while the controls in them scaled with the font, and those two
    /// factors do not match.
    /// <para>
    /// <see cref="System.Windows.Forms.TaskDialog"/> is the comctl32 dialog the rest of Windows
    /// uses. It is laid out and drawn by the OS, so it is per-monitor DPI-correct for free and
    /// there is no layout here left to maintain or to test.
    /// </para>
    /// <para>
    /// It needs visual styles and the Common Controls v6 assembly, both of which this application
    /// already guarantees: <c>ProgramRoot.Main</c> calls <c>EnableVisualStyles</c> and
    /// <c>Properties\app.manifest</c> carries the dependency, which is there for this.
    /// </para>
    /// </remarks>
    public static class CTaskDialog
    {
        /// <summary>Whether the verification check box was ticked when the dialog closed.</summary>
        public static bool VerificationChecked;

        /// <summary>Index of the selected radio button, or -1 when there were none.</summary>
        public static int RadioButtonResult = -1;

        /// <summary>Index of the clicked command link, or -1 when none was clicked.</summary>
        public static int CommandButtonResult = -1;

        /// <summary>Separates one command link, or one radio button, from the next.</summary>
        private const char ItemSeparator = '|';

        /// <summary>Separates a command link's label from its description.</summary>
        private const char DescriptionSeparator = '\n';

        private static readonly TaskDialogIcon QuestionIcon = new TaskDialogIcon(SystemIcons.Question);

        #region ShowTaskDialogBox

        public static DialogResult ShowTaskDialogBox(IWin32Window owner,
            string title,
            string mainInstruction,
            string content,
            string expandedInfo,
            string footer,
            string verificationText,
            string radioButtons,
            string commandButtons,
            ETaskDialogButtons buttons,
            ESysIcons mainIcon,
            ESysIcons footerIcon,
            int defaultIndex)
        {
            VerificationChecked = false;
            RadioButtonResult = -1;
            CommandButtonResult = -1;

            var page = BuildPage(title, mainInstruction, content, expandedInfo, footer, verificationText,
                                 radioButtons, commandButtons, buttons, mainIcon, footerIcon, defaultIndex);

            var clicked = owner == null
                ? System.Windows.Forms.TaskDialog.ShowDialog(page)
                : System.Windows.Forms.TaskDialog.ShowDialog(owner, page);

            VerificationChecked = page.Verification?.Checked ?? false;

            RadioButtonResult = page.RadioButtons.Count == 0
                ? -1
                : page.RadioButtons.ToList().FindIndex(radio => radio.Checked);

            CommandButtonResult = clicked.Tag is int commandIndex ? commandIndex : -1;

            return ResultFor(clicked);
        }

        public static DialogResult ShowTaskDialogBox(IWin32Window owner,
            string title,
            string mainInstruction,
            string content,
            string expandedInfo,
            string footer,
            string verificationText,
            string radioButtons,
            string commandButtons,
            ETaskDialogButtons buttons,
            ESysIcons mainIcon,
            ESysIcons footerIcon)
        {
            return ShowTaskDialogBox(owner, title, mainInstruction, content, expandedInfo, footer, verificationText,
                radioButtons, commandButtons, buttons, mainIcon, footerIcon, 0);
        }

        public static DialogResult ShowTaskDialogBox(string title,
            string mainInstruction,
            string content,
            string expandedInfo,
            string footer,
            string verificationText,
            string radioButtons,
            string commandButtons,
            ETaskDialogButtons buttons,
            ESysIcons mainIcon,
            ESysIcons footerIcon)
        {
            return ShowTaskDialogBox(null, title, mainInstruction, content, expandedInfo, footer, verificationText,
                radioButtons, commandButtons, buttons, mainIcon, footerIcon, 0);
        }

        #endregion

        #region MessageBox

        public static DialogResult MessageBox(IWin32Window owner,
            string title,
            string mainInstruction,
            string content,
            string expandedInfo,
            string footer,
            string verificationText,
            ETaskDialogButtons buttons,
            ESysIcons mainIcon,
            ESysIcons footerIcon)
        {
            return ShowTaskDialogBox(owner, title, mainInstruction, content, expandedInfo, footer, verificationText, "",
                "", buttons, mainIcon, footerIcon);
        }

        public static DialogResult MessageBox(string title,
            string mainInstruction,
            string content,
            string expandedInfo,
            string footer,
            string verificationText,
            ETaskDialogButtons buttons,
            ESysIcons mainIcon,
            ESysIcons footerIcon)
        {
            return ShowTaskDialogBox(null, title, mainInstruction, content, expandedInfo, footer, verificationText, "",
                "", buttons, mainIcon, footerIcon);
        }

        #endregion

        #region Building the page

        /// <summary>
        /// Turns this class's strings and enums into a task dialog page.
        /// </summary>
        /// <remarks>
        /// Separate from showing it so that it can be tested. A shown dialog is modal and would
        /// hang a test run, but everything worth asserting is decided here: which buttons exist,
        /// whether the expander and the check box are there, and how the command links were split.
        /// </remarks>
        internal static TaskDialogPage BuildPage(string title,
            string mainInstruction,
            string content,
            string expandedInfo,
            string footer,
            string verificationText,
            string radioButtons,
            string commandButtons,
            ETaskDialogButtons buttons,
            ESysIcons mainIcon,
            ESysIcons footerIcon,
            int defaultIndex)
        {
            var page = new TaskDialogPage
            {
                Caption = NullIfEmpty(title),
                Heading = NullIfEmpty(mainInstruction),
                Text = NullIfEmpty(content),
                Icon = IconFor(mainIcon),
                AllowCancel = buttons == ETaskDialogButtons.Cancel ||
                              buttons == ETaskDialogButtons.Close ||
                              buttons == ETaskDialogButtons.OkCancel ||
                              buttons == ETaskDialogButtons.YesNoCancel,
                SizeToContent = true
            };

            if (!string.IsNullOrEmpty(expandedInfo))
                page.Expander = new TaskDialogExpander(expandedInfo);

            if (!string.IsNullOrEmpty(footer))
                page.Footnote = new TaskDialogFootnote(footer) { Icon = IconFor(footerIcon) };

            if (!string.IsNullOrEmpty(verificationText))
                page.Verification = new TaskDialogVerificationCheckBox(verificationText);

            AddRadioButtons(page, radioButtons, defaultIndex);
            var commandLinkCount = AddCommandLinks(page, commandButtons, defaultIndex);
            AddStandardButtons(page, buttons, defaultIndex, commandLinkCount);

            return page;
        }

        private static void AddRadioButtons(TaskDialogPage page, string radioButtons, int defaultIndex)
        {
            if (string.IsNullOrEmpty(radioButtons))
                return;

            var labels = Split(radioButtons);
            for (var i = 0; i < labels.Count; i++)
                page.RadioButtons.Add(new TaskDialogRadioButton(labels[i]) { Checked = i == defaultIndex });
        }

        /// <summary>
        /// Adds one command link per item, splitting "label\ndescription" where there is one.
        /// </summary>
        /// <remarks>
        /// Each link carries its index in <see cref="TaskDialogControl.Tag"/>, which is how
        /// <see cref="CommandButtonResult"/> is recovered from the button the user clicked.
        /// </remarks>
        private static int AddCommandLinks(TaskDialogPage page, string commandButtons, int defaultIndex)
        {
            if (string.IsNullOrEmpty(commandButtons))
                return 0;

            var items = Split(commandButtons);
            for (var i = 0; i < items.Count; i++)
            {
                var parts = items[i].Split(DescriptionSeparator);
                var description = parts.Length > 1
                    ? string.Join(Environment.NewLine, parts.Skip(1).Select(part => part.Trim())).Trim()
                    : null;

                var link = new TaskDialogCommandLinkButton(parts[0].Trim(), NullIfEmpty(description)) { Tag = i };

                page.Buttons.Add(link);
                if (i == defaultIndex)
                    page.DefaultButton = link;
            }

            return items.Count;
        }

        private static void AddStandardButtons(TaskDialogPage page, ETaskDialogButtons buttons, int defaultIndex,
                                               int commandLinkCount)
        {
            var standard = StandardButtonsFor(buttons);
            foreach (var button in standard)
                page.Buttons.Add(button);

            // defaultIndex addresses the command links when there are any; only when there are
            // none does it select among the ordinary buttons.
            if (commandLinkCount == 0 && defaultIndex >= 0 && defaultIndex < standard.Count)
                page.DefaultButton = standard[defaultIndex];
        }

        private static List<TaskDialogButton> StandardButtonsFor(ETaskDialogButtons buttons)
        {
            switch (buttons)
            {
                case ETaskDialogButtons.YesNo:
                    return new List<TaskDialogButton> { TaskDialogButton.Yes, TaskDialogButton.No };
                case ETaskDialogButtons.YesNoCancel:
                    return new List<TaskDialogButton>
                        { TaskDialogButton.Yes, TaskDialogButton.No, TaskDialogButton.Cancel };
                case ETaskDialogButtons.OkCancel:
                    return new List<TaskDialogButton> { TaskDialogButton.OK, TaskDialogButton.Cancel };
                case ETaskDialogButtons.Ok:
                    return new List<TaskDialogButton> { TaskDialogButton.OK };
                case ETaskDialogButtons.Close:
                    return new List<TaskDialogButton> { TaskDialogButton.Close };
                case ETaskDialogButtons.Cancel:
                    return new List<TaskDialogButton> { TaskDialogButton.Cancel };
                case ETaskDialogButtons.None:
                    return new List<TaskDialogButton>();
                default:
                    throw new ArgumentOutOfRangeException(nameof(buttons), buttons, null);
            }
        }

        /// <summary>
        /// Maps the button the user clicked back to the <see cref="DialogResult"/> callers expect.
        /// </summary>
        /// <remarks>
        /// A command link is identified by its tag and answered first, before any comparison
        /// against the standard buttons. That ordering matters: <c>==</c> on
        /// <see cref="TaskDialogButton"/> compares by value, not by reference - the static
        /// properties hand out a fresh instance on every read - so a command link whose label
        /// happens to read "Cancel", which this application really does create, would otherwise
        /// come back as <c>DialogResult.Cancel</c> instead of as a command link.
        /// <para>
        /// A command link carries no result of its own; callers read
        /// <see cref="CommandButtonResult"/> for those, so <c>DialogResult.OK</c> stands for
        /// "something was chosen".
        /// </para>
        /// </remarks>
        internal static DialogResult ResultFor(TaskDialogButton clicked)
        {
            if (clicked.Tag is int) return DialogResult.OK;

            if (clicked == TaskDialogButton.Yes) return DialogResult.Yes;
            if (clicked == TaskDialogButton.No) return DialogResult.No;
            if (clicked == TaskDialogButton.OK) return DialogResult.OK;
            if (clicked == TaskDialogButton.Cancel) return DialogResult.Cancel;
            if (clicked == TaskDialogButton.Close) return DialogResult.Cancel;
            if (clicked == TaskDialogButton.Retry) return DialogResult.Retry;
            if (clicked == TaskDialogButton.Abort) return DialogResult.Abort;
            if (clicked == TaskDialogButton.Ignore) return DialogResult.Ignore;
            return DialogResult.OK;
        }

        /// <summary>
        /// The task dialog icon for one of this application's four.
        /// </summary>
        /// <remarks>
        /// Windows has no question icon for task dialogs - it was dropped deliberately, on the
        /// grounds that a question mark tells the reader nothing the text does not - so the
        /// standard set has no counterpart to <see cref="ESysIcons.Question"/>. The system question
        /// icon is passed through as a custom icon rather than silently becoming an information
        /// "i", which would change the look of every confirmation in the application.
        /// </remarks>
        private static TaskDialogIcon IconFor(ESysIcons icon)
        {
            switch (icon)
            {
                case ESysIcons.Information: return TaskDialogIcon.Information;
                case ESysIcons.Warning: return TaskDialogIcon.Warning;
                case ESysIcons.Error: return TaskDialogIcon.Error;
                case ESysIcons.Question: return QuestionIcon;
                default:
                    throw new ArgumentOutOfRangeException(nameof(icon), icon, null);
            }
        }

        private static List<string> Split(string items)
        {
            return items.Split(ItemSeparator)
                        .Select(item => item.Trim())
                        .Where(item => item.Length > 0)
                        .ToList();
        }

        private static string NullIfEmpty(string text)
        {
            return string.IsNullOrEmpty(text) ? null : text;
        }

        #endregion
    }
}
