---
date: 2026-09-22
status: accepted
---

# Build verification into the application as --selftest

The environment this fork is developed in cannot reach an RDP or SSH host. The application's
core behaviour therefore cannot be exercised where it is written,
and the things that break in a WinForms application of this age are mostly things no unit test
sees: a malformed manifest, a form that throws in its constructor, a missing embedded resource,
an ActiveX control that will not create.

Rather than accept that gap, `a170da0` added **`mRemoteNG.exe --selftest`** (`mRemoteUG.exe`
since [ADR-0024](0024-rename-to-mremoteug.md)): the real startup path, run non-interactively,
printing PASS/FAIL per check, setting the process exit code and writing
`%LOCALAPPDATA%\mRemoteUG\mRemoteUG-selftest.log`. It covers logging, settings, embedded
resources, construction of every form and control, creation of the RDP ActiveX control, the
effective `HighDpiMode`, each monitor's DPI, every container's autoscale
baseline, a real task dialog shown and closed from `page.Created`, and (since 2026-09-24) a real
PuTTY hosted in a scratch panel when one is configured.

The alternative was the usual one — launch the app, check `MainWindowTitle`, hope. It cannot
tell a working start from a window that appeared and is broken, and it reports nothing a
non-developer could send back.

Verification here is therefore three-tiered, and the tiers are not interchangeable:

1. `dotnet build` and `dotnet test` — correctness of code that can be tested headlessly.
2. `mRemoteUG.exe --selftest` — that the thing actually starts and constructs on a real
   machine. `--selftest --largefont` repeats it with a 1.5× UI font, which is the only way to
   test a larger system font at all (see [ADR-0011](0011-take-fonts-from-windows.md)).
3. Anything genuinely behavioural — live sessions, protocols — requires copying the binaries to
   another machine and reading the log back (since
   [ADR-0034](0034-log-outside-the-roaming-profile.md), `%LOCALAPPDATA%\mRemoteUG\mRemoteUG.log`).

Tier 3 costs a manual round trip, and that shapes how code gets written here: **diagnostic
logging is built into the code rather than added during debugging.** At what level is
[ADR-0017](0017-one-filtered-writer-for-every-log-line.md)'s rule: a value a diagnosis depends on
that occurs once per session or per start - a `discReason`, the display scale declared before
connecting - goes out at Information, so it is in every default log and a tester needs no
configuration to send it back. Anything per stage, per attempt or per save goes out at Debug, which
only `--verbose` writes. So a round trip aimed at one specific fault asks for a `--verbose` log,
and says so; a round that did not is recorded in platform-findings as having proved nothing.
Values that can only be observed on a connected machine (RDP `discReason` codes, for instance)
are logged first and hardcoded only in a later round. `--selftest` failing the run when
`HighDpiMode` is not `PerMonitorV2` is the same idea: make the machine assert it, because
nobody here can see it.

This is the ADR most likely to look like over-engineering to a reader with a normal
development machine. It is a direct consequence of the environment, and
[ADR-0010](0010-per-monitor-v2.md) and [ADR-0012](0012-task-dialog-on-comctl32.md) both
depended on it to find real defects.

*History: written 2026-09-22; corrected 2026-10-07 - the trace level follows ADR-0017: per-session
values at Information, per-stage detail at Debug behind `--verbose`, which this record used to
put at Information.*
