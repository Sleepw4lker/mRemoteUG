---
date: 2026-09-22
---

# Make the application Per-Monitor V2 DPI aware

The .NET 10 migration deliberately held `dpiAware=false` in `Properties/app.manifest`, letting
Windows bitmap-stretch the whole window — blurry, but consistent. `890b245` replaced that with
real awareness: `dpiAware=true/pm` plus `dpiAwareness=PerMonitorV2, PerMonitor`. Both elements
are needed and are not redundant — `dpiAwareness` is read from Windows 10 1703 on and is where
`PerMonitorV2` falls back to `PerMonitor`; `dpiAware` covers anything that does not read
`dpiAwareness` at all. The Windows 7/8/8.1 `supportedOS` entries were dropped in the same pass
as already untrue.

HiDPI is one of the fork's reasons to exist (see [ADR-0001](0001-fork-to-do-one-thing.md)), so
"blurry but consistent" was not acceptable. What makes this an ADR rather than a manifest edit
is the class of bug it opens up, and the amount of machinery now in place to contain it.

**Everything the framework fixes at construction and never revisits is a silent failure.**
`890b245` only made the application correct when it *starts* at a given scale. Dragging the
window between a 100% and a 200% monitor was reported broken on 2026-09-23 and fixed the same
day (`364c697`, `f85f9fe`); the specific properties involved — `ImageList.ImageSize`,
`TreeView.ItemHeight`, `ColumnHeader.Width`, `ToolStrip.ImageScalingSize`, `TabControl.ItemSize`
— are catalogued in [docs/platform-findings.md](../platform-findings.md). The decisions that
fell out of it:

- **`DpiScaling` (`src/mRemoteUG/UI/DpiScaling.cs`) is the one place that reacts to a DPI change**,
  and `FrmMain` routes both `RescaleConstantsForDpi` and `OnDpiChanged` into a single
  idempotent pass. A `Form` receives `WM_DPICHANGED` and raises `OnDpiChanged`; only *children*
  get `WM_DPICHANGED_BEFOREPARENT` and hence `RescaleConstantsForDpi`. Overriding only the
  latter on a top-level form is dead code, which is exactly the bug `f85f9fe` fixed.
- **The pass sweeps the whole window** rather than the changed control, because drop-downs never
  receive a DPI change at all and the tool windows are several parents deep.
- **It runs posted (`BeginInvoke`), not inline.** Both hooks fire mid-relayout and the form font
  they depend on is not final yet.
- **`SetThreadDpiHostingBehavior(Mixed)` is called in `ProgramRoot.Main`, before any HWND
  exists.** It is fixed at a window's creation, so calling it next to `SetParent` — the
  intuitive place, since PuTTY reparenting is what needs it — does nothing.
- **`--selftest` fails the run if the effective `HighDpiMode` is not `PerMonitorV2`** and dumps
  each monitor's DPI and every container's autoscale baseline. See
  [ADR-0013](0013-selftest-as-the-verification-mechanism.md).
- **`RdpDisplayScale` is a pure class with its own unit tests.** Every constraint in
  `UpdateSessionDisplaySettings` **fails silently** per MS-RDPEDISP — returns `S_OK`, changes
  nothing — so the values must be validated before the call and logged after it. (The old code
  passed pixel sizes for `ulPhysicalWidth`/`ulPhysicalHeight`, which are millimetres, claiming
  a 1.9-metre monitor.)
- **`PuttyBase` no longer asks `SystemInformation` for caption or border sizes.** Those wrap
  `GetSystemMetrics`, which answers for the *system* DPI under a per-monitor thread. It strips
  `WS_CAPTION|WS_THICKFRAME|WS_BORDER` off the reparented window instead, so both branches
  reduce to `MoveWindow(h, 0, 0, W, H, true)`.

One defect is **known, open and deliberately left**: after a 200% → 100% round trip, a
*second*-level drop-down (View ▸ Jump To) keeps its 200% padding and row height while its font
and image size are correct. It is cosmetic. Attempting to fix it by assigning to nested
drop-downs made it much worse at every scale and was reverted — the reasoning error and the
measurements are in [docs/platform-findings.md](../platform-findings.md). **Do not retry that
approach.**

**Amended 2026-09-23: the baseline is now Windows 11, and both DPI fallbacks are gone.**
`dpiAwareness` is a single value, `PerMonitorV2`, and the `dpiAware` element has been deleted.
The paragraph above is the reasoning as it stood for a Windows 10 1607 floor; against a
Windows 11 floor the list can only ever select its first entry and the 2005 element can only
ever be shadowed.

One correction to that paragraph while it is being read: the `dpiAwareness` **element** is
honoured from 1607, and it is the `PerMonitorV2` **value** that needs 1703. The original
conflated the two, and `SelfTest` repeated the error.

The `supportedOS` entry is unchanged and stays unchanged: `{8e0f7a12-...}` is the shared
Windows 10 and Windows 11 identifier. There is no separate Windows 11 GUID to add, which the
manifest now says in place, because looking for one is the obvious next move.

**Amended 2026-09-24: the `PuttyBase` bullet above did not work, and is superseded by
[ADR-0023](0023-host-stock-putty-as-a-maximised-child.md).** PuTTY reinstates its caption on every
`SIZE_RESTORED`, including the one the strip itself causes, so the window is hosted maximised
instead, and without `WS_CHILD`, which cost it the keyboard. The reason for no longer asking
`SystemInformation` stands.

**Amended 2026-09-25: two of the properties listed above are no longer only *sized* - they are
re-resolved.** [ADR-0026](0026-generate-the-icons-at-build-time.md) gave every glyph eight frames,
which changes what a DPI pass can do about artwork:

- `ToolStrip.ImageScalingSize` was only ever half the job, and the half that showed. It says what
  rectangle to draw into; the `Image` on each item stays whatever was assigned when the menu was
  built. So a strip could report a perfectly correct 32 while every glyph on it was a 16 pixel
  bitmap being stretched - which is what every toolbar here did above 100%. `ApplyImageScaling`
  now sets the size **and then walks the items reassigning `Image`**. It finds each one's artwork
  by reference identity through the glyph cache, so no call site names its glyph twice. The walk
  is guarded by `HasDropDownItems`, because *reading* `DropDownItems` creates the drop-down.
  **This is not the nested-drop-down fix this record forbids** - that was assigning a nested
  strip's own `ImageScalingSize`, and the walk never touches it. The open second-level drop-down
  defect is unchanged and still deliberately left.
- `ImageList.ImageSize` gained an overload that rebuilds the list from its **keys** at the new
  size instead of carrying the old images across to be stretched. That also takes the edge off the
  key-table bug recorded here: keys are the input now rather than something recovered from a round
  trip, so the images and the key table cannot come back out of step. The `Images.Clear()` before
  the `ImageSize` assignment is still required and still there.

Reference identity is the mechanism for the first and cannot be for the second:
`ImageList.Images[i]` hands back a *fresh* `Bitmap` on every read, so an image put into a list
cannot be recognised coming out. That is why the connection keys encode the icon name and the
connected state rather than being opaque.
