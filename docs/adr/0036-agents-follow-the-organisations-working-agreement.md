---
date: 2026-10-21
status: accepted, amended by ADR-0037, ADR-0039 and ADR-0040
---

# Agents follow the organisation's working agreement

Until now an agent working here followed house rules in `CLAUDE.md`: a `claude/<slug>` branch
with one finished commit, a plain imperative subject without a type prefix, a push and a compare
URL, and an ADR left on the branch as a proposal for the maintainer to accept or delete. The
maintainer opened the pull request and merged it.

The organisation's Repository-Template now carries one working agreement, in `AGENTS.md`, for
every repository the organisation keeps. This repo adopts it, and `CLAUDE.md` shrinks to a
pointer to `AGENTS.md`. The repo's own knowledge (the three orientation documents, the table of
decisions to read before editing, the build, test and self-test steps, what "done" means)
moved over into `AGENTS.md` unchanged.

What changes:

- **Branches** take the template's prefixes (`feature/`, `fix/`, `docs/`, `chore/`, `refactor/`,
  `spike/`, `hotfix/`) instead of `claude/<slug>`. `git-guard.ps1` keeps every protection it had
  (no push to `master`, no tags, no force-push, no merge into `master` except the `--ff-only`
  resync, no `reset --hard`); only its messages changed.
- **Commits** are Conventional Commits (`type(scope): description`), and so is the PR title,
  because the squash commit takes it. The body keeps the house style: the why, measured kept apart
  from assumed, 72 columns, `ADR-NNNN` citations and the closing `Verified:` paragraph.
- **Pull requests**: the agent pushes, opens the PR from the PR template, waits for CI and never
  merges. Every push still runs the full build, tests, installer and self-test on the Windows
  runner, so finished work is pushed, ideally once.
- **ADRs**: an agent writes the record in its PR, and the maintainer's merge accepts it. The bar
  for writing one stays in `docs/adr/README.md`.
- **Issues**: one issue per change, closed by its PR, labelled as `docs/agents/triage-labels.md`
  says.
- **Changelog**: not changed. The release workflow builds the draft release notes from
  `CHANGELOG.TXT`, which holds the history inherited from mRemoteNG, so a `CHANGELOG.md` was not
  started. The template's changelog rule is not adopted until the release notes have a new source.
  Superseded by ADR-0037: the rule applies to `CHANGELOG.TXT` itself.

The coding conventions came over as `docs/conventions/csharp.md` and `powershell.md`, cut down to
what does not conflict with this repo's `.editorconfig`, build settings and tests; each file lists
what it left out. The template's testing conventions did not come over: "Build, test, verify" is
this repo's equivalent.
Amended by ADR-0039: the PowerShell conventions now apply in full, linted in CI, with one
exception for `git-guard.ps1`.

Commits before this one use the old style (plain subjects, `claude/<slug>` branches, an ADR
written as a proposal), so a reader of `git log` meets two styles. The history is not rewritten.

## Alternative considered: keep the house rules

Rejected. A second set of rules per repository means an agent has to relearn the agreement in each
one, and the organisation's rules drift apart. Taking the template as the source keeps one
agreement, and the repo's own additions stay visible on top of it.
