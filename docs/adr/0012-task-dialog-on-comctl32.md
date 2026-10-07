---
date: 2026-09-22
---

# Re-implement the task dialog on the comctl32 TaskDialog

mRemoteNG carried `frmTaskDialog`, a hand-built imitation of the Windows task dialog: 1,571
lines across six files, laying out an icon, heading, content, expander, command links, footnote
and verification checkbox from literal coordinates. Under
[ADR-0010](0010-per-monitor-v2.md) it had three separate DPI overlap bugs — panels sized from
scaled literals containing auto-scaled children, which is a latent overlap by construction.

Rather than fix them, the whole thing was deleted and `cTaskDialog` re-implemented on
`System.Windows.Forms.TaskDialog`, the real comctl32 dialog: **377 lines in one file, and the
layout is now the OS's problem, DPI included.** The public API (`ShowTaskDialogBox`,
`MessageBox`, `VerificationChecked`, `CommandButtonResult`) is unchanged, so no call site moved.
`e915199` then dropped the separate About window in favour of one.

The alternative was to keep fixing the layout; rejected because the bug class recurs with every
font and DPI combination and there is no test that covers them all.

The cost is a dependency on visual styles and Common Controls v6 — `TaskDialog.ShowDialog`
throws without them. Both are already guaranteed here, and `--selftest` shows a real dialog and
closes it from `page.Created` via `page.BoundDialog.Close()`, which stays non-interactive and
proves the dependency holds on the deployed machine rather than only on this one.

Four measured facts about the native API that the implementation depends on:

- **`TaskDialogButton.Yes` and friends return a fresh instance on every read**, so `Is.SameAs`
  never holds — but `==` is overloaded and compares **by value**. A command link labelled
  "Cancel" (which `DialogFactory` really creates) therefore compares equal to
  `TaskDialogButton.Cancel`. `cTaskDialog.ResultFor` identifies command links by their `Tag`
  *before* comparing against any standard button.
- **A fresh `TaskDialogPage` already has a non-null `Expander`, `Footnote` and `Verification`.**
  What decides whether they render is whether they have text, not whether they are null.
- **Windows has no question icon for task dialogs.** It is passed through as a custom icon
  (`new TaskDialogIcon(SystemIcons.Question)`) so confirmations keep their appearance.
- The dialog cannot be shown without a message loop, which is why `TaskDialogPageTests` tests
  page construction and `--selftest` tests the showing.
