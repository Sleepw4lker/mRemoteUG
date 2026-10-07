---
date: 2026-09-29
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
- A two-pixel resolution nudge on the `SessionCreated` pass was tried during this work, as
  insurance against the second candidate mechanism in platform-findings, and is deliberately
  **not** in the code: it settled nothing, and once the session is created scaled it would have
  nothing to do. Don't re-add it without a measurement that asks for it.
