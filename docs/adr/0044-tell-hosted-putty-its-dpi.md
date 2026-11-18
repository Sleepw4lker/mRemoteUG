---
date: 2026-11-18
status: accepted
---

# Tell a hosted PuTTY its DPI with a WM_DPICHANGED of our own

A PuTTY Tab kept the text size of the monitor it was opened on after a dock, until it was
reconnected (#262). Windows sends `WM_DPICHANGED` to top-level windows only, and the PuTTY that
[ADR-0023](0023-host-stock-putty-as-a-maximised-child.md) reparents into a Tab is not one. PuTTY
0.85 handles the message itself: it takes the new DPI from `wParam`, rebuilds its fonts, and,
maximised, keeps its window and fits its rows and columns to it. Nothing else makes it re-read
its DPI.

**Decision: when the panel or the main window hears of a DPI change, send PuTTY
`WM_DPICHANGED` with the new DPI.** `PuttyBase.TellPuttyItsDpi` does it from both
`NotifyDpiChanged` and `FrmMain`'s DPI follow-up, once per DPI. `HostedPuttyWindow.TellDpi` sends
it with `SendMessageTimeout(SMTO_ABORTIFHUNG)`, 1 s, so a hung PuTTY cannot hold the UI thread
([ADR-0005](0005-park-rdp-controls-before-disposing.md)).

The surprising part: `lParam` is a pointer to a `RECT` in our process, and PuTTY dereferences
it unconditionally. That is safe only because user32 copies the `RECT` into the receiving
process. Measured, not assumed: a non-maximised PuTTY moved to exactly the rectangle we sent, and
an invalid pointer failed in our process and left PuTTY untouched. The detail is in
[docs/platform-findings.md](../platform-findings.md#a-hosted-putty-never-hears-of-a-dpi-change).
`--selftest` sends it to a real PuTTY on every run.

**Rejected: `IDM_RECONF` (Change Settings).** PuTTY reads its DPI once, at startup; reconfiguring
does not re-read it, and the dialog is not scriptable anyway.

**Rejected: offer a reconnect after a DPI change.** It works, because a new PuTTY reads its DPI
when it starts, but it ends the session to fix a font.

**Rejected: accept and record the limitation.** The message is PuTTY's own interface for exactly
this change, and NETworkManager (PR #3352) ships the same approach.

**Consequences.** PuTTYNG gets the same message; it is untested whether it handles it. A Windows
build that stopped marshalling the `RECT` would most likely crash PuTTY on a dock (inferred
from the source, not seen); the self-test check is where that would show first.
