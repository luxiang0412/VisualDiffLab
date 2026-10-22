# Testing and manual acceptance

## Automated tests

| Suite | Command | Requires desktop | Purpose |
|---|---|---|---|
| Core | `dotnet test tests/VisualDiffLab.Core.Tests -c Release -p:Platform=x64` | No | Algorithm, ignore regions, statistics, determinism, export composition |
| Imaging | `dotnet test tests/VisualDiffLab.Imaging.Tests -c Release -p:Platform=x64` | No (Windows APIs) | Decode PNG/JPEG/BMP, EXIF orientation, premultiplied alpha, corrupt/unsupported/locked files, PNG encode |
| Rendering | `dotnet test tests/VisualDiffLab.Rendering.Tests -c Release -p:Platform=x64` | No | View transform, zoom anchoring, fit, DPI 100%, viewport layout |
| UI | `$env:VDL_APP_CONFIG='Release'; dotnet test tests/VisualDiffLab.UITests -c Debug -p:Platform=x64` | **Yes** | Real window workflow, modes, shortcuts, threshold, regions/undo, theme, resize, save dialog, exports, import/swap/clear, English/Chinese switch |
| Performance UI | `... --filter "FullyQualifiedName~PerformanceUiTests"` | **Yes** | 4K first-load timing, in-app diff/frame timings, working set, saves report |

The UI test host must be per-monitor DPI aware; this is enforced in `UiTestBootstrap`.

## Real-run acceptance steps (executed 2026-10-22)

1. Build the Release unpackaged app. Launch it empty; import A and B through the real `Open image A`/`Open image B` file pickers (isolated fixture folder), verify both inspector labels, then test Swap and Clear (command-bar overflow).
2. Launch with two 1920×1080 synthetic screenshots (automation hook) and wait for the first statistics; verify **313,300 different pixels / 2,073,600 compared / 15.11%**.
3. Switch `Side by side` → `Wipe` → `Overlay` (65%) → `Heatmap`; verify screenshots and shared view state.
4. Press `2` and `4` on the focused canvas; verify Wipe/Heatmap become selected.
5. Set threshold 64 and back to 16; verify different-pixel count changes (313,300 → 242,384 without regions) and returns. Rapidly send 10→20→30→40→50→64; verify the final result matches 64 and no stale value wins.
6. Enable region mode, drag a rectangle over the avatar area; verify ignored pixels become **34,970**, statistics drop to **306,816 (15.05%)** and the region is drawn in image coordinates.
7. Exit region mode, `Ctrl+Z`; verify ignored returns to **0**. Re-add a region.
8. At 100% and after `Space+drag` pan, verify ignored pixel count is unchanged (region stays in image space).
9. Press `Esc` in region mode; verify the mode exits.
10. In Wipe mode press Left twice; verify split position 48%.
11. Switch to Dark; verify side-by-side + heatmap screenshots and title bar theme. Return to Light.
12. Resize to 1024×640; verify layout and a 2048×1280 physical window at 200% DPI. Maximize/restore and capture.
13. Export **Difference overlay PNG** and **Side-by-side PNG** through the real confirmation + native Save As dialogs.
    - Difference output decoded: 1920×1080 with 313,300 fixed-color pixels.
    - Side-by-side output decoded: 3856×1112.
14. Load the mismatched-size fixtures (1600×1000 vs 1720×1080); verify `257,600 added · 0 missing`, heatmap/screenshot, and export with exactly 257,600 added-color pixels.
15. Launch with no images: capture empty state in Light and Dark. Launch with one image: verify the hint. Launch with a corrupt PNG: verify the `Could not open image` dialog and graceful dismissal.
16. Run the 4K workflow: process start → first difference 1458 ms, working set 473 MB, mode-switch max 16.0 ms, diff max 51 ms.
17. Build the MSIX, extract the layout, register it on the Developer Mode machine, launch the packaged app, verify the packaged process path/window, and uninstall.
18. Localization: with the app in English, select `中文` under Language; verify toolbar, inspector, statistics, status, secondary commands and export flyout switch immediately; open the empty state, a single-image state and a corrupt-file error in Chinese; switch back to English.
19. Upgrade path: register 0.1.0.0, rebuild/register 0.1.1.0, verify `Version 0.1.1.0` and launch; uninstall; rebuild and re-register final 0.1.0.0, launch, uninstall.

- Import/swap/clear evidence: `artifacts/acceptance/phase-2-file-picker-import.png`.
- Localization evidence: `phase-4-localization-chinese.png`, `phase-2-empty-zh.png`, `phase-2-single-image-zh.png`, `phase-2-error-corrupt-zh.png`.
- Localization resources are validated by `python scripts/validate-localization.py` (171 keys, both languages).

## Evidence

- `artifacts/acceptance/` — screenshots for empty state, all modes, both themes, region, export, size mismatch, narrow window, maximized, high DPI 4K, installed MSIX launch.
- `artifacts/acceptance/exports/` — exported PNGs from the UI flow.
- `artifacts/phase-0/` — environment logs and the Win2D spike screenshot.
- `artifacts/performance/` — benchmark JSON/Markdown and UI performance report.
- `artifacts/test-results/` — TRX files from the final release test run.

## NOT RUN (with reasons)

| Item | Reason |
|---|---|
| Windows 11 26H2 execution | Only build 26200 (25H2) available; no second machine/VM with 26H2 |
| ARM64 build/run | Development machine is x64; ARM64 assets exist but no test hardware |
| Signed MSIX install / clean machine | No code-signing certificate; verification used loose-layout registration with Developer Mode |
| Microsoft Store certification | No Partner Center account/identity; not a code task |
| GitHub Actions execution | Workflows are authored but not executed on GitHub from this environment |
| Appium Windows Driver | Evaluated and rejected in ADR-0003 (WinAppDriver unmaintained); FlaUI/UIA3 used instead |
| Narrator / high-contrast / text scaling | No automated or formal manual audit performed; known canvas-label limitation recorded |
| Drag-and-drop import | Implemented (DragOver/Drop handlers, AllowDrop on the canvas surface). A scripted Explorer drag attempt did not produce DragOver/Drop events on this machine, so it is not counted as verified; only the real file-picker import path is automated and verified |
| Additional languages / persistence | Only en-US and zh-CN exist; language choice is session-only and resets to the Windows language on launch. Native file dialogs follow the Windows UI language. |
| ICC-profile conversion sample | Decoding requests ColorManageToSRgb, but no embedded-ICC fixture was created; behavior not directly verified |
| Cold-cache 4K timing | Benchmark/measurements are warm-cache; cold-start disk timings not measured |
| Automatic update flow | No signed package/update channel exists |
| HEIC/WebP/TIFF | Explicitly unsupported in the MVP |

## Environment-only caveats

- The UI automation runs manipulate window z-order (topmost while capturing) and inject mouse/keyboard input; do not run them on a machine in active use.
- The test hook `--a/--b` exists only for automation and manual verification.
