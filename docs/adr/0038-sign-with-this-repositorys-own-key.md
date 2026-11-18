---
date: 2026-10-27
status: accepted
---

# Sign with this repository's own ssh key, and name the signing server in the forge's Actions variables

[ADR-0032](0032-sign-on-the-signing-server.md) put signing on the Windows runner, against a remote
signing server reached over ssh, and left where to find that server and how to authenticate to it
to "the CI environment". In practice that meant the runner itself: the server and the user were
variables of the runner's own environment, and ssh authenticated with the runner's default
identity, shared by every repository whose builds run there.

The signing server has changed. It now queues every request, and it knows each client repository
by an ssh key of its own, which it maps to a client name. A request signed with the runner's shared
identity cannot be told apart from any other repository's. So this repository's build now brings
its own key, and the runner's environment no longer names the server.

**The server and its user are the organisation's Actions variables.** The workflow maps them at
job level from `vars.*`, the forge's configuration rather than the runner's. Nothing in this
repository names the server, as before.

**The key is the repository secret `CODESIGN_SSH_KEY`.** The workflow writes it to a file in the
job's temporary directory, created empty and restricted to the job account's SID before the key
is written (OpenSSH for Windows refuses a private key others can read, and a service account has
no user name `icacls` could resolve). It normalises the line endings to LF and checks that ssh can
load the key, failing by name if it cannot. Then it logs the public fingerprint, which is what the
server's administrator compares against the keys the server knows. A last step that runs whatever
happened (`if: always()`) deletes the file. A run without the secret fails at the signing
preparation, before the build: every build signs (ADR-0032), so a missing key is a failure, not a
reason to build unsigned.

**ssh offers that key and nothing else.** `-o IdentitiesOnly=yes -i <key>` stops ssh from also
trying the runner's default identities. Without it, the server could accept one of those first
and sign the request under another client's name, which is the thing per-repository keys exist
to prevent. `-o BatchMode=yes` makes ssh fail instead of waiting for a prompt that never comes.
ADR-0032's error message already relied on it, but the call itself did not pass it.

**Temporary failures are retried; everything else still fails at once.** The server answers some
failures as temporary: service unavailable (exit 5), busy (6) and timestamp service failed (8).
ssh exits 255 when it cannot connect. Those are retried after 30, 60 and 120 seconds. A 255 caused
by a rejected key or a host-key mismatch is not retried, because waiting fixes neither, and nor is
any other non-zero exit. ssh's error output is shown in the log. Everything ADR-0032 demands of
what comes back is unchanged: something came back, it is not empty, it differs from what was
sent, and it carries a signature. The verify step's signer, chain and timestamp checks are also
unchanged.

The alternative was to keep the runner's identity and have the server tell repositories apart
some other way. That was not this repository's choice to make, and a key per repository also lets
one repository's access be revoked without touching the runner. The cost is a secret to set per
repository, and a build that fails, rather than signs, until it is set.

This workflow runs only on `push`, never on a pull request from a fork, so the forge withholding
secrets from fork pull requests changes nothing here. The key file is readable by the runner's
account while a job of this repository runs, so it guards against other repositories' requests
going out under this name. It does not guard against code that already runs as that account.
