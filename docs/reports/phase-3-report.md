# Phase 3 report — ignore regions and exports

## Completed content

- Region mode: drag to create a rectangle in **image coordinates**, clipped to the comparison canvas; zero-area drags cancelled.
- Region list in the Inspector with `R1…Rn` labels, pixel coordinates/size, per-row delete, selection sync, delete button and `Ctrl+Z` undo of add/delete.
- Selected region: thicker outline, four corner handles, inset dashed outline, numeric chip — selection is not conveyed by color alone.
- Overlapping regions use their union; out-of-bounds regions are clipped; ignored pixels are excluded from stats/heatmap/exports.
- Difference Overlay PNG:
  - canvas size, A base over `#1F1F1F`;
  - fixed magenta/blue/amber colors at 85%;
  - ignored pixels show plain A;
  - opaque output; composed by Core and encoded with WIC.
- Side-by-side PNG:
  - natural sizes, 16 px gap, 32 px label bar with bitmap `A`/`B` glyphs;
  - neutral background for size differences; transparent pixels flattened;
  - output size limit 120 M pixels.
- Export UX: confirmation dialog states exact output dimensions, then the native Save As dialog; failure keeps the session and allows retry.
- Latency/quality optimization: heatmap overlay bitmap/buffer reused across threshold changes and only maintained while Heatmap mode is visible.

## Modified files

- `src/VisualDiffLab.Core/IgnoreRegions/**`, `src/VisualDiffLab.Core/Export/**`
- `src/VisualDiffLab.App/Services/ExportService.cs`, `ViewModels/MainViewModel.cs`, `CanvasInteraction.cs`, `MainWindow.xaml*`
- `tests/VisualDiffLab.Core.Tests/ExportRendererTests.cs`, `IgnoreRegionTests.cs`
- `tests/VisualDiffLab.UITests/ExportUiTests.cs`, `UiActions.cs`

## Commands

```powershell
dotnet test tests/VisualDiffLab.Core.Tests -c Release -p:Platform=x64
$env:VDL_APP_CONFIG='Release'
dotnet test tests/VisualDiffLab.UITests -c Debug -p:Platform=x64 --filter "FullyQualifiedName~ExportUiTests"
```

## Test results (final)

| Suite | Passed | Failed | Skipped |
|---|---:|---:|---:|
| Core | 44 | 0 | 0 |
| UI export tests | 2 | 0 | 0 |

Export UI test facts:

- Difference overlay decoded as **1920×1080**, containing **313,300** pixels in the fixed magenta composite range (exactly matching the no-region different-pixel count).
- Side-by-side decoded as **3856×1112** (1920 + 16 + 1920 wide, 32 + 1080 high), opaque.
- Mismatched fixtures (1600×1000 vs 1720×1080): coverage **257,600 added · 0 missing**; exported overlay contains exactly **257,600** added-color pixels.
- Exports were produced through the real confirmation dialog + native Save As dialog, not by calling the exporter directly.

## Real run verification

- Created an ignore region over the dynamic avatar area: ignored pixels **34,970**, compared **2,038,630**, different **306,816 (15.05%)**.
- `Ctrl+Z` restored ignored=0; re-adding recovered the region.
- At 100% zoom and after Space+drag pan, ignored-pixel count remained **34,970** (region stays in image space).
- Screenshots:
  - `artifacts/acceptance/phase-3-ignore-region.png`
  - `artifacts/acceptance/phase-3-region-100-percent.png`
  - `artifacts/acceptance/phase-3-region-panned.png`
  - `artifacts/acceptance/phase-3-size-mismatch.png`
  - `artifacts/acceptance/phase-3-export-difference.png`
- Exported PNGs (openable by external viewers; verified by decoding with WIC):
  - `artifacts/acceptance/exports/difference/difference-overlay.png`
  - `artifacts/acceptance/exports/side-by-side/side-by-side.png`
  - `artifacts/acceptance/exports/size-mismatch/difference-size-mismatch.png`

## Performance data

- Difference export composition at 4K: median 39.1 ms, max 43.8 ms (`DifferenceOverlayRenderer.ComposeExport`).
- Side-by-side composition at 4K: median 167.3 ms, max 170.5 ms.
- Reusing the overlay bitmap reduced the 4K working set from 755 MB to 473 MB in the stress workflow.

## Known issues and risks

- No export cancellation UI once encoding starts; windows this small complete quickly, but very large outputs could take longer.
- Save failures were tested by code paths/logging; an automated UI test for permission-denied/disk-full was NOT RUN (would need a reliably unwritable target).
- The side-by-side label glyphs are bitmap-drawn; localization is not supported (labels are fixed `A`/`B` by design).
- No “heatmap-only” export (not in MVP).
- Exports overwrite after the native picker's own overwrite confirmation; no extra in-app confirmation.

## Next phase

Quality pass: accessibility names/IDs, UI automation coverage, themes/DPI/min size/maximize, 4K performance and resource lifecycle (Phase 4).
