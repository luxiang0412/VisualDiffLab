# Performance report — 4K comparison

Date: 2026-10-22
Machine: LENOVO 20UD0004CD, AMD Ryzen 7 PRO 4750U (8 cores / 16 logical processors), 47.2 GiB RAM, AMD Radeon integrated graphics (driver 31.0.21921.11005), Windows 11 Pro build 26200 (25H2), 200% display scaling (app window 2564×1660 physical ≈ 1282×830 DIP), 3840×2160 physical desktop. Remote/virtual display adapters are installed on this machine; measurements are from the local interactive session.

Software: .NET SDK 10.0.402, .NET runtime 10.0.13, Windows App SDK 2.5.1, Win2D 1.4.0, Release app build. Core benchmark is a separate console process referencing the same Core/Imaging assemblies.

Fixtures: `assets/test-images/dashboard-4k-before.png` and `dashboard-4k-after.png`, 3840×2160 PNG, generated deterministically by `scripts/generate-test-images.py`. Different pixels at threshold 16: **1,254,909** of 8,294,400.

## Method

1. **Core benchmark** (`tools/VisualDiffLab.Benchmark`): one warm-up; five measured iterations. Each iteration decodes both PNGs, runs the diff, builds the heatmap overlay, composes the difference export, and composes the side-by-side export. File cache is warm; this is not a cold-disk measurement. Median and maximum are reported. Working set is intentionally not used here (the process retains warm-up/diff buffers); the product working-set target is measured in the real UI.
2. **UI performance** (`PerformanceUiTests`): launches the real Release executable with the 4K fixtures; waits for the first difference; records process-start-to-visible time; drives eight mode switches, ten threshold changes (five at 16 and five at 64), and captures two screenshots. In-app durations come from the app's diagnostics log (`%LOCALAPPDATA%\VisualDiffLab\logs\app.log`). Working set is read from the app process after the workflow.
3. UI automation and screenshots add test overhead; the reported “process start to first difference” includes app/WinUI startup and UI Automation polling, not only decode+diff.

Commands:

```powershell
dotnet build src/VisualDiffLab.App -c Release -p:Platform=x64 -r win-x64
dotnet run -c Release --project tools/VisualDiffLab.Benchmark -p:Platform=x64
$env:VDL_APP_CONFIG='Release'
dotnet test tests/VisualDiffLab.UITests -c Debug -p:Platform=x64 --filter "FullyQualifiedName~PerformanceUiTests"
```

Raw data: `artifacts/performance/benchmark.json`, `benchmark.md`, `ui-performance.json`, `ui-performance.md`.

## Results — core pipeline (warm cache, 5 iterations)

| Iteration | Decode A+B (ms) | Diff + heatmap (ms) | Diff export compose (ms) | Side-by-side compose (ms) |
|---:|---:|---:|---:|---:|
| 1 | 59.5 | 59.4 | 39.1 | 165.7 |
| 2 | 65.2 | 59.2 | 43.8 | 165.9 |
| 3 | 61.4 | 65.4 | 35.8 | 169.0 |
| 4 | 59.6 | 57.2 | 41.6 | 170.5 |
| 5 | 65.0 | 60.2 | 38.6 | 167.3 |
| **Median** | **61.4** | **59.4** | **39.1** | **167.3** |
| **Max** | **65.2** | **65.4** | **43.8** | **170.5** |

## Results — real UI (4K)

| Metric | Result | Target | Status |
|---|---:|---:|---|
| Process start → first 4K difference visible | 1458 ms | (load+diff target ≤ 2500 ms) | Pass (stricter: includes app startup) |
| In-app 4K diff (threshold 16/64) | median 42.0 ms / max 51.0 ms | ≤ 1500 ms | Pass |
| Mode switch, first frame after selection | median 9.1 ms / max 16.0 ms | ≤ 100 ms | Pass |
| Threshold change wall clock incl. 120 ms debounce | median 263 ms / max 268 ms | ≤ 1500 ms | Pass |
| App working set after 4K workflows | 473 MB | ≤ 500 MB | Pass |
| App working set after 4K load + first heatmap | 332 MB | ≤ 500 MB | Pass |
| Decode A/B in the app (log) | 161 ms / 60 ms | — | Informational |

Notes:

- Threshold changes ranged 247–268 ms including the 120 ms debounce; the maximum was 268 ms. No separate initial-allocation spike was observed in the final run.
- Working set was reduced from an earlier 755 MB baseline by reusing the heatmap overlay bitmap/buffer and rebuilding the overlay only while Heatmap mode is visible. The optimization is covered by the same performance test.
- Mode switches remain under 12 ms in the 4K run, including the frame that rebuilds the heatmap overlay when entering Heatmap mode.

## Targets vs. results

| Brief target | Result |
|---|---|
| First load + diff for two 4K PNGs ≤ 2.5 s | 1458 ms from process start to visible result |
| Mode switch with existing result ≤ 100 ms | max 16.0 ms |
| Single diff ≤ 1.5 s | max 51 ms in app; max 65.4 ms in benchmark |
| No sustained stutter while dragging/zooming | UI screenshots/automation stayed responsive; no frame-time telemetry collected (limitation) |
| Normal working set ≤ 500 MB | 473 MB after 4K workflows (peak observed 755 MB before optimization) |

## Limitations and risks

- Warm-cache measurements only; cold-disk startup was not measured.
- No GPU frame-time capture (PresentMon/ETW) was collected; “no sustained stutter” is supported by interaction tests and mode-first-frame timings, not by a frame-time histogram.
- Only one machine/OS build was measured (Windows 11 25H2 build 26200). Windows 11 26H2 and ARM64 are NOT RUN.
- UI Automation polling resolution is 200 ms, so process-start-to-visible has up to ~200 ms of measurement overhead.
- The heatmap overlay build (4K ≈ 33 MB) currently runs on the UI thread after the background diff; this is the main remaining UI-thread cost on threshold changes. It is within targets, but a future optimization would move overlay byte generation to the background with a double-buffered upload.
- Working set is sensitive to GC timing and Win2D staging allocations; the reported value is after a reproducible workflow, not an absolute peak guarantee.
