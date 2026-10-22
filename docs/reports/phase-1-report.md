# Phase 1 report — Core algorithm and deterministic tests

## Completed content

- Created `VisualDiffLab.Core` (net10.0, no WinUI/Win2D/WIC references).
- Implemented:
  - `ImageDocument` normalization limits (16,384 px side, 80 M pixels) and buffer validation;
  - comparison canvas (`max` dimensions), strict-greater-than threshold 0–64;
  - premultiplied BGRA8 comparison (alpha normalization tested);
  - `OnlyInA` / `OnlyInB` size-mismatch pixels and statistics;
  - ignore-region union, clipping and statistics;
  - `DiffOptions`, `DiffResult`, nullable ratio, version stamp;
  - cancellation checks during row loops;
  - `IgnoreRegionHistory` with add/remove/undo and version increments;
  - pure BGRA export composition (difference overlay and side-by-side) with fixed colors and flattening.
- Documented rules in `docs/product/algorithm-rules.md`.
- Built a deterministic test-image factory and boundary test vectors.

## Modified files

- `src/VisualDiffLab.Core/**`
- `tests/VisualDiffLab.Core.Tests/**`
- `docs/product/algorithm-rules.md`

## Commands

```powershell
dotnet test tests/VisualDiffLab.Core.Tests -c Release -p:Platform=x64 --logger "trx;LogFileName=core-tests.trx" --results-directory artifacts/test-results
```

Final result (re-run 2026-10-22): **44 passed, 0 failed, 0 skipped** — TRX `artifacts/test-results/core-tests.trx`.

Covered cases include identical images, single-pixel change, delta equal to/above threshold, transparent hidden RGB, alpha-only change, A/B larger, full/partial/overlapping/out-of-bounds ignores, deterministic repeat, cancellation, invalid threshold, 0×0 rejection, export colors, ignored suppression, side-by-side padding/labels and output limit.

## Test results

| Suite | Passed | Failed | Skipped |
|---|---:|---:|---:|
| Core | 44 | 0 | 0 |

The suite is independent of UI threads and uses only deterministic generated buffers. `Time: 237 ms`.

## Real run verification

Not applicable to this phase (library). The full app exercises the same Core code; see Phase 2/3 screenshots and statistics (313,300 different pixels for the 1920×1080 fixture pair; 306,816 with a 34,970-pixel ignore region).

## Performance data

Core diff benchmark is in `docs/reports/performance.md`: 4K diff + heatmap overlay median **59.4 ms**, max **65.4 ms** over five warm iterations (separate benchmark process).

## Known issues and risks

- The `DiffResult` classification mask is one byte per canvas pixel (8.3 MB at 4K); a new mask is allocated per comparison. GC handles it; mask pooling was considered but rejected due to aliasing with exports. Peak working set is monitored by the UI performance test (473 MB after workflows).
- Overlay byte generation is in Core (`DifferenceOverlayRenderer.BuildHeatmapOverlay`) and is shared by rendering/export so statistics and pixels cannot diverge.
- Core has no image decoding; malformed-file behavior is covered by Imaging tests.

## Next phase

Build the WinUI workspace: layout, empty state, file opening/drag-drop, four modes, zoom/pan, threshold/statistics, themes (Phase 2).
