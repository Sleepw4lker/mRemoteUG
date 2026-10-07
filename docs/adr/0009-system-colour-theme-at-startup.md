---
date: 2026-09-22
---

# Take the colour theme from Windows, once, at startup

mRemoteNG shipped its own theming engine: a set of distributed theme files, a colour palette
applied control-by-control, and a theme picker. `a0c5c1b` deleted it and `57cd595` replaced it
with `Application.SetColorMode` — the application follows the system light/dark setting, with a
light or dark override on the Appearance page for users who want to differ from the system.

**`SetColorMode` is called exactly once, from `ProgramRoot.ConfigureApplication`, and never
again while the process runs. The Appearance page saves the setting and shows a restart notice
rather than applying it.** This is a deliberate limitation, not an unfinished feature, and it is
the thing a future reader will want to "fix":

`Application.SetColorMode` re-points `SystemColors` and notifies `SystemEvents`. It does **no**
native re-theming. `SetWindowTheme`, the DWM call that darkens a title bar, and
`TVM_SETBKCOLOR`/`LVM_SETBKCOLOR` all run at handle creation and nowhere else. A runtime switch
therefore recolours the managed surface and leaves the title bar, tree, lists and scrollbars
light — visibly worse than a restart notice. Both startup paths go through
`ConfigureApplication` so neither can miss it.

Two further facts, both measured:

- **ToolStrips need both halves of the change.** Deleting `RenderMode = Professional` is not
  enough (the manager's default *is* Professional) and setting
  `ToolStripManager.RenderMode = System` is not enough (it cannot override a strip that pins
  its own). Only `ToolStripSystemRenderer` has a dark variant. Note that
  `ToolStripManager.RenderMode` then *reports* `Professional` while handing out a
  `ToolStripSystemRenderer` — trust the renderer, not the property.
- **Colours are cheap to get right.** A `SystemColors` value is a handle resolved on every
  read, so it follows the theme even when cached in a field. Only literal colours freeze, which
  is what `ThemeTests` guards, asserting `Color.IsSystemColor` — **not** `IsKnownColor`, which
  passes for `Color.White`.

It is worth recording that this needed no suppressions: WFO5001 was removed in .NET 10, and the
only `[Experimental]` left in `System.Windows.Forms.dll` 10.0.12 is WFO5003 on
`IAsyncDropTarget`.

Interpreting the diagnostics is a trap in itself — see the dark-mode section of
[docs/platform-findings.md](../platform-findings.md) before concluding from a native colour
read-back that something is broken.
