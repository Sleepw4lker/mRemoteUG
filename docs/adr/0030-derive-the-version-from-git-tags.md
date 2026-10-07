---
date: 2026-09-29
status: proposed — supersedes ADR-0025
---

# Derive the version from the git tags, and release by tagging

The version is no longer written down anywhere in the tree. `ComputeVersionFromGit` in
`Directory.Build.props` asks `git describe` one question and derives the whole number from the
answer:

```
git describe --tags --long --always --dirty=.dirty --match "v[0-9]*.[0-9]*.[0-9]*"

  v2.1.0-0-g1a2b3c4d          HEAD is the tag        -> 2.1.0.<build>
  v2.1.0-5-g1a2b3c4d          five commits past it   -> 2.1.1.<build>
  v2.1.0-5-g1a2b3c4d.dirty    ...with local edits    -> 2.1.1.<build>+g1a2b3c4d.dirty
  1a2b3c4d                    no tag reachable       -> 2.1.0.<build>   (the seed, 2.1)
```

The fourth field is the CI run number, `0` for a local build. So **every build has a distinct
version, and pushing a `v*` tag is the only act that changes which version a build carries.**
That is the rolling model this record exists to write down: builds are rolling and daily rather
than cut for a release channel, and a tag does not make CI do anything to the
output beyond what it does for every other push.

This supersedes [ADR-0025](0025-release-on-a-tag.md), which is five days old. That record
argued the opposite case carefully, and the argument was sound on its own terms — it is worth
saying what changed rather than pretending it was wrong.

## What ADR-0025 got right, and why it goes anyway

0025 required the tag to name a version **already committed**, gated CI on the two agreeing, and
bumped `master` afterwards. Its reason for rejecting a tag-derived version was that doing so
"puts the number in the one place that cannot be reviewed before it takes effect."

That is true and it is the cost accepted here. What it bought was a version that could be
reviewed in a diff — but it bought that with: two files that had to hold the same number, only
one of which reached the binary; a script (`bump-version.ps1`) whose entire purpose was to keep
them in step and refuse to run when they had drifted; a CI gate to catch a mistyped tag; and a
deploy key that let CI push to a protected branch. Five moving parts existed to maintain one
number, and the failure they guarded against — shipping the old version in silence — was created
by the arrangement itself.

Deriving the number removes the number from review and removes all five. The thing being
reviewed was never really the version; it was whether two copies of it agreed.

## The consequence that is not obvious: the MSI

Between releases every build reads the same `major.minor.patch` and differs only in the fourth
field. **Windows Installer ignores the fourth field entirely when comparing versions.** With
`ProductCode="*"` — which this package uses, so every build gets a fresh product code — the
installer therefore sees the installed package as the same version, declines to treat the new
one as a major upgrade, and installs it *alongside*: two entries in Programs and Features, and
no upgrade. In a rolling model that is fatal, and it is exactly why 0025 pinned the fourth field
at 0.

`MajorUpgrade` now carries `AllowSameVersionUpgrades="yes"`, which sets
`msidbUpgradeAttributesVersionMaxInclusive`. Verified by reading the built package's `Upgrade`
table rather than by trusting the attribute: the row reads `VersionMax 2.1.0.0`,
`Attributes 513` — `MigrateFeatures | VersionMaxInclusive`. A package with the same version now
counts as a major upgrade.

The cost is real and is accepted: **downgrade protection stops applying between two builds of
the same patch.** An older rolling build will install over a newer one, because Windows
Installer considers them equal. Across a release boundary `DowngradeErrorMessage` still applies.
The alternative — putting the run number in the *third* field so every build is genuinely newer —
was rejected because it takes the patch slot away from the tag, so `v2.1.0` could no longer mean
2.1.0.

This also raises ICE61. The wixproj suppresses that one ICE **by name**, not with
`SuppressValidation`, so every other ICE still runs where validation can run at all.

## Two version strings, because they have different readers

`AssemblyVersion` holds its fourth field at 0; `FileVersion` carries the build number. Not
cosmetic: **measured on .NET 10, the roaming `user.config` path is keyed on `AssemblyVersion` and
on nothing else.** A build number in that field would move every user's settings directory on
every single build. The MSI binds its `ProductVersion` off `FileVersion`, which is where the
build number needs to be.

`InformationalVersion` is `<file version>+g<commit>[.dirty]`, and `GeneralAppInfo` splits it at
the `+`: the About dialog shows what comes before, the log keeps the whole string. The development
environment cannot reach an RDP or SSH host, so a fault is diagnosed by reading a log off the machine that
saw it ([ADR-0013](0013-selftest-as-the-verification-mechanism.md)); which commit produced that
binary is not recoverable from anything else in the log.

Two measurements shaped this and are recorded in
[platform-findings](../platform-findings.md): the SDK ships SourceLink and appends
`SourceRevisionId` to whatever informational version it is given, which produced the commit
twice; and `Application.ProductVersion` answers for the **entry** assembly, which under the test
host is `testhost.exe` — the version came back as `17.11.1`. `GeneralAppInfo` now reads the
attribute off its own assembly, as `Copyright` beside it already did.

## No MinVer

MinVer does this, is better tested than the ~60 lines in `Directory.Build.props`, and was the
starting proposal. It was rejected on [ADR-0015](0015-purge-third-party-dependencies.md)'s
stated test — *"not whether it has dependencies, but whether anyone has to resolve them to build,
run or ship the application."* MinVer has to be resolved to build. Since the build number comes
from CI rather than from commit height, almost nothing of what MinVer offers was actually being
used here.

The arithmetic is exercised without manufacturing tags: `VersionDescribeOverride` substitutes a
`git describe` string, which is also the only way to test it in a repository whose git guard
refuses to let anyone create a tag by hand.

## What is gone

`Tools\bump-version.ps1`, `Tools\rename_installer_with_version.ps1`, `Tools\exes\sigcheck.exe`,
the CI tag/version gate, and the deploy-key push to `master`. The package is named by MSBuild
from the same `$(FileVersion)` that stamped the executable, so the rename that read the version
back out of the binary with Sysinternals `sigcheck.exe` under `-ErrorAction SilentlyContinue` —
and left an unversioned MSI when it failed — has no reason to exist. A `Move` task fails the
build instead.

The MSI goes to the Forgejo **artifact store**, on every push including a tagged one, alongside
test results and the self-test report — the same place, the same zip wrapping, for all of them.
An earlier revision of this workflow instead PUT the MSI to the Forgejo generic package
registry, specifically to avoid that wrapping, and separately created a Forgejo Release with the
MSI attached when the push was a `v*` tag. Both were removed: the builds were rolling and
daily, and a tag's only remaining effect is the version number a build carries. A
tagged build's MSI is retrieved from that run's artifact like any other build's.

## What is unchanged

`CHANGELOG.TXT` is still edited by hand before tagging. Signing is still unwired — no
certificate — so the uploaded MSI is unsigned; with no release notes to carry a SHA256 anymore,
`Get-FileHash` against the artifact is the only integrity check available, and it is not run by
CI. The self-test is still graded on its report rather than its exit code
([ADR-0013](0013-selftest-as-the-verification-mechanism.md)).

**Amended 2026-10-01: signing is wired, and the single `dotnet build` described above is now
two.** There is a certificate, behind a remote signing service that CI calls, and every build
signs the executable, the DLLs that are ours, the custom action and the package — the payload
*inside* the package included, which is what forced the split: everything WiX packages is read
from `bin\Release` during its own compile, so the payload has to be signed between the
application build and the installer build. [ADR-0032](0032-sign-on-the-signing-server.md) has
it. The uploaded MSI is signed; `Get-FileHash` is no longer the only check a downloader has.

**Amended 2026-10-07: a tag drafts a release again.** A tagged build now drafts a Forgejo release
with the signed MSI attached, and publishing the draft publishes the release to GitHub - so a tag
is once more the start of a release channel, not only a version number. The rolling builds and
the derived version are unchanged. [ADR-0035](0035-publish-releases-to-github-as-snapshots.md)
has it.
