# Phase 2 report — workspace and base interaction

## Completed content

- WinUI 3 main window with native title bar, `CommandBar` (dynamic overflow), `SelectorBar` modes, collapsible Inspector (336 DIP), status bar.
- Empty state with drop target and `Open image A` / `Open image B`.
- File opening via `FileOpenPicker`, drag & drop (single, pair, replace prompt), swap, clear.
- A/B slot semantics; single-image state remains usable with an `InfoBar` hint instead of an error.
- Four modes: side-by-side, wipe (live split with handle and keyboard), overlay (0–100%), heatmap (base A + fixed diff colors).
- Shared zoom/pan; pointer-anchored wheel zoom; `Fit`; true `100%` at physical pixel ratio; double-click = Fit; Space+drag pan.
- Threshold slider 0–64 with 120 ms debounce, versioned/cancelled comparison, stale-result rejection and placeholder stats while busy.
- Statistics: different pixels, compared pixels, ratio (`N/A` when zero compared), ignored pixels, canvas coverage.
- Light / Dark / Follow system with title-bar theme follow where supported.
- Keyboard: Ctrl+O chooser, Ctrl+E export menu, 1–4 on focused canvas, F, 0, Esc, Ctrl+Z, Delete, arrows in Wipe.
- Error dialog for corrupt/unsupported files; no crash.
- Notifications: transient status messages are visible in the status bar.

## Modified files

- `src/VisualDiffLab.App/**`, `src/VisualDiffLab.Imaging/**`, `src/VisualDiffLab.Rendering/**`
- `tests/VisualDiffLab.Imaging.Tests/**`, `tests/VisualDiffLab.Rendering.Tests/**`, `tests/VisualDiffLab.UITests/**`
- `scripts/generate-test-images.py`, `scripts/capture-window.ps1`, `scripts/ui-drive.ps1`

## Commands

```powershell
dotnet build src/VisualDiffLab.App -c Release -p:Platform=x64 -r win-x64
$env:VDL_APP_CONFIG='Release'
dotnet test tests/VisualDiffLab.UITests -c Debug -p:Platform=x64 --filter "FullyQualifiedName~CoreWorkflow"
```

## Test results (final)

| Suite | Passed | Failed | Skipped |
|---|---:|---:|---:|
| Core | 44 | 0 | 0 |
| Imaging | 12 | 0 | 0 |
| Rendering | 15 | 0 | 0 |
| UI workflow | 1 | 0 | 0 |
| UI import / swap / clear | 1 | 0 | 0 |

The UI workflow test verifies: first statistics, swap/export enabled states, all four modes, threshold changes, rapid threshold settling, keyboard `2`/`4`, region creation/undo, 100% + pan stability, Wipe arrows, Esc, Dark/Light screenshots, 1024×640 min size and maximize/restore.

## Real run verification

- Actual window at 200% DPI: 2564×1660 physical (≈1282×830 DIP), 1920×1080 fixtures fitted at 44% side-by-side / 93% single-view.
- Initial statistics: **313,300 different / 2,073,600 compared / 15.11%**.
- Threshold 64: **242,384 different** (no region); returning to 16 restores 313,300.
- Wipe split at 50%, handle visible; Overlay at 65%; Heatmap shows magenta changed pixels over A. See:
  - `artifacts/acceptance/phase-2-empty-light.png`, `phase-2-empty-dark.png`
  - `phase-2-single-image-hint.png`
  - `phase-2-side-by-side-light.png`, `phase-2-side-by-side-dark.png`
  - `phase-2-wipe-light.png`, `phase-2-overlay-light.png`
  - `phase-2-heatmap-light.png`, `phase-2-heatmap-dark.png`
  - `phase-2-threshold-64.png`
  - `phase-2-error-corrupt.png`
  - `phase-4-narrow-window.png`, `phase-4-maximized.png`
- Corrupt file produced a `Could not open image` dialog and the app remained usable.

## Performance data

- Mode switch first frame: median 9.1 ms / max 16.0 ms.
- Threshold change wall clock incl. debounce: median 263 ms / max 268 ms.
- 4K process start → first difference: 1458 ms; working set 473 MB.
- Full detail: `docs/reports/performance.md`.

## Known issues and risks

- The UI screenshot capture is performed by the test harness; captured windows include the native title bar and exclude the invisible DWM border.
- Side-by-side mode uses a shared transform; each half fits the maximum canvas, so differing image sizes show explicit outlines but the smaller image is not independently centered.
- Canvas text labels do not follow system text scaling.
- Narrator/high-contrast not audited (Phase 4 gap).
- Dragging/zooming has no frame-time telemetry yet; responsiveness is supported by mode-switch timings and interaction tests.

## Next phase

Ignore-region editing UX, coordinate mapping under zoom/pan, and both PNG export paths (Phase 3).
