# PowerShell conventions

The organisation's PowerShell conventions. They digest the community
[PowerShell Practice and Style guide](https://github.com/PoshCode/PowerShellPracticeAndStyle)
(PoshCode), plus the practices that guide leaves unfinished. Where this repository departs from
them, the table at the end says so and why. Background:
[ADR-0036](../adr/0036-agents-follow-the-organisations-working-agreement.md),
[ADR-0039](../adr/0039-powershell-follows-the-organisations-conventions.md).

The scripts here are `Tools/*.ps1` and `.claude/hooks/git-guard.ps1`, with the hook's Pester tests
beside it in `.claude/hooks/git-guard.Tests.ps1`.

## Enforced by tooling

[`PSScriptAnalyzerSettings.psd1`](../../PSScriptAnalyzerSettings.psd1) is the source of truth for
formatting and lint; `.editorconfig` and `.gitattributes` cover encoding and line endings. Check
your work before committing:

```powershell
Invoke-ScriptAnalyzer -Path . -Recurse -Settings ./PSScriptAnalyzerSettings.psd1
Invoke-Formatter -ScriptDefinition (Get-Content -Raw ./file.ps1) -Settings ./PSScriptAnalyzerSettings.psd1
```

CI runs PSScriptAnalyzer 1.25.0 and Pester 6.2.0, in the `windows` job of
`.forgejo/workflows/build.yml`, before the build. It takes both from the organisation's package
mirror, pinned in `build.yml` by version and by the SHA-256 of the mirrored `.nupkg`, and imports
them by path from the job's temp folder; nothing comes from the PowerShell Gallery at run time, and
nothing is installed on the runner. Bumping either module means mirroring the new version first,
then changing its version and checksum in `build.yml` together.

CI does not run that single call. PSScriptAnalyzer 1.25 on pwsh 7.6 runs a file's rules in parallel,
and five of them resolve commands through one shared lookup: `PSAvoidUsingCmdletAliases`,
`PSAvoidUsingPositionalParameters`, `PSUseCmdletCorrectly`, `PSUseCorrectCasing` and
`PSShouldProcess`. When two of them run at the same moment, PSScriptAnalyzer intermittently crashes
("The term 'Get-Command' is not recognized", or a `NullReferenceException`,
[PowerShell/PSScriptAnalyzer#1351](https://github.com/PowerShell/PSScriptAnalyzer/issues/1351)) or,
rarely, hangs. So CI analyses each file once with every other rule and then once per
command-resolving rule, one at a time. No rule is switched off, and each call reads the settings
file. A call that still crashes with one of those two errors is retried, up to three attempts;
findings and any other error fail the step at once. If the single call above crashes on your
machine, run it again, or run the "Lint PowerShell" step from `build.yml`.

CI fails on any `Error` or `Warning` finding. `Information` findings (missing comment-based help on
exported functions, `[OutputType()]` mismatches) show in the editor; treat them as work to do, not
noise.

What the settings encode, in short:

- **OTBS braces** (opening brace on the same line, `} else {` cuddled), 4-space indentation,
  consistent whitespace, correct casing of commands and keywords.
- **120-character lines.** PoshCode says 115; we use PSScriptAnalyzer's default. Break long lines
  with splatting or the natural continuation inside `()`, `[]`, `{}` and after `|`. Backtick
  continuation is a last resort.
- **Full command and parameter names**: no aliases, no positional arguments, no semicolons as line
  terminators.
- **Syntax must parse on the target version** (see below).

## Target runtime and files

**Target: PowerShell 7.6 (LTS), supported until 2028-11-14.**

- Scripts start with `#Requires -Version 7.6`; module manifests set `PowerShellVersion = '7.6'` and
  `CompatiblePSEditions = @('Core')`.
- Source files are **UTF-8 without BOM, LF line endings**. Files a script *writes* follow the same
  rule: PowerShell 7's default encoding already is `utf8NoBOM`, so leave `-Encoding` off or say
  `utf8NoBOM` explicitly.

**Lifting the target** to the next LTS happens before the date above runs out; the
[PowerShell support lifecycle](https://learn.microsoft.com/powershell/scripting/install/powershell-support-lifecycle)
page lists the current LTS and its end-of-support date. A lift is its own `chore/` change and
updates the target line above (version and date), `PSUseCompatibleSyntax.TargetVersions` in
`PSScriptAnalyzerSettings.psd1`, and the `#Requires` and `PowerShellVersion` values in this file and
in every script and manifest of the project.

**Opting into Windows PowerShell 5.1** (for a project that depends on a Windows-only module that
won't load in 7): add `'5.1'` to `PSUseCompatibleSyntax.TargetVersions` and remove
`PSUseBOMForUnicodeEncodedFile` from `ExcludeRules` in the analyzer settings, set
`charset = utf-8-bom` for `*.{ps1,psm1,psd1}` in `.editorconfig`, use `#Requires -Version 5.1`, and
avoid 7-only syntax (`??`, `?.`, ternary, `&&`/`||`, `ForEach-Object -Parallel`). 5.1 reads
BOM-less files as the system ANSI code page, so the BOM is what keeps non-ASCII characters intact.
Record the opt-in as an ADR. The opt-in is part of these conventions, not a departure from them: it
needs no rows in "Where this repository departs from these conventions".

## Naming

- Functions are `Verb-Noun` with an approved verb (`Get-Verb`) and a singular noun.
- PascalCase for every public identifier: functions, parameters, module-scope variables. Local
  variables in camelCase.
- Keywords and operators in lowercase (`foreach`, `-eq`); comment-based help keywords in uppercase
  (`.SYNOPSIS`).
- Two-letter acronyms stay uppercase (`PSDrive`, `ADUser`); longer ones are PascalCase
  (`HttpClient`). `Id` and `Ok` are words.

## Function structure

Every function is an advanced function:

```powershell
function Get-Widget {
    <#
    .SYNOPSIS
    Gets widgets from the widget store.

    .EXAMPLE
    Get-Widget -Name 'Sprocket'
    #>
    [CmdletBinding()]
    [OutputType([Widget])]
    param (
        # Name of the widget. Wildcards are not supported.
        [Parameter(Mandatory, ValueFromPipelineByPropertyName)]
        [ValidateNotNullOrEmpty()]
        [string] $Name
    )

    process {
        Get-WidgetFromStore -Name $Name
    }
}
```

- `[CmdletBinding()]` and `[OutputType()]` on every function.
- Validate with attributes (`[ValidateNotNullOrEmpty()]`, `[ValidateSet()]`, `[ValidateScript()]`),
  not with checks at the top of the body.
- Blocks in the order `begin`, `process`, `end`. A function taking pipeline input does its work in
  `process`.
- Anything that changes state declares `[CmdletBinding(SupportsShouldProcess)]` and wraps the change
  in `if ($PSCmdlet.ShouldProcess(...))`, so `-WhatIf` and `-Confirm` work. Add
  `ConfirmImpact = 'High'` for destructive operations.
- Credentials are `[PSCredential]` parameters, secrets are `[SecureString]`.
- Return objects, not formatted text. Formatting (`Format-*`) belongs to the caller.

## Comment-based help

- Help goes **inside** the function, at the top, as a `<# ... #>` block; a script's help goes at
  the top of the script.
- Every exported function has `.SYNOPSIS` and at least one `.EXAMPLE` per main use case. Longer
  explanations go in `.DESCRIPTION` or `.NOTES`.
- Document each parameter with a comment directly above it in `param()`, not with `.PARAMETER`
  (PoshCode's preference: the doc stays next to the code it describes).

## Errors

- Scripts start with `Set-StrictMode -Version Latest` and `$ErrorActionPreference = 'Stop'`.
- Module functions leave `$ErrorActionPreference` alone: the caller decides how errors propagate.
  Module files (`.psm1`) set `Set-StrictMode -Version Latest` at the top.
- Raise a terminating error with `throw` in scripts, and with `$PSCmdlet.ThrowTerminatingError()` in
  advanced functions (the error then points at the caller, not at your internals). Use
  `Write-Error` for non-terminating, per-item failures inside a pipeline.
- Every `catch` handles the error: rethrows it, wraps it with context, or logs it and continues on
  purpose (say why in a comment). Catch specific exception types where you can.
- After calling a native executable, check `$LASTEXITCODE`.

Strict mode turns a read of a property that does not exist into an error. Where a missing property
is ordinary input, as in the JSON a hook is handed, check that the property exists before reading
it. `.claude/hooks/git-guard.ps1` must not fail on any input: Claude Code lets the command through
when a hook exits with an error, so an error there turns the guard off.

## Output streams

- Results go to the success stream (just emit them). Diagnostics go to `Write-Verbose`,
  `Write-Debug`, `Write-Information`, `Write-Warning`.
- `Write-Host` is for interactive UI scripts only (prompts, coloured status for a human at a
  console). It is fine in CI step scripts, where the output *is* the log. In a script file it
  takes a suppression (see below), as in `Tools/regenerate-mstsc-interop.ps1`.
- Suppress unwanted output with `$null = ...` or `[void]`, which is faster than piping to
  `Out-Null`.

## Paths

- Build paths from `$PSScriptRoot`, never from `.`, `..` or `~`: the current location is the
  caller's, not yours.
- Use `-LiteralPath` whenever the path is not meant to contain wildcards, which is almost always.
- Join with `Join-Path`, not string concatenation.

## Modules and tooling

- A module is a folder with a `.psd1` manifest and a `.psm1`. List exported functions explicitly in
  `FunctionsToExport` (no wildcards), so the module loads fast and its public surface is deliberate.
- On a development machine, install dependencies with **PSResourceGet** (`Install-PSResource`), which
  ships with PowerShell 7.4+. The older PowerShellGet v2 cmdlets (`Install-Module`) are legacy. CI
  installs nothing: it takes modules from the package mirror, as described above.
- Tests use **Pester 6** (CI runs the version pinned in `build.yml`, see above): one `*.Tests.ps1`
  file per function or script, next to it, with `Describe '<Function-Name>'`,
  `Context 'when <situation>'`, `It '<does X>'`. Put setup in `BeforeAll`/`BeforeEach`, never loose
  in a `Describe` body (since Pester 5, discovery and execution run separately). Mock external
  commands with `Mock`, through a wrapper function of your own when the command may not be
  installed. Tests touching the file system use `TestDrive:`. Tests depend on nothing beyond the CI
  runner baseline (see "Write tests that run on the CI runner" in `AGENTS.md`). Tests that need a
  real environment carry `-Tag 'Integration'`; CI leaves them out.

## Departing from these conventions

These conventions apply by default. A repository departs from them in one of three ways, and they
are never mixed up:

- An **exception** is permanent and reasoned. It is one row in the table under "Where this
  repository departs from these conventions" below.
- A **temporary exclusion** switches a rule off only until its findings are fixed. It is linked to
  an open issue and never appears in this file. The list only gets shorter: the PR that fixes a
  rule's findings removes its temporary exclusion.
- A **suppression** switches a rule off for one place in the code:
  `[Diagnostics.CodeAnalysis.SuppressMessageAttribute('<Rule>', '<target>', Justification = '<reason>')]`.
  It is scoped as narrowly as possible (one function or `param` block, a whole test file at most)
  and needs no row in this file, because its reason sits next to the code it excuses.

An exception needs a **technical reason**, and so does a suppression's `Justification`: Windows
PowerShell 5.1 compatibility, an interface that has already been published, or the purpose of the
repository. A suppression, and only a suppression, may also give this reason: the rule's finding is
wrong for this code, and the `Justification` says why (for example: a test mock, or an object built
only in memory). An exception that switches a whole rule off never qualifies on that ground. "The
existing code doesn't do it this way" is not a reason; a rule the existing code breaks is a
temporary exclusion until the code is fixed.

A suppression without a `Justification`, or with one that doesn't meet that bar, is not allowed.

Each exception row names the rule or convention, why it can't be followed, its scope (all files, or
a path) and, optionally, until when.

A rule switched off in [`PSScriptAnalyzerSettings.psd1`](../../PSScriptAnalyzerSettings.psd1), in
`ExcludeRules` or with `Enable = $false`, sits in one of two commented blocks there. In
"Exceptions", each entry names its row in the table below. In "Temporary exclusions", each entry
names its issue on the same line (`'PSAvoidUsingWriteHost' # #42`). An entry in neither block is not
allowed, and CI fails on an `ExcludeRules` entry outside them or a temporary exclusion without an
issue number. The `PSUseBOMForUnicodeEncodedFile` entry is in the "Exceptions" block, pointing to
"Target runtime and files" above, which sets that rule.

## Where this repository departs from these conventions

| Rule | Reason | Scope | Until |
|------|--------|-------|-------|
| `#Requires -Version 7.6` | Runs as a Claude Code hook on every developer machine; an unmet `#Requires` exits 1, Claude Code then lets the command through and the guard is off. The hook requires `-Version 7`. | `.claude/hooks/git-guard.ps1` | — |
