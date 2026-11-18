---
date: 2026-11-09
status: accepted
---

# C# follows the organisation's conventions, brought in line one step at a time

ADR-0036 brought the organisation's coding conventions over cut down to what did not conflict with
this repo, and `docs/conventions/csharp.md` listed what it left out: file-scoped namespaces, `var`
only where the type is apparent, the `s_` and `t_` field prefixes, `ImplicitUsings`,
`AnalysisLevel` and warnings as errors, nullable everywhere, XML documentation, 120-character lines,
Central Package Management and the Microsoft.Testing.Platform runner, and the test framework and
layout. Measured against the organisation's rule for departures (an exception needs a technical
reason; "the existing code doesn't do it this way" is not one), almost none of those had a reason.
Three things the file said the code did were not true either: 112 files are indented with tabs
although `.editorconfig` says spaces, IDE0011 finds 1,002 single-line bodies without braces, and
over a hundred fields miss the naming rules.

So the C# code is brought in line with the conventions rather than excused from them, as ADR-0039
did for PowerShell. Unlike the five scripts there, this touches nearly every one of some 370 files,
so it is a series of PRs, one rule at a time, each from a fresh `master`: whitespace, namespaces,
braces, warnings as errors with the analyzers, `var` and unused usings, field names, long lines.
Each PR switches its rule's `.editorconfig` severity on together with the fix, so the build holds the
line from then on. Until its PR lands, a rule is a pending fix listed under "Not yet in line" in
`csharp.md`, with its issue.

This first step:

- **Departures are recorded in the organisation's format**: a table of exceptions, each with a
  technical reason, and an `Exception:` or `Temporary exclusion: #N` comment on the setting that
  implements it. Three exceptions: CRLF line endings (ADR-0039), CS1591, because this is an
  application with no consumers of a public API documentation, and the installer's custom action,
  which runs in msiexec's .NET Framework host (net472, C# 7.3) and so cannot have nullable
  reference types, `ImplicitUsings` or the style rules that need a newer language version.
- **The nullable ratchet of ADR-0022 is a temporary exclusion**, not a departure. Its end state was
  always nullable enabled everywhere; #68 tracks getting there.
- **Central Package Management.** Every NuGet version is in `Directory.Packages.props`, the
  installer's WiX extensions and `Tools/IconGen` included. The package set is unchanged.
- **Generated code is marked** `generated_code = true` in `.editorconfig`, so the formatting and
  renaming passes leave the designer files and `Resources.g.cs` alone.
- **CS0660 and CS0661 left `NoWarn`.** They warn about an `==` operator without `Equals` or
  `GetHashCode`, and no type here defines one.
- **The test framework stays**: NUnit with NSubstitute under `src/`. The organisation's choice of
  framework, layout and naming binds new repositories only. 32 test files need NUnit's STA threads
  for WinForms and the RDP ActiveX control.

The Microsoft.Testing.Platform runner is not adopted yet. It ran all 1,295 tests, after a
NUnit3TestAdapter update, but CI's TRX test report would need a new package under it. That is a
temporary exclusion with its own issue, #73.

This amends the conventions paragraph of ADR-0036 for C#.

## Alternative considered: keep the departures and record reasons for them

Rejected for the same reason as in ADR-0039: apart from the three exceptions above, there were no
technical reasons to record. Upstream mergeability is not one either. Nothing has been taken from
mRemoteNG since the fork, and ADR-0001 rules it out.
