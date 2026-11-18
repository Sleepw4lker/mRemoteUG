---
date: 2026-11-09
status: accepted
---

# Embed the application's symbols in mRemoteUG.dll instead of shipping a .pdb

The installer harvests the whole of `src\mRemoteUG\bin\Release`, so the package installed
`mRemoteUG.pdb` next to the executable. Nobody had decided that; it was simply in the folder.
The symbols do earn their place, though: `CrashLogger` writes every unhandled exception with its
stack trace into the log, and the log is the evidence a user's report comes with (ADR-0013). File
names and line numbers only appear in that trace when the runtime finds symbols.

`src/mRemoteUG` now builds with `DebugType=embedded`. The portable PDB is stored inside
`mRemoteUG.dll`, the assembly that holds the managed code - `mRemoteUG.exe` is the .NET apphost
and does not change. So:

- Logged stack traces keep their file names and line numbers.
- No loose `.pdb` exists for the harvest to pick up, and the package no longer installs one.
- The Authenticode signature on `mRemoteUG.dll` covers the symbols as well.
- The paths in the symbols are `/_/…`, not the build machine's, because CI builds with
  `ContinuousIntegrationBuild` and `Deterministic`.

The tests, the custom action and the tools keep the SDK default; only the shipped application
needed a decision.

## Alternatives considered

- **Keep shipping `mRemoteUG.pdb`.** Same stack traces, but a second file that only exists by
  accident of the harvest, unsigned, and easy to drop as clutter by someone who does not know why it
  is there.
- **Ship no symbols, keep the PDB as a CI artifact or a release asset.** Rejected because it would
  not work: a .NET stack trace written without symbols carries method names only, no IL offsets,
  so there is nothing to map back to a line afterwards. The artifact would be kept for a
  symbolisation that cannot happen.

## Cost

The package does not get smaller. Measured on 2026-11-09, `mRemoteUG.dll` grows by about 100 KB
and the MSI by 12 KB, because the embedded PDB is already compressed and the cab compresses it
worse than the loose file (docs/platform-findings.md, Installer).

## Guarded by

- `EmbeddedSymbolsTests` (unit tests, run against the Release build in CI): an exception thrown in
  the application names its line, with no `mRemoteUG.pdb` beside the assembly, and the assembly's
  debug directory holds an embedded portable PDB. Either goes red if `DebugType` is switched back
  to `none` or `portable`.
- `Tools/verify-msi-payload.ps1`, run by `build.yml` on the signed package: fails if the `File`
  table lists any `*.pdb` or `mRemoteUG.xml`.
