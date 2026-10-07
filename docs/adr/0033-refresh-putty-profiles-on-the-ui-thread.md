---
date: 2026-10-01
status: proposed
---

# Refresh the PuTTY Profiles on the UI thread, and serialise the registry notification

Uwe added a host in PuTTY, outside this application, while an SSH session was open. The moment he
saved it, mRemoteUG died. The log carries three `InvalidOperationException: Collection was
modified; enumeration operation may not execute` stacks in the same millisecond, all rooted in
`RegistryKeyChangeWatcher.OnKeyChanged` on `.NET TP Worker` threads: one in
`ConnectionTree.RebuildChildren` on the UI thread, one in `AbstractPuttySessionsProvider.Sessions`,
one in `ContainerInfo.SortOnRecursive`. All three were walking the same
`RootPuttySessionsNodeInfo.Children` list.

Two independent defects combined to produce it.

**The watcher raised one event on several threads at once.** `RegNotifyChangeKeyValue` gives a
one-shot notification, so the registration has to be renewed on every callback, and
`RegistryKeyChangeWatcher` renews it *before* raising `Changed` so a change happening during a
handler is not lost. That is correct and has to stay. But it also means the next notification can
fire on a fresh thread pool thread while earlier handlers are still running. Saving one PuTTY
profile is not one registry write: PuTTY writes every setting as its own `RegSetValueEx`, so a
single save signals dozens of times. Measured by writing 40 values to one watched key: **6 and 8
concurrent raises** of the one `Changed` event. The handler for a "something under here changed"
edge re-reads the whole key by definition, so it is never re-entrant, and no handler can be
written around this.

**The refresh ran on the watcher's thread.** `PuttySessionsManager.PuttySessionChanged` called
`AddSessions()` directly on whatever pool thread arrived, mutating and enumerating a tree that the
UI thread reads whenever the PuTTY subtree is expanded, filtered or rebuilt, and that the
connection property grid reads through `SessionList`. Even serialised to one thread at a time, that
is a data race with the UI thread; it had simply been getting away with it.

## Alternatives

**Lock the PuTTY Profile tree.** Rejected, and it is worth saying why clearly, because it is the
first thing anyone will reach for. `AddSession` raises a collection-changed event whose
`ConnectionTree` handler calls `Control.Invoke`, which blocks until the UI thread runs it. A
background thread holding the gate across that `Invoke`, with the UI thread wanting the same gate
to read `Children`, deadlocks — and a wedged UI thread freezes the whole desktop, not just this
application ([ADR-0005](0005-park-rdp-controls-before-disposing.md)). A lock would convert a crash
that restarts into a hang that does not.

**Make `ContainerInfo.Children` a concurrent collection.** Rejected. It is the tree model the whole
application is built on; changing its type to fix one off-thread caller is the tail wagging the
dog, and it would make every enumeration elsewhere silently tolerant of concurrent mutation rather
than correct.

**Debounce the notification on a timer.** Rejected as the primary fix: it narrows the window
without closing it, and adds a latency the user would notice as the tree updating late.

## Decision

**The refresh is marshalled onto the UI thread, and the watcher never raises `Changed`
concurrently with itself.**

`CoalescingDispatcher` (new, in `Tools`) posts one action to a `SynchronizationContext` captured
when `PuttySessionsManager.StartWatcher()` runs — which is on the UI thread, during startup — and
folds every request arriving while a run is outstanding into a single further run. It uses `Post`
rather than `Send`, so the watcher's thread never blocks on the UI thread and shutdown cannot
deadlock against a missing message loop. A null context, as in tests and anything before the UI
exists, means the action runs inline; this matches the `InvokeIfRequired` pattern already in
`ConnectionTree`.

`RegistryKeyChangeWatcher` keeps renewing the registration before raising, but gates the raise: a
notification arriving while a handler runs sets a flag, and the thread already raising loops once
more when it returns. Changes are therefore coalesced, never dropped and never concurrent.

Coalescing is not only about safety. Without it, one PuTTY save meant dozens of registry re-reads
and dozens of full tree rebuilds. Measured on the 40-value burst: **2 handler runs** instead of a
fan-out across 6–8 threads.

## Consequences

- The PuTTY Profile tree is only ever mutated on the UI thread. The `Control.Invoke` that
  `ConnectionTree.OnPuttySessionsCollectionChanged` used to make per added profile is now a direct
  call, so the refresh is also cheaper.
- `RegistryKeyChangeWatcher.Changed` has a stronger contract — at most one handler running, bursts
  coalesced — which any future user of the class gets for free. The cost is that a handler's
  duration now bounds how quickly a later change is seen.
- `StopWatcher` stops the dispatcher, so a notification racing shutdown does nothing rather than
  touching a tree that is going away.
- The self-test's `PuTTY session watcher` check now writes a 40-value burst and fails if it ever
  raises on more than one thread, and proves a refresh requested off-thread comes back to the UI
  thread through the live `WindowsFormsSynchronizationContext`. Against the unfixed watcher that
  check fails with 6 concurrent raises, so it will catch a regression
  ([ADR-0013](0013-selftest-as-the-verification-mechanism.md)).

The measured detail is in
[docs/platform-findings.md](../platform-findings.md#putty-profiles-and-the-registry-notification).

**Not verified here:** the development environment has no SSH host to connect to, and PuTTY is only reachable as a
Chocolatey shim, so the actual reported sequence — a profile saved in PuTTY while an SSH session is
open — has not been reproduced end to end. What is verified is the mechanism underneath it: the
concurrent raise, its disappearance, and the marshalling.
