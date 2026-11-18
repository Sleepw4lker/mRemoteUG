---
date: 2026-10-07
status: accepted
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

There are **no pre-releases**. Only a `v<major>.<minor>.<patch>` tag drafts a release; a tag such
as `v2.2.0-rc.1` fails the draft step by name, and `git describe` ignores it, so it cannot move the
version either ([ADR-0030](0030-derive-the-version-from-git-tags.md)). Supporting them was
considered and dropped: nothing here has needed one, and the version scheme cannot express one.
A release flagged as a pre-release by hand in Forgejo is still not published.

`publish-github.yml` can also be run by hand for a tag, with `dry_run` on by default,
to see exactly which files and which release would go out, or to redo a publish that failed half
way. A dry run reads the draft, so a release can be inspected before anything is published - the
one run that cannot do that is the real one, which refuses a draft. It cannot fetch the
draft's assets, which the instance serves only once the release is published, so their
checksums and signature check are the real run's.

## A tag is published by the tooling it was tagged with

The job checks out twice. `source` is the tag: what is published, and the `include`, `denylist`
and `overlay/` that decide it, because what goes out is part of the release. `tooling` is the
commit the workflow file came from, and supplies the two scripts. On a release event both are the
tagged commit - a release event runs the workflow file as it is at the tag, as GitHub documents
it and as assumed here for Forgejo; `v2.1.0` could not tell the two apart - so a tag is
published exactly as it was tagged. On a manual run `tooling` is the branch the run was started from, which is what makes
a manual run the way to redo a release with fixed tooling.

This was learned the expensive way. With a single checkout of the tag, three fixes to the
scripts merged after `v2.1.0` was tagged never ran: each manual run read the new workflow from
`master` and then executed the scripts as they were at the tag. The consequence to remember: a
fix to the publishing scripts or the workflow reaches a tag that already exists only through a
manual run; publishing its release runs what was tagged.

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

## What v2.1.0 proved

The first release, 2026-10-07, went through end to end: the tagged build drafted the release
and attached the MSI with the job token; publishing the draft fired `release: published`; the
snapshot and the tag reached GitHub in one push, from 1193 files byte-identical to their source
blobs; and the GitHub release was created with the notes, the SHA256 and the MSI.

Getting there took four corrections, all now in
[platform-findings](../platform-findings.md#publishing-to-github): the runner's Node options need
Node 22, its proxy needs `NODE_USE_ENV_PROXY`, the scripts had to come from the workflow's
commit rather than the tag, and a draft's assets are not served to the job token at all - only a
published release's are, which is why a dry run against a draft cannot check its assets.

Not yet exercised: a second release on top of the first, which is the case the snapshot history
exists for; an expired token; and a publish that fails half way and is redone by hand.
