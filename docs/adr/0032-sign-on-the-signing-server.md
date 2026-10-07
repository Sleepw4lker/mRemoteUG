---
date: 2026-10-01
status: proposed
---

# Sign on the runner, against a signing server, and split the build to make room for it

The published package was unsigned, and so was everything inside it.
[ADR-0025](0025-release-on-a-tag.md) and [ADR-0030](0030-derive-the-version-from-git-tags.md)
both recorded that as deliberate-for-now with the same reason — there was no certificate — and
0030 named the consequence: `Get-FileHash` against the artifact was the only integrity check a
downloader had.

There is a certificate now, behind a remote signing service that CI calls. It takes a file and
returns it signed, with a verb per extension — `exe`, `dll`, `msi` — and where to find it and how
to authenticate to it live in the CI environment, not in this repository.

Every build on the runner now signs the executable, the three DLLs that are ours, the custom
action stub and the package. Not only the package: the files **inside** it are signed too, which
is the part that dictated everything else about this.

## Signing lives in the workflow, and nowhere else

`.forgejo/workflows/build.yml` is the only file that knows how to sign. No `.csproj`, no
`.wixproj`, no `Directory.Build.targets`, no script in `Tools\`. A local
`dotnet build mRemoteUG.slnx -c "Release Installer"` produces unsigned output exactly as it
always did, and that is the point: the signing service is available only to CI, the build
has to keep working on the others, and a build file that reaches for a server it cannot find is a
build file that fails for the wrong reason.

The alternative was an MSBuild target — `AfterTargets="Build"` on the application project, gated
on a property CI passes. It is the conventional answer, it would have needed no split build, and
it was rejected: it puts signing into the thing every contributor runs, where it then needs an
opt-out, and the opt-out is what silently produces an unsigned release. Keeping it in CI means
there is exactly one place that signs and it always signs. Note the near-miss if that is ever
revisited: WiX's own SDK already defines no-op `SignMsi`/`BeforeSigning`/`AfterSigning` targets
and wires them in when `SignOutput` is `true`, so a property of that name is already taken.

Because the workflow asks for signing on every run, a failure to sign is a failed run. There is
no path through `Invoke-CodeSigning` that leaves a file unsigned and returns successfully — not a
missing variable, not a signing call that exits non-zero, not an empty or unchanged reply, not a reply
with no signature on it. An artifact that is unsigned but looks signed is the one outcome worth
designing against.

The cost of keeping it out of the build files is that the helper is written to a file by the
workflow at run time, because it is needed at two points with a build between them and the
alternative was two copies of it in the same YAML.

## The build is split in two, and that is not an accident

It used to be one command:

```
dotnet build mRemoteUG.slnx -c "Release Installer"
```

which built the application and the package together. **Everything the package carries is read
from disk during the wixproj's `CoreCompile`** — `HeatDirectory` globs
`src\mRemoteUG\bin\Release`, `MainExeFragment.wxs` names `mRemoteUG.exe` by path, and the custom
action is a `Binary` row streamed in from the CustomActions output. So the payload has to be
signed *before* the wixproj compiles, and one command that produces both leaves nowhere to put
that step.

It is now two: `-c Release` builds the application (the wixproj does not build in that solution
configuration at all), and after the tests and the signing, the `.wixproj` is built on its own.

Two measurements forced that shape, and both are in
[platform-findings](../platform-findings.md):

- **A build that runs after signing silently unsigns.** MSBuild's `Copy` skips only files that
  match in both size and last-write time, and a signed file matches in neither, so
  `bin\Release\mRemoteUG.dll` is replaced by the unsigned `obj\Release\mRemoteUG.dll` again.
  `dotnet test` is such a build. Signing therefore comes after the last build that touches the
  application, which puts it after the tests and the self-test rather than next to the build.
- **A lone `.wixproj` build does work**, given `-p:SolutionDir=`. The comment this replaced said
  it could not — that `$(var.SolutionDir)` is defined only by a solution build — which was true
  but did not say why, and the why is what makes it fixable: MSBuild assigns `SolutionDir` the
  literal sentinel `*Undefined*` when nothing has set it, and WiX guards the constant on exactly
  that sentinel, so a project build never hands it to the preprocessor at all. Passing it as a
  global property settles that and a second, silent breakage with it — `HarvestPath` is built
  from `$(SolutionDir)` while it is still empty, so it comes out relative and harvests nothing.
  `-p:BuildProjectReferences=false` then stops the build rebuilding the application, which is the
  whole point. It is not needed for the preprocessor: nothing WiX reads comes from
  project-reference metadata, and with the switch on, only `GetTargetPath` runs — a target with
  no dependencies at all.

The alternatives were a staging directory of signed files (rejected: `MainExeFragment.wxs` and
the `Binary` row resolve their own paths, so `HarvestPath` alone cannot redirect them, and the
rest is only redirectable by editing the `.wxs`) and signing the package but not the payload
(rejected: it is the weaker half of the guarantee, and the half a user never sees).

## What is signed, and what deliberately is not

`mRemoteUG.exe`, `mRemoteUG.dll`, `Interop.MSTSCLib.dll`, `AxInterop.MSTSCLib.dll`,
`mRemoteUG.Installer.CustomActions.CA.dll`, and the package. The interop assemblies are ours —
generated once and committed ([ADR-0004](0004-commit-the-mstsc-interop.md)) — and carried no
signature of any kind before this.

`Serilog.dll` keeps Serilog's own signature. Re-signing a third-party assembly replaces its
publisher's statement with ours, about code that is not ours, and there is nothing to gain by it.
`WixToolset.Dtf.WindowsInstaller.dll` is third party too and is not packaged at all.

One thing cannot be helped and should not be rediscovered as a bug: **the managed custom action
inside the signed `.CA.dll` is unsigned.** DTF's `MakeSfxCA` has already packed it into the
native stub by the time any build output exists, so signing the stub is as deep as this goes
without taking over `MakeSfxCA` itself.

Two relics went with this: `Tools\sign_binaries.ps1` and `Tools\verify_binary_signatures.ps1`,
inherited from mRemoteNG and never once run here. Neither could have worked. One returned early
unless a certificate path and password were passed, and nothing passed them; it also timestamped
against `http://timestamp.verisign.com/scripts/timstamp.dll`, dead for years. The other declared
that same path mandatory and therefore **threw on every build** — visibly, in the log, for as
long as the fork has existed, without failing anything.

## What this still does not prove

The signing service is available only to CI, so the call against it is unproven anywhere
else. What was proven before the first push, against a stand-in signer driven
by a throwaway self-signed certificate: the transport round-trips a PE byte-exactly from
a path containing a space; the signed file comes back to exactly the path that went in, under its
own name; the package built afterwards carries the signed payload, read back out of its `File`
table; and the verify step fails, by name, when one file is put back unsigned.

`Get-AuthenticodeSignature` reports `UnknownError` — *"a certificate chain processed, but
terminated in a root certificate which is not trusted by the trust provider"* — for a signature
whose issuer the checking machine does not trust. That is a statement about the machine, not the
file, so **the verify step passes on a present signature rather than on a valid one** and fails
only on `NotSigned` and `HashMismatch`. Demanding `Valid` would make the step fail on any runner
whose trust store lacks the issuing CA.

Two things remain observable only where a package can be installed: the signatures on the payload
inside the package, which live in the embedded cab, and what the UAC prompt says the publisher
is.

**Amended 2026-10-01: the one unsigned copy this left in a CI workspace is gone.**
`copy_release_installer.ps1` used to drop a copy of the package into `Release\` during the
installer build — which is *before* the signing step, so that copy was the pre-signing one.
Nothing read it there and CI takes the package from `src\mRemoteUG.Installer\bin\Release\en-US`
directly, so it was a trap rather than a fault. The script and the chain around it are gone; the
wixproj now copies the package itself, and only when `$(BuildNumber)` is 0, which is a local
build.
