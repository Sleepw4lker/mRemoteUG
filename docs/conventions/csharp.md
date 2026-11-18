# C# conventions

The organisation's C# conventions, trimmed to what fits this repository. Microsoft's own
([C# coding conventions](https://learn.microsoft.com/dotnet/csharp/fundamentals/coding-style/coding-conventions),
the [.NET runtime coding style](https://github.com/dotnet/runtime/blob/main/docs/coding-guidelines/coding-style.md))
are the background. Where this repo's `.editorconfig`, `Directory.Build.props` or project files say
something different, they win and this file leaves the point out. Background:
[ADR-0036](../adr/0036-agents-follow-the-organisations-working-agreement.md).

## Enforced by tooling

The build is the checker: `dotnet build mRemoteUG.slnx -c Release` (see "Build, test, verify" in
`AGENTS.md`). Every warning fails it.

- [`.editorconfig`](../../.editorconfig) is the organisation's template: the formatting, `var` and
  naming preferences, the severities the build enforces, and the two dead-code diagnostics (IDE0051,
  IDE0052). It marks the generated files (`*.Designer.cs`, `*.g.cs`) as generated code; these
  conventions apply to hand-written code only.
- [`Directory.Build.props`](../../Directory.Build.props) sets, for every C# project, warnings as
  errors, `AnalysisLevel=latest-recommended`, `EnforceCodeStyleInBuild`, `ImplicitUsings` and the
  generated XML documentation file (which the installer leaves out of the MSI). It also sets the
  version derivation and nullable reference types, on for every file, with nullable warnings as
  errors ([ADR-0022](../adr/0022-nullable-a-directory-at-a-time.md) is how they got there). A file
  needs no `#nullable` directive.
- [`Directory.Build.targets`](../../Directory.Build.targets) relaxes CS1591 and CA1707 for the test
  project, as the template does.
- [`Directory.Packages.props`](../../Directory.Packages.props) holds every NuGet version (Central
  Package Management). `PackageReference` items carry no `Version`.
- [`global.json`](../../global.json) pins the .NET SDK.

The rules below are the organisation's. Where the existing code does not meet one yet, the rule it
breaks is lowered to `suggestion` in `.editorconfig` as a temporary exclusion naming the issue whose
PR brings the code in line (see "Not yet in line" below). New code follows them; do not
reformat code you are not otherwise changing, because that is what those PRs are for.

- **Allman braces**, 4-space indentation, braces even around single-line bodies. Lines up to 120
  characters: no analyzer checks that, so `LineLengthTests` in the test project does.
- **File-scoped namespaces.** `using` directives outside the namespace, `System.*` first.
- **`var` only when the type is apparent** from the right-hand side (`new`, a cast). Built-in types
  and method results are typed explicitly.
- **Access modifiers always written out.**
- **Naming:** PascalCase for types, members, and constants; camelCase for locals and parameters;
  `_camelCase` for private instance fields; `s_camelCase` for private static fields (including
  `static readonly`); `t_camelCase` for `[ThreadStatic]` fields; interfaces `I`-prefixed; type
  parameters `T`-prefixed.

## Projects and solutions

- Target the **current .NET LTS** (`<TargetFramework>` in each csproj).
- One solution at the repo root, in the **`.slnx`** format.
- `PackageReference` items carry no `Version`; add a `PackageVersion` to `Directory.Packages.props`
  instead.
- Prefer the base class library. Adding any third-party package needs the user's go-ahead first (see
  `AGENTS.md` and [ADR-0015](../adr/0015-purge-third-party-dependencies.md)).

## Language features

Use modern C# where it makes the code clearer:

- **Records** for immutable data carriers (DTOs, messages, value objects).
- **Primary constructors** on small types whose parameters are only stored or passed on. Classes with
  real logic keep an explicit constructor and `private readonly` fields: primary-constructor parameters
  are mutable captures, not `readonly` fields.
- Switch expressions, pattern matching, collection expressions (`[]`), raw string literals, `required`
  and `init` members: all welcome.
- `ArgumentNullException.ThrowIfNull` and `ArgumentException.ThrowIfNullOrWhiteSpace` for argument checks.

## Nullable reference types

Annotate honestly: `string?` where null is a valid value, and fix warnings at their source rather than
with `!`. Each `!` is a claim you can defend in one sentence; add that sentence as a comment if it is
not obvious.

## Structure and practices

- **Guard clauses first**: validate and return early, keeping the main path unindented.
- One top-level type per file, file named after the type.
- Async all the way: `async` methods end in `Async`, take a `CancellationToken` as the last parameter
  where the operation can be cancelled, and never block on `.Result` or `.Wait()`. UI code stays on its
  synchronization context; library code uses `ConfigureAwait(false)`.
- Throw specific exception types (`ArgumentException`, `InvalidOperationException`, or a custom one for
  domain errors). Every `catch` handles the error: rethrow with `throw;`, wrap with context, or recover
  on purpose.
- Prefer immutability: `readonly` fields, `init` properties, `IReadOnlyList<T>`/`IReadOnlyDictionary<TKey, TValue>`
  in public signatures.

## Documentation

- Comments explain *why*; the code says *what*.

## Tests

The tests live in `src/mRemoteUG.Tests` and use NUnit with NSubstitute. The organisation's test
framework, layout and naming bind new repositories only, so this needs no departure. How to run
them, and what each tier proves, is "Build, test, verify" in `AGENTS.md`. A test that needs a real
environment is tagged `[Category("Integration")]` and skips with `Assume.That` or `Assert.Ignore`
when its prerequisite is missing.

### Changing production code to make it testable

- **Allowed without asking:** making a member `internal` so the test project can reach it
  (`InternalsVisibleTo` exists), and extracting pure logic into a static `internal` helper.
  Neither changes behaviour.
- **Only with a reason in the PR:** a new interface or seam, where the logic behind it matters and
  cannot be reached otherwise.
- **Never:** a behaviour change made only to make something testable.

### Mutation testing

Stryker.NET mutates `src/mRemoteUG` and reruns the tests against each mutant, to show code the tests
run without checking ([ADR-0042](../adr/0042-mutation-testing-by-hand-with-stryker.md)). It never
runs on its own, and takes hours:

- **Start it** from the forge: Actions, "Mutation testing" (`.forgejo/workflows/mutation.yml`), "Run
  workflow", on the branch to measure. It needs the Windows runner with the `performance` label and
  waits until that runner is free; a second run queues behind the first.
- **Read the report** from the run's `mutation-reports` artifact: `mRemoteUG/reports/mutation-report.html`
  opens in a browser, with the score and each file's killed, survived, timeout and no-coverage
  mutants; `mutation-report.json` holds the same data. `mutation-report.md` next to them is the
  per-file score table as Markdown, and the job log prints it in the step "Print the mutation score
  table", just after the Stryker run (Stryker's own `cleartext` table does not fit the runner's
  80-column console, so it is not used).
- **The score is a trend, not a gate.** The run fails only when Stryker cannot run or hits the
  timeout. "No coverage" is code no test reaches, mostly WinForms UI. A survivor is confirmed by hand
  before a test is written for it, and each run's survivors are filed as one issue, grouped by area.
- **Locally**, from `src/mRemoteUG`: `dotnet tool restore`, then
  `dotnet tool run dotnet-stryker --config-file ../../build/stryker/mRemoteUG.json`. Add
  `--mutate "**/Some/File.cs"` to mutate one file in minutes rather than the whole project in hours.
  The report lands in `src/mRemoteUG/StrykerOutput/`, which git ignores.

What is excluded from mutation, and why, is in the comments of `mutation.yml`. That includes
`App/SelfTest.cs`: it only runs under `--selftest`, which the unit tests do not reach, so its roughly
1,000 mutants would all be "no coverage", costing run time and skewing the score without a survivor
ever being actionable. No test is filtered
out: the STA and ActiveX tests run under Stryker's test hosts like the others. If one ever has to be,
it gets an NUnit category, excluded by `test-case-filter` in `build/stryker/mRemoteUG.json`, with
the reason beside the category.

## Departing from these conventions

A departure is one of three kinds, and they are never mixed up:

- An **exception** is permanent and needs a technical reason: an interface that has already been
  published, the runtime the code has to run in, or the purpose of the repository. It is one row in
  the table below. "The existing code doesn't do it this way" is not a reason.
- A **temporary exclusion** switches a rule off only until its findings are fixed. It names an open
  issue, and the PR that fixes the findings removes it. The list only gets shorter.
- A **suppression** switches a rule off for one place in the code:
  `[SuppressMessage("<Category>", "<Id>", Justification = "<reason>")]`, or
  `#pragma warning disable <Id>` with a comment giving the reason and a matching `restore`, around as
  little code as possible (one member, a whole test file at most). Its justification meets the same
  bar as an exception, and may also be "the rule's finding is wrong for this code, and here is why".
  It needs no row in this file, because its reason sits next to the code it excuses.

In the build configuration, a departure is a diagnostic lowered or switched off in `.editorconfig`
(`dotnet_diagnostic.<id>.severity`), a `<NoWarn>` entry, a project property set against the
organisation's default, or a warnings-as-errors opt-out. Each carries a comment directly above it:
`# Exception:` (`<!-- Exception: -->` in a project file) naming its row in the table below, or
`# Temporary exclusion: #42` (`<!-- Temporary exclusion: #42 -->`) naming its issue.

## Where this repository departs from these conventions

| Rule | Reason | Scope | Until |
|------|--------|-------|-------|
| LF line endings | Windows-only WinForms app; the `*.TXT` files are shipped in the MSI from the working tree. CRLF is enforced by `.gitattributes`. | All files except `*.sh` and the PowerShell files (`*.ps1`, `*.psm1`, `*.psd1`) | — |
| XML documentation on every public member (CS1591) | Application, not a library: there are no consumers of a public API documentation. The same holds for the installer's custom actions, which only msiexec loads, and for the build tool `Tools/IconGen`. | Every C# project (`Directory.Build.props`); the test project is relaxed by `Directory.Build.targets` anyway | — |
| Nullable reference types, `ImplicitUsings`, and the style rules that need a newer language version than C# 7.3 | Runs in msiexec's .NET Framework host: net472, C# 7.3. | `src/mRemoteUG.Installer.CustomActions` | — |
| PascalCase namespaces (IDE1006) | Renaming the root namespace changes the settings path in `user.config`, so existing users would lose their settings. | The namespace `mRemoteUG`. A naming rule cannot single out one namespace, so `.editorconfig` takes namespaces out of the naming rules, and `NamespaceNamingTests` checks every segment below `mRemoteUG` instead | — |

### Not yet in line

These are temporary exclusions and pending fixes, not departures. Each is listed only so this file
does not claim what the code does not do yet, and each line goes with the PR that closes its issue.

None at present. The last one, nullable reference types under the ADR-0022 ratchet, ended with
#68.
