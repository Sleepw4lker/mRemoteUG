---
date: 2026-09-22
---

# Replace the connection tree's ObjectListView with a TreeView

The connection tree was an `ObjectListView` in tree mode — a third-party `ListView` subclass
supplying model binding, filtering, and a `SimpleDropSink` that could drop *between* rows as
well as onto them. `c2eb9a7` replaced it with the framework's `TreeView`.

The library was doing four jobs, and each had to be replaced by hand rather than merely
deleted:

- **Model binding** → `ConnectionTree.NodeSync` keeps the `TreeNode` tree in step with the
  connection model, which `ObjectListView` did by rebuilding.
- **Filtering** → a `TreeView` cannot filter rows, so matching nodes are collected and the tree
  rebuilt. The matching logic itself was first extracted into a UI-free
  `ConnectionTreeFilterEvaluator` (`3e91d97`) so it could be unit-tested at all.
- **Drag and drop** → `TreeNodeDropLocationCalculator` plus `DropTargetLocation` reproduce the
  three drop positions actually used (above, below, onto), not the sink's full set.
- **The drag payload** → `ConnectionInfoDataObject` replaces `OLVDataObject`, and carries a
  token rather than the connections themselves (`bef9720`).

So this was not a simplification in line count; it is roughly a wash. What it buys is that the
tree is now a framework control, which means it follows fonts, themes and DPI changes the way
every other control does — `ObjectListView` did not, and each of
[ADR-0009](0009-system-colour-theme-at-startup.md),
[ADR-0010](0010-per-monitor-v2.md) and [ADR-0011](0011-take-fonts-from-windows.md) would
otherwise have needed a special case for it.

The residual cost, recorded so nobody rediscovers it: a `TreeView` owner-draws less willingly
than a `ListView`, and its `ItemHeight` and `Indent` have their own DPI traps — see
[docs/platform-findings.md](../platform-findings.md).

Type names in the new code deliberately echo the ones they replace (`DropTargetLocation`,
`ModelDropEventArgs`) so the upstream code they were derived from is still findable.
