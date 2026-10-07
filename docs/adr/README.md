# Architecture decision records

Why this fork is built the way it is. Each record states a decision that was hard to reverse,
surprising without context, and the result of a real trade-off — so that nobody, including a
future me, "fixes" something that was deliberate.

**These were written retroactively on 2026-09-23**, from the commit history and working notes of
the fork's first four days (2026-09-20 to 2026-09-23). The `date:` in each record is when the
decision was actually made, not when it was written down. Every factual claim was re-checked
against the code at the time of writing.

Measured platform behaviour that constrains the code but is not itself a decision lives in
[../platform-findings.md](../platform-findings.md). Project vocabulary is in
[../../CONTEXT.md](../../CONTEXT.md).

## Scope

| # | Decision | Date |
|---|---|---|
| [0001](0001-fork-to-do-one-thing.md) | Fork mRemoteNG and strip it to one job — *amended by 0002* | 2026-09-20 |
| [0002](0002-restore-the-putty-protocols.md) | Restore the PuTTY-driven protocols; RDP and SSH are the product, Telnet/rlogin/RAW are incidental | 2026-09-21 |
| [0014](0014-delete-rather-than-keep.md) | Delete rather than keep — the standing policy and the explicit no-s | 2026-09-22 |
| [0024](0024-rename-to-mremoteug.md) | Take the name mRemoteUG, and move the projects under `src/` | 2026-09-24 |

## Build and packaging

| # | Decision | Date |
|---|---|---|
| [0003](0003-target-net-10.md) | Target .NET 10, SDK-style, and drop the Portable edition | 2026-09-22 |
| [0004](0004-commit-the-mstsc-interop.md) | Commit the MSTSCLib interop assemblies instead of generating them | 2026-09-22 |
| [0015](0015-purge-third-party-dependencies.md) | Reduce the runtime dependency set to log4net — *amended by 0019* | 2026-09-22 |
| [0016](0016-wix-5-and-the-desktop-runtime.md) | Build the installer with WiX 5 and require the .NET Desktop Runtime | 2026-09-22 |
| [0025](0025-release-on-a-tag.md) | Build every push on Forgejo, and move the version only on a release tag - *superseded by 0030* | 2026-09-24 |
| [0026](0026-generate-the-icons-at-build-time.md) | Replace Silk with Fluent, rasterized at build time and committed | 2026-09-25 |
| [0027](0027-tint-the-glyphs-once-at-load.md) | Ship the glyphs colourless and tint them once, at load | 2026-09-25 |
| [0030](0030-derive-the-version-from-git-tags.md) | Derive the version from the git tags, and release by tagging - *proposed, supersedes 0025* | 2026-09-29 |
| [0032](0032-sign-on-the-signing-server.md) | Sign on the runner, against a signing server, and split the build to make room for it - *proposed, amends 0030* | 2026-10-01 |
| [0035](0035-publish-releases-to-github-as-snapshots.md) | Publish each release to GitHub as a snapshot, not a mirror - *proposed, amends 0030* | 2026-10-07 |

## Sessions and protocols

| # | Decision | Date |
|---|---|---|
| [0005](0005-park-rdp-controls-before-disposing.md) | Park RDP controls off-screen and dispose them once their session is down | 2026-09-22 |
| [0006](0006-floor-the-rdp-client-at-v11.md) | Drive the RDP control through `IMsRdpClient10`, floored at v11 | 2026-09-22 |
| [0023](0023-host-stock-putty-as-a-maximised-child.md) | Host stock PuTTY as a maximised, non-child window: PuTTY reinstates its caption on every `SIZE_RESTORED`, and a child window cannot take the keyboard — *amended by 0031* | 2026-09-24 |
| [0028](0028-declare-the-display-scale-before-connecting.md) | Declare the display scale before connecting, through the extended-settings property bag | 2026-09-29 |
| [0031](0031-retry-the-putty-chrome-strip-once.md) | Retry the PuTTY chrome strip once, after reading the result back: `SetParent` can itself trigger the `SIZE_RESTORED` that puts the caption back — *proposed, amends 0023* | 2026-09-30 |
| [0033](0033-refresh-putty-profiles-on-the-ui-thread.md) | Refresh the PuTTY Profiles on the UI thread, and serialise the registry notification: one PuTTY save raised the change event on 6–8 pool threads at once — *proposed* | 2026-10-01 |

## User interface

| # | Decision | Date |
|---|---|---|
| [0007](0007-static-layout-instead-of-dockpanelsuite.md) | Replace DockPanelSuite with a static split/tab layout | 2026-09-22 |
| [0008](0008-treeview-instead-of-objectlistview.md) | Replace the connection tree's ObjectListView with a TreeView | 2026-09-22 |
| [0009](0009-system-colour-theme-at-startup.md) | Take the colour theme from Windows, once, at startup | 2026-09-22 |
| [0010](0010-per-monitor-v2.md) | Make the application Per-Monitor V2 DPI aware — *amended by 0023* | 2026-09-22 |
| [0011](0011-take-fonts-from-windows.md) | Take fonts from Windows, and leave `AutoScaleDimensions` alone | 2026-09-22 |
| [0012](0012-task-dialog-on-comctl32.md) | Re-implement the task dialog on the comctl32 `TaskDialog` | 2026-09-22 |
| [0018](0018-show-the-window-before-loading-the-connections.md) | Show the main window before loading the connections | 2026-09-23 |
| [0029](0029-no-autocomplete-on-the-quick-connect-box.md) | Drop autocomplete from the quick connect box: with it, a DPI change recreates the hosted window and the process dies | 2026-09-29 |

## Diagnostics

| # | Decision | Date |
|---|---|---|
| [0013](0013-selftest-as-the-verification-mechanism.md) | Build verification into the application as `--selftest` | 2026-09-22 |
| [0017](0017-one-filtered-writer-for-every-log-line.md) | Route every log line through one filtered writer; Information is bounded by sessions and starts, the rest is Debug behind `--verbose` | 2026-09-23 |
| [0019](0019-serilog-instead-of-log4net.md) | Replace log4net with Serilog, and write the file sink by hand | 2026-09-23 |
| [0020](0020-carry-the-exception-and-the-event-time.md) | Carry the exception and the event time to the log, instead of flattening them away | 2026-09-23 |
| [0021](0021-log-unhandled-exceptions.md) | Log unhandled exceptions before the process goes down | 2026-09-23 |
| [0022](0022-nullable-a-directory-at-a-time.md) | Enable nullable reference types a directory at a time, with the build as the ratchet — *amended by 0025* | 2026-09-24 |
| [0034](0034-log-outside-the-roaming-profile.md) | Write the log under `%LOCALAPPDATA%`, keep settings under `%APPDATA%` — *proposed* | 2026-10-01 |

## Writing a new one

Number sequentially from the highest here. Record a decision only when all three hold: it is
hard to reverse, it is surprising without context, and there were genuine alternatives. If a
decision is easy to reverse, skip it — you will just reverse it.
