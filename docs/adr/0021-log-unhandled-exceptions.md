---
date: 2026-09-23
---

# Log unhandled exceptions before the process goes down

A crash was the one failure the log could not report. The application installed no handler of any
kind — `FrmMain` said so in as many words, and used a `try`/`catch` around its posted startup
callback to work around it — so an exception that escaped a `catch` went to a console nobody was
reading, and the process exited. On a machine that is only deployed to, which ADR-0013 makes the
normal case for anything behavioural, that left nothing behind at all.

This is not hypothetical. Running `mRemoteNG.exe --selftest` with its output redirected ends in a
`Win32Exception (1406): Error creating window handle` on roughly a third of runs. Before this
change that exception appeared in the log nowhere; the run simply exited 127 after reporting
`RESULT: PASS`. The failure is intermittent and environment-dependent, which is exactly the kind
that is impossible to chase without a log line.

## What was installed

Two handlers, in `ProgramRoot.Main`, before anything that could fail:

- **`AppDomain.CurrentDomain.UnhandledException`** — on every path, including `--selftest`. This is
  a notification: the process still dies exactly as it did before, so nothing about the
  application's behaviour changes and the only difference is that the log now says what happened.
- **`Application.ThreadException`** — on the interactive path only. Registering this
  **suppresses the WinForms error dialog**, so having handled the exception the handler has to see
  the user off itself: it logs, shows the application's own task dialog (ADR-0012) naming the log
  file, and exits. Letting the application carry on after a failure it did not expect, in whatever
  state that failure left it, is worse than stopping.

It is deliberately *not* installed under `--selftest`. A modal dialog on a non-interactive run is
never answered, and a self-test that hangs instead of failing is worse than one that crashes.

## Reports go through the filter, like everything else

The handlers write through `FilteredLogWriter`, not to the sink directly (ADR-0017). The message
collector cannot be used here — its writers run on the calling thread and may not be attached yet —
but the user's Options ▸ Notifications ▸ Logging settings still decide. Error messages are on by
default, so in practice a crash is logged unless someone has turned errors off on purpose, and
`CrashLoggerTests.ACrashReportHonoursTheErrorLoggingSetting` pins that rather than leaving it to be
discovered. The alternative — exempting crashes from the filter — was rejected for the reason
ADR-0017 gives: a setting that is quietly overridden silently does not mean what it says.

Nothing in the handlers may throw. They run while the process is already failing, and an exception
raised from a crash handler replaces a diagnosable crash with a mystifying one. The sink already
swallows IO errors; the handlers swallow everything else, the dialog included.

`Environment.Exit` rather than `Application.Exit`: the UI thread has just failed, and asking it to
run a graceful shutdown is asking the broken thing to tidy up. The log is on disk either way,
because the sink flushes every event as it writes it.

## Consequences

- This adds a feature and changes what the user sees when the UI thread fails, which is why it is
  recorded rather than just done (ADR-0014).
- An unhandled exception now costs one Error line plus the exception, and the process still ends.
- `--selftest` keeps the crash it always had; it is simply logged now. That is the end-to-end proof
  the change works, and it is how it was verified — the handler's own reporting is covered by
  `CrashLoggerTests`, but the wiring was confirmed by catching the real thing.
