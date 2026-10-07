# Platform findings

Measured behaviour of .NET 10, WinForms, comctl32 and `mstscax` that constrains this codebase.

These are **not** decisions — see [adr/](adr/) for those. They are facts, each established by
measurement rather than by reading documentation, and each expensive enough to rediscover that
it is written down. Most of them are silent: the API returns success and does nothing.

If you are about to change layout, scaling, theming or session teardown, read the relevant
section first. If you measure something that contradicts a claim here, correct it here.

---

## Working on this repo

**Fixed.** Several files (`ConfigWindow.cs`, `ErrorAndInfoWindow.cs`, `MainFileMenu.cs` and
others) used to carry a genuine mix of `\r\n`, bare `\n` and even a bare `\r` within a single
file, with `core.autocrlf` `true` and no `.gitattributes` to enforce a convention. Every one of
those stray bytes turned out to be line-terminator noise — reconstructed from a corruption
pattern common to all of them (a `\r` duplicated a few bytes early, leaving its rightful
position a bare `\n`) — with the underlying content byte-for-byte identical either way; none was
inside a string literal or comment. All were normalized to CRLF by hand, and
[`.gitattributes`](../.gitattributes) (`* text=auto eol=crlf`) now enforces it going forward, so
`git checkout --`/`git restore` are ordinary safe reverts again.

The technique below remains the right one for any future file that needs a byte-precise,
whole-file-rewrite-avoiding edit — a stray or newly-introduced mixed-ending file, or any other
edit where a normal save would blow up the diff.

Any tool that rewrites the whole file normalises endings and turns a three-line change into a
whole-file diff. During the HiDPI work this was 3,200 insertions / 2,253 deletions before the
endings were restored and 1,142 / 194 afterwards, **for identical content**.

- **What breaks it:** `sed -i` (strips every CR), and editors that write the file back wholesale.
- **`unix2dos` does not fix it** — it converts the LF-only lines too, which is a third variant. A
  `dos2unix`/`unix2dos` round trip on an untouched file produces a 320-line diff.
- **What works:** `perl -0777 -i -pe` with `binmode ARGV; binmode ARGVOUT;` and literal
  `\Q...\E` substitutions, which rewrites only the matched bytes. When inserting whole lines,
  terminate them the way the surrounding lines are terminated and anchor on a line plus its
  `(\r?\n)` so the anchor's own terminator survives.
- Note `s{...}{...}` breaks when the pattern contains a brace — use `s!...!...!`. And a
  non-greedy `.*?` across a method body stops at the first inner `}`, not the method's.
- **Three ways a substitution silently does nothing**, all of which cost a round trip during the
  mRemoteUG rename because perl reports no error and `numstat` just shows an unchanged file:
  - **`\x` escapes are not escapes inside `\Q...\E`.** `\Q` quotes the backslash too, so
    `\QCopyright \xc2\xa9\E` looks for a literal `\xc2`. Put byte escapes outside the quoted run,
    or drop `\Q` when the text has no metacharacters. The same applies to `\Q$"...\E`: `$"` is a
    real perl variable and is interpolated before `\Q` ever applies.
  - **`$(` in a *replacement* is a perl variable.** `s!...!$(SolutionDir)...!` substitutes `$(`,
    the process group id, and writes `197121SolutionDir)`. Write `\x24(` instead.
  - **Backslashes in a replacement may not survive the shell.** `..\..\ThirdParty` can arrive
    at perl as `....ThirdParty`. Write `\x5c` for a literal backslash in Windows paths.
  In short: in a replacement, spell `$` and `\` as `\x24` and `\x5c`, and keep `\x` escapes out
  of `\Q...\E`.
- **Check before finishing:** `git diff --numstat`, looking for any file whose counts are far
  larger than the change actually made.

**PowerShell reads a hex literal as `Int32` by bit pattern whenever it fits in 32 bits.** So
`0xFFFFFFFF` is `-1`, which makes `$code -band 0xFFFFFFFF` a silent no-op rather than a mask —
write `0xFFFFFFFFL`. And `0xC000041D` is the `Int32` `-1073740771`, so a hashtable keyed by
`0xC000041D` cannot be looked up by the unsigned value 3221226525. Key such tables by the
formatted hex *string*. Both bit `Tools\run-selftest.ps1` while it was being written, and neither
raises an error — the first returns the input unchanged, the second just misses.

**Do not open a form in the Visual Studio designer.** It runs out-of-process, executes control
constructors, and rewrites `InitializeComponent` and the `.resx` on save. Edit `*.Designer.cs`
by hand.

**`Properties/app.manifest`: no angle brackets inside comments.** The Windows side-by-side
parser is stricter than XML and rejects the whole manifest, so the application dies with "the
side-by-side configuration is incorrect" before any managed code runs. No build, test or
analyzer catches this — only `--selftest` does.

---

## WinForms DPI and scaling

See [ADR-0010](adr/0010-per-monitor-v2.md) and [ADR-0011](adr/0011-take-fonts-from-windows.md).

### Autoscale baselines

Measured with WinForms' own algorithm at 96 DPI, not guessed:

| Font | Baseline |
|---|---|
| Segoe UI 9pt (`Control.DefaultFont` = `SystemFonts.MessageBoxFont`) | 7 × 15 |
| Segoe UI 8.25pt | 6 × 13 |

To identify which font a form was *authored* against, use control metrics, not the declaration:

| Baseline | Button | Label | CheckBox/RadioButton | ComboBox |
|---|---|---|---|---|
| 6 × 13 | 75 × 23 | 13 | 17 | 21 |
| 7 × 15 | 88 × 27 | 15 | — | 23 |

On a form whose `AutoSize` controls are recomputed at runtime, only the `Location`s carry the
authoring baseline.

### The anisotropy

**WinForms scales controls by the *font* metric; `LogicalToDeviceUnits` scales by the *DPI*.**
From 96 to 120 DPI that is ×1.33 wide and ×1.46 tall versus ×1.25. So **any panel whose size is
a scaled literal, but whose contents are auto-scaled controls, is a latent overlap.** Size such
a panel from `child.Bottom`/`child.Right` instead. This is what broke the message box and led to
[ADR-0012](adr/0012-task-dialog-on-comctl32.md).

**An embedded tool window must not position its children with anchors.** A tool window is laid out
as a `Form`, against its own font-scaled client size, and is then stretched into a host panel whose
width is a *DPI*-scaled number (`MainLayout`, `MainLayoutSettings.RescaleWidth`). Those two factors
are the anisotropy above, so a distance to an edge cached during the first layout does not reach it
after the second, and what is left over is a blank band. The connection tree carried one, along
with a hard-coded `Top` that put its toolbar over the tree as soon as the toolbar grew: measured at
192 DPI, a 40-pixel toolbar against a tree pinned to the scaled literal 29. Docking removes the
whole class - a docked child's bounds are recomputed on every layout pass and there is no cached
distance to go stale.

- **Docked children are laid out from the highest index down to 0**, each edge-docked one taking
  its band out of what is left, so **the `Fill` child is laid out last and must therefore be added
  first**. Getting this backwards lays the edge-docked control out at zero width, which the
  compiler cannot see. `OptionsForm.Designer.cs` is the in-tree example that has it right.
- **A single-line `TextBox` docked to `Fill` is the fix for its own height**, not a problem with
  it: it pins its height to `PreferredHeight` and takes the full width, which is what the
  `Multiline` flip in the connection tree used to be for.

*Still unmeasured:* whether the anisotropy accumulates across repeated monitor moves. Needs real
hardware.

### Properties fixed at construction and never revisited

Every one of these is silent, and each was measured:

- **`ImageList.ImageSize` empties the list once its native handle exists.** Two images in, none
  out. Before a handle it keeps them — which is exactly why this was missed: at startup nothing
  is realized, so icons were fine, and the first DPI change wiped them. `DpiScaling.ResizeForDpi`
  copies the images out and puts them back.
- **...and it does not empty the key table beside it, which is the half that cost the icons a
  second time.** `ImageCollection` keeps a parallel list of keys that the assignment leaves
  untouched, so images put back afterwards land *behind* stale entries. Measured, two images in:
  `Count` 0 and `Keys` 2 straight after the assignment, then `Count` 2 and `Keys` 4 once they are
  re-added. `ContainsKey` and `IndexOfKey` scan the key table but bound the answer by the image
  count, so **every key added after a rescale is invisible** - `IndexOfKey` returns -1, and a
  `TreeNode` whose `ImageKey` does not resolve falls back to image 0 - and, once the counts catch
  up, every lookup is shifted by however many images were present at the assignment. The
  connection status icons are held in plain/overlaid pairs and that shift was odd, so each key
  resolved to the other member of its own pair: the green badge appeared on exactly the hosts that
  were **not** connected. `Images.Clear()` before assigning `ImageSize` empties both.
- **An `ImageList` stretches whatever it is handed; it never re-selects an icon frame.** Measured
  on .NET 10 against the old `mRemote_Icon.ico`, which carried 14 frames (it has since been
  deleted with the rest of the upstream branding - take any multi-frame `.ico` to repeat this):
  an `Icon` built at 16 and added
  to a 32x32 list fills the slot with the *stretched 16* frame - 1024 opaque pixels, identical to
  drawing that frame through `Graphics` at 32 - while the same icon built at 32 yields the real
  32 frame at 1012. Added as an `Icon`, as a `Bitmap`, or rasterized through `Graphics` first,
  all three fill the slot identically. **Frame selection happens in `new Icon(stream, w, h)` and
  nowhere else**, and a single-frame `.ico` returns its only frame whatever is asked of it - which
  is why `ConnectionIcon.FromString`'s size argument changed nothing before the artwork gained a
  ladder. The claim that an `Icon` sits as a small stamp in a larger slot was measured again from
  every direction and is **not reproducible**; the bullet asserting it has been deleted rather
  than left to be believed.
- **`System.Drawing.Icon` will not hand back a 256x256 frame.** Measured against
  `Resources/Icons/App_Icon.ico`, which carries 16, 20, 24, 32, 48, 64 and 256 as PNG-compressed
  frames: `new Icon(path, 256, 256)` returns a **64x64**. Every other size comes back exactly as
  asked. The frame is genuinely in the file - the directory entry stores 256 as a width byte of
  0, and the blob behind it is a valid 4,289-byte PNG - so this is the managed loader's limit and
  not a defect in the icon. The shell has its own loader and has read 256px PNG frames since
  Vista, which is what Explorer's extra-large view uses, so **do not "fix" a generated icon on the
  strength of what `Icon` says about it**. Extract the frame from the file and look at it instead.
  The practical consequence is narrow: a test that asserts on a 256 frame through `Icon` will fail
  against a correct file.
- **`ImageList.Images.Add` does not copy until the native handle exists.** Before it the list
  holds the *reference* and reads it during `CreateHandle`; after it the copy is immediate.
  Measured: add an image, dispose it, then touch `Handle` - an `Icon` throws
  `ObjectDisposedException` and a `Bitmap` throws `ArgumentException: Parameter is not valid`,
  and both come from the `Handle` access rather than from the `Add` that looked wrong. Do the two
  in the other order and each is fine. `StatusImageList.cs:109` says "the list takes its own copy
  of whatever it is given", which holds only once realized: `GetConnectionIcon` adds inside
  `using` blocks and is saved today only by ADR-0018's ordering, which shows the window - and so
  realizes the handle - before any connection is loaded. Realize the handle before adding
  anything you mean to dispose.
- **...and getting that wrong is catastrophically slow *before* it crashes.** Adding an image to
  an unrealized list and disposing it straight away does not fail at the disposal, and does not
  always fail at the handle either: measured, it first put GDI into a state where the DPI test
  fixture went from **824 ms to 2 minutes 40 seconds** for the same 42 tests, and only then began
  crashing the test host. So a sudden, unexplained collapse in the speed of anything that draws
  is worth reading as this bug rather than as a performance problem. It was introduced by putting
  a `using` around an `ImageList.Images.Add` argument in a constructor, which is the most
  reasonable-looking way to write it.
- **`TreeView.ItemHeight` is derived from font and image list only while nothing has set it**
  (18 at 9pt, 34 at 18pt, 32 with a 32px list). Set it once and it never moves again — the
  designer had pinned 18, so rows stayed 18px while the font grew to 36. **Leave it unset.**
  `ConnectionTree` carries a comment saying so.
- **`TreeView.Indent` is derived from nothing** and must be scaled by hand.
- **`ColumnHeader.Width` is not a control property**, so the auto-scale pass never sees it.
- **`TabControl.ItemSize` does not follow the font** — held 100 × 24 while the font doubled.
- **A `ToolStrip` does not inherit its parent font.** Measured: a form went 9pt → 18pt, a `Label`
  followed, the `MenuStrip` stayed at 9pt with nothing of its own set (it reads a process-wide
  default). Menu *text* therefore never follows a DPI change; assign it.
- **`ToolStrip.ImageScalingSize`: assigning it propagates to drop-downs** (existing and
  later-built), **but the strip's own DPI rescale does not** — a drop-down measured 16 × 16 while
  its owner had moved to 32 × 32. Assign it explicitly on every DPI change.
- **A `ToolStripItem` is not a `Control`**, so the auto-scale pass does not reach its `Size` or
  `Margin` the way it reaches a control's bounds — the same fact as `ColumnHeader.Width` above. On
  a `ToolStripComboBox` that stops being cosmetic: assigning the item's `Size` pushes the width onto
  the hosted `ComboBox`, and `ToolStripControlHost.GetPreferredSize` reads it straight back, so the
  value sustains itself. The quick connect field sat at **200 physical pixels from 96 DPI to 192**
  while the font in it doubled. Measure such a width from the font each pass, floored by the scaled
  literal, and never from its own previous value: measured, `Control.Scale(2f, 2f)` over the strip
  *does* take that field from 200 to 396, so anything derived from the current width compounds.
- **A `ToolStripComboBox` does not get its font from the strip it sits on.** Measured: the strip
  was assigned 18pt and the hosted `ComboBox` stayed at 9pt, because `ToolStripControlHost.Font`
  *is* the hosted control's font rather than something resolved from the owner. So
  `DpiScaling.FollowDpiChange` grew the menu text on every DPI change and never the quick connect
  field's — the only symptom being an entry box whose text stayed small at 200%. Hand the font down
  to the hosted control by name. `ToolStripItem.Font` is no substitute for reading it back: on a
  control host that property *is* the control's font, so it reports the stale value too.
- **`ToolStripItemImageScaling.None` takes an item's image out of `ImageScalingSize` altogether.**
  The quick connect connections button had it, so it drew 16 × 16 at every scale while the button
  beside it followed the strip to 32 × 32. There is never a reason to opt out - with artwork now
  picked at the exact size, `SizeToFit` into an equal rectangle is an identity transform, so
  leaving it on costs nothing and opting out still breaks the item.
  `DpiScalingTests.NoToolStripItemOptsOutOfGlyphScaling` guards it.
- **`ToolStrip.ImageScalingSize` does not change any item's `Image`.** It says what rectangle to
  draw into, and the framework stretches whatever is there to fit. So a strip can report a
  perfectly correct `ImageScalingSize` of 32 while every glyph on it is a 16 pixel bitmap being
  blown up - which is what every toolbar in this application was doing above 100% until the
  artwork gained more than one frame. Re-picking is a **second, separate pass** over the items:
  `DpiScaling.ApplyImageScaling` sets the size and then walks `ItemsOf` reassigning `Image`.
  Walk with `ToolStripDropDownItem.HasDropDownItems`, never with the item count - *reading*
  `DropDownItems` creates the drop-down, so an unguarded walk builds every submenu in the
  application during the first DPI pass. This is not the nested-drop-down fix ADR-0010 forbids:
  that was assigning a nested strip's own `ImageScalingSize`, and this never touches it.
- **The re-pick is a one-shot pass, so an `Image` assigned later is back to 16.** Measured on the
  property grid toolbar: with the strip at 192 DPI and `ImageScalingSize` 32, the connection icon
  and the host status mark read back 16 × 16 while the four mode buttons beside them read 32 × 32.
  The difference is *when* the image is assigned. The mode buttons get theirs once, in
  `InitializeComponent`, so `ApplyImageScaling` reassigns them on the way up; the other two are
  reassigned on every selection change, every ping reply and every pick from the icon menu - all
  long after that pass - and each of those took `Resources.Foo`, which is always the 16 pixel
  frame. Two soft buttons sat beside four sharp ones. Anything that assigns a glyph while the
  window is running has to name the size itself, and the size to name is
  `item.Owner.ImageScalingSize.Width`: it is the rectangle the strip will actually draw into, and
  both the framework's own startup scaling and `ApplyImageScaling` keep it current, so it is right
  without a DPI change ever having happened. `PropertyGridToolbarTests` pins both buttons.
- **Reference identity does not survive an `ImageList`.** `Images[i]` hands back a *fresh*
  `Bitmap` on every read, so an image put in cannot be recognised coming out. A reverse lookup
  from image to name - which is how a `ToolStripItem` is re-picked without every call site naming
  its glyph twice - therefore works on a `ToolStripItem` and cannot work on an `ImageList`. Image
  lists have to be rebuilt from their **keys** instead, which is why the connection keys encode
  the icon name and the connected state rather than being opaque.

### PropertyGrid builds its own toolbar, and fills it with its own bitmaps

- **The strip is the control's, not the application's.** A `PropertyGrid` creates a
  `PropertyGridToolStrip` among its child controls and puts five items on it: **Categorized,
  Alphabetical, NoSort, a separator, Property Pages**. `ConfigWindow` finds it by walking
  `_pGrid.Controls`, hides Property Pages, and merges its own buttons onto the end - so the strip
  ends up 5 + 6 = 11 items, with the framework's occupying 0-4.
- **Its images are framework bitmaps and cannot be re-picked.** They never came from the glyph
  cache, so `Glyphs.TryRepick` does not recognise them and `DpiScaling.ApplyImageScaling` leaves
  them alone - they were 16 pixel bitmaps stretched to 24 at 150% while everything beside them was
  crisp. Assigning `Item.Image` from the cache fixes both the look and the scaling at once, and is
  what `ConfigWindow.StyleFrameworkSortButtons` does.
- **NoSort ships with no text, no tooltip, and the same bitmap as Alphabetical.** Measured on
  .NET 10: items 1 and 2 return byte-identical PNGs (365 bytes, same hash), and item 2's `Text`,
  `ToolTipText` and `AccessibleName` are all empty. So stock WinForms draws two adjacent buttons
  that look identical, and the second cannot be identified by hovering it.
- **Identify those buttons by behaviour, not by text.** `Text` comes from the framework's own
  resources and follows the OS UI culture, not the application's, so matching on "Alphabetical"
  breaks on a non-English Windows. Setting `PropertySort` checks exactly one button, and which one
  it checks is the definition of what that button is - that is what `PropertyGridToolbarTests`
  asserts on. Everything else here is by index, guarded by the item count.
- **None of it happens until `Load` runs.** `AddToolStripItems` is called from the Load handler,
  and Load does not run for a form that has only had `CreateControl` called on it. A test written
  against `CreateControl` sees the five framework items unmerged and every one of them reporting
  `Visible = false`, because the strip was never shown - so it passes or fails for the wrong
  reason. `Show()` the window. (The same trap is recorded above for the connection tree search
  box.)

### Which hook fires where

- **`RescaleConstantsForDpi` is the *child* hook** (`WM_DPICHANGED_BEFOREPARENT`). A top-level
  `Form` is sent `WM_DPICHANGED` and raises **`OnDpiChanged`** instead, so an override of the
  former on a form is dead code. Override both and route to one idempotent pass.
- **Run the fix-up posted (`BeginInvoke`), not inline.** Both hooks fire mid-relayout and the
  form font they depend on is not final yet.
- **The fix-up also has to run once at startup, and there it is called rather than posted.** It
  had only ever been reached from a DPI *change*, so a first launch on a 144 DPI monitor left
  every `ToolStrip` on the process-wide default font until the window was dragged elsewhere. The
  reason for posting does not apply in `Load`: the auto-scale pass has run, so the font is
  already final, and posting would push the work past the reveal and show the change happening.
- **`SetThreadDpiHostingBehavior(Mixed)` must be called before any HWND exists.** It is fixed at
  a window's creation, so calling it next to `SetParent` does nothing.
- **`SystemInformation.CaptionHeight`/`FrameBorderSize`/resize-border thicknesses wrap
  `GetSystemMetrics`**, which answers for the *system* DPI under a per-monitor thread. Do not use
  them for per-monitor geometry.

### Nested submenu padding — known, open, deliberately left

After a 200% → 100% round trip, a *second*-level drop-down (View ▸ Jump To) keeps its 200%
geometry — `Padding.Left` 49 and row height 38 instead of 33 and 22 — while its font (9pt) and
`ImageScalingSize` (16) are correct. First-level drop-downs are fine. Cosmetic, and left alone
on purpose.

**Do not "fix" it by assigning to nested drop-downs. That was tried and made it much worse.**
Walking `DropDownsOf(strip)` and setting `ImageScalingSize`/`Font` then `PerformLayout()` on each
broke Tools, Help and View at *every* scale:

- **Touching a nested drop-down directly realizes its window at the current DPI.** Its
  `DeviceDpi` went from 96 (where it sits permanently if untouched) to 192, and from then on it
  **re-scaled anything assigned**: an 18pt font assigned at 192 rendered as **36pt**, padding
  49 → 65, row height 38 → 74.
- **Inheriting is not assigning.** The framework never assigns fonts to level-1 drop-downs — they
  inherit, which does not double-scale. "Do for level 2 what the framework does for level 1" is a
  false analogy, and it was the reasoning error.

Ruled out by measurement on real hardware: `DeviceDpi` on drop-downs reads 96 throughout (a red
herring), and `RescaleConstantsForDpi` does **not** touch drop-down padding at all — padding is
derived from `ImageScalingSize` at layout time. The defect is a missing *layout*, not a missing
value. Five in-process harnesses all stayed green, because a synthetic `MenuStrip` recreates the
drop-down window where the real application caches it: **this cannot be reproduced off real
hardware.** The `ImageScalingSize`-propagates comment in `DpiScaling.ApplyImageScaling` is
hard-won, not an assumption.

### A ToolStrip-hosted ComboBox recreates its window on a font change

Measured, and it crashed the application: moving the main window from a 200% monitor to a 300% one
died with `Win32Exception (1400)` - `ERROR_INVALID_WINDOW_HANDLE` - inside
`ComboBox.RecreateHandleCore`, reached from `Control.WmDpiChangedBeforeParent`. No application code
on the stack, and no connection needed. See [ADR-0029](adr/0029-no-autocomplete-on-the-quick-connect-box.md).

- **WinForms scales a control's font from inside `WM_DPICHANGED_BEFOREPARENT`, but only when that
  control has an *explicitly set* font.** An inherited font is left to follow its parent.
- **`ToolStripControlHost` gives every hosted control an explicitly set font**, so every hosted
  control is in that first category whether it wants to be or not. Measured: the quick connect box
  reads Segoe UI 9pt as its own font, and assigning `strip.Font` - which
  `DpiScaling.FollowDpiChange` does on every DPI change - reaches it and changes it (9pt to 18pt).
- **A `ComboBox` with `AutoCompleteMode` set recreates its window when its font changes; with
  `None` it keeps the handle.** Clean A/B on the real `QuickConnectToolStrip`: `SuggestAppend`
  recreated, `Suggest` recreated, `None` did not. This is the whole reason the crash was reachable.
- **The crash could not be reproduced without a real move between real monitors.** `--selftest`
  drives the genuine path in a genuine per-monitor-aware process - font scaled, window recreated,
  same as the failing machine - and it succeeds. So the recreation is necessary but not sufficient,
  and what the real move adds is still unknown. **The fix is confirmed on the affected machine
  even so**: with autocomplete gone, the 200%-to-300% move no longer crashes.

Three plausible explanations were **disproved**, and are recorded so the next attempt does not
spend itself on them:

1. Re-entrancy through `QuickConnectComboBox.SyncItemHeightToFont`. Setting `ComboBox.ItemHeight`
   does **not** recreate the handle - same HWND before and after.
2. Overflow reparenting. The recreation succeeds with the combo reparented into a
   `ToolStripOverflow`, whether that popup's own window exists or has been destroyed. Narrowing the
   form does not push the item into the overflow either, the strip being `Dock = DockStyle.None`.
3. `DpiScaling.FollowDpiChange`. The failing run's log has no `DPI follow-up` lines before the
   crash: the child's `WM_DPICHANGED_BEFOREPARENT` is handled before the form's `WM_DPICHANGED`,
   so the application's own sweep had not run yet.

**In .NET 10 there is no `Control._deviceDpi` field.** The cached DPI that
`WmDpiChangedBeforeParent` compares against is behind the internal `DeviceDpiInternal` property,
which is settable - that is how the selftest check creates the inequality a monitor move would,
since a window that has not moved makes `GetDpiForWindow` answer the current DPI and WinForms take
its "nothing changed" early return.

### RDP display scale

Per MS-RDPEDISP, every constraint on `UpdateSessionDisplaySettings` **fails silently** — returns
`S_OK` and changes nothing:

- desktop scale factor must be 100–500
- device scale factor must be exactly 100, 140 or 180
- the width must not be odd
- both dimensions must be 200–8192
- **`ulPhysicalWidth`/`ulPhysicalHeight` are millimetres, not pixels.** The old code passed the
  pixel size, claiming a 1.9-metre monitor.

Hence `RdpDisplayScale`, a pure class with unit tests, and a log line recording exactly what was
sent.

#### A session created at 100% cannot reliably be rescaled

Reported from the target machine: connecting to a host **nobody is logged on to** gave a session
whose fonts stayed at 100% after logon, until the window was resized by hand. The values were
never wrong - a resize sends the same numbers the application already sent.

**Measured on the target machine, 2026-09-29**, over two rounds:

- Telling the session its scale through `UpdateSessionDisplaySettings` **after** it exists does
  not fix it. Not at `OnLoginComplete` (the original behaviour), and not at `OnConnected` before
  any desktop exists in the session either, with the resolution nudged two pixels so the host
  could not read the monitor layout as unchanged.
- **Declaring the scale before `Connect()` does.** See the next section. The session comes up
  scaled and nothing has to talk it into changing.

**Which mechanism defeats the post-connect route was never established.** Nudging the resolution
two pixels, to make the layout one the host could not read as unchanged, was tried during this
work and settled nothing either way - it is deliberately not in the code. The candidates, none of
them eliminated:

1. **The layout arrives while the desktop is being created.** A session's scale is session-wide
   state; a user desktop created after the change should pick it up, but one created *during* it
   need not.
2. **The host reads a resolution-identical layout as no change.** A monitor layout carries
   resolution and scale in one PDU, and after `SetResolution` the session already has the
   resolution being asked for, so every scale-only change is resolution-identical. Whether
   mstscax or the host also coalesces two layouts sent back to back is unknown, which is why
   nudging the resolution did not settle this either way.
3. **`Control.DeviceDpi` on the hosted `AxHost` reads 96 that early**, in which case both passes
   asked for `desktopScaleFactor 100` and correctly did nothing.

If it ever needs reopening: `Display scale [<session>]: <pass> pass at <n> DPI` names the pass and
the DPI it used, and `asked the session for WxH at n DPI (desktopScaleFactor ...)` says what went
out. Both are `DebugMsg`, and **debug is off by default** (ADR-0017) - the failing round above
proved nothing precisely because the run was not `mRemoteUG.exe --verbose`.

#### The extended-settings property bag takes the display scale - measured

The pre-connect route is `IMsRdpExtendedSettings.Property`, the bag mstsc drives from
`desktopscalefactor:i:` and `devicescalefactor:i:` in a `.rdp` file. Measured by `--selftest`
(`CheckRdpControl`) on **mstscax 10.0.26100**, on a control that has not connected to anything -
which is why this is measurable on a machine with no route to a server at all:

- `DesktopScaleFactor` and `DeviceScaleFactor` are both **accepted by name**, and read back the
  value that was set.
- **The value must be VT_UI4.** A boxed `int` throws `COMException 0x80004005` (E_FAIL). So the
  `uint` in `RdpProtocol.SetDisplayScale` is load-bearing, and the selftest re-measures it on
  every run rather than trusting this paragraph.
- The interop declares the setter as `set_Property(string, ref object)`, so the C# indexer does
  **not** bind and it must be called as a method with an explicit `ref`. Pinned by
  `AxInteropSurfaceTests.TheExtendedSettingsBagTakesItsValueByReference`.

An unknown name in that bag throws, which is the whole reason the restraint above
`SetRedirection` exists; ADR-0028 proposes spending it on these two names and no others.

### .NET 10 ignores bounds assignments on an in-place AxHost

`Size`, `Bounds`, `SetBounds` and `Width`/`Height` all return without changing anything and
without throwing, once the hosted ActiveX control is in place. .NET Framework 4.8 honoured every
one of them. **Docking is the only thing that still resizes such a control**, so the RDP control
is `Dock = DockStyle.Fill` in `ProtocolBase.Initialize` and **must not be given an `Anchor`**
(setting `Anchor` clears `Dock`).

This silently broke automatic RDP resizing: the control never changed size, so `ResizeEnd` saw
the size captured at `ResizeBegin`, and no guard in `ReconnectForResize` was ever reached to log a
reason. Pinned by `ProtocolBaseHostedControlSizingTests`, which **needs a real ActiveX control** —
a `Panel` resizes either way and passes against the bug.

---

## PuTTY hosting

See [ADR-0023](adr/0023-host-stock-putty-as-a-maximised-child.md).

**PuTTY puts its caption back on every `SIZE_RESTORED`.** In `windows/window.c` (PuTTY 0.85;
PuTTY-CAC is identical) the `WM_SIZE` handler calls `clear_full_screen()` whenever `wParam ==
SIZE_RESTORED`, and that function ORs `WS_CAPTION | WS_BORDER` back in, re-adds `WS_THICKFRAME`
unless the session disables resizing, and calls `SetWindowPos(SWP_FRAMECHANGED)`. Stripping
those styles from a non-zoomed PuTTY window is therefore a no-op: the `SWP_FRAMECHANGED` that
applies the strip changes the client size, Windows sends `WM_SIZE(SIZE_RESTORED)`, and the
caption is back before `SetWindowPos` returns. `890b245` shipped exactly this, and the title bar
was drawn inside the tab.

- **`is_full_screen()` is `IsZoomed(hwnd) && !(style & WS_CAPTION)`.** A maximised window
  receives `SIZE_MAXIMIZED`, which never touches the style; in that state `reset_window` sizes
  the terminal to the client area and centres it, and `wintw_request_resize` returns without
  resizing the window. Hence `HostedPuttyWindow.Adopt`: strip, `SetParent`, `SW_MAXIMIZE`, and
  nothing that sends a `WM_SIZE` in between.
- **Reconfigure (`IDM_RECONF`) re-evaluates the styles** from the session settings, using the
  full-screen scrollbar option and clearing `WS_THICKFRAME` while `is_full_screen()`. The
  maximised state survives it; a `SW_RESTORE` (PuTTY's own Alt+Enter toggle) does not.
- **Read back, do not assume.** `HostedPuttyWindow.Describe` reports style, parent and zoom
  state; a session logs it after its first `Resize` and warns if the caption is present, and
  `--selftest` fails on it. The self-test also types into the hosted PuTTY and reads the
  keystroke back off the loopback connection.
- **`SetParent` can itself be the source of the `SIZE_RESTORED` that undoes the strip**,
  contradicting the "nothing sends a `WM_SIZE` in between" reasoning above. Measured with a
  50-run loopback harness outside this repo (real PuTTY 0.85, `Adopt` called exactly as
  `PuttyBase.Connect` calls it): 2/50 runs read back a window that was zoomed (`ShowWindow`
  had run) *and* captioned — style `0x15EF0000` against the clean `0x152B0000`, which is exactly
  the pre-adopt style OR `WS_MAXIMIZE`. Since `SIZE_MAXIMIZED` never touches the style, the only
  way to end up zoomed-with-caption is a `SIZE_RESTORED` landing on PuTTY's thread between the
  style strip and the `SW_MAXIMIZE` call, and `SetParent` is the one step in between that can
  itself change what Windows considers the window's maximized geometry. This is the user-visible
  "PuTTY session has a window border, maybe 10% of the time" defect. `HostedPuttyWindow.Adopt`
  now reads the state back after maximizing and, if the caption is present, repeats the strip and
  the maximize once — not `SetParent`, which only runs once and is not exposed to the race again.
  Re-running the same harness with the retry in place: 0/150. See
  [ADR-0031](adr/0031-retry-the-putty-chrome-strip-once.md) (proposed).

### Undocking to a different-DPI monitor — reported, not yet measured

Uwe reported this; it has not been reproduced here, and there is no multi-monitor, mixed-DPI
hardware on this machine to reproduce it on. Recorded as told, not guessed at:

- mRemoteUG was running on an external monitor at 200% scaling.
- The laptop was undocked, leaving only its own panel, at 150% scaling.
- A *new* stock PuTTY session opened after the undock filled only about two thirds of the panel.
- Sessions that were already open at the time of the undock kept their correct size. Only a
  session opened fresh, after the monitor changed, was affected.

That last fact rules out the two explanations that looked most likely from the code alone:

- **Not `HostedPuttyWindow.Adopt`'s `MoveWindow` being DPI-virtualized against a lower-than-PMv2
  child.** If the coordinates this process hands to `MoveWindow` were being scaled down before
  reaching a system-aware or unaware PuTTY, every call to it would be affected equally — the one
  `InterfaceControl.OnDpiChangedAfterParent` → `PuttyBase.NotifyDpiChanged` → `Resize` makes for
  an *existing* session when the monitor changes, and the one `PuttyBase.Connect` makes for a
  *new* one. Only the second is reported broken.
- **Not a stale `InterfaceControl.Size` left over from before the undock.** A newly constructed
  `InterfaceControl` reads `Parent.Size` (the tab page) at construction time, after the tab page
  has already been docked and laid out on the new monitor - there would be nothing stale for it
  to inherit.

Both point at something specific to the sequence a *new* stock PuTTY process goes through that
an existing, already-adopted one does not: `HostedPuttyWindow.Adopt`'s strip → `SetParent` →
`SW_MAXIMIZE`, run for the first time on a window that PuTTY (unaware or system-DPI-aware, per
above) has just placed and sized for itself before this process ever touches it - against a
system DPI that may not match the monitor the undock has left the window on. That is a
hypothesis, not a finding: it fits the one fact gathered so far, but nothing here can exercise a
real monitor-topology change to test it.

`PuttyBase.LogHostedWindow` now reads the hosted window's actual bounds back with
`NativeMethods.GetWindowRect` - called from this (per-monitor-aware) thread, so, unlike a call
made by an unaware or system-aware thread, it is not itself DPI-virtualized - and logs a Warning
if they do not match the panel `Resize` asked for. The next time this is reproduced, that line
(or its absence) settles which half of the hypothesis was right: an actual rect smaller than the
panel means Windows did not honour the requested size for this child; a matching actual rect
means `InterfaceControl`'s own size was already wrong by the time `Resize` ran, which points back
upstream, to the tab and panel layout instead of to the PuTTY adoption sequence.

**A `WS_CHILD` window cannot take the keyboard from another process.** Keystrokes are delivered
to the focus window of the thread that owns the *foreground* window, and a child window can
never be the foreground window. With `WS_CHILD` set on the reparented PuTTY,
`SetForegroundWindow(putty)` left the foreground on a window of this process (measured: the
self-test's host form) and PuTTY never received `WM_SETFOCUS` — its cursor stayed hollow and
typing went nowhere. Without `WS_CHILD` the same call makes PuTTY's window foreground, its thread
reports it as `hwndFocus` in `GetGUIThreadInfo`, and a typed `x` + Enter arrives on the raw
connection. PuTTY does not call `SetFocus` on itself, so it cannot rescue a child window by
being clicked either. Hence `HostedPuttyWindow.Adopt` leaves the window top-level with a parent,
which `SetParent`'s documentation advises against and which this application did for years.

- **`GetParent` returns null for such a window.** It answers only for `WS_CHILD` windows and
  returns the owner otherwise; the parent set by `SetParent` is read with
  `GetAncestor(hwnd, GA_PARENT)`. The first readback used `GetParent` and reported a
  successful reparent as a failure.

**The Chocolatey `putty.exe` cannot be hosted.** `C:\ProgramData\chocolatey\bin\PUTTY.EXE` is a
ShimGen shim (`is gui? True, wait for exit? False`): it starts the real binary under
`lib\putty.portable\tools` in a separate process and exits. `Process.MainWindowHandle` on the
shim never finds a window and `Exited` fires at once. `PuttyPathProvider` finds the shim first
because it is on PATH; point `CustomPuttyPath` at the real executable
(`putty.exe --shimgen-noop` prints it).

### PuTTY Profiles and the registry notification

**Saving one profile in PuTTY produces a storm of notifications, not one.** PuTTY writes each
setting as its own `RegSetValueEx` under the session's key, so with `REG_NOTIFY_CHANGE_LAST_SET`
and a subtree filter a single save signals dozens of times. Measured by writing 40 values to one
key in a tight loop: 40 writes, and the kernel signals enough of them that handlers overlap.

**`RegNotifyChangeKeyValue` re-armed before the handler runs fans out across the thread pool.**
The registration is one-shot and has to be renewed on every callback, and renewing it *before*
raising is the only way not to lose a change that happens while a handler runs. The consequence
is that each renewal can fire on a fresh pool thread while earlier handlers are still going:
measured **6 and 8 concurrent raises** of one `Changed` event for the 40-value burst above
(`RegistryKeyChangeWatcher`, 15–20 ms per handler). That is not a race the handler can be written
around — a handler for a "something under here changed" edge re-reads the whole key by definition
and is never re-entrant — so `RegistryKeyChangeWatcher` serialises raises itself and folds changes
arriving mid-run into one further run. The same burst then collapses to **2 handler runs**.

This is what crashed the application: `PuttySessionsManager.AddSessions` ran on several pool
threads at once, each mutating and enumerating the same `RootPuttySessionsNodeInfo.Children`
list, and the process died with `InvalidOperationException: Collection was modified; enumeration
operation may not execute` the moment a host was saved in PuTTY. Three separate stacks were
logged in the same millisecond — one in `ConnectionTree.RebuildChildren` on the UI thread, one in
the `Sessions` getter, one in `ContainerInfo.SortOnRecursive`.

**The refresh cannot be fixed with a lock.** `AddSession` raises a collection-changed event whose
`ConnectionTree` handler calls `Control.Invoke`, which blocks until the UI thread runs it. A
background thread holding a PuTTY gate across that `Invoke`, with the UI thread wanting the same
gate to read `Children`, deadlocks — and a wedged UI thread freezes the whole desktop
([ADR-0005](adr/0005-park-rdp-controls-before-disposing.md)). The refresh is marshalled onto the
UI thread instead, which removes the shared-state problem rather than guarding it.

**`SynchronizationContext.Current` is a `WindowsFormsSynchronizationContext` on the self-test's
thread**, so `CoalescingDispatcher` captures a usable context there. But **nothing pumps messages
during a self-test run**: a posted callback only arrives if the probe calls `Application.DoEvents()`
itself. The self-test's `PuTTY session watcher` check reports the burst's run count and peak
concurrency, and fails if a burst ever raises on more than one thread.

---

## Dark mode

See [ADR-0009](adr/0009-system-colour-theme-at-startup.md).

- **`Application.SetColorMode` does no native re-theming.** It re-points `SystemColors` and
  notifies `SystemEvents`. `SetWindowTheme`, the DWM title-bar call and
  `TVM_`/`LVM_SETBKCOLOR` all run at handle creation and nowhere else.
- **It is not experimental in .NET 10.** WFO5001 was removed; the only `[Experimental]` left in
  `System.Windows.Forms.dll` 10.0.12 is WFO5003 on `IAsyncDropTarget`. Nothing to suppress.
- **`ToolStripManager.RenderMode` reports `Professional` while handing out a
  `ToolStripSystemRenderer`.** Trust the renderer, not the property. Only
  `ToolStripSystemRenderer` has a dark variant.
- **A `SystemColors` value is a handle resolved on every read**, so it follows the theme even
  when cached in a field. Only literal colours freeze.
- **A native colour read-back that disagrees with the managed one usually means nothing.**
  `LVM_GETBKCOLOR` returns 0 and `TVM_GETTEXTCOLOR` returns `CLR_NONE` in a *light* run too,
  where both controls are plainly correct — they mean "nobody set one". The real failure signal
  is a background that stays *light* while dark mode is on.

### The RDP control's connecting page is white, and stays white

`mstscax` fills its whole client area pure white (`#FFFFFF`) and draws `ConnectingText` on it.
Measured on 10.0.26100 in a process with `Application.SetColorMode(Dark)` already applied:

- It is white **before `Connect()` is ever called** — from the moment the OCX has a window.
  So this is not a connection-time screen that could be avoided by connecting faster; it is what
  an idle RDP control looks like.
- **`AxHost.BackColor` is accepted, read back unchanged, and ignored.** Setting it to
  `#202020` before parenting or after `CreateControl()` both succeed, `BackColor` reads back
  `#202020`, and the client area stays `#FFFFFF`. The OCX never asks for the ambient colour.
- The mstscax type library has **no colour property other than `ColorDepth`** — nothing on
  `IMsRdpClient`..`IMsRdpClient10`, `IMsRdpClientAdvancedSettings`..`8`, or the non-scriptable
  side interfaces. There is nothing to set.

The only remedy is to put something themed in front of it: `UI\Controls\ConnectingOverlay`, a
sibling of the `AxHost` inside the `InterfaceControl`, added in `RdpProtocol.Initialize` (before
the handle wait, which pumps and is therefore where the white page is first painted) and removed
on `OnConnected`. Siblings clip against each other through `WS_CLIPSIBLINGS`, which WinForms sets
on every control, so the front one wins — measured with a real OCX underneath, by screen capture
rather than `DrawToBitmap`.

**`Control.DrawToBitmap` cannot measure this.** It walks the child controls and paints them in an
order of its own, ignoring z-order, so a control that is behind another is drawn on top of it. A
sibling-covering test written that way fails against correct code. Only a screen capture
(`Graphics.CopyFromScreen` over the control's screen rectangle) shows what the user sees.

Removed on `OnConnected` rather than `OnLoginComplete`: between those two events the OCX is
already showing the remote side — on a connection without NLA that is the server's own logon
screen, and covering it would hide a prompt the user has to answer.

---

## Task dialogs

See [ADR-0012](adr/0012-task-dialog-on-comctl32.md).

- **`TaskDialogButton.Yes` and friends return a fresh instance on every read**, so `Is.SameAs`
  never holds — but `==` is overloaded and compares **by value**. A command link labelled
  "Cancel" compares equal to `TaskDialogButton.Cancel`. Identify command links by their `Tag`
  *before* comparing against any standard button.
- **A fresh `TaskDialogPage` already has a non-null `Expander`, `Footnote` and `Verification`.**
  What decides whether they render is whether they have text.
- **Windows has no question icon for task dialogs.** Pass it as a custom icon
  (`new TaskDialogIcon(SystemIcons.Question)`).
- **`TaskDialog.ShowDialog` throws without visual styles and Common Controls v6.**

## Owner-drawn tab strip

**A `TabControl` paints the strip around its tabs itself, and `DrawItem` never gets the chance.**
The owner-draw is called once per item with that item's rectangle, so the band beside the last tab,
and the padding above and below the row, are whatever comctl32 put there - the themed `Tab` parts,
which have no dark variant and which `Application.SetColorMode` does not re-theme. That is a light
band across a dark window, and no amount of work inside `DrawItem` reaches it.

- **Overriding `OnPaintBackground` is dead code here.** `TabControl` wraps `SysTabControl32` and
  does not set `ControlStyles.UserPaint`, so the framework never raises it. Setting that style
  would raise it *and* take item drawing away from comctl32, which is what sends the `WM_DRAWITEM`
  the owner-draw depends on.
- **Answering `WM_ERASEBKGND` is enough.** comctl32 draws the items and the pane border in
  `WM_PAINT` but does not fill the strip again, so the fill survives. Measured by removing the
  handler: the band goes back to the theme colour.
- **`TabControl.BackColor` is a constant.** Its getter returns `SystemColors.Control` whatever is
  assigned and its setter is empty - measured by assigning and reading straight back. So a fill
  painted with it is painting the theme's own colour back over the theme, which looks exactly like
  a fill that never ran. `SessionTabControl` overrides the property with a real backing field, and
  that is also the only way the painting can be tested: in a light process the themed band and
  `SystemColors.Control` are both `#F0F0F0`, so asserting the band *is* `Control` passes just as
  well against the defect.


**`TabPageCollection.Remove` and `Insert` each leave a window where the native tab control and
the managed page list disagree by one.** `RemoveTabPage` drops the page from the managed list
*before* sending `TCM_DELETEITEM`; `InsertItem` sends `TCM_INSERTITEM` *before* adding to the
managed list. Inside either native call comctl32 may repaint synchronously, and with
`OwnerDrawFixed` that repaint reflects a `WM_DRAWITEM` for every native item - including the one
the managed list does not hold. Seen as `index ('1') must be less than '1'` from
`TabPages[e.Index]` in `DrawItem` while dragging a tab from second to first.

The follow-on failure is worse than the first. The `ThreadException` handler shows a modal
dialog, whose message loop delivers the pending `MouseUp` while the reorder is half done;
`SelectedTab` then indexes the managed list with the native selection and throws
`Index was out of range` from inside `get_SelectedTab`. **Nothing that runs during a
`TabPages` mutation may pump messages.**

`DrawItem` therefore ignores any index outside `TabPages.Count`; the native control repaints
once the counts agree. `WM_SETREDRAW` around the reorder would suppress the repaint instead,
but it clears `WS_VISIBLE` on the strip and everything under it, and what the hosted RDP
control makes of that is unmeasured - see [ADR-0005](adr/0005-park-rdp-controls-before-disposing.md).


---

## RDP session teardown

See [ADR-0005](adr/0005-park-rdp-controls-before-disposing.md) for the design this produced.

- **Disposing a control whose session is still up blocks.** `Connected == 0` at close: 44–73 ms,
  ten for ten. `Connected == 1`: 72–124 ms four times and **47.5 s** on the fifth.
- **`RequestClose()` does not separate the two cases.** It answers `controlCloseWaitForEvents`
  and the control raises `OnConfirmClose` ("go ahead"), which is not a disconnect. **Wait for
  `Connected == 0`, not for the event.**
- **A live RDP control still undisposed at process exit crashes the process.**
- **So does one that has merely been unparented.** Dispose it with its parent chain intact and
  exit is clean every time.
- **A wedged UI thread takes the whole desktop with it.** With `RedirectKeys` on,
  `SetRedirection` sets `KeyboardHookMode = 1`, so `mstscax` installs a low-level keyboard hook.
  Windows calls low-level hooks on the installing thread, so while that thread is not pumping,
  keyboard input serialises behind it **system-wide**.
- **`HostedControlCanBeDisposed` overrides must combine with `base`.** Replace rather than
  combine and the shutdown check silently stops applying.
- **`MessageCollector` runs its writers on the calling thread**, so code reporting a stuck UI
  thread cannot use it. See [ADR-0017](adr/0017-one-filtered-writer-for-every-log-line.md).
- **`AxMsRdpClient12` is not available on every Windows build** —
  `CLASS_E_CLASSNOTAVAILABLE`, and falling back to v11 is expected, not a fault.

---

## What an RDP session costs, and what a closed one gives back

Measured on this machine by instantiating `MsTscAx.MsTscAx.11` directly and reading properties
back, so none of it needed a reachable host.

- **An unconnected control costs ~0.3 MB.** Eight instances added 1.4 MB of private bytes
  between them. So a session's memory is not the `AxHost`, the interop wrappers or the managed
  heap — it is allocated natively at connect, by `mstscax`, and nothing on the managed side is
  proportional to it.
- **The bitmap caches are left at the control's defaults, and they are not small.** The fork
  sets none of these; the values below are what every session therefore gets:

  | Property | Default |
  |---|---|
  | `BitmapVirtualCacheSize` | 10 MB |
  | `BitmapVirtualCache16BppSize` | 20 MB |
  | `BitmapVirtualCache24BppSize` | 30 MB |
  | `BitmapVirtualCache32BppSize` | 40 MB |
  | `BitmapCacheSize` | 1500 KB |
  | `NumBitmapCaches` / `ScaleBitmapCachesByBPP` | 0 / 0 |
  | `BitmapPersistence` | 1 |
  | `ColorDepth` | 16 |

  Note the last two. `BitmapPersistence` defaults to **on** in the control and the fork turns it
  *off* (`RdpProtocol.SetProps`, from `CacheBitmaps`, default `False`) — but that property governs
  the cache persisted to disk, not the in-memory virtual cache above, which is never configured
  either way.
- **Session surfaces are sized in physical pixels since `PerMonitorV2`.** `SetResolution` takes
  `DesktopWidth`/`DesktopHeight` from `InterfaceControl.Size` on the default `FitToWindow`, and
  those are no longer virtualised to 96 DPI — see the DPI section above. A full-screen surface is
  15.8 MB at 3840×2160/16bpp and 31.6 MB at 32bpp, and `mstscax` holds several per session, so a
  4K panel at 150% costs roughly 2.25× what the same window cost while the process was
  DPI-unaware. That is the dominant term in the working set, and it is the price of ADR-0010 and
  ADR-0028 rather than a defect.

- **`AdvancedSettings2` … `AdvancedSettings8` are one COM object.** Fetched twice they are
  reference-equal, and every version returns the same `IUnknown`. The same holds for
  `SecuredSettings`/`SecuredSettings2` and for `TransportSettings`/`TransportSettings2`. Reading
  the pointers gives **four distinct objects in total**: those three plus the control. So the 43
  property-chain accesses in `RdpProtocol` produce three sub-interface RCWs, not forty-three and
  not eight. `IMsRdpExtendedSettings` is not among them — it is reached by casting the client, so
  it is the control's own RCW.
- **Releasing those three RCWs is safe, and the control survives it.** `FinalReleaseComObject`
  on each returned 0 references remaining; afterwards the control still read `ColorDepth`, still
  read *and wrote* `AdvancedSettings2.shutdownTimeout` through a freshly created RCW, and still
  released cleanly itself. `FinalReleaseComObject` hands back only the references the RCW itself
  took, so this is balanced rather than an over-release. This is what lets
  `RdpProtocol.ReleaseTheSessionsComReferences` exist: until those RCWs go, a contained settings
  object holds a reference on its container, so the native control and everything `mstscax`
  allocated for the session outlive `Control.Dispose()` and wait for a finalizer — and since
  nothing reports that native memory to the GC, an idle application may not run one for a long
  time. `_rdpClient` itself still must not be released: `GetOcx()` hands back the RCW the
  `AxHost` owns.

- **Server GC is not involved.** There is no `runtimeconfig.template.json`, no `app.config` and no
  GC property anywhere in the tree, so the process runs workstation GC with background GC on, as
  the SDK default for a `WinExe`. Nothing calls `GC.AddMemoryPressure`.

- **`ToolStripItemCollection.Clear()` disposes nothing, and neither does disposing an item reach
  its `Image`.** Worse for anyone testing it: **`ToolStripItem.IsDisposed` is only set when
  `Dispose` finds an `Owner`.** An item removed from its collection first and disposed afterwards
  is disposed, but reports `IsDisposed == false` for ever, which reads as a broken helper rather
  than a wrong order. Dispose while the item is still owned — that also removes it from the
  collection, so snapshot the collection first or the loop skips every other entry.

---

## Encryption

Connection files are AES-GCM through `System.Security.Cryptography.AesGcm`, at **ConfVersion 2.9**.
Anything older is refused by version in `ValidateConnectionFileVersion`, not read and partially
understood - see [ADR-0014](adr/0014-delete-rather-than-keep.md).

- **`AesGcm` accepts a 12-byte nonce and nothing else.** `AesGcm.NonceByteSizes` reports min 12,
  max 12, and a 16-byte nonce throws "the specified nonce is not a valid size for this
  algorithm". GCM itself allows any length, but a nonce that is not 96 bits has to be folded
  down with GHASH first, and the managed wrapper does not expose that. **`NonceBitSize` is
  therefore not a free parameter.**
- This is why the application carried `BCryptAesGcm`, 155 lines of `bcrypt.dll` P/Invoke, long
  after its stated reason (.NET Framework had no managed GCM) expired: bcrypt accepts any nonce
  length, and the file format used 16 bytes. Moving to the platform type meant changing the
  format, which is a one-way break, not a refactor.
- The envelope is **salt 16, nonce 12, ciphertext, tag 16**, base64-encoded.
  `AeadCryptographyProviderTests.EnvelopeIsSaltThenNonceThenCipherTextThenTag` asserts exactly
  that, so a change to either size fails there rather than in the field. Verified by
  reintroducing the defect: shortening the tag to 96 bits turns it red.
- **The writer and the reader have to agree on the version.** `XmlRootNodeSerializer` writes
  `ConfVersion` and `XmlConnectionsDeserializer.MinSupportedConfVersion` refuses anything below
  it. Writing a version this build will not read makes every file it saves unopenable, and only
  on the next start. `ConfVersionSerializedIsTheOnlySupportedVersion` pins the pair.
- **A decrypt failure on load is not always a wrong password.** `ConnectionsFileIsAuthentic`
  decrypts the `Protected` attribute and, when that throws, falls through to `Authenticate` and
  prompts. An envelope the build cannot parse therefore presents as a password prompt that
  cannot succeed - which is why the version gate exists and runs first.

---

## Versioning and assembly attributes

Measured on .NET 10 (SDK 10.0.401) while moving the version out of the tree and onto the git tags
— see [ADR-0030](adr/0030-derive-the-version-from-git-tags.md). Several of these are silent: the
wrong answer is a plausible-looking string, not an error.

- **The roaming `user.config` path is keyed on `AssemblyVersion`, and on nothing else.** Measured
  with a throwaway WinForms app carrying three deliberately different versions:

  ```
  AssemblyVersion        2.1.0.0
  FileVersion            2.1.0.123
  InformationalVersion   2.1.1-alpha.0.7+1a2b3c4d…9a0b

  ConfigurationManager.OpenExeConfiguration(PerUserRoaming).FilePath
      …\Roaming\<company>\<exe>_Url_<hash>\2.1.0.0\user.config      <- AssemblyVersion
  Application.LocalUserAppDataPath
      …\Local\<company>\<product>\2.1.1-alpha.0.7+1a2b3c4d…9a0b     <- informational version
  ```

  So holding `AssemblyVersion`'s fourth field at 0 is what keeps a user's settings in place
  across builds, and `Application.LocalUserAppDataPath` is a trap if the informational version
  moves — nothing in this application calls it.

- **`Application.ProductVersion` returns the informational version whole, `+` metadata
  included.** It is not trimmed at the `+`. A version of `2.1.1+g1a2b3c4d` therefore reaches any
  label bound to it complete with the commit hash.

- **`Application.ProductVersion` answers for the *entry* assembly, not the calling one.** Under
  the NUnit test host the entry assembly is `testhost.exe`, and the property returned `17.11.1` —
  the test SDK's version. Anything that wants *this* application's version has to read the
  attribute off its own assembly:
  `typeof(T).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()`.

- **The SDK appends `SourceRevisionId` to whatever `InformationalVersion` it is given.**
  SourceLink ships in the box and sets `SourceRevisionId` for any git working tree, so an
  informational version of `2.1.0.0+g64fe361` was emitted as
  `2.1.0.0+g64fe361.64fe36173c9924b6665ae0bd088b7ea92d0709cf` — the commit twice, once
  abbreviated and once not. `<IncludeSourceRevisionInInformationalVersion>false</…>` stops it.

- **`GenerateAssemblyInfo` is not all-or-nothing.** Turning it off to keep a hand-written
  `AssemblyInfo.cs` also throws away the version attributes, which is what forces the version to
  be written by hand. `Microsoft.NET.GenerateAssemblyInfo.targets` exposes one switch per
  attribute — `GenerateAssemblyVersionAttribute`, `GenerateAssemblyCompanyAttribute`,
  `GenerateNeutralResourcesLanguageAttribute` and so on — so generation can be narrowed to the
  three version attributes and everything else left to the file. That is what avoids CS0579
  without giving up the computed version.

- **MSBuild can do the arithmetic, but not with indexers.** `$(x.Split('.')[0])` and
  `Regex::Match(…).Groups[1].Value` are not valid MSBuild property functions.
  `[System.Text.RegularExpressions.Regex]::Replace($(v), '^v(\d+)\.(\d+)\.(\d+).*$', '$1')` is,
  one call per field, and `[MSBuild]::Add()` increments.

- **A property set inside a target is visible to every later target**, which is what lets a
  `BeforeTargets="Build"` target compute `$(FileVersion)` and an `AfterTargets="Build"` target use
  it to name a file. It is *not* visible at property-evaluation time, so it cannot feed
  `OutputName` or anything else read before any target runs.

---

## Installer

Windows Installer and WiX 5, measured on Windows 11 26200 while chasing an MSI that refused the
machine it was built on — see [ADR-0016](adr/0016-wix-5-and-the-desktop-runtime.md).

- **Inside `msiexec.exe`, `VersionNT` is 603 and `WindowsBuild` is 9600 on every Windows 10 and
  11 machine.** msiexec's compatibility manifest declares only the Windows 8.1 `supportedOS`
  GUID (`{1f676c76-80e1-4239-95bb-83d0f6d0da78}`), so the version APIs it calls are shimmed to
  6.3.9600, and a launch condition on those properties is evaluated against Windows 8.1 numbers.
  The real build is in `HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\CurrentBuildNumber`
  (`REG_SZ`), which a `RegistrySearch` reads unshimmed.
- **Opening the package from PowerShell does not test a condition.** The
  `WindowsInstaller.Installer` COM object runs the session in the calling process, and PowerShell
  is manifested for Windows 10, so `WindowsBuild` reads 26200 there and 9600 in msiexec. That is
  how the first Windows 11 floor was "verified" and shipped broken. Check conditions through
  msiexec: `msiexec /i package.msi /qn /l*v probe.log REQUIREDDOTNETMAJORVERSION=99` fails a
  launch condition on purpose (`REQUIREDDOTNETMAJORVERSION` is a public property the runtime
  check reads) and the log ends with every property value as msiexec saw it.
- **A property compared with an integer literal is compared numerically.** `"26200" >= 22000` is
  True; `"19045"`, `""` and `"abc"` are False. A registry value that is missing or malformed
  therefore fails a `>=` floor closed, not open.
- **An unelevated silent run stops at `Privileged`** before any later launch condition is
  evaluated, and a reduced-UI run (`/qr`) blocks on a modal message box whose log is only flushed
  when the client exits. The silent run is still enough: the property dump at the end of a `/l*v`
  log is written even when the run stops at the first condition.
- **ICE validation needs the Windows Installer service, which a build agent without
  administrative rights cannot open.** The build fails with
  `WIX0217: Error executing ICE action 'ICE78'`, quoting "The Windows Installer Service could not
  be accessed". That is not an ICE78 problem - ICE78 is simply the first ICE to run, so
  `-sice:ICE78` moves the failure to the next one and nothing narrower than all of validation
  helps. WiX 5 runs validation as its own MSBuild target, `WindowsInstallerValidation` in
  `wix.targets`, gated on `$(SuppressValidation)`; measured, `-p:SuppressValidation=true` skips the
  target entirely on a full rebuild and leaves the MSI otherwise unchanged. CI passes that switch,
  so **a local `-c "Release Installer"` build is the only place ICE runs, and the check to run
  before tagging a release.** `wix msi validate` exists as a standalone command (`-ice`, `-sice`,
  `-cub`) if the check is ever wanted as a step of its own.
- **Windows Installer ignores the fourth version field when comparing packages.** Two builds
  that differ only there are the same version to it, so with `ProductCode="*"` the newer one
  installs *alongside* the older instead of upgrading it: two entries in Programs and
  Features. `MajorUpgrade AllowSameVersionUpgrades="yes"` fixes it by setting
  `msidbUpgradeAttributesVersionMaxInclusive`; verified by reading the built package's
  `Upgrade` table, which then holds `VersionMax 2.1.0.0`, `Attributes 513`
  (`MigrateFeatures | VersionMaxInclusive`). It raises ICE61, which `<SuppressIces>ICE61</…>`
  silences by name without disabling the rest of validation. The cost is that downgrade
  protection stops applying between two builds of the same `major.minor.patch`.
- **The installer only builds under the `Release Installer` solution configuration:**
  `dotnet build mRemoteUG.slnx -c "Release Installer"`. Plain `Release` skips the project.
- **Corrected 2026-10-01: a lone `.wixproj` build does work, given `-p:SolutionDir=`.** This
  entry used to end "and building `Installer.wixproj` directly fails with WIX0150 because
  `$(var.SolutionDir)` is only defined by the solution build". True, and the mechanism is worth
  having, because it is a sentinel rather than an empty string.
  `Microsoft.Common.CurrentVersion.targets:347` assigns `SolutionDir` the literal value
  `*Undefined*` when nothing else has set it, at the *end* of evaluation, and WiX guards on
  exactly that sentinel - `wix.targets:691`:
  `<SolutionDefineConstants Condition=" '$(SolutionDir)'!='*Undefined*' ">$(SolutionDefineConstants);SolutionDir=$(SolutionDir)</SolutionDefineConstants>`.
  In a project build the condition is false, so the preprocessor is never given `SolutionDir` at
  all and `$(var.SolutionDir)` is genuinely undefined. `wix.targets:601` concatenates the four
  constant groups rather than replacing them, so the wixproj's own
  `<DefineConstants>HarvestPath=…</DefineConstants>` was never the cause.
  **There is a second breakage in the same place, and it is silent.** The wixproj body is
  evaluated *before* that `*Undefined*` assignment runs, so `$(SolutionDir)` is still empty
  there and `HarvestPath` comes out as the relative `src\mRemoteUG\bin\Release` - which resolves
  under `src\mRemoteUG.Installer\` and harvests nothing. Both are fixed by passing it as a global
  property, which no `<PropertyGroup>` can override and nothing here declares
  `TreatAsLocalProperty` for. Measured by evaluation alone, `-getProperty` with no targets run:
  `SolutionDir` `*Undefined*` and `HarvestPath` relative without the switch, both absolute with
  it. Measured by building: `dotnet build src\mRemoteUG.Installer\mRemoteUG.Installer.wixproj
  -c Release -p:Platform=x64 -p:SolutionDir=<repo>\ -p:BuildProjectReferences=false` produces
  the package, correctly named, without rebuilding the application. The trailing backslash is
  load-bearing - every `.wxs` concatenates it straight onto `src\`. `Configuration` needs no
  help; it comes from `ProjectDefineConstants` at `wix.targets:672`. CI relies on all of this to
  sign the payload between the two builds ([ADR-0032](adr/0032-sign-on-the-signing-server.md)).
- **`-p:BuildProjectReferences=false` is the load-bearing switch, and not for the reason it
  looks like.** Nothing WiX reads comes from project-reference metadata - the `.CA.dll` is a
  literal path in `mRemoteUG.wxs` and the harvest is a directory glob - so it is not needed to
  make the preprocessor work. It is needed because the wixproj's `ProjectReference` to
  `mRemoteUG.csproj` would otherwise rebuild the application and put the unsigned payload back.
  With it, only `GetTargetPath` runs on the referenced projects, and that target has no
  dependencies at all: `GetTargetPathDependsOn` is never assigned anywhere in the SDK. WiX still
  gets `@(_WixResolvedProjectReference)` from it, because `wix.targets:121-128` sets
  `OutputItemType` and the `<Output>` that honours it carries no `ReferenceOutputAssembly`
  condition.
- **The same switch does not work on the solution.** `-c "Release Installer"
  -p:BuildProjectReferences=false` rebuilds the application anyway: a solution build feeds its
  projects to an explicit `<MSBuild>` task in a generated metaproject rather than through
  `ResolveProjectReferences`, and that path the property does not reach. The project-scoped
  target `-t:'Installer\mRemoteUG_Installer:Build'` does exist (`.` becomes `_`, and the solution
  folder becomes an `Installer\` prefix) but has the same problem by a different route: the
  slnx `<BuildDependency>` builds `mRemoteUG.Installer.CustomActions` first, through an
  `<MSBuild>` task the property also cannot reach.
- **`bin\Release\en-US\*.msi` is a hard link to the package in `obj`.** `wix.targets:883`
  turns `CreateHardLinksForCopyFilesToOutputDirectoryIfPossible` on by default, and
  `fsutil hardlink list` on the intermediate package lists both paths. So an **in-place** MSI
  signer mutates the intermediate as well; a signer that writes a staged copy and moves it over
  the destination breaks the link and leaves `obj` alone. CI does the latter.

---

## Code signing

Measured on 2026-10-01 while wiring the Forgejo build to the signing service
([ADR-0032](adr/0032-sign-on-the-signing-server.md)). The signing service itself is available only
to CI, so these were established against a stand-in signer driven by a
throwaway self-signed certificate - which is enough for all three, because none of them is about
the certificate.

- **A build that runs after signing silently unsigns the output.** MSBuild's `Copy` task skips
  only files that match the source in *both* size and last-write time, and an Authenticode
  signature changes both, so `CopyFilesToOutputDirectory` copies `obj\Release\mRemoteUG.dll` over
  the signed `bin\Release\mRemoteUG.dll` again on the next build. Measured by appending 22 bytes
  to each file and rebuilding `-c Release`: `mRemoteUG.dll` (1055254 -> 1055232),
  `Interop.MSTSCLib.dll`, `AxInterop.MSTSCLib.dll` and
  `mRemoteUG.Installer.CustomActions.CA.dll` all lost the marker. **The apphost,
  `mRemoteUG.exe`, kept it**, which is why a half-signed output directory is a shape this can
  actually produce. Three of the five have no incrementality to hide behind at all:
  `CopyFilesToOutputDirectory` and `_CopyFilesMarkedCopyLocal` carry no `Inputs`/`Outputs`, so
  they run on every build and the `Copy` task decides per file. The apphost goes a different
  way - `_CreateAppHost` writes `obj\Release\apphost.exe` and a `PreserveNewest` item copies it
  through `_CopyOutOfDateSourceItemsToOutputDirectory`, which *is* incremental - so it survives
  only a build that recompiles nothing, and a build that recompiles the assembly clobbers it
  too. The custom action stub survives on a similar accident: `PackCustomAction` populates
  `@(AddModules)` inside its own body, so when it is up to date the copy that consumes it is
  conditioned out. Neither exception is worth relying on. `dotnet test` rebuilds
  `mRemoteUG.csproj` through its `ProjectReference`, so signing has to come after the last build
  that touches the application, not next to the build.
- **An Authenticode signature does not change the size of an MSI.** It grows a PE file - by 1432
  bytes with the test certificate - but a signed package came back byte-for-byte the same length
  as the one that went in, 2068480 both ways, because the signature goes into a stream inside the
  package's existing structured storage. Any check of the form "a signature only makes a file
  larger" therefore passes for every DLL and fails for the package; compare hashes instead.
- **`Get-AuthenticodeSignature` reports `UnknownError` for a signature whose issuer the checking
  machine does not trust**, with `StatusMessage` "A certificate chain processed, but terminated in
  a root certificate which is not trusted by the trust provider". The file is signed and the
  signer certificate is readable; only the chain is the problem, and the problem is the machine's.
  So a CI check must pass on a *present* signature and fail on `NotSigned` or `HashMismatch`;
  requiring `Valid` fails on any runner whose trust store lacks the issuing CA.
- **What the package carries is whatever is on disk when the wixproj compiles.** Verified by
  reading the built MSI's `File` table through `WindowsInstaller.Installer`: after signing the
  payload, the recorded `FileSize` for `mRemoteUG.exe` read 175000 against 173568 unsigned, and
  `mRemoteUG.dll` 1056664 against 1055232, while `Serilog.dll` - deliberately left alone - still
  read 163840. Reading the `File` table is the cheap way to ask what went into a package without
  extracting the cab; `msiexec /a` for an administrative install produced nothing on this machine.

---

## The self-test exit code is not the verdict

**`--selftest` exits non-zero on a run in which every check passed.** `SelfTest.Run` returns 0
or 1 from the failure count, but the process then dies during shutdown, *after* the report has
been written whole by `SelfTest.Emit`. It is intermittent — three consecutive runs on the
development machine exited 0xC000041D, then 0, then 0 — but when it does happen it is always the
same value, on the Forgejo runner and on the development machine alike:

```
Self-test exit code: -1073740771  =  0xC000041D  =  STATUS_UNHANDLED_EXCEPTION
```

That is Windows terminating the process on a managed exception with no handler, not the
self-test returning a verdict. The primary exception is never logged — nothing reaches
`mRemoteUG.log` after the `RESULT:` line. The next subsection takes that apart: the exception in
the log is the *second* one, the handler that hid the first has been fixed, and the first is still
unidentified. Either way the report is what is trustworthy.

**So grade the `RESULT:` line, not the exit code.** `Tools\run-selftest.ps1` does, and CI calls
it. Three things it has to get right, and only the first is obvious:

- A run that reports `RESULT: PASS` and then exits non-zero is a pass, with a warning. A run that
  reports `RESULT: FAIL` is a failure whatever it exits.
- **A missing `RESULT:` line is a failure.** The report is one `File.WriteAllText` at the end of
  the run, so its RESULT line existing *is* the evidence that the run reached the end. A report
  that stops mid-way means the process died during the checks, which is a real regression and the
  one thing the old exit-code test would have caught.
- **The previous report has to be deleted before the run.** The path is fixed
  (`%LOCALAPPDATA%\mRemoteUG\mRemoteUG-selftest.log`) and the self-hosted runner is not clean between
  jobs, so a run that dies early otherwise gets graded against whatever passed last week.

Before this, the CI step both branched on the exit code and carried `continue-on-error: true`,
with a throw message telling the reader to ignore the value it had just branched on. It was
permanently red and therefore ignored, which is the failure mode worth avoiding: a verification
tier nobody reads is weaker than none.

### What kills the process, and what is still unknown

Two exceptions, and only the second one was ever visible. The Windows event log has the whole
picture that `mRemoteUG.log` did not (`.NET Runtime`, event 1026):

```
System.ComponentModel.Win32Exception (1406): Error creating window handle.
   at System.Windows.Forms.NativeWindow.CreateHandle(CreateParams cp)
   ...
   at System.Windows.Forms.PictureBox.OnVisibleChanged(EventArgs e)
   ...
   at System.Windows.Forms.Form.ShowDialog(IWin32Window owner)
   at System.Windows.Forms.Application.ThreadContext.OnThreadException(Exception ex)
   at System.Windows.Forms.NativeWindow.Callback(HWND, UInt32, WPARAM, LPARAM)
```

Read it from the bottom. Something throws inside a **window procedure** — `NativeWindow.Callback`
is the last frame, and WinForms catches anything raised there and hands it to
`Application.ThreadContext.OnThreadException`. That is the **primary** exception, and nothing
records it. `OnThreadException` then shows `ThreadExceptionDialog`, whose error-icon `PictureBox`
cannot get a window handle during teardown; 1406 is `ERROR_TLW_WITH_WSCHILD`. *That* exception —
the **secondary**, about a dialog nobody asked for — escapes, and the process dies at
`0xC000041D`.

**The secondary is a defect of ours, and it is fixed.** `CrashLogger.Install` used to subscribe
`Application.ThreadException` only on the interactive path, reasoning that a modal dialog on a
non-interactive run never gets answered. That reasoning inverts: leaving the handler off does not
avoid a dialog, it hands the dialog to WinForms. Subscribing is what *suppresses* the built-in
dialog. So the handler now goes on unconditionally, and the non-interactive path logs the
exception, counts it, and lets the run continue. `--selftest` grew a last check, `UI thread`,
that fails the run if the count is non-zero — an exception inside a window procedure never
reaches the `Check` whose code provoked it, so without that check a run could raise one and still
report `RESULT: PASS`.

**The primary is still unknown**, and this is the honest state of it:

- It is thrown inside a window procedure, after `SelfTest.Run` has returned and written the
  report. The likely window is CLR shutdown on an STA thread, which pumps messages while
  releasing COM objects — the RDP ActiveX control among them — so a queued message can reach a
  control that is already disposed. That is a hypothesis, not a measurement.
- **The reproduction was lost before it could be caught.** It ran at **12 crashes in 30 runs**
  between 08:23 and 08:25 on 2026-09-28, then stopped: 0/30 on a pristine binary, 0/55 with
  in-process instrumentation, 0/30 under six concurrent processes, 0/30 after the fix — about 145
  runs with no recurrence. Instrumentation is *not* proven to be what perturbed it, because the
  pristine control is equally silent. Something environmental closed the window and this does not
  yet know what.
- **WER has no dump to read.** `HKLM\...\Windows Error Reporting\LocalDumps` is not configured, so
  the 26 archives under `ReportArchive` hold only `Report.wer` — fault parameters that say no more
  than event 1026 does. Configure `LocalDumps` *before* hunting it again; a minidump would carry
  the primary exception object that the log never sees.
- After the fix, a recurrence documents itself: the primary lands in `mRemoteUG.log` with its
  stack, the run reports `FAIL` if it happened during the checks, and `Tools\run-selftest.ps1`
  warns off the log if it happened after them.

A loop worth keeping for the next attempt: run `--selftest` N times, deleting the report each
time, and assert the pair *report says PASS* **and** *process exited non-zero*. Nothing else
distinguishes this crash from an ordinary failure.

## Tests that pass for the wrong reason

Three tests in the HiDPI and font work passed against deliberately broken code before being
fixed. The pattern is always the same — **asserting something that is true for the wrong
reason**:

- Asserting a control's font *family* matches the system font passes for "Segoe UI 8.25pt",
  because the family is the system family and only the size differed.
- Asserting a derived font's size tracks the window's passes when the derivation is **deleted**,
  because the control then simply inherits. Assert the *difference* — ratio, style, family.
- A fixture that walks containers never reaches a `ToolStrip`, which is not a `ContainerControl`,
  whose only host is an excluded form.

**Always reintroduce the defect and watch the test fail before trusting it.** The process failure
in the nested-submenu episode above was shipping a fix on a plausible mechanism without a red
loop — the instrumentation round was right; acting on it without a failing test was not.

Two traps when checking a hand-laid-out window:

- **`Control.Visible` returns false for every child of a form that has never been `Show()`n**,
  whatever its own setting — so an unshown form measures as having no controls and every check
  passes vacuously. Show it off-screen at `Opacity = 0`.
- **`Rectangle.Intersect(a, b).IsEmpty` is not an overlap test.** `IsEmpty` is true only when x,
  y, width and height are *all* zero, so controls whose edges merely touch (stacked radio
  buttons) read as overlapping. Test `shared.Width > 0 && shared.Height > 0`.

Two things that cannot be tested directly, and what stands in:

- **`FrmMain` cannot be constructed in a test at all**, so the DPI sweep lives in
  `DpiScaling.FollowDpiChange` and is tested on a stand-in. `DpiChangeTests` drives
  `RescaleConstantsForDpi` by reflection, which is the only way to make a DPI change happen
  without a second monitor and a hand.
- **A font assigned after construction does not re-run the auto-scale pass**, because
  `CurrentAutoScaleDimensions` is already cached — the factor comes out 1.0 and only auto-sizing
  controls move. Testing a larger system font therefore requires `Application.SetDefaultFont`
  before any control exists, which is what `--selftest --largefont` does.

---

## Publishing to GitHub

Measured on 2026-10-07 while building the release publish
([ADR-0035](adr/0035-publish-releases-to-github-as-snapshots.md)), against local repositories:
nothing here touched GitHub.

- **`git archive` into a clone and `git add -A` reproduces every blob exactly.** The archive
  applies `.gitattributes` on the way out, so with `* text=auto eol=crlf` every text file arrives
  in the target's work tree with CRLF; the same `.gitattributes`, published alongside, normalizes
  them back on `git add`. Published into a bare repository from the tagged tree, all 1193 files
  came out byte-identical to their source blobs - `git ls-tree` hashes compared one for one. The
  round trip depends on `.gitattributes` being in the allowlist; leave it out and the published
  blobs carry CRLF.
- **That same rule would break the publish scripts on Linux.** `* text=auto eol=crlf` checks
  every text file out with CRLF on every platform, a Linux runner included, and bash reads the
  CR as part of each line. `*.sh text eol=lf` pins the scripts. The configuration files the
  scripts read (`include`, `denylist`, `protect`) stay CRLF and have the CR stripped on read.
  Inferred from how git applies `eol`, not yet observed on the runner - the first publish run
  executes `test-sync.sh` there before anything else.
- **`git archive <commit> -- <path>` fails on a path that matches nothing** - `pathspec ... did
  not match any files`, non-zero - so a stale allowlist entry stops the publish instead of
  publishing less. Inside a pipeline the error only surfaces with `pipefail`.
- **The private-address patterns do not match version strings.** `\b10\.[0-9]{1,3}\.[0-9]{1,3}\.
  [0-9]{1,3}\b` passes `10.0.22000.0` and `net10.0-windows10.0.22000.0`, which appear throughout
  the tree: the third field is five digits, and `\b` stops a partial match inside `windows10`.
  The same patterns behave identically under `grep -iE` and JavaScript `RegExp(..., 'i')`, which
  is why one file serves both scripts.
- **A descending range in PowerShell is not empty.** `$lines[5..4]` returns two elements, in
  reverse. The draft step's changelog extraction guards on it, for a section with nothing under
  its header.
- **The runner sets `NODE_OPTIONS=--use-system-ca`, and Node 20 will not start with it.**
  Observed on the first dispatch of `publish-github.yml`, 2026-10-07: `node: --use-system-ca is
  not allowed in NODE_OPTIONS`, exit 9, before any of the job's own code ran. The flag makes Node
  trust the system certificate store, which is how it accepts the instance's certificate, so the
  answer is a Node that knows it - 22.15 or 23.8 onwards, by Node's changelog - rather than
  clearing it. The job image is `node:22-bookworm`.
- **Behind a proxy, `fetch` needs `NODE_USE_ENV_PROXY=1`.** The second dispatch failed its first
  call to GitHub with a bare `fetch failed`; git in the same job honours `HTTPS_PROXY` without
  being told, `fetch` does not. With the variable set, Node logs `[UNDICI-EHPA] Warning:
  EnvHttpProxyAgent is experimental` and the call goes through - observed 2026-10-07.
- **A release asset's `browser_download_url` answers the job token with 404.** That URL is the
  web path `/{owner}/{repo}/releases/download/{tag}/{name}`; for the `v2.1.0` draft it
  returned 404 to a request carrying `Authorization: token`, observed 2026-10-07. Whether that
  is the path refusing the token header (a private repository answers 404 rather than 401) or
  the path not serving drafts was not separated. `release.mjs` downloads from
  `/attachments/{uuid}` instead, which every asset in the API response carries; that this path
  takes the token is, until the next run, an inference.
- **A manual run reads the workflow from the branch, and a checkout of the tag brings that
  tag's scripts.** Observed 2026-10-07: three fixes to `release.mjs` merged after `v2.1.0` was
  tagged had no effect on dispatches of `publish-github.yml`, while fixes in the workflow file
  itself did - the job checked out `refs/tags/v2.1.0` and ran the scripts from there. The job now
  checks out twice: `tooling` at the workflow's own commit for the scripts, `source` at the tag
  for what is published and the configuration that decides it. On a release event the two are
  the same commit, so a tag is published by the tooling it was tagged with.

Not measured - from GitHub's documentation, recorded because it fails the push rather than
warning: **a token without the Workflows permission cannot push a change under
`.github/workflows/`.** The overlay publishes `.github/` without a workflow in it, so Contents
alone is enough today; adding a GitHub Actions workflow to the overlay needs Workflows: read and
write on the token as well.

Not measured, and only a real release can show it: that publishing a Forgejo draft fires
`release: published`; that the job token may create a release, attach to it, and download the
attachment through `browser_download_url` (a private instance answers an unauthorised attachment
request with its sign-in page and a 200, which `release.mjs` refuses by content type); and that
GitHub accepts the token as an `http.extraheader` for the push.
