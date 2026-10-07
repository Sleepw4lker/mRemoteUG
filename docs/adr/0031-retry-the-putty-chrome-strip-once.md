---
date: 2026-09-30
status: proposed
---

# Retry the PuTTY chrome strip once, after reading the result back

Uwe reported a PuTTY session occasionally showing a window border — maybe one session in ten.
[ADR-0023](0023-host-stock-putty-as-a-maximised-child.md) hosts stock PuTTY by stripping its
caption and frame, reparenting it, then maximising it, on the reasoning that a maximised window
only ever receives `SIZE_MAXIMIZED`, which never touches the style, so nothing after the strip
can put the caption back.

That reasoning has one gap: `SetParent` sits between the strip and the maximise, and it is not
inert. Measured with a 50-run loopback harness outside this repo, calling `HostedPuttyWindow.Adopt`
against a real PuTTY 0.85 exactly as `PuttyBase.Connect` does: 2 of 50 runs read back a window
that was zoomed (the `ShowWindow(SW_MAXIMIZE)` had already run) *and* captioned — style
`0x15EF0000` against the clean `0x152B0000`, which decodes to exactly the pre-adopt style OR'd
with `WS_MAXIMIZE`. The measured detail is in
[docs/platform-findings.md](../platform-findings.md#putty-hosting). Since PuTTY's
`SIZE_MAXIMIZED` branch never touches the style, the only way to reach that combination is a
`SIZE_RESTORED` landing on PuTTY's thread somewhere between the strip and the maximise —
`SetParent` recomputing the window's position against its new parent is the one step in that
window capable of producing it, and it does not happen every time, which matches the reported
rate.

**Decision: after maximising, read the style back; if the caption is present, strip and maximise
again, once.** Neither retried step is `SetParent`, which already ran and does not run again, so
the retry is not exposed to the same race. Confirmed against the same harness with the retry in
place: 0 captioned runs in 150. `HostedPuttyWindow.Adopt` now does this unconditionally; no
caller-visible change.

**Rejected: insert a delay before maximising, to let any pending `SetParent` fallout settle.**
Untestable from outside the target process — there is no signal to wait *for*, only a duration to
guess, and guessing a duration long enough to be reliable would slow down every session opening,
not just the unlucky ones.

**Rejected: loop until stable instead of a single retry.** The harness never needed a second
retry across 150 runs; a bounded single retry is simpler and the existing `LogHostedWindow` warn
already exists to catch it if that assumption turns out to be wrong in the field.

**Consequences.**

- `Adopt` now calls `GetWindowLongPtr` once more than before in the common case (to read the
  style back), and repeats two cheap calls (`SetWindowLongPtr` twice, `ShowWindow` once) in the
  uncommon case. No new failure mode: if the retry does not clear the caption, `Adopt` returns
  the way it always did, and the existing warning in `PuttyBase.LogHostedWindow` still names it.
- The strip is now factored into `HostedPuttyWindow.StripChromeStyles`, called from `Adopt` and
  from the retry; `--selftest`'s `CheckPuttyHosting` exercises the same `Adopt`, so it covers the
  retry path too, though not the race itself — the race needs many runs to show up 4% of the
  time, and the self-test runs `Adopt` once.
