---
date: 2026-09-24
status: superseded by ADR-0030 (proposed)
---

# Build every push on Forgejo, and move the version only on a release tag

Until now the only thing that verified a commit was the machine it was written on, which
[ADR-0013](0013-selftest-as-the-verification-mechanism.md) describes at length and
[ADR-0022](0022-nullable-a-directory-at-a-time.md) leans on. `.forgejo/workflows/build.yml`
adds a second machine: a self-hosted Windows CI runner that, on **every
push**, builds the application and the MSI and runs the 1162 NUnit tests, failing the run if
any of them fails. On a **release tag** it additionally publishes a Forgejo Release with the
MSI attached and then advances the patch version on `master`.

Nothing about the local story changes. The nullable ratchet stays in `Directory.Build.props`
rather than moving into the workflow, `--selftest` stays the tier that unit tests cannot
reach, and `dotnet build` remains the command that enforces the line. CI repeats that work on
a second machine; it does not become the place the rules live. A runner being down must never
be the reason a rule is not enforced.

## The version moves after the release, not before

The tag names a version that is **already committed**. You edit the two version files, commit,
tag `v2.1.0`, and CI builds exactly that commit; the tag, the executable's `FileVersion`, the
MSI's `ProductVersion`, the MSI's filename and the release all read 2.1.0. Only then does the
workflow bump `master` to 2.1.1 and push, so what sits on `master` is always the version being
worked towards rather than the one last shipped.

The alternative was to make the tag a bare trigger and let CI bump first, releasing the bumped
number. Rejected because a Forgejo Release hangs off a tag: releasing 2.1.1 from a run
triggered by some other tag means CI has to create a second tag for the release to attach to,
and the repository then carries two tags per release whose names disagree with each other.
Letting the tag *set* the version outright was also considered and rejected for the same
reason it is attractive — it puts the number in the one place that cannot be reviewed before
it takes effect.

The first step of the job is therefore a gate: on a tag push it compares `vX.Y.Z` against
`AssemblyInfo.cs` and refuses to build if they differ. It runs before the build so that a
mistyped tag costs seconds.

## Only the third field moves, and only via one script

The version is written down twice and only one copy has any effect — `AssemblyInfo.cs` reaches
the assembly, `Directory.Build.props` does not, because `GenerateAssemblyInfo` is false. That
trap is stated in [ADR-0003](0003-target-net-10.md) and its consequence for the package in
[ADR-0016](0016-wix-5-and-the-desktop-runtime.md): the MSI binds `ProductVersion` off the built
executable. A bump that edits only the props file ships the old number in both the binary and
the package, and nothing fails.

`Tools\bump-version.ps1` is the answer: one script that knows about both files, **reads the
current version from `AssemblyInfo.cs` because that is the copy that is true, and refuses to
run at all if the props file disagrees with it.** Drift between the two is the failure the
arrangement invites, and a release is the worst moment to find it. Every replacement asserts
it matched exactly once, so a rewrite that silently does nothing fails loudly instead — the
failure mode `rename_installer_with_version.ps1` has, running under `-ErrorAction
SilentlyContinue` throughout.

Only the first three fields move; the fourth is held at 0. Windows Installer compares just
`major.minor.build` when deciding whether a package is a major upgrade, so a bump confined to
the fourth field would produce a package with a fresh `ProductCode` (`ProductCode` is `*`) that
**installs alongside its predecessor instead of replacing it** — two entries in Add/Remove
Programs and no upgrade. For the same reason the script rejects anything exceeding 255.255.65535,
which nothing else in the repository checks. The patch field is what CI advances; minor and
major are raised by hand, through the same script so the same checks apply.

`CHANGELOG.TXT` is left alone. Renaming its `Unreleased:` heading is part of deciding to
release, not part of building one, and it stays where `928f682` put it: in the commit you make
before you tag.

## What the workflow will not do on its own

The self-test runs and its report is captured, but it **never fails the run**. Its exit code is
not currently trustworthy: there is a known intermittent crash during shutdown after the run has
already printed `RESULT: PASS`. Gating on it would make every release a coin toss. The per-check
lines are printed into the log and the report is uploaded, which is what a human needs anyway.

**Amended 2026-09-25: the second reason given here is gone.** The "Connection icons" check also
failed, and was described as clean-tree fallout from [ADR-0024](0024-rename-to-mremoteug.md).
That was the wrong diagnosis: every icon resolves and the shipped default is correct. It failed
on any profile that had ever run a pre-rename build, because `ConDefaultIcon` is user-scoped and
`Settings.Upgrade` carries the old value forward out of the previous version's `user.config` -
including on a runner whose profile survives between versions. The default is now resolved
against what is actually embedded, so the check passes and only the shutdown crash is left.

The installer step asserts that exactly one MSI exists and that its **name carries a version**.
That is not belt-and-braces: the rename that puts the version there swallows its own errors, so
an unversioned `mRemoteUG-Installer.msi` is the only visible symptom of a broken post-build, and
it should stop the run rather than be published.

Signing remains unwired. `Tools\sign_binaries.ps1` is a no-op unless `CertPath` and
`CertPassword` are passed, and CI passes neither. Deliberate for now — there is no certificate —
but it means the published MSI is unsigned, and the release notes carry a SHA256 because that is
the only integrity check a downloader has.

The bump push uses a **deploy key**, not the run's built-in token, so that the one operation
that writes to `master` needs a credential that was granted on purpose. Its commit ends in
`[skip ci]`; without that, pushing to `master` from the tag run would start a second build of a
commit nothing is waiting on.
