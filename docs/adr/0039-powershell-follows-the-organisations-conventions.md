---
date: 2026-11-09
status: accepted
---

# PowerShell follows the organisation's conventions in full, linted in CI

ADR-0036 brought the organisation's coding conventions over cut down to what did not conflict with
this repo, and `docs/conventions/powershell.md` listed what it left out: OTBS braces, strict mode,
`[OutputType()]`, parameter docs in `param()`, 120-character lines, `#Requires -Version 7.6`, LF
line endings, and the PSScriptAnalyzer settings and CI lint step. Measured against the
organisation's later rule for departures (an exception needs a technical reason; "the existing code
doesn't do it this way" is not one), only one of those had a reason. The rest only described the
scripts as they were, and nothing checked them: the brace style was mixed, and with the template's
analyzer settings the five scripts had 93 findings.

So the scripts now follow the conventions, and CI holds them to it:

- **Lint and tests in CI.** `PSScriptAnalyzerSettings.psd1` is the template's, and the `windows` job
  of `build.yml` runs the template's steps before the build: PSScriptAnalyzer 1.25.0 and Pester
  6.2.0 from the organisation's package mirror, the check that every excluded rule is a labelled
  exception or a temporary exclusion with its issue, the lint, and the Pester tests. The lint is
  clean with no temporary exclusions.
- **The scripts were fixed**, not excused. `Write-Host` in the two interactive developer tools is a
  suppression in the code, with its justification next to it.
- **`git-guard.ps1` runs under strict mode** and checks that a property of the hook's JSON exists
  before reading it. Claude Code lets the command through when a hook exits with an error, so a
  strict-mode error on a payload without a command would have switched the guard off without a
  word. Its Pester tests cover a missing or empty command and a blocked and an allowed command.
- **One exception stays**: `git-guard.ps1` requires PowerShell 7, not 7.6. An unmet `#Requires`
  exits 1 as well, and the guard would be off on every developer machine still on an older 7.
- **PowerShell files are LF** (`*.ps1`, `*.psm1`, `*.psd1` in `.gitattributes` and
  `.editorconfig`), like `*.sh`; everything else stays CRLF. The files git stores were LF already,
  so only Windows working trees change.
- **`Tools/CreateBulkConnections_ConfCons2_6.ps1` is deleted.** It was inherited from mRemoteNG,
  nothing referenced it, and it could not run: it loaded the removed `BouncyCastle.Crypto.dll`.

This amends the conventions paragraph of ADR-0036 for PowerShell. The C# conventions are a separate
change.

## Alternative considered: keep the departures and record reasons for them

Rejected. There were no technical reasons to record: no script here has to run on Windows
PowerShell 5.1 or keeps a published interface, and the scripts are few enough to fix in the same
change that switches the lint on.
