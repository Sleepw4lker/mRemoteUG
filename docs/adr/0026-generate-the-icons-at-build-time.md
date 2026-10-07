---
date: 2026-09-25
---

# Replace Silk with Fluent, rasterized at build time and committed

Every glyph this application shipped was a **single-frame 16×16 raster from the famfamfam Silk
set**, drawn in 2005-2008. Above 100% Windows stretched it, and in dark mode it was still
light-theme artwork. HiDPI and dark mode are two of the three reasons this fork exists
([ADR-0001](0001-fork-to-do-one-thing.md)), and the artwork was the last part that had not
followed. Four separate source comments said so in place and said they were waiting for larger
artwork.

The replacement is **Fluent UI System Icons**: MIT, so it can be bundled and needs only its notice
retained, and the family Windows 11 itself uses, so the application reads as native. Not the Segoe
Fluent Icons font that ships with Windows — that licence does not allow redistribution.

## Rasterized at build time, not rendered at run time

Fluent ships SVG and WinForms wants bitmaps, so something has to rasterize. The decision is that
it happens **once, in a tool, and the PNGs are committed** — 685 files across 90 names.

The alternative was a runtime SVG renderer, either a NuGet one or a path parser written here. Both
were rejected for the same reason, and it is not the obvious one: they are not meaningfully better.
Fluent hand-tunes its geometry per size, and it ships the sizes that a 16px logical glyph actually
becomes on the Windows DPI ladder. Rendering a vector at 24px gets the 24px *drawing*; picking the
16px frame's geometry and rendering it at 24 does not. So a committed ladder of pre-rendered frames
is the **same artwork or better**, with no rasterizer in the shipping assembly, no startup cost,
and files a human can look at in a diff.

What it costs is that generated output can drift from its source. `Tools/verify-icons.ps1` is the
answer to that, and it is deliberately not an NUnit test: the test project would have to take a
reference on the rasterizer and on the Fluent packages to run it, which is the dependency the whole
arrangement exists to avoid.

## Why the tool may have dependencies when the application may not

`Tools/IconGen` references SkiaSharp and the Fluent icon packages.
[ADR-0015](0015-purge-third-party-dependencies.md) puts the one-`PackageReference` invariant on
`src/mRemoteUG/mRemoteUG.csproj`, and argues it from maintenance cost — *a dependency that has to
be evaluated, updated or replaced at each framework bump*. IconGen is outside `mRemoteUG.slnx` and
never ships. Everything it produces is committed, so a framework bump cannot break anything that
runs. It is the same reasoning as the committed COM interop in
[ADR-0004](0004-commit-the-mstsc-interop.md): a thing in the tree specifically so that nothing has
to be resolved at build time.

## The art comes from NuGet, because GitHub is not reachable

The obvious route was vendoring SVGs from `github.com/microsoft/fluentui-system-icons`. This
development environment has no route to github.com. It **can** reach nuget.org, which was worth establishing rather
than assuming, because Microsoft publishes the same geometry as
`Microsoft.FluentUI.AspNetCore.Components.Icons` — one generated type per icon per size, with the
markup on a `Content` property. Versioned, restorable, and offline of GitHub, which
[ADR-0013](0013-selftest-as-the-verification-mechanism.md) says everything here has to be.

## Only paths are rendered

Measured across all 20,392 Regular and Filled icons in the package: **99.2% are a single
`<path d="…">`**, and the entire attribute vocabulary is `d` (20,950 occurrences), `fill` (238),
`clip-path` (5), `opacity` (3) and `stroke` (1). So `SKPath.ParseSvgPathData` is the whole job and
no SVG DOM is needed. The handful that carry paint of their own are **rejected by name at
generation time** rather than flattened into a mask that silently loses it.

Two details that are not obvious and cost a render each to find: Skia defaults to winding fill and
Fluent's geometry needs **even-odd** to punch its holes, and a badge has to be generated *inset
into its corner* rather than full-bleed, or compositing it onto a node icon covers the icon it is
meant to annotate.

## The ladder is eight sizes

16, 20, 24, 28, 32, 40, 48, 64 — what `DpiScaling.Scale(16, dpi)` produces at 100%, 125%, 150%,
175%, 200%, 250%, 300% and 400%. A request snaps **up**.

An earlier six-size ladder (16/20/24/28/32/48) came from the sizes Fluent publishes, and it is
wrong: Windows offers 225%, 250%, 350% and 400% on real hardware, which want 36, 40, 56 and 64
device pixels. 36 snapped to 48 and 56 fell off the top.

Upstream coverage is uneven and this is the fact the generator is shaped around: **only 374 of
3,082 Regular names carry all of 16/20/24/28/32/48.** A size with no geometry of its own is
rendered from the nearest below it. That is still crisp — the source is vector and is rendered at
the exact target size — and what is lost is the hand-tuning, not the sharpness. Every fallback is
printed rather than applied silently. Four remain at 16, 20 or 24, each because Fluent genuinely
has nothing closer, and each is recorded in the manifest's `note` field next to the choice.

## The manifest is the vocabulary

`src/mRemoteUG/Resources/icon-manifest.json` is the only list of what artwork exists. It is
hand-edited; everything else is generated from it, including `Resources.g.cs` — which keeps the
same class, namespace and member names the resx generator produced, so all ~110 `Resources.Foo`
call sites compile untouched, including six in `.Designer.cs` files that must not be opened in the
designer to be edited. A name dropped from the manifest is now a **compile error at every call
site** rather than a runtime null.

`Properties/Resources.resx` is gone. It held 68 `ResXFileRef` links and nothing else, and
one-name-one-file cannot express a size ladder.

## What was dropped, and why that is not data loss

Following this project's habit of recording the explicit "no"s
([ADR-0014](0014-delete-rather-than-keep.md)), the connection icon set was curated from 30 names to
24. **Fax, Tel, SharePoint, ESX, Backup, Log, Build Server, Test Server, Finance, Telnet,
Workstation, Terminal Server** and **mRemote** went; Laptop, Server, Container, Cloud, Storage,
Printer, Phone and Desktop arrived.

Those names are in users' connections files. **14 legacy names heal to their nearest replacement on
read**, and on read only — nothing rewrites the file behind the user. The table is generated from
the manifest so it cannot drift from the artwork, and it is an explicit table rather than a
fallback, because an unknown name must still resolve to nothing: an icon picker that answers every
string would hide a dropped csproj glob completely, which is precisely what `--selftest` exists to
catch.

One constraint worth stating because the workaround is tempting: **Fluent contains no Microsoft
brand marks by design.** The "Windows" connection icon is a generic window. Do not source a logo
from anywhere else to fill that gap — it reintroduces exactly the non-MIT asset this record is
about removing.

## Attribution

The Silk set was CC-BY 2.5 and required attribution; Fluent is MIT and requires the copyright *and
permission notice* be retained, which is more than the licence's name. The full MIT text is in
`CREDITS.TXT`, and `AboutTextTests` asserts the running program still names it — the same test that
made forgetting the Silk attribution impossible.

The ordering was a licence constraint rather than a preference: the MIT notice went in **before**
the first byte of Fluent-derived artwork, and the CC-BY notice came out **with** the last Silk file.

**Amended 2026-09-26: the brand mark is gone too, and nothing replaces it.**

This record said `mRemote_Icon.ico` was the one piece of artwork deliberately left alone. It
is no longer: the mRemoteNG logo has been removed from the executable, the main window, the
session windows, the tray, the installer dialogs, the shortcuts and Programs & Features, along
with the 34-file `Resources/Other Graphics` folder of upstream logos, source `.psd`s and the
HandelGothic fonts that the build never referenced.

It is **replaced by the defaults rather than by a new mark**, which is the part worth
recording because it looks like something half-finished:

- No `<ApplicationIcon>`. An executable with no icon resource is shown by Windows with its
  default application icon.
- No `Form.Icon` on the main or session windows. Measured before relying on it: a `Form` with
  nothing assigned still reports a real 32x32 icon, so this is a default rather than a blank
  title bar.
- `SystemIcons.Application` for the tray, which is the one place that cannot be left unset - a
  `NotifyIcon` with no icon shows nothing at all.
- No `WixUIBannerBmp` or `WixUIDialogBmp`. Those are *overrides*; unbound, WixToolset.UI.wixext
  supplies its own `bannrbmp.bmp` and `dlgbmp.bmp`, so the dialogs are stock WiX rather than
  empty. Verified in the built MSI.
- No `Icon` element, no `ARPPRODUCTICON`, no `Icon` attribute on either shortcut. MSI has no
  default icon to fall back to, so these are simply dropped: a non-advertised shortcut shows
  its target's icon, which is now the Windows default. The `Icon` table is absent from the
  built MSI entirely.

Shipping someone else's logo is a trademark question rather than a licence one, and it is the
same question as the "Windows" connection icon above: the answer is not to find a different
logo. `GlyphTests.NoUpstreamBrandMarkIsEmbedded` scans the manifest resource names so that
reintroducing it through any of the three globs fails the build rather than shipping.

**Proposed 2026-09-26, not yet accepted: the executable has an icon again, and it is generated.**

The amendment above says no `<ApplicationIcon>` and nothing replacing it. One half of that has
stopped being true, and the reason is worth the paragraph.

Removing the brand mark left the application showing **two different marks at once**. A `Form`
that assigns no `Icon` does not go blank - WinForms supplies its own default, and Windows 11 draws
it on the taskbar button. The executable, having no icon resource at all, fell back to the shell's
generic application icon instead. So the running window showed one thing and the file, its
shortcuts and its Programs and Features entry showed another.

`Resources/Icons/App_Icon.ico` is generated from the manifest like every other window icon, from
Fluent's `SquareMultiple` - the same two-overlapping-squares mark WinForms itself draws, so the
window and the file now agree without anything being assigned in code. It is **not** the
framework's own bitmap lifted out of `System.Windows.Forms.dll`: that is a Microsoft resource and
redistributing it is the same question as the Segoe Fluent Icons font this record already declines.

Two details that are decisions rather than defaults:

- **Accent blue, not a mask.** Every other window icon is `fixed` at `#0078D4` because Windows
  draws it on a caption this application does not paint. An application icon has it worse: it
  appears on a dark taskbar *and* on a white Explorer background, so a white mask would be
  invisible in half the places it shows up. Checked at every frame on both grounds.
- **`Form.Icon` stays unset.** It would be reasonable to assign the new icon to the windows as
  well, and it is deliberately not: WinForms already supplies the same mark, so assigning it would
  add a line of code that changes nothing visible and one more place to keep in step.

This is still not a brand. It is a placeholder that is consistent with itself, and the moment this
fork has a mark of its own it replaces one manifest row.
