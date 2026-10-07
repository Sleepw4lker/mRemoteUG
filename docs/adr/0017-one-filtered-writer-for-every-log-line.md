---
date: 2026-09-23
---

# Route every log line through one filtered writer

The application has two ways to record something: `MessageCollector`, which fans a message out
to its writers (the notification list, a popup, the text log), and `Logger.Instance`, which
writes straight to the log file. The Options ▸ Notifications ▸ Logging filter applied only to the
first. Code that used the second — because it had to — ignored the user's settings entirely.

`FilteredLogWriter.Instance` becomes the **single** thing in the process that writes to the log:
a `MessageTypeFilterDecorator` over a `TextLogMessageWriter`. The message collector's text-log
writer *is* that instance, so there is no second place where the log filter could be decided
differently.

The reason a second path exists at all is worth recording, because it looks removable:
**`MessageCollector` runs its writers on the calling thread.** `TeardownWatchdog` exists to
report a UI thread that has stopped returning ([ADR-0005](0005-park-rdp-controls-before-disposing.md)),
so it must stay off that thread — it cannot use the collector without deadlocking on precisely
the condition it is reporting. Routing it through `FilteredLogWriter` instead gives it the
user's filter settings without the collector's threading.

**The filtering options read `Settings.Default` on every message rather than caching.** That is
a deliberate cost: it means a setting changed in the options dialog applies to the very next
message with nothing rebuilt — including while a teardown is hung, which is exactly when
someone would be turning logging up.

Landed as `0c41405`, with three consequences of the same consolidation following it: nothing
reported before the writers exist is lost any more (`ea869e1`), the log path options can no
longer contradict each other (`1854873`), and the two message boxes that deliberately ignore the
pop-up settings now say why (`24dfecf`).

Alternatives considered: make `MessageCollector` thread-safe and use it everywhere (rejected —
it would have to marshal, and marshalling to a wedged UI thread is the deadlock again); leave
`Logger.Instance` calls unfiltered (rejected — the setting then silently does not mean what it
says).

## Levels, and `--verbose`

Having one filtered path made it worth deciding what actually goes down it. Two further commits
(`6538436`, `2e3b6b0`) set the policy:

**Information is bounded by the number of sessions and application starts. Anything per-stage,
per-attempt or per-save is Debug.** It was not: teardown stages, resize attempts, capability
probes and per-save notices all sat at Information, so a default log was mostly the application
describing itself working normally. Closing one RDP tab wrote about fifteen Information lines,
and closing ten wrote a hundred and fifty — which is exactly the case someone needs to read.
`ProtocolBase.LogClose` alone has eighteen call sites, every one firing on every close; they are
now Debug behind a single Information line giving the close and its duration. A default RDP open
and close went from roughly fifteen Information lines to about four.

**That reclassification was only possible because `--verbose` landed first**, and this is the
part worth recording. Debug logging is off by default, so the only way to be *certain* a line
would reach a user's log was to report it as Information — `RdpProtocol.cs` said so in a comment.
That is why the detail was at Information, and why moving it would simply have been moved back by
the next person who needed to see something. `mRemoteNG.exe --verbose` turns Debug on for one run
without touching the saved setting, so "reproduce it under `--verbose` and send me the log"
replaces "change this setting, restart, reproduce, and remember to change it back". Given
[ADR-0013](0013-selftest-as-the-verification-mechanism.md) — where a behavioural finding costs a
manual round trip to another machine — a switch that needs no instructions is the difference
between one round and two.

Consequences:

- **`--verbose` is read on the first line of `Main`**, setting
  `LogMessageTypeFilteringOptions.ForceDebugMessages`. Recognising it in
  `StartupArgumentsInterpreter` would be far too late: that does not run until `frmMain_Load`, by
  which point the startup detail it is most wanted for has already been reported.
- **Nothing that matters is behind the switch.** A teardown stage that never returns is still
  reported at Warning by `TeardownWatchdog`, and two conditions that had been *failures*
  mis-reported as Information were raised to Warning in the same pass: PuTTY's DPI awareness
  being unreadable, and parked sessions that never settled before shutdown disposed them anyway
  ([ADR-0005](0005-park-rdp-controls-before-disposing.md)). Logging a failure too quietly is the
  same bug as logging noise too loudly.
- **`--selftest` reports whether `--verbose` is on, and stands its message-routing check aside
  for the duration** — that check is about the saved settings and would otherwise fail under
  `--selftest --verbose` for the one reason that is not a bug.
- **Anyone diagnosing from Information-level close timings needs the switch from now on.** That is
  the accepted cost.
