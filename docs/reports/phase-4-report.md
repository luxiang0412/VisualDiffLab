# Phase 4 report — quality, performance, accessibility

## Completed content

### Automation and accessibility identifiers

- Every command/input has `AutomationProperties.Name`; stable `AutomationId`s are set on the command bar, mode selector, export actions, threshold/overlay/wipe sliders, stats values, region list, inspector scroll, status and canvas.
- UI tests use only the production UI (no test-only window/controls). The `--a/--b` startup hook is the only test affordance and is documented.

### UI automation

- FlaUI 5.0.0/UIA3 suite with 8 test methods (see ADR-0003):
  - core workflow: modes, threshold, rapid threshold settling, keyboard, region add/undo/zoom/pan/Esc, Wipe arrows, themes, min size, maximize/restore;
  - exports: both PNG types through the native save dialog, decoded and pixel-verified;
  - size mismatch: added/removed coverage and export colors;
  - file-picker import, swap and clear through the real UI;
  - empty/single-image/corrupt-file states;
  - localization: live English↔Chinese switch, localized accessible names, secondary commands/export flyout, Chinese empty/single-image/error states;
  - 4K performance/workload.
- Test host is per-monitor DPI aware; UIA lookups retry transient COM timeouts.
- UI test classes are explicitly non-parallel (single real app + shared desktop input).
- Save-dialog automation navigates to the target folder with Ctrl+L, then sets the file name (the modern picker treats a full path in the name field as a file name only).

### Themes, DPI, window states

- Light/dark screenshots for empty state, side-by-side and heatmap; native title bar theme follows.
- 200% DPI workflow verified (window 2564×1660 physical ≈ 1280×800 DIP; 1024×640 minimum produces 2048×1280 physical).
- Narrow window, maximized and restored screenshots.
- Region coordinates remain correct at 100%, after panning and after returning to Fit.

### Resource lifecycle / performance

- Canvas bitmaps are created in `CreateResources`, so device loss recreates them from retained pixel data.
- Window close cancels comparisons, disposes the view model and canvas bitmaps, removes the Win2D visual tree, disposes the text format and window subclass.
- Heatmap overlay bitmap/buffer reused; overlay only alive while Heatmap mode is visible. 4K working set: **473 MB** after the stress workflow (target ≤500 MB); 755 MB before this optimization.
- Mode switch first frame max 16.0 ms; 4K diff max 51 ms; threshold change max 268 ms (first), ~255 ms steady.
- Full results: `docs/reports/performance.md`, `artifacts/performance/`.

### Localization

- English and Simplified Chinese UI (171 keys each), embedded JSON resources, key-parity validator.
- Language selector uses radio buttons because WinUI ComboBox caches selected-item text and did not refresh on a language-only change.
- Default follows the Windows preferred language; switching is immediate and covers toolbar, inspector, stats, status, dialogs, accessible names, secondary commands and the export flyout.
- Verified by two UI tests with Chinese screenshots (main UI, empty state, single-image hint, corrupt-file error).

### Dependencies and static analysis

- Central package management (`Directory.Packages.props`), versions pinned.
- `dotnet list VisualDiffLab.sln package --vulnerable --include-transitive`: **no vulnerable packages** for all nine projects.
- `--outdated`: only test infrastructure is behind (`Microsoft.NET.Test.Sdk 17.14.1` vs 18.11.0, `xunit.runner.visualstudio 3.1.5` vs 4.0.1). These are deliberate to stay on xUnit v2 + VSTest; production packages, Windows App SDK, Win2D and BuildTools are current. Audit log: `artifacts/dependency-audit.txt`.
- Nullable reference types on; .NET analyzers enabled; no global warning suppression. The only build warning left is the MSIX symbol-tooling warning (`mspdbcmf.exe` missing) during packaging, which does not produce a symbols package.

## Test results (final)

| Suite | Passed | Failed | Skipped |
|---|---:|---:|---:|
| Core | 44 | 0 | 0 |
| Imaging | 12 | 0 | 0 |
| Rendering | 15 | 0 | 0 |
| UI automation | 8 | 0 | 0 |
| **Total** | **79** | **0** | **0** |

UI automation duration: 2m50s in one non-parallel run. TRX files: `artifacts/test-results/`.

## Real run verification

- All eight UI tests passed in a single run using the Release app (`VDL_APP_CONFIG=Release`).
- Screenshots: `artifacts/acceptance/` (empty, modes, themes, regions, exports, size mismatch, narrow/maximized, 4K high-DPI, Chinese UI/empty/error, MSIX launch).
- Corrupt file dialog, threshold recompute and stale-result rejection verified.
- Export PNGs decoded and pixel-checked by the test suite.

## Performance data

| Metric | Result |
|---|---|
| 4K first difference after process start | 1458 ms |
| Working set after 4K workflows | 473 MB |
| Mode switch max | 16.0 ms |
| 4K diff max | 51 ms |
| Threshold change max | 268 ms (range ~247–268 ms) |

## Known issues and gaps

- **Narrator / high-contrast / text scaling audit NOT RUN.** Canvas chips use fixed 12 px text and do not scale with system text size.
- **Frame-time telemetry NOT RUN** (no PresentMon/ETW). No sustained stutter was observed during automated interaction; this is weaker evidence than a frame histogram.
- **Close-during-compute** is implemented (cancel + version checks) but not covered by an automated test.
- **Permission-denied/disk-full export** is handled in code and logged, but not automated (no reliably unwritable test location).
- **Windows 11 26H2** and **ARM64** execution NOT RUN.
- Language preference is session-only; additional locales and native file-dialog localization are not implemented.
- Canvas chip labels and the app brand remain language-neutral.
- UI automation is desktop-only and not part of hosted CI.
- The test harness manipulates window z-order and input; it is not suitable for a machine in active use.

## Next phase

Release preparation: MSIX packaging, clean install/upgrade/uninstall, CI workflows, documentation, license/privacy, and the release checklist (Phase 5).
