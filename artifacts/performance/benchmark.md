# Visual Diff Lab benchmark

- Timestamp: 2026-10-22T03:23:34.5379780+08:00
- OS: Microsoft Windows 10.0.26200 (X64)
- Runtime: .NET 10.0.13
- Logical processors: 16
- Processor: AMD64 Family 23 Model 96 Stepping 1, AuthenticAMD
- Images: dashboard-4k-before.png / dashboard-4k-after.png
- Iterations: 5 (warm file cache)
- Managed heap after GC: 199 MB
- The product working-set target is measured in the real UI by tests/VisualDiffLab.UITests/PerformanceUiTests (see artifacts/performance/ui-performance.md).

| # | Decode A+B (ms) | Diff + heatmap (ms) | Diff export compose (ms) | Side-by-side compose (ms) | Different px |
|---:|---:|---:|---:|---:|---:|
| 1 | 59.5 | 59.4 | 39.1 | 165.7 | 1,254,909 |
| 2 | 65.2 | 59.2 | 43.8 | 165.9 | 1,254,909 |
| 3 | 61.4 | 65.4 | 35.8 | 169.0 | 1,254,909 |
| 4 | 59.6 | 57.2 | 41.6 | 170.5 | 1,254,909 |
| 5 | 65.0 | 60.2 | 38.6 | 167.3 | 1,254,909 |

| Metric | Median (ms) | Max (ms) | Target |
|---|---:|---:|---:|
| Decode A+B (4K) | 61.4 | 65.2 | ≤ 2500 (load + compare) |
| Diff + heatmap overlay | 59.4 | 65.4 | ≤ 1500 |
| Difference export compose | 39.1 | 43.8 | no target (informational) |
| Side-by-side compose | 167.3 | 170.5 | no target (informational) |
