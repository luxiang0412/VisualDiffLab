# Design notes — Quiet Precision

## Direction

The interface is deliberately quiet: neutral surfaces, a single accent color from the Windows accent setting, and no decorative glass, gradients or large card stacks. The image canvas is the visual center; the command bar and inspector support it.

- Native WinUI title bar and system window behavior.
- Fluent default control sizes, corner radii, spacing, focus visuals and theme resources.
- Accent color comes from the Windows personalization setting (`UISettings.GetColorValue(Accent)`) and is used for the active mode indicator, wipe split, selected region outline and selection handles.
- Difference colors are fixed product constants (`#E3008C`, `#00A2FF`, `#FFB900`) and are never used for unrelated UI states.

## Layout

```text
┌──────────────────────────────────────────────────────────────┐
│ native title bar                                             │
├──────────────────────────────────────────────────────────────┤
│ CommandBar: Open A · Open B · Swap │ 1 2 3 4 │ Fit 100%      │
│             Regions · Export · Inspector                     │
├───────────────────────────────────────────┬──────────────────┤
│ Canvas (empty state / images / overlays)  │ Inspector (336)  │
│                                           │ images           │
│                                           │ threshold/stats  │
│                                           │ ignore regions   │
│                                           │ display/theme    │
│                                           │ export           │
├───────────────────────────────────────────┴──────────────────┤
│ status: zoom · difference summary · status/spinner           │
└──────────────────────────────────────────────────────────────┘
```

- Design reference size: **1280 × 800** effective pixels; initial window is 1280 × 800 client at the current DPI.
- Minimum window: **1024 × 640** (enforced with `WM_GETMINMAXINFO`, DPI-scaled). Verified: a 1024 × 640 window at 200% DPI produced a 2048 × 1280 physical window, with all primary commands reachable and the inspector scrollable.
- `CommandBar` dynamic overflow prevents command clipping at narrow widths.
- Inspector can be collapsed from the command bar or its × button.
- Canvas is clipped to its viewport; selections and overlays never paint over the inspector.

## Empty and partial states

- No images: dashed drop zone, title `Drop two screenshots to compare`, supported formats, local-only statement, `Open image A` / `Open image B`.
- One image: the image is displayed and fitted; an `InfoBar` explains that the other side is needed. No error dialog.
- Comparison in progress: statistics show `Comparing…` and the status bar shows a spinner; export is disabled so old values cannot be misread as current.
- Invalid file: a `ContentDialog` with a plain-language cause and an `OK` action; the current comparison remains intact.

## Selection and non-color cues

- Ignore regions always show a numbered chip (`R1`, `R2`, …).
- The selected region gets a thicker outline, four corner handles and an inset dashed outline. Selection is therefore recognizable without relying on color.
- Wipe mode shows a visible split line plus a circular grab handle with opposing chevrons.
- Side-by-side shows fixed `A`/`B` viewport chips and outlines each image boundary; when sizes differ, the uncovered canvas area is outlined with the documented added/removed colors.
- Mode selection is indicated by the `SelectorBar` underline and text emphasis, not only color.

## Typography

- Segoe UI Variable Text / WinUI default text styles; no custom fonts.
- 12 px canvas chips for region numbers and A/B viewport labels.
- Known gap: canvas chip text uses a fixed Win2D font size and does not follow system text scaling. UI text outside the canvas does scale. Recorded in `DEFERRED.md` and `release-readiness.md`.

## Themes

- `Follow system`, `Light`, `Dark` in the inspector.
- Both themes were captured for empty state, side-by-side and heatmap. Canvas chip backgrounds stay dark with white text in both themes for readability; region strokes flip between near-black and white.
- `AppWindowTitleBar.PreferredTheme` is applied when supported so the native title bar follows the chosen theme.

## Language

- English and Simplified Chinese are available; the default follows the Windows preferred language.
- The Inspector exposes three radio options for language (`Follow system`, `English`, `中文`) and three for theme. Radio buttons are used instead of ComboBoxes because WinUI caches ComboBox selected text and did not refresh when only the item language changed.
- Language switches immediately and covers the command bar, inspector, statistics, status bar, dialogs, accessible names, secondary commands and export flyout. No restart is required.
- 171 localizable strings per language live in embedded JSON resources; `scripts/validate-localization.py` enforces key parity.
- Native file dialogs follow the Windows UI language, not the in-app setting. The app brand `Visual Diff Lab` and canvas chips (`A`, `B`, `R1`) stay language-neutral.
- The choice is session-only; there is no settings persistence in the MVP.

## Accessibility implemented

- `AutomationProperties.Name` on commands and inputs; stable `AutomationId`s used by UI tests.
- Commands are in a visual and keyboard order matching the workflow; `CommandBar` overflow presents the same commands.
- Keyboard: `Ctrl+O`, `Ctrl+E`, `1`–`4`, `F`, `0`, `Space+drag`, `Esc`, `Ctrl+Z`, `Delete`, Left/Right in Wipe mode.
- Default WinUI focus visuals are preserved; the canvas is a tab stop.
- Side-by-side, region and mode states have text/number/handle cues in addition to color.

## Not yet verified

- Narrator reading order and announcements.
- High-contrast mode rendering of custom Win2D overlays.
- Text scaling for canvas labels.
- Full keyboard-only walkthrough with focus screenshots.
