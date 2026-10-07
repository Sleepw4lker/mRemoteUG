---
date: 2026-09-25
---

# Ship the glyphs colourless and tint them once, at load

Almost every modern icon set is monochrome by design, and colour is applied by the theme. That is
what makes one set work in light and dark mode, and it is the part of the move to Fluent
([ADR-0026](0026-generate-the-icons-at-build-time.md)) that needed a decision rather than a tool.

Tintable glyphs are generated **colourless**: white RGB with the shape carried entirely in the
alpha channel. `UI/Glyphs.cs` tints each one to `SystemColors.ControlText` the first time it is
asked for, and caches the result.

## Once is correct, and it is correct *because* of ADR-0009

`Application.SetColorMode` is called exactly once, from `ProgramRoot.ConfigureApplication`, and
never again while the process runs — [ADR-0009](0009-system-colour-theme-at-startup.md) records
that as a deliberate limitation and explains why a runtime switch is worse than a restart notice.

That is what makes a lazy, one-shot tint safe rather than sloppy. `SetColorMode` re-points the
whole `SystemColors` table before any control exists, so a tint taken on first use is guaranteed to
see the final value and can never be stale. A mid-session Windows theme change leaves tinted glyphs
behind — and leaves the title bar, the tree, the lists and the scrollbars behind too, which is
exactly the limitation ADR-0009 already accepts. **This adds no new failure mode.** If that record
is ever revisited, this one has to be revisited with it.

The alternative was shipping a light and a dark copy of every glyph. It doubles the asset count,
still cannot follow a live switch, and would have to be regenerated whenever the theme colours
move.

## `LockBits`, not `ColorMatrix`

The obvious way to recolour a bitmap in GDI+ is an `ImageAttributes` with a `ColorMatrix` through
`DrawImage`. That path applies its own colour adjustment, so the result is *close* to the requested
colour but not bit-exact.

That matters more than it sounds. A mask that comes back only approximately the tint colour cannot
be asserted on, and two assertions are what make this whole arrangement trustworthy: that a
tintable glyph **ships** colourless, and that it **comes back** in the theme foreground. `LockBits`
is exact, so `GlyphTests` can say both. It also keeps this codebase's property of having no
`ColorMatrix` or `ImageAttributes` use anywhere.

## One tint for two surfaces

Glyphs land on two backgrounds: `Control` for menus and toolbars, `Window` for the connection tree
and the notifications list. Their foregrounds are `ControlText` and `WindowText`, and the first
draft of this said those resolve to the same value on stock Windows themes.

**Measured, they do not.** In dark mode on Windows 11, `ControlText` is `FFFFFFFF` and `WindowText`
is `FFF0F0F0` — a gap of 15/255 on each channel. One tint is still the right call, because a gap
that size is not visible and a second tinted copy of every glyph is a real cost. But the claim was
wrong, and `--selftest` now prints both values and the size of the gap rather than a yes/no, so a
theme that ever opens it up properly shows up in a log instead of as a washed-out glyph on one
surface.

This is the kind of thing that is only ever found by printing it. The development environment cannot
test behaviour interactively ([ADR-0013](0013-selftest-as-the-verification-mechanism.md)), so the
instrumentation is the finding.

## What is not tinted

Two categories, and both would be actively wrong to tint:

- **Semantic glyphs.** An error is red, a warning is amber, an information notice is blue, a
  connected host is green. Tinting those to the foreground makes an error and a warning identical.
  They are listed in the manifest as `fixed` and carry their colour; the generator refuses a row
  that says `fixed` without a colour, or `mask` with one.
- **Window icons.** Windows draws these on a title bar this application does not paint, at the
  shell's scaling rather than the window's. A glyph tinted to `ControlText` would vanish into a
  light caption. They carry the accent blue, which is legible on both.

`ThemeTests` needs no new entry for any of this. Its allow-list is consulted for `BackColor` and
`ForeColor` on constructed controls; a colour inside a bitmap never reaches it. The equivalent
guard for artwork is `GlyphTests`, which asserts that `Glyphs.TintColor.IsSystemColor` — using
`IsSystemColor` rather than `IsKnownColor`, for the same reason ThemeTests gives: `Color.White` is
known and is not a system colour.
