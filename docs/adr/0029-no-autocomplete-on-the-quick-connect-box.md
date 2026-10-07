---
date: 2026-09-29
---

# Drop autocomplete from the quick connect box

## What happened

Moving the main window from a 200% monitor to a 300% one killed the process. No connection open,
nothing of this application's code on the stack:

```
System.ComponentModel.Win32Exception (1400): Fehler beim Erstellen des Fensterhandles.
   at System.Windows.Forms.NativeWindow.CreateHandle(CreateParams cp)
   at System.Windows.Forms.ComboBox.CreateHandle()
   at System.Windows.Forms.Control.RecreateHandleCore()
   at System.Windows.Forms.ComboBox.OnFontChanged(EventArgs e)
   at System.Windows.Forms.Control.SetScaledFont(Font scaledFont, Boolean raiseOnFontChangedEvent)
   at System.Windows.Forms.Control.WmDpiChangedBeforeParent(Message& m)
```

The measured chain, top to bottom:

1. The hosted `ComboBox` gets `WM_DPICHANGED_BEFOREPARENT` at its own window.
2. WinForms acts on it only for a control with an **explicitly set** font — and
   `ToolStripControlHost` gives every hosted control one. Measured: the quick connect box reads
   Segoe UI 9pt as its own font, not as an inherited one.
3. Scaling that font raises `ComboBox.OnFontChanged`.
4. **A `ComboBox` with `AutoCompleteMode` set recreates its window there.** Measured as a clean
   A/B on the real strip: `SuggestAppend` recreates, `Suggest` recreates, `None` keeps the handle.
5. `CreateWindowEx` inside that recreation failed.

So a monitor of a different scale means recreating a window from inside that window's own message
handler, and autocomplete is what makes step 4 happen at all.

## Decision

`cmbQuickConnect` asks for no autocomplete: `AutoCompleteMode.None`, `AutoCompleteSource.None`.

The drop-down is untouched — it still holds the connection history and still opens. What is lost
is suggest-as-you-type while the field is being typed into.

## Alternatives

- **Keep autocomplete; stop the hosted control having an explicitly set font.** Then step 2 never
  fires, WinForms never scales that font inside the DPI message, and the recreation happens later
  — from `DpiScaling.FollowDpiChange`, outside `WM_DPICHANGED`. This keeps the feature and is the
  better answer if suggest-as-you-type is worth it. It was not taken because
  `ToolStripControlHost` assigns the font *by design*, on every owner font change, so suppressing
  it needs an override that fights the framework, in the area
  [ADR-0010](0010-per-monitor-v2.md) and platform-findings both warn about — and because the
  recreation would still happen, just somewhere believed safer, which is not the same as knowing
  it is safe.
- **Catch it.** `Application.ThreadException` already sees it, and the process could be kept
  alive. Rejected: a control whose window failed to be created is not in a state worth continuing
  from, and it would hide the fault rather than remove it.
- **Leave it.** Rejected outright. It is an unhandled crash on an ordinary gesture, on the
  hardware this fork is used on.

## What is measured, and what is not

Measured on the development machine — the details are in
[../platform-findings.md](../platform-findings.md):

- `ToolStripControlHost` gives the hosted control an explicitly set font.
- Autocomplete is what makes a font change recreate a `ComboBox`'s window; `None` does not.
- Driving the real path in `--selftest`, in a real per-monitor-aware process, scales the font and
  recreates the handle exactly as on the failing machine — **and then succeeds.**

**The crash itself was never reproduced on the development machine**, so the fix was reasoned
rather than watched being made: the recreation is necessary but not sufficient, and what a real
move between two real monitors adds on top is still not known.

**Confirmed on the affected machine, 2026-09-29:** with autocomplete gone, moving the window from
the 200% monitor to the 300% one no longer crashes. So the fix holds even though the mechanism
underneath it does not, and that is worth stating plainly rather than leaving the reasoning to
stand in for the evidence. The reasoning is still the reason it was expected to work: a hosted
combo box that keeps its window through a DPI change cannot fail inside `CreateWindowEx` at all.

Three hypotheses were disproved along the way and are recorded so that the next attempt does not
spend itself on them: `SyncItemHeightToFont`'s `ItemHeight` assignment does not recreate the
handle; the recreation succeeds with the combo reparented into a `ToolStripOverflow` whether that
popup's window exists or not; and `DpiScaling.FollowDpiChange` is not involved, the failing run's
log having no `DPI follow-up` lines before the crash.

## Consequences

- `--selftest` gains **DPI change on hosted combo boxes**, which drives a per-monitor DPI change
  at every `ToolStrip`-hosted `ComboBox` and fails if one recreates its window. It failed before
  this change and passes after, which is the only red-green available for this bug.
- A headless test pins the same property, and a second one pins that the drop-down still keeps its
  items, so the fix cannot quietly widen into "the drop-down does nothing".
- If suggest-as-you-type is ever wanted back, the first alternative above is the route, and the
  selftest check is what says whether it worked.
