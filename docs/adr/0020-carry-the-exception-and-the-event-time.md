---
date: 2026-09-23
---

# Carry the exception and the event time to the log, instead of flattening them away

A message used to lose two things on its way to the file, and both of them were the sort of thing
that only matters once, on a machine nobody can attach a debugger to.

## The exception

`MessageCollector` had two helpers, and each kept a different half of the exception:

- `AddExceptionStackTrace` (69 call sites) rendered `ex.Message + ex.StackTrace` and **dropped the
  entire `InnerException` chain.**
- `AddExceptionMessage` (47 call sites) walked the chain with `GetExceptionMessageRecursive` and
  **dropped the stack.**

Whichever a caller reached for, half the evidence was gone before anything was written. The half
that went missing was usually the half worth having: `AddExceptionStackTrace` was the one used
around reflection, XML deserialization and COM — exactly the calls that wrap. A
`TargetInvocationException` out of `ConnectionInfo.TryGetInheritedPropertyValue` logged
*"Exception has been thrown by the target of an invocation"* and a stack of reflection frames, and
the actual cause was discarded. `SelfTest` already unwrapped `TargetInvocationException` by hand,
which is a fair sign of how often the shape comes up.

Two smaller losses came with it. The exception **type** was recorded at five sites in the whole
product, and `Exception.Message` is localized — so a log from a German machine named the failure in
German and nothing else identified it. `COMException.HResult`, the one field that is greppable
across machines, appeared nowhere but `RdpClientCandidates`.

**`IMessage` now carries the `Exception` itself.** `Text` stays the short, readable sentence — the
message and the reason down the inner chain — because that is what the notification list and the
pop-up show, and `NotificationMessageListViewItem` flattens newlines into a single row, so a stack
trace there was one unreadable line thousands of characters wide. The log gets
`Exception.ToString()` on the lines *after* the message line, which is where log4net put it too
(`PatternLayout.IgnoresException`), so the one-line-per-event shape ADR-0013 depends on is
unchanged. Type, stack and every inner exception arrive together.

The alternative was to keep flattening but flatten better — one renderer producing all four parts
as a string. That is a smaller change and it would have closed the same gaps in the file, but it
puts the stack back into `Text`, which is the one place it actively hurts, and it leaves the
decision about how much to show with the collector rather than with each writer.

**The two helpers then became the same method, so one of them went** (ADR-0014): the 69
`AddExceptionStackTrace` call sites are now `AddExceptionMessage`. No call site changed shape —
both helpers already took `(string message, Exception ex)`, which is why this cost a rename and
not a sweep.

## The event time

`Message.Date` was captured when the message was created and then never used by the log writer, so
Serilog stamped each line when it was *written*. For a live message those are the same instant. For
anything replayed out of `MessageCollector.SubscribeAndReplay` they are not: that exists precisely
because the writers do not exist until `frmMain_Load`, so the startup banner, command-line parsing,
settings loading and connection-file failures are all reported before there is anywhere to put
them, and handed over in a batch afterwards. Every one of those lines carried the replay time, which
bunched the whole of startup onto a single instant — and startup timings in the log are how the
effect in ADR-0018 is observed in the first place.

`ILogSink` now takes the timestamp as a parameter. `SerilogLogSink` builds the `LogEvent` itself
and calls `ILogger.Write(LogEvent)`, because `Information()` and friends always stamp `Now`.

Enrichers still run on that path — Serilog applies them when it dispatches the event — so the
thread column is still filled in on the thread that logged. That was worth confirming rather than
assuming, and `LoggerTests.TheThreadColumnNamesTheThreadThatLogged` is what confirms it: it asserts
a thread *name* appears, which the formatter's fallback could not produce because the fallback uses
the numeric managed thread id.

## Consequences

- `IMessage` gained a member. `Message` is its only implementation, which is what made this cheap.
- The user-visible text of an error got shorter, and the log got longer. Both are improvements in
  the direction each audience needs.
- `ILogSink` methods are now `(message, timestamp, exception = null)`. The exception is optional so
  that the ordinary case stays a one-liner.
- Nothing about the line format, the file name, the log path or the bug-report instructions
  changed, so ADR-0013 and ADR-0019 still hold as written.
