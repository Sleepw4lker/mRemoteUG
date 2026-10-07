---
date: 2026-09-22
---

# Replace DockPanelSuite with a static split/tab layout

mRemoteNG's main window was built on DockPanelSuite: every tool window and every session tab
was a floating, dockable, re-arrangeable `DockContent`, with the arrangement persisted as
DockPanelSuite XML. `f38a954` replaced it with a fixed layout — `MainLayout` owns splitters,
the connection tree and config on one side, a tab container for sessions — and deleted the
dependency.

The trade is real and it is a loss of capability: users can no longer float a tool window onto
a second monitor or rearrange the main window freely. What is bought:

- A large third-party layout engine leaves the dependency set (see
  [ADR-0015](0015-purge-third-party-dependencies.md)).
- The window's geometry becomes something the framework scales, which matters a great deal for
  [ADR-0010](0010-per-monitor-v2.md) — DockPanelSuite did its own measurement and did not
  follow a DPI change.
- The persisted-layout XML, a recurring source of "the window came back wrong" bugs, goes away.
  What replaces it is much smaller: remember which Panel a session was in (`00e7f67`), nothing
  about geometry.

Panels — multiple side-by-side tab containers — were kept, because opening two sets of sessions
next to each other is ordinary use. Arbitrary docking was not.

This is hard to reverse in the sense that matters: the tool windows are now ordinary child
controls in a known tree, and [ADR-0010](0010-per-monitor-v2.md)'s DPI sweep walks exactly that
tree. Reintroducing floating windows means revisiting the DPI work.
