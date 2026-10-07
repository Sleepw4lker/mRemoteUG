---
date: 2026-09-23
---

# Replace log4net with Serilog, and write the file sink by hand

Logging moved from log4net 3.4.0 to Serilog 4.4.0. log4net was not broken and nothing was
failing; the move is to a library that is actively developed and whose `net10.0` target carries
**no transitive package dependencies at all**, so the swap is one DLL for one DLL and
[ADR-0015](0015-purge-third-party-dependencies.md)'s single-`PackageReference` invariant survives
literally rather than approximately.

That is the easy half. The rest of this record is about what had to be held still, because the
log file is not an implementation detail here — [ADR-0013](0013-selftest-as-the-verification-mechanism.md)
makes reading `%APPDATA%\mRemoteNG\mRemoteNG.log` (now
`%LOCALAPPDATA%\mRemoteUG\mRemoteUG.log`, [ADR-0034](0034-log-outside-the-roaming-profile.md)) off another machine the fork's verification
mechanism, and `README.MD` tells anyone reporting a problem to attach that exact file.

## Three things the swap was not allowed to change

**The line format.** `%date [%thread] %-6level- %message%newline` is reproduced byte for byte by
`Log4NetStyleFormatter`, down to log4net's own level spellings — `INFO` and `WARN`, not Serilog's
`Information` and `Warning` — left-aligned in six columns. A log written before the change and one
written after concatenate with no seam, which was checked by appending to a real pre-change file.

**Which file is the live one.** This is why the sink is hand-written rather than
`Serilog.Sinks.File`, and it is the only genuinely hard call in this record. log4net's
`StaticLogFileName` layout keeps `mRemoteNG.log` as the file being written and trails the backups
behind it as `.1` … `.5`. Serilog's own rolling does the reverse: the live content moves to
`mRemoteNG_001.log` and the base name is left holding the *oldest* content. Taking that would have
quietly invalidated `Logger.LogPath`, the Open File button on the options page, the marker
`--selftest` writes and reads back, five test fixtures, and the instruction in `README.MD`. The
file a user is told to send has to be the file being written, so `RollingLogFileSink` reproduces
log4net's scheme instead. It is about eighty lines, and `ILogEventSink` and `ITextFormatter` are
both in Serilog itself, so writing it costs nothing in dependencies.

**When the log file comes into existence.** Still on first write, never at construction. A user
who turns every message type off under Options ▸ Notifications ▸ Logging gets no log file at all,
and `--selftest` asserts it.

## The message template hazard

Serilog parses its first argument as a message template. log4net had no such behaviour, so this is
a hazard the swap *introduced* rather than one the application had.

It is narrower than it first looks — an unbound `{token}` renders as itself, so the obvious cases
(a GUID in braces, a `{0}`, a stray `{`) come through unharmed. Two cases do not:

- `{{` and `}}` are escapes and collapse to single braces, so `{{Default}}` is logged as
  `{Default}`.
- A name matching a property already on the event is **substituted** — a message containing the
  literal text `{ThreadId}` comes out carrying the thread id instead of the words.

Both were confirmed by reintroducing the defect and watching the test fail. `SerilogLogSink`
therefore passes the caller's text as a *property* under a constant template, and the formatter
reads that property directly rather than rendering the template, so caller text is never parsed at
all. `LoggerTests.BracesInAMessageAreLoggedVerbatim` pins it.

This is worth knowing about beyond this file: anyone who later reaches for Serilog's structured
API directly, rather than going through `ILogSink`, re-opens it.

## Consequences

- **`Logger.Log` is `internal ILogSink` rather than `public ILog`.** A public member cannot return
  an internal type, and nothing outside the assembly has any business logging directly
  ([ADR-0017](0017-one-filtered-writer-for-every-log-line.md)). `InternalsVisibleTo` already covers
  the tests. The four method names are log4net's — `Info`, `Warn` — precisely so the two sanctioned
  call sites, `TextLogMessageWriter` and `SelfTest`, did not have to change at all.
- **Serilog appears in exactly one file's worth of code**, behind `ILogSink`. The next framework
  change costs that file rather than a sweep.
- **`SetLogPath` rebuilds the pipeline instead of retargeting an appender.** Serilog loggers are
  immutable once built. This is the better half of the bargain: disposing closes the handle on the
  old file, which reassigning `RollingFileAppender.File` in place never clearly did.
- **The log is now UTF-8 without a BOM**, where log4net defaulted to the system ANSI codepage. A
  connection name with non-ASCII characters in it now survives into the log. An existing log that
  is appended to will have ANSI bytes in its older lines and UTF-8 in its newer ones; that is
  accepted rather than solved, since the alternative is keeping the worse encoding forever.
- **Logging still cannot throw.** log4net swallowed appender failures internally and
  `TextLogMessageWriter` has no `try`/`catch` of its own, so the sink swallows IO errors by
  design. A log path that has gone read-only costs lines, not the process.
- **Serilog decides nothing about levels.** The minimum level is Debug and the real filtering
  happens upstream in `MessageTypeFilterDecorator`, per ADR-0017. Serilog is a file writer here,
  not a filter.

## Alternatives not taken

**`Serilog.Sinks.File`.** Rejected for the live-file-name reason above. Working around it would
have meant teaching `LogPath`, the options page, `--selftest`, the README and five fixtures to
resolve "the newest `mRemoteNG*.log`" — more moving parts than the eighty-line sink, spread across
more places, to end up somewhere worse for the person being asked to attach a log.

**Keeping log4net.** Entirely viable; it worked. Had the one-package invariant been at risk, or
had the format or file naming had to change to accommodate Serilog, that would have been the right
answer.
