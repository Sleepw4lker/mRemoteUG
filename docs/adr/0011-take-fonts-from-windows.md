---
date: 2026-09-22
---

# Take fonts from Windows, and leave AutoScaleDimensions alone

`44321a4` removed every hardcoded font family and absolute point size from the application.
`Control.DefaultFont` *is* `SystemFonts.MessageBoxFont` (Segoe UI 9pt on a default install), so
a control that sets no font already follows the user's setting; most of the change was deleting
`Segoe UI, 8.25pt` declarations. The ten fonts that genuinely differ are **derived from the
window's own font** — ratios for headings, `FontFamily.GenericMonospace` for the credits and
changelog, italic for a notification date — and rebuilt in `OnFontChanged`, because explicitly
setting a font opts that control out of inheritance.

The decision worth recording is the one that looks like an oversight:

**`AutoScaleDimensions` were deliberately not touched.** Thirteen forms declare
`AutoScaleDimensions = 6F, 13F` while running at 9pt, which reads like a bug. It is not. **The
declared value is the font the layout was *authored* against, not the runtime font.** Those
forms have been scaled ×1.167/×1.154 ever since .NET Core changed the default font, and that
scaled result is what ships and looks right. "Correcting" them to `7F, 15F` shrinks them about
15% into clipped labels.

Two forms *were* corrected in the other direction (`AppearancePage`, `ExportForm` — `19ed669`,
`78edbc0`), and that is not a contradiction: they declared `7F, 15F` over 6×13 *literals*, so
they never scaled at all, and their `AutoSize` controls grew into the whitespace their unscaled
`Location`s reserved — `ExportForm` had six genuinely overlapping controls. The rule is
therefore: **identify the authoring font from control metrics, not from the declaration.**
6×13 ships Button 75×23, Label 13, CheckBox/RadioButton 17, ComboBox 21; 7×15 ships Button
88×27, Label 15, ComboBox 23. Only rewrite a baseline that disagrees with its own literals.

Consequences that constrain future work:

- **`AutoScaleDimensions` cannot be read back after construction** — `PerformAutoScale`
  overwrites it with `CurrentAutoScaleDimensions`, so every constructed form reports a factor of
  exactly 1.000 whatever its designer said. Only "declared nothing" (0×0) stays detectable.
- **`AutoScaleMode` must not be set in a base class constructor.** Assigning it caches
  `CurrentAutoScaleDimensions` before the derived `InitializeComponent` has set its font, so the
  window is scaled against a font it does not use. Doing this in `BaseWindow` made the four
  Segoe UI 8.25pt windows 5% too tall. `BaseWindow` therefore declares nothing and is excluded
  from the checks.
- **`--selftest --largefont`** exists because of this: it calls `Application.SetDefaultFont`
  before any control exists, which is the *only* way to test a larger system font. A font
  assigned after construction does not re-run the auto-scale pass, so the factor comes out 1.0
  and only auto-sizing controls move. Known finding from it: `AboutWindow` and `ConfigWindow`
  hit the screen edge at 13.5pt rather than scaling.

The anisotropy behind all of this — WinForms scales controls by the *font* metric while
`LogicalToDeviceUnits` scales by the *DPI* — and the measured baselines are in
[docs/platform-findings.md](../platform-findings.md). It is not theoretical; it is what broke
the message box and led to [ADR-0012](0012-task-dialog-on-comctl32.md).
