---
date: 2026-11-10
status: accepted
---

# Run twelve mutation test hosts on the performance runner

Amends [ADR-0042](0042-mutation-testing-by-hand-with-stryker.md) on one point: Stryker.NET runs
twelve test hosts at once (`"concurrency": 12` in `build/stryker/mRemoteUG.json`), not four.

ADR-0042 took four from TameMyCerts.ACME, where Stryker's default of half the logical cores ran a
16 GB machine out of memory. The performance runner this workflow is pinned to has 12 cores,
20 vCPUs and 64 GB of RAM, and with four hosts a full run took over three hours (run 289: 3 h 24 min
for 4,663 tested mutants) while the runner showed almost no CPU load.

The hosts wait rather than compute. With per-test coverage each mutant reruns the tests that reach
it, and the RDP protocol tests take about 1.3 s each, most likely creating the real RDP ActiveX
control through COM on the UI thread (measured on 2026-11-10; where the time goes is #224). So the
run is bound by how many hosts wait side by side, not by CPU, and four left most of the machine
idle.

Twelve keeps a margin on both limits: a test host with WinForms and the RDP control takes a few
hundred MB, far inside 64 GB, and eight vCPUs stay free for the build and the runner itself. The
first full run with twelve records its duration and the runner's peak memory in its tracking issue;
if memory runs short, the number comes down there.

This repository now differs from ACME on purpose. ADR-0042's rule that a Stryker version bump
updates both repositories still holds; the host count is per machine, not per tool version.

## Alternatives considered

- **Stryker's default** (half the logical cores, ten here): close to twelve, but it moves with the
  machine the job lands on, and an unpinned number is what ran ACME out of memory.
- **Twenty, one per vCPU:** the hosts mostly wait, so it might go faster still, but it leaves nothing
  for the runner and the build, and it is a guess until a run at twelve has been measured.
- **Keeping four and making the tests faster first (#224):** worth doing anyway, but it does not
  stop sixteen vCPUs idling in the meantime.
