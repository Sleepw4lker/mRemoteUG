---
date: 2026-09-24
---

# Host stock PuTTY as a maximised, non-child window

Stock PuTTY starts as a top-level window of its own and is then reparented into the tab's
panel. `890b245` ([ADR-0010](0010-per-monitor-v2.md)) removed the old negative-offset trick —
push the caption and frame outside the panel by amounts computed from `SystemInformation`,
which answers for the *system* DPI — and replaced it with a style strip:
`WS_CAPTION|WS_THICKFRAME|WS_BORDER` off, `WS_CHILD` on, `SWP_FRAMECHANGED`. It shipped with
the title bar and frame drawn inside the tab, reported on 2026-09-23.

The strip cannot work, and the reason is PuTTY's, not ours. Its `WM_SIZE` handler calls
`clear_full_screen()` on every `SIZE_RESTORED`, and that function puts
`WS_CAPTION|WS_BORDER|WS_THICKFRAME` back unconditionally. The `SWP_FRAMECHANGED` that applies
a strip shrinks the client area, which *is* a `SIZE_RESTORED`, so the strip is undone before
the call returns — and so is every `MoveWindow` after it. The measured detail is in
[docs/platform-findings.md](../platform-findings.md#putty-hosting).

**Decision 1: the reparented window is maximised (`ShowWindow(SW_MAXIMIZE)`) and stays so.** A
zoomed window only ever receives `SIZE_MAXIMIZED`, a branch that never touches the style.
Zoomed-and-captionless is also PuTTY's own definition of full-screen: it sizes the terminal
grid from whatever client area it is given, centres it, and ignores server-side requests to
resize the window — which is what an embedded terminal wants anyway.

**Decision 2: the window is *not* made `WS_CHILD`, against the `SetParent` documentation's
advice.** The first cut of decision 1 kept `890b245`'s `WS_CHILD`, and the title bar went away
while the keyboard went with it: PuTTY's cursor stayed hollow and no keystroke arrived. A child
window can never be the foreground window; keystrokes go to the focus window of the thread that
owns the foreground window; and `SetForegroundWindow` on a child activates the main form
instead. Left top-level with a parent — how this application hosted PuTTY for years before
`890b245` — the window activates on its own when clicked and `Focus()` can make it foreground.
The cost is the one the application always paid: while PuTTY has the keyboard, the main
window is technically inactive.

Both decisions live in one place, `HostedPuttyWindow`, and `--selftest` runs it against a real
PuTTY ([ADR-0013](0013-selftest-as-the-verification-mechanism.md)): it asserts the style, the
parent and the zoom state, then focuses the window and types into it, and requires the
keystroke to come out of the loopback connection on the other side. Both defects — the strip
that undoes itself and the child that cannot take the keyboard — were seen failing that check
before the fix was kept.

**Rejected: negative offsets again, with `GetSystemMetricsForDpi`.** It would have to pick the
right DPI for a child that may be unaware, system-aware or per-monitor and is bitmap-scaled
under mixed hosting, and it leaves PuTTY free to resize its own window from `reset_window`
whenever the font grid does not fit. The maximised state removes both problems rather than
computing around them.

**Rejected: `WS_CHILD` plus `AttachThreadInput`/`SetFocus`.** It would tie the UI thread's input
queue to PuTTY's, so a hung PuTTY would hang the desktop — the class of failure
[ADR-0005](0005-park-rdp-controls-before-disposing.md) exists to avoid — and it would still
need a click handler of its own, because PuTTY does not call `SetFocus` on itself.

**Consequences.**

- PuTTY believes it is full-screen. Its settings dialog, still reachable from the tab menu,
  therefore applies the session's *full-screen* scrollbar option rather than the normal one,
  and the terminal grid is centred with a small margin rather than pinned top-left.
- Alt+Enter, if "full screen on Alt-Enter" is enabled in the PuTTY session, calls
  `ShowWindow(SW_RESTORE)` and brings the chrome back. It is off by default and is not handled.
- `GetParent` returns null for this window, because it is not `WS_CHILD`. Anything that needs
  its parent must ask `GetAncestor(GA_PARENT)`.
- A Chocolatey `putty.exe` on PATH is a shim that launches PuTTY in another process and exits,
  so no window is ever found: a session now fails with a timeout instead of opening an empty
  tab, and the self-test names the shim. Resolving the shim to its target is a separate change.
