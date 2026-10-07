---
date: 2026-10-07
status: proposed
---

# Publish each release to GitHub as a snapshot, not a mirror

mRemoteUG is developed on a self-hosted Forgejo instance that cannot be reached from outside,
and so is every release on it. A public copy lives on GitHub. The question is what that copy is.

**It is a sequence of snapshots: one commit per release, each on top of the previous one,
holding an allowlisted subset of the tree at that release's tag.** Next to the commit, the
release carries over too: the tag, a GitHub release with the Forgejo release's notes, a SHA256
for each asset, and the MSI itself. GitHub never gets the development history, never gets a
force-push, and only receives anything when a release is published.

## The release is the trigger

A `v*` tag already sets the version a build carries
([ADR-0030](0030-derive-the-version-from-git-tags.md)). It now does one more thing: once the
signed MSI exists, a tagged build of `build.yml` drafts a Forgejo release for the tag with the
MSI attached and the notes pre-filled from the top of `CHANGELOG.TXT`. Releasing means editing
that draft and publishing it. Publishing fires `publish-github.yml`, which runs on a Linux
`docker` runner and does the rest.

This **partly reverses ADR-0030**, which removed release creation on the grounds that builds
were rolling and daily and a tag needed no release channel. Publishing to GitHub is a release
channel. The draft is built where it is because the signed MSI exists as a file in exactly one
place - the tagged build's workspace - and because a draft can then only exist for a tag whose
build passed the tests, the self-test, the signing and the signature check. "Only release a green
tag" becomes a property of the pipeline rather than a rule to remember.

Pre-releases - a tag with a hyphen, or the flag set by hand - are drafted but never published to
GitHub. `publish-github.yml` can also be run by hand for a tag, with `dry_run` on by default,
to see exactly which files and which release would go out, or to redo a publish that failed half
way. A dry run reads the draft, so a release can be inspected before anything is published - the
one run that cannot do that is the real one, which refuses a draft.

## Why snapshots

- **A mirror publishes the history, and the history is not clean.** It holds the docs before
  they were redacted for publication, the maintainer's own tooling configuration, and the
  workflows. Every one of those would have to be scrubbed from every commit, and kept scrubbed
  for every commit after. A snapshot only ever has to be clean once, at the tag.
- **A force-pushed orphan commit** would be clean too, but it throws away the one thing a
  history is good for on the public side - the diff from one release to the next - and it
  rewrites a branch other people may have cloned.
- **A filtered rewrite of the history** (`git filter-repo` on each release) is the mirror with
  extra steps: brittle, slow, re-done from scratch every time, and it still publishes the old
  wording of every file that was ever corrected.

The cost of snapshots is that the GitHub history says nothing about *why* a line changed. That is
what `docs/adr/` is for, and it is published.

## What is published is listed, not excluded

`.forgejo/github-sync/include` names every published path. Anything not listed stays private,
so a new internal file - a hook, a workflow, a working note - is private until someone decides
otherwise. A denylist would publish it on the next release unless someone remembered first.

Behind the allowlist sits a **denylist of patterns** (`denylist`: the instance's domain, the
signing setup, key material, private address ranges). Any match in any published file fails the
publish before anything is pushed, and names the file and line but not the text. It is a safety
net for what the allowlist lets through - a hostname pasted into a doc - not a second way of
choosing what goes.

`overlay/` supplies files that exist only on GitHub - the `.github/` contributing note and
templates, which say that pull requests are not merged there. It can add paths, never replace
synced ones; a collision fails the publish rather than letting one source win silently. A
`protect` list for paths edited on GitHub itself exists and is empty on purpose: everything
published comes from this repository.

## GitHub is output only

Forgejo is the source of truth and the next release overwrites whatever is on GitHub. A pull
request opened there cannot be merged there; the README and the contributing note say so, and a
fix offered that way is taken over by hand. Issues are welcome. Stopping the publish when GitHub
holds commits it did not make was considered and rejected: it sounds safer, and it means the
first click on GitHub's edit button breaks every release after it.

## Mechanism: git, not rsync

The behaviour is rsync's - the target ends up exactly like the source subset, deletions
included - but the tool is `git archive` of the tagged commit into a clean clone of the target.
That reads the commit itself, so nothing untracked, ignored or locally modified can leak in; an
allowlist entry that matches nothing is an error from git rather than a silent omission; and it
needs no rsync, so the whole of it is tested headlessly against local repositories, on the
development machine and again on the runner before every real publish
(`test-sync.sh`, `release.test.mjs`). Branch and tag go in one atomic push, a tag already on
GitHub is never moved, and re-running a published release changes nothing.

The scripts carry nothing specific to this repository. What is published, the product name, the
asset pattern and the target are configuration.

## Credentials

One fine-grained GitHub token, limited to the one repository, with Contents read and write -
enough to push, tag, create a release and upload to it. It is stored as the `GH_SYNC_TOKEN`
secret. A deploy key would have been the narrower credential for the push alone, but the release
and the upload need the API. Fine-grained tokens expire; the first step of the publish checks the
token and fails by name when GitHub rejects it.

## What this does not prove

Measured here: the tree half, end to end into a local bare repository - 1193 files, every one
byte-identical to its blob in the source, nothing from outside the allowlist.
Both scripts' failure paths are tested. Unmeasured, because none of it is reachable from the
development environment: that publishing a Forgejo draft fires `release: published`; that the job
token may create a release, attach to it and download the attachment; the push to GitHub; and the
upload. The first real release proves them. If the draft does not fire the workflow, running it
by hand for the tag is the fallback.
