---
date: 2026-09-22
status: amended by ADR-0019
---

# Reduce the runtime dependency set to log4net

`mRemoteV1` now has exactly one `PackageReference`: log4net. Everything else the fork inherited
was removed, each for its own reason:

- **AdmPwd.PDSWrapper** (`252cd01`) — LAPS/AdmPwd.E password retrieval. A native wrapper for an
  enterprise feature outside this fork's scope.
- **BouncyCastle** (`08cf2ba`) — used for AES+GCM only, which `System.Security.Cryptography`
  provides. A whole cryptography library for one algorithm.
- **MagicLibrary (Crownwood.Magic)** (`5e23c29`) — a docking/UI library from the .NET 1.x era.
- **DockPanelSuite** — see [ADR-0007](0007-static-layout-instead-of-dockpanelsuite.md).
- **ObjectListView** — see [ADR-0008](0008-treeview-instead-of-objectlistview.md).
- **NUnitForms** (`b4b65fb`) — a UI-driving test library that had no working successor; the
  tests it supported were replaced by headless construction tests (`2e8a606`) and
  `--selftest` ([ADR-0013](0013-selftest-as-the-verification-mechanism.md)).
- **System.Management** (`8f902d1`) — pulled in to watch PuTTY's registry key via WMI, which
  also put a WMI call on the startup path. Replaced with `RegNotifyChangeKeyValue`, which is
  what that API is for.

log4net stays because file logging is the fork's primary diagnostic channel — behavioural
verification happens by reading a log off another machine
([ADR-0013](0013-selftest-as-the-verification-mechanism.md)) — and because it is configured in
code (`2ee61ff`) rather than from `app.config`, which was deleted along with it (`c940aa7`,
carrying settings over from the old build). See
[ADR-0017](0017-one-filtered-writer-for-every-log-line.md) for how the application reaches it.

log4net was itself replaced by Serilog on 2026-09-23; see
[ADR-0019](0019-serilog-instead-of-log4net.md). The invariant this record is about survived that
swap intact - Serilog's `net10.0` target has no transitive dependencies of its own, so
`mRemoteV1` still has exactly one `PackageReference`, and the test project no longer needs a
logging one at all.

The test project keeps NUnit 4 (`b4d14a0`, moved from NUnit 2-era `NUnit3TestAdapter`
conventions), NSubstitute, and log4net. `mRemoteNG.Specs` was dropped from the solution
(`23f69a2`) as an unmaintained third test project.

The motivation is not dependency purism: every one of these had to be evaluated for .NET 10
compatibility during [ADR-0003](0003-target-net-10.md), and a dependency that has to be
evaluated, updated or replaced at each framework bump is precisely the maintenance cost this
fork exists to avoid. The committed COM interop
([ADR-0004](0004-commit-the-mstsc-interop.md)) is the deliberate exception — it is a binary in
the tree specifically so that nothing has to be resolved at build time.

**Amended 2026-09-25: the invariant is on `src/mRemoteUG/mRemoteUG.csproj`, not on the
repository.** `Tools/IconGen` ([ADR-0026](0026-generate-the-icons-at-build-time.md)) references
SkiaSharp and two Microsoft Fluent icon packages. It is deliberately not in `mRemoteUG.slnx`, it
never ships, and everything it produces is committed - so a framework bump cannot break anything
that runs, which is the cost the paragraph above says this record is actually about. The shipping
assembly still has exactly one `PackageReference`.

That is the test for any future tool: not whether it has dependencies, but whether anyone has to
resolve them to build, run or ship the application. `Tools/verify-icons.ps1` is the reason this
stays honest rather than becoming a loophole - it re-renders and compares, so the committed output
can be checked against its source without the application ever gaining a rasterizer.
