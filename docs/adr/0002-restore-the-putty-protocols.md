---
date: 2026-09-21
---

# Restore the PuTTY-driven protocols

[ADR-0001](0001-fork-to-do-one-thing.md) stripped the fork to RDP alone. That went too far:
SSH sits alongside RDP in everyday use, and a client that manages one but not the other is
not lean, just incomplete. `06ee15f` brought SSH back.

SSH could not come back by itself. All four of mRemoteNG's terminal protocols — SSH, Telnet,
rlogin and RAW — are served by the same mechanism: PuTTY is launched and its window is
reparented into a tab (`PuttyBase`). Supporting SSH means shipping that host, and once it is
there the other three are a `ProtocolType` value and a command-line switch each. Removing them
again would cost more code than keeping them.

So the supported set and the *goal* are deliberately not the same thing:

- **RDP and SSH are the product.** They get the attention, the fixes and the testing.
- **Telnet, rlogin and RAW are incidental.** They work because the PuTTY host is there. They
  are not advertised, and effort should not be invested in them.

The distinction matters for future work: neither "we support five protocols, so let us add a
sixth" nor "Telnet is dead, let us remove it" follows from this. The first widens a scope
[ADR-0001](0001-fork-to-do-one-thing.md) deliberately narrowed; the second spends effort to
remove something that costs nothing.
