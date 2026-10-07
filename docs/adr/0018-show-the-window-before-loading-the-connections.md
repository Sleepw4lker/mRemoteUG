---
date: 2026-09-23
---

# Show the window before loading the connections

`FrmMain` is `Opacity = 0` in the designer, and `frmMain_Load` restored it as its
second-to-last statement. Everything startup does therefore happened behind a black
rectangle: the FIPS registry probes and a `Process.GetProcessesByName`, the legacy
`user.config` scan and import, PuTTY path discovery, reading and parsing the connection file
with **one PBKDF2 derivation per stored password** — and then `PreviousSessionOpener`, which
reopens every connection that was open last time, RDP controls and all.

On a small file that is imperceptible. On a large one with sessions to restore it is seconds
of nothing at all, followed by a fully populated application appearing at once. The
application was not slow to *start*; it was slow to *appear*, and those are different
problems with different fixes.

`frmMain_Load` is now split by what the work is for. Everything that decides how the window
looks — settings, the tool windows, the restored layout, the menu text — still runs first,
and `Opacity = 1` comes straight after it. The rest moved to `CompleteStartup`, posted with
`BeginInvoke` so that `frmMain_Load` returns to the message loop and the window paints before
the load begins rather than after it.

**`CompleteStartup` is still on the UI thread and still blocks interaction while it runs.**
That is not what this changes, and it is worth being explicit about: moving the connection
load onto a background thread is a different and much larger decision, touching the tree
model, the message collector's threading and session restore. What this changes is whether
there is a window to look at while it happens.

Consequences:

- **The order within the posted part is load-bearing.** `AppWindows.TreeForm.Focus()` focuses
  a tree that has no nodes until `LoadCredsAndCons` has run, and the `CreateEmptyPanelOnStartUp`
  block has to see the panels the load created. Both move with the load rather than ahead of it.
- **The try/catch in `CompleteStartup` is not decoration.** An exception on a posted callback
  has no caller to propagate to, and this application installs no `ThreadException` handler, so
  without it a failure there is an unhandled-exception dialog rather than a notification.
- **Two dialogs now appear over a visible window** rather than over nothing: the
  connection-file-not-found task dialog and the FIPS warning.
- **`RdpProtocol.Initialize` waits for its control's window handle, and that wait used to be
  unbounded.** It normally leaves on the first turn, but it now runs while the main window is
  still coming up, which is where a handle chain is least certain. `5b7875f` gave it a ten
  second bound and a Warning. The pumping inside it is deliberately unchanged — see
  [ADR-0005](0005-park-rdp-controls-before-disposing.md).
- **Two `Information` lines record the split**: time to the window appearing, and time to the
  load finishing. The machine this was written on has no connection file worth loading, so per
  [ADR-0013](0013-selftest-as-the-verification-mechanism.md) those lines are how the effect gets
  measured on a machine that has one. It has not been measured yet.

Alternatives considered: showing the window first and loading on a worker thread (rejected for
now — the connection tree, the message collector and session restore are all UI-thread bound,
so it is a redesign rather than a reordering); and a splash screen (rejected — it is a second
window to build, scale and theme, and it still does not let anyone see the application).
