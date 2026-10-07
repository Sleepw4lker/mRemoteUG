---
date: 2026-09-20
status: amended by ADR-0002
---

# Fork mRemoteNG and strip it to one job

[mRemoteNG](https://github.com/mRemoteNG/mRemoteNG) is a mature, full-featured multi-protocol
connections manager: RDP, VNC, ICA, SSH, Telnet, HTTP/HTTPS, rlogin and raw sockets, plus
credential vaults, LAPS/AdmPwd integration, an in-app help browser, an update checker, a
custom theming engine and translations into some twenty languages. All of that breadth has a
price — it is a large surface for one person to maintain, and features nobody uses still break
builds, still carry dependencies, and still have to be reasoned about on every change.

This fork was started from mRemoteNG's last stable release (`8f38c0d`) with a single goal: a
fast, lean, modern tabbed client that does one thing well. Concretely that means RDP and SSH,
HiDPI support, dark mode, current .NET, and a codebase small enough to be understood in a
sitting. The first act of the fork (`b089a39`) deleted every non-RDP protocol and every
auxiliary feature.

Breadth is the thing being traded away, deliberately and permanently. A feature request that
widens the scope is expected to be declined rather than accommodated; that is the point of the
fork, and the reason it is a fork rather than a set of upstream pull requests. The
corresponding "explicit no"s are recorded in [ADR-0014](0014-delete-rather-than-keep.md) and
[ADR-0015](0015-purge-third-party-dependencies.md).

The RDP-only boundary did not survive contact with actual use; see
[ADR-0002](0002-restore-the-putty-protocols.md) for what came back and why.
