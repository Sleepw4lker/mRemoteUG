---
date: 2026-11-09
status: accepted, amended by ADR-0043
---

# Run mutation testing with Stryker.NET, by hand, on a runner of its own

About 1,300 unit tests show which code runs, not whether they check what it does. Mutation testing
answers the second question: Stryker.NET changes the application's code in small ways and reruns
the tests against each change, and a change no test notices is code the tests run without checking.

`.forgejo/workflows/mutation.yml` runs Stryker.NET over `src/mRemoteUG` against
`src/mRemoteUG.Tests`, configured in `build/stryker/mRemoteUG.json`. It follows TameMyCerts.ACME's
mutation workflow, so the two repositories are read and maintained the same way:

- **Started by hand only** (`workflow_dispatch`): no push, pull request or schedule, because a run
  takes hours. It builds no installer, signs nothing and publishes nothing.
- **On the runner with the `performance` label**, a machine of its own, so a long run never holds up
  a build. At most four test hosts at once.
- **Report only.** No break threshold until three runs show a stable baseline. The HTML and JSON
  reports are uploaded as an artifact, and each run's survivors go into one tracking issue, grouped
  by area. A survivor is confirmed by hand before a test is written for it.
- **`dotnet-stryker` as a local tool**, pinned in `.config/dotnet-tools.json` with
  `rollForward: false` and restored from nuget.org like every other package. It is a build tool, not
  a runtime dependency, so ADR-0015 does not apply to it. The version is ACME's; a bump updates both
  repositories.
- **Stryker's default VSTest runner**, not its Microsoft Testing Platform runner. ACME needs the
  latter because xUnit v3 runs tests in a process of its own; NUnit with NUnit3TestAdapter still
  works under VSTest after the switch of `dotnet test` to the Microsoft Testing Platform (#73), and
  VSTest takes a test filter, which the MTP runner does not.
- **Per-test coverage analysis**, so code no test reaches (most of the WinForms UI) is reported as
  "no coverage" without running anything.

Generated code (`*.Designer.cs`, `*.g.cs`) and `App/NativeMethods.cs` are not mutated. The installer's
custom action is not referenced at all: it is tiny, targets .NET Framework 4.7.2 and runs inside
msiexec.

## Alternatives considered

- **A job in `build.yml`.** Every push would wait hours for it, and the signing and release steps
  would need conditions to stay out of it.
- **A nightly schedule.** Hours of a machine for a number nobody reads every day. Kept for later, if
  a stable baseline makes a regression worth catching automatically.
- **A break threshold from the start.** Without a measured baseline, any number would be a guess
  that either never fails or always does.
