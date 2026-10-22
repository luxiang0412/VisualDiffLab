# UI performance (4K)

- Timestamp: 2026-10-22T08:08:33.5440363+08:00
- OS: Microsoft Windows NT 10.0.26200.0
- Runtime: 10.0.13
- Logical processors: 16
- Images: dashboard-4k-before.png / dashboard-4k-after.png (3840x2160 PNG)
- Measurement: UI Automation driving the real Release/Debug app; app log timestamps for in-app work.
- Process start to first 4K difference visible: 1458 ms
- Working set after 4K workflows: 473 MB (target <= 500 MB)
- Mode switch first frame (ms): 11.1, 15.7, 4.6, 12.1, 16.0, 7.0, 3.8, 2.8
- Mode switch median/max: 9.1 / 16.0 ms (target <= 100 ms)
- Diff + heatmap (ms): 51.0, 42.0, 41.0, 42.0, 42.0, 44.0, 42.0, 42.0, 42.0, 42.0, 42.0
- Diff median/max: 42.0 / 51.0 ms (target <= 1500 ms)
- Decode A/B (ms): 161.0, 60.0
- Threshold change wall clock incl. 120 ms debounce (ms): 268, 247, 249, 253, 262, 263, 264, 265, 264, 263
- Threshold median/max: 263 / 268 ms

Screenshots: `artifacts/acceptance/phase-4-high-dpi-4k-heatmap.png`,
`artifacts/acceptance/phase-4-performance-4k-side-by-side.png`.