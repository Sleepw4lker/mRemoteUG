---
date: 2026-09-29
status: accepted
---

# Declare the display scale before connecting, through the extended-settings property bag

## What this is about

A session opened against a host **nobody is logged on to** comes up with its remote UI rendered
at 100%: unreadable on a HiDPI panel, and it stays that way after logon until the user resizes
the window by hand. Resizing sends the same scale the application already sent, so the numbers
were never wrong.

The scale reaches a session through `IMsRdpClient10.UpdateSessionDisplaySettings`, which needs a
session to exist. Everything the fork did up to here therefore *rescaled* a session that had
already been created at 100%, and two attempts at doing that better both failed:

- Applying the scale at `OnLoginComplete`, the original behaviour. Did not survive a new session.
- Applying it at `OnConnected` instead, before any desktop exists in the session, with the
  resolution nudged two pixels so the host could not read the monitor layout as unchanged. Tested
  on the target machine: **no improvement.** Debug logging was off for that run, so *why* it did
  not help is not known — see the note in
  [../platform-findings.md](../platform-findings.md).

## Decision

Set `DesktopScaleFactor` and `DeviceScaleFactor` in the `IMsRdpExtendedSettings.Property` bag
during `Initialize`, before `Connect()`. The session is then *created* at the right scale, and
nothing has to talk it into changing afterwards.

These are the same two values `mstsc.exe` reads from `desktopscalefactor:i:` and
`devicescalefactor:i:` in a `.rdp` file, which is what makes this the ordinary way to do it
rather than a trick.

## Why this crosses a restraint on purpose

`SetRedirection`'s remarks say this fork sets nothing in that bag, and name
`EnableHardwareMode` and `DisableUDPTransport` as the sort of thing it stays away from. That
restraint is about **not pinning capabilities that should be negotiated**: a property that fixes
a protocol decision per connection is how a v12 control stops talking to an old server.

Neither of these two is that. They carry a client-side *display* preference, negotiated by
nothing, and a server that does not understand a scale factor renders at 100% — which is the
behaviour being fixed, so the failure mode of being ignored is the status quo. The restraint is
narrowed to the properties it was written about, not abandoned.

## Alternatives

- **Keep rescaling after the fact, and retry harder.** A delayed re-apply after logon, repeated
  until it sticks. Rejected: how long a fresh logon takes to create a desktop is a guess ranging
  from under a second to a slow roaming profile, every attempt reflows a desktop the user is
  looking at, and none of it can be verified from here. It also still rescales rather than
  creates, which is the thing that demonstrably did not work.
- **Accept it, and document the workaround.** "Resize the window once after logging on."
  Rejected: the fork's stated scope is RDP and SSH done properly, and a HiDPI session is the
  common case on the hardware it runs on.
- **Do nothing until the mechanism is understood.** Reasonable, and the cheaper first step — one
  `--verbose` run says which of three candidates is biting. Rejected only because this change is
  small, is the documented route, and settles the symptom whichever candidate it was; the
  diagnostics ship with it either way.

## What is measured, and what is not

Measured here, by `--selftest` on mstscax 10.0.26100 (`CheckRdpControl` re-measures it on every
run, so a build of mstscax that changes its mind will say so):

- Both property names are **accepted** and read back their value.
- The value must be **VT_UI4**. A boxed `int` throws `COMException 0x80004005` (E_FAIL). The cast
  to `uint` in `SetDisplayScale` is load-bearing.
- The interop declares the setter as `set_Property(string, ref object)`, so the C# indexer does
  not bind and it must be called with an explicit `ref`. Pinned by
  `AxInteropSurfaceTests.TheExtendedSettingsBagTakesItsValueByReference`.

Measured on the target machine, 2026-09-29: **a session created this way comes up scaled.** That
is the whole of the evidence for this record — it cannot be reproduced on the development environment,
which has no RDP host to connect to. `SetDisplayScale` logs what it declared at Information level
— one line per session, within ADR-0017's bound — precisely because nothing here can observe the
effect.

What was *not* established is why the post-connect route fails; see
[../platform-findings.md](../platform-findings.md). This record does not depend on knowing: the
pre-connect declaration is the documented way to do it, and it works.

## Consequences

- One connection property more, guarded per name, that cannot fail a connection.
- `TryApplyDisplayScale`'s post-connect passes stay, because they cover what no pre-connect value
  reaches. **They are not, however, known to work**, and this record should not be read as saying
  they are: they carry the scale the same way that was measured not to be enough for a new session,
  and neither case below has been tested.
  - **A window moved to a monitor of a different DPI.** The `DpiChanged` pass sends the session's
    current size with the new scale — resolution-identical, the shape the host may read as no
    change. What probably saves this in the default case is the resize that comes with the move:
    Per-Monitor V2 resizes the window, so `ReconnectForResize` sends a genuinely different
    resolution *and* the new scale. That leg skips — and logs which guard stopped it — when
    `AutomaticResize` is off, the resolution is fixed rather than FitToWindow/Fullscreen, or
    SmartSize is on, and in those cases only the resolution-identical update is left.
  - **mstscax's own automatic reconnect.** `EnableAutoReconnect` never enters managed code, so
    there is no point at which `SetDisplayScale` could be called again for it, and a session it
    re-creates comes up at whatever scale was last declared. If the window has changed monitors
    since, only the post-connect passes are left to correct it — which is exactly the thing this
    record establishes is not reliable. **This case has no fix here.**

    `tmrReconnect_Elapsed` *is* reachable and does re-declare the scale before its
    `_rdpClient.Connect()`, so the reconnect this application drives itself is covered.

    **Accepted as a known limitation, 2026-10-07.** It needs both a monitor change during the
    session and a network drop that mstscax recovers from by itself, and the remedy is
    *Reconnect* on the tab's menu: it closes the session and opens a new one, which declares the
    scale for the monitor the window is on now, before connecting. Replacing mstscax's
    reconnect with this application's own would close it, at the price of changing session
    handling - the area [ADR-0005](0005-park-rdp-controls-before-disposing.md) is about - and is
    not worth doing unless the monitor-change case above turns out not to rescale either.
- A two-pixel resolution nudge on the `SessionCreated` pass was tried during this work, as
  insurance against the second candidate mechanism in platform-findings, and is deliberately
  **not** in the code: it settled nothing, and once the session is created scaled it would have
  nothing to do. Don't re-add it without a measurement that asks for it.

**Amended 2026-11-17: the monitor-change case above is measured, and it was broken in the default
case.** A `--verbose` log of a dock from a 150% panel to a 200% monitor, with a FitToWindow
connection, AutomaticResize on and SmartSize off, showed the resize that comes with the move doing
its job: its last request was the Tab's final size, 4364x2254, at 200%. The `DpiChanged` pass ran
9 ms *after* it. It took the size from the control's `DesktopWidth`/`DesktopHeight`, which still
reported 3604x1940, a request two behind, and sent that. The session ended up smaller than the
Tab, with a grey border round it. So the guess above, that the resize "probably saves" this case,
was wrong about the order: the pass that was meant to be the backstop undid the resize.

The `DpiChanged` pass now sends the size the session was last asked for, and reads the control's
reported size only when nothing has been asked for yet (`RdpDisplayScale.TryChooseRescaleSize`).
After a resize at the new scale it therefore sends nothing. A fixed-resolution session still gets
the scale-only update this record describes, because its last request is the desktop size recorded
when it connected. The lag itself is in
[../platform-findings.md](../platform-findings.md). Whether the scale-only update rescales a
fixed-resolution session is still untested.

**Amended 2026-11-17: mstscax's own automatic reconnect, measured.** The consequence above says the
reconnect "never enters managed code" and that the case "has no fix here". Three things are now
measured on a current Windows 11 host:

- The reconnect raises `OnAutoReconnecting2` and `OnAutoReconnected`. Both are now logged: the
  disconnect reason, whether the network is available, the attempt out of the maximum, and the
  reconnect itself. Until then, a lab run had no trace of a drop at all.
- **`OnLoginComplete` fires again after it**, so the `LoginComplete` pass re-applies the scale. Since
  that pass reads the DPI from the window (`CurrentDpi()`), it uses the monitor the window is on
  now. The limitation above is therefore narrower than it reads.
- **`UpdateSessionDisplaySettings` returns `E_UNEXPECTED` right after every connect and every
  automatic reconnect**, before the display channel is open. The code read that as "a server older
  than RDP 8.1" and switched the session to resizing by reconnecting. Sending size and scale from
  `OnAutoReconnected` as well was tried: it hit exactly that window, and every reconnect it fell
  back to dropped the session again, every ten seconds, more than thirty times, until the host
  locked the session ("blocked by Remote Desktop Configuration") and only the console could free it.

So `OnAutoReconnected` only logs, and `E_UNEXPECTED` never latches a session and never falls back
to a reconnect (`RdpDisplayScale.IsRefusalForNow`). The price is that a genuinely old server
answering `E_UNEXPECTED` no longer gets reconnect-based resizing; such servers are outside the
fork's range. **A reconnect is never the answer to a refusal that may be transient**, because a
reconnect that fails the same way repeats on every attempt.

**Amended 2026-11-18: the `LoginComplete` pass sends a following session its window's size.**
A `--verbose` log of an undock from 200% to 150% that also changed the network (#246) showed the
cost of the amendment above. While the control reconnected by itself, every resize and the
`DpiChanged` pass were refused with `E_UNEXPECTED` and, as decided, neither latched nor retried by
reconnecting; each was forgotten. The `LoginComplete` pass after the reconnect then sent the size
the control reported, 3462x1915, the docked size, at 150%, into a Tab of 1627x1011. The session
showed scrollbars, and its text stayed at 200%. That last part is consistent with the second
candidate in [../platform-findings.md](../platform-findings.md), a scale-only change at an
unchanged resolution, and is not proven: the only request with the new scale had the session's
old resolution. Nothing sent anything again until the laptop was docked.

So for a session whose resolution follows its window (`AutomaticResize` on, FitToWindow or
Fullscreen, SmartSize off), the `LoginComplete` pass now sends the window's size at the window's
DPI, as one request. That pass is the replay of everything dropped before it, by the logon guard
or by a refusal for now, without keeping a record of what was dropped; it follows every logon,
including the one after either kind of reconnect. The `DpiChanged` pass, when a refusal has left
nothing asked for, falls back to the window's size too instead of the lagging reported one. A
fixed-resolution session is unchanged: it is sent the reported size, so only its scale changes.

Still no retry of its own and still no reconnect on `E_UNEXPECTED`: each event makes one attempt.
If the `LoginComplete` pass itself were refused for now, the next resize or DPI change would be
the next attempt. That was not seen in either lab log; the log says so if it happens.
