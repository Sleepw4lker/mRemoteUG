---
date: 2026-10-21
status: accepted
---

# Agents record user-visible changes in CHANGELOG.TXT

ADR-0036 left the changelog out of the working agreement: the organisation's template asks for a
Keep a Changelog `CHANGELOG.md`, but the release workflow fills a release's draft notes from the
top section of `CHANGELOG.TXT`, so a second file would have been one the release never reads.
Leaving the rule out entirely meant no agent wrote a changelog entry at all, and the notes of the
next release would start empty.

The changelog stays `CHANGELOG.TXT`, and the template's rule applies to it: each user-visible
change goes into an `Unreleased:` section at the top, in the same PR as the change, in the file's
plain-text style and written for the people using the application. The release step already
recognises `Unreleased:` as a section header, and the maintainer renames it to the version when
tagging. Internal-only work needs no entry.

The same agreement now also states this repo's form of "Keep CI real": `build.yml` builds, tests,
builds the installer and runs `--selftest` on every push, a new test goes into what it already
runs, and nothing is merged on a red pipeline without saying why.

This supersedes the changelog paragraph of ADR-0036; the rest of ADR-0036 stands.
