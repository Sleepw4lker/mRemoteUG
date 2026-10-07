---
date: 2026-09-22
---

# Park RDP controls off-screen and dispose them once their session is down

Upstream mRemoteNG's `RdpProtocol.Close()` unadvises the OCX event handlers and calls
`base.Close()`. **It never disposes the hosted `AxHost` at all**, so every RDP control the
process has ever hosted is leaked — the likely cause of its long-standing "unresponsive after about
thirty minutes with several sessions open" reports.

This fork disposes them (`069fc11`, `2a5e7d8`, `83580bd`, `de8f97b`), which is why this fork
can freeze where upstream cannot: releasing a session that `mstscax` has not finished with
blocks *inside* the release, on the UI thread. And a wedged UI thread here does not merely
freeze the app. With `RedirectKeys` on, `SetRedirection` sets `KeyboardHookMode = 1`, which makes
`mstscax` install a low-level keyboard hook; Windows calls low-level hooks on the thread that
installed them, so while that thread is not pumping, **keyboard input serialises behind it
system-wide**. The failure mode of getting this wrong is a dead desktop, not a dead window.

Three measurements pin the design, each isolated to one variable:

1. **Disposing a control whose session is still up blocks.** Separated cleanly in a real log:
   `Connected == 0` at close took 44–73 ms, ten times out of ten; `Connected == 1` took
   72–124 ms four times and **47.5 seconds** on the fifth. `RequestClose()` does *not*
   distinguish the two — it answers `controlCloseWaitForEvents` and the control raises
   `OnConfirmClose` ("go ahead"), which is not a disconnect. Wait for `Connected == 0`, not for
   the event.
2. **A live RDP control still undisposed at process exit crashes the process** — so simply
   abandoning it is not an option either.
3. **So does one that has merely been unparented.** Dispose it with its parent chain intact and
   exit is clean every time.

So this is not the "hang or leak" choice it looks like: disposing inline hangs, abandoning
crashes at exit. The fork parks instead (`Connection/Protocol/DeferredControlDisposal.cs`). The
control moves straight from the dying tab onto an off-screen holding form — **never
parentless** — and each `Application.Idle` turn asks whether its session has gone down,
disposing it for real when it has. `ProgramRoot.Main` disposes whatever is left after
`Application.Run` returns, because nothing may still be alive at exit.

Two traps around this:

- **`HostedControlCanBeDisposed` overrides must combine with `base`**
  (`base.HostedControlCanBeDisposed && …`). Replace rather than combine and the shutdown check
  silently stops applying.
- **A session that is still logging on must not be interrupted** (`3afeab6`): it is not yet
  connected, so a naive `Connected == 0` test reads it as safe to dispose.

`TeardownWatchdog` reports a teardown stage that stops returning, so a hang that does happen is
diagnosable from a log rather than only from a hung machine. That is also why the watchdog
cannot log through `MessageCollector` — see
[ADR-0017](0017-one-filtered-writer-for-every-log-line.md).
