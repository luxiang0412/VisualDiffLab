# AGENTS.md — Visual Diff Lab

Persistent project instructions for coding agents. Read this before changing code.

## Mission and current state

Visual Diff Lab is a **local-first native Windows workspace** for inspecting visual differences between two UI screenshots (WinUI 3 / Windows App SDK 2.5.1 / Win2D 1.4.0 / .NET 10).

The MVP is implemented and locally verified. The authoritative status, checklist, evidence and blockers are in:

- `docs/reports/release-readiness.md`
- `docs/reports/phase-0-report.md` … `phase-4-report.md`
- `docs/reports/performance.md`
- `docs/testing/manual-acceptance.md`

**Do not call it released or production-ready.** It is an unsigned developer build with pending license and unverified clean-machine/signed install.

## Read first

1. `Visual-Diff-Lab-Development-Brief.md` — authoritative product brief and acceptance criteria.
2. `README.md` — features, build/run, shortcuts, limitations.
3. `docs/product/algorithm-rules.md` — comparison/export invariants.
4. `docs/decisions/ADR-0001…0004.md` and `DEFERRED.md` — recorded decisions and non-goals.
5. `docs/reports/release-readiness.md` — what is verified vs NOT RUN.

## Non-negotiable product rules

- **No unapproved scope creep.** New features require a product decision; record deferred ideas in `docs/decisions/DEFERRED.md`. The explicit non-goals in the brief (browser automation, AI, cloud/accounts, video, batch folders, auto-registration, plugins, OCR, etc.) stay out.
- **Local-first privacy.** No networking, telemetry, accounts, uploads or background sync. Never modify input images. Never log pixel data or full user paths. Do not add administrator requirements.
- **Evidence over compilation.** A successful build is not completion. Exercise the real window; add deterministic tests or record `NOT RUN` with a reason. Never fabricate tests, screenshots, timings, machine info or support claims.
- **Dependencies stay minimal and pinned.** All NuGet versions live in `Directory.Packages.props`. Record purpose, license, maintenance status, version and alternatives before adding a package.
- **License is not selected.** `LICENSE` intentionally says so. Do not choose a license, publish binaries under an assumed license, or remove that notice without owner approval.
- **Don't claim unverified environments.** Windows 11 26H2, ARM64, signed MSIX, clean-machine install, Microsoft Store and GitHub Actions execution are **NOT RUN** as of the last report. Update the report with real evidence before changing that.

## Environment (development machine)

- Windows 11 Pro build **26200** (25H2), x64, interactive desktop, 200% DPI.
- .NET SDK **10.0.402**, installed **per-user** at `%USERPROFILE%\.dotnet`. Prefix PATH if needed:
  `$env:PATH = "$env:USERPROFILE\.dotnet;$env:PATH"`
- Visual Studio 2022 17.8.3 is installed but **not used**; the brief's VS 2026 toolchain is not available/verified.
- Developer Mode is enabled (needed only for loose-layout MSIX registration).
- Projects target `net10.0-windows10.0.26100.0`; minimum OS is **Windows 11 25H2 (build 26100)**.
- Node.js exists but is not needed for the product or tests. Appium/WinAppDriver is not used (see ADR-0003).

## Build (verified commands)

```powershell
dotnet restore .\VisualDiffLab.sln

# Libraries/tests/tools (no RID; solution-level -r is NOT supported)
dotnet build .\VisualDiffLab.sln -c Debug -p:Platform=x64
dotnet build .\VisualDiffLab.sln -c Release -p:Platform=x64

# The WinUI app requires a RID
dotnet build .\src\VisualDiffLab.App\VisualDiffLab.App.csproj -c Debug -p:Platform=x64 -r win-x64
dotnet build .\src\VisualDiffLab.App\VisualDiffLab.App.csproj -c Release -p:Platform=x64 -r win-x64
```

Unpackaged Release executable:

```text
src\VisualDiffLab.App\bin\x64\Release\net10.0-windows10.0.26100.0\win-x64\VisualDiffLab.exe
```

## Tests (verified commands)

```powershell
# 71 deterministic unit/integration tests (no UI desktop)
dotnet test .\tests\VisualDiffLab.Core.Tests -c Release -p:Platform=x64
dotnet test .\tests\VisualDiffLab.Imaging.Tests -c Release -p:Platform=x64
dotnet test .\tests\VisualDiffLab.Rendering.Tests -c Release -p:Platform=x64

# 8 real UI-automation tests — require an interactive desktop
$env:VDL_APP_CONFIG = "Release"
dotnet test .\tests\VisualDiffLab.UITests -c Debug -p:Platform=x64
```

Localization key parity:

```powershell
python .\scripts\validate-localization.py
```

UI-test facts:

- Uses FlaUI/UIA3 against the real app and native Open/Save dialogs.
- Non-parallel by design; it injects mouse/keyboard input, manipulates z-order and **kills `VisualDiffLab` processes**. Do not run it on a machine in active use.
- Test host must be per-monitor DPI aware; `UiTestBootstrap` handles this.
- Do not add `[Collection]` parallelism or run two UI sessions simultaneously.
- The `--a`/`--b`/`--lang` startup arguments are documented test/automation hooks, not user features. UI tests must force `--lang en-US` so English assertions stay deterministic.

## Localization

- User-facing strings live in `src/VisualDiffLab.App/Localization/en-US.json` and `zh-CN.json` (embedded resources). Both files must contain the same non-empty keys; run `python scripts/validate-localization.py` after edits.
- XAML uses classic `{Binding [Key]}` with `RootGrid.DataContext = Loc` so a language switch updates the window without recreating it. Flyout/secondary-command labels are assigned in code because they are outside the visual tree.
- Theme/language selectors are **RadioButtons**, not ComboBoxes: WinUI caches ComboBox selection text and did not refresh when only the item content language changed.
- `LocalizationService.Language` is `System`, `English` or `Chinese`; System follows `GlobalizationPreferences.Languages[0]` with a `CurrentUICulture` fallback. Language is session-only (no persistence).
- VM state stores message **keys + args** (`SetTransientMessage`), never pre-formatted strings, so status/statistics re-render on switch. Image-load errors map from `ImageLoadErrorKind` to localized messages.
- Native file dialogs keep the Windows UI language; the selector only changes in-app text. Do not claim otherwise.

## Architecture

```text
App → Rendering → Core
App → Imaging  → Core

src/VisualDiffLab.Core       pure comparison model, diff engine, ignore regions, statistics, export pixel composition
src/VisualDiffLab.Imaging    WIC decode/encode, BGRA8 premultiplied sRGB normalization
src/VisualDiffLab.Rendering  Win2D renderer, ViewTransform, viewport layout, overlays
src/VisualDiffLab.App        WinUI window, MVVM-lite view model, dialogs, canvas interaction
tests/                       Core.Tests, Imaging.Tests, Rendering.Tests, UITests
tools/VisualDiffLab.Benchmark  reproducible CPU benchmark
```

Keep `Core` free of WinUI/Win2D/WIC references. Rendering and hit-testing must share the same `ViewTransform`; never mix screen and image coordinates.

## Algorithm invariants

- Canvas = `max(widthA,widthB) × max(heightA,heightB)`; both images align at `(0,0)`; no auto-scaling/registration.
- Normalized pixels are BGRA8 **premultiplied sRGB**; hidden RGB under alpha 0 must not create differences.
- A pixel is different when `max(|ΔB|,|ΔG|,|ΔR|,|ΔA|) > threshold` (strictly greater). Threshold 0–64, default 16.
- Only-one-image-area counts as different (`OnlyInA` / `OnlyInB`).
- Ignore regions live in **comparison-canvas pixels**, use their union, and exclude pixels from stats/heatmap/exports.
- `DiffResult.ClassificationMask` is the single source of truth for stats, heatmap and exports; never recompute differences separately.
- Ignore-region add/delete are undoable; regions must not modify source pixels.
- Fixed colors: changed `#E3008C`, added `#00A2FF`, removed `#FFB900`, 85% overlay alpha; don't change without product decision + tests + docs.
- Export composition is in `Core`, encoding in `Imaging`; both exports must use the current `DiffResult`.
- 100% = one image pixel per **physical** screen pixel (`Zoom = 96 / rasterizationScale`).
- Comparisons are versioned + cancellable; stale results must never overwrite newer threshold/image state. Compare runs off the UI thread.
- Heatmap overlay bitmap/scratch is reused and only maintained while Heatmap mode is visible; keep the 4K working set ≤500 MB.

## Performance budget and measurement

Targets (brief): first 4K load+diff ≤ 2.5 s, mode switch ≤100 ms, diff ≤1.5 s, working set ≤500 MB.

Last verified Release results (Windows 11 25H2, Ryzen 7 PRO 4750U, 200% DPI, 4K fixtures):

- process start → first 4K difference: **1458 ms**
- 4K diff: median **42 ms** / max **51 ms**
- mode switch first frame: median **9.1 ms** / max **16.0 ms**
- threshold change incl. debounce: median **263 ms** / max **268 ms**
- working set after 4K workflows: **473 MB**

Use `tools/VisualDiffLab.Benchmark` for CPU timings and `PerformanceUiTests` for app timings/memory. Report machine, OS build, DPI, image sizes, iterations, median/max, and method. Do not tune tests to hide regressions.

## Packaging / release facts

- MSIX is built with:
  `dotnet build .\src\VisualDiffLab.App\VisualDiffLab.App.csproj -c Release -p:Platform=x64 -r win-x64 -p:VisualDiffLabPackaged=true`
- Manifest: `src/VisualDiffLab.App/Package.appxmanifest`, identity `VisualDiffLab`, version `0.1.0.0`, min OS 26100, `runFullTrust`, self-contained WASDK/Win2D (~96 MB).
- Package is **unsigned**. Verification used MSIX extraction + `Add-AppxPackage -Register` on a Developer Mode machine; upgrade 0.1.0.0→0.1.1.0 and uninstall were tested this way.
- Signed package, clean-machine install, Store certification and auto-update are **NOT RUN**. Don't claim them.

## Known gaps (keep reports accurate)

- Drag-and-drop import is implemented but not verified; a scripted Explorer drag produced no DragOver/Drop events. File-picker import is verified.
- ICC-profile conversion with a custom ICC fixture is not verified (code requests `ColorManageToSRgb`).
- Narrator/high-contrast/system text-scaling audit not done; canvas chip text is fixed at 12 px.
- Close-during-export and permission-denied/disk-full export are handled but not automated.
- Windows 11 26H2, ARM64, GitHub Actions execution: NOT RUN.
- Language choice is not persisted across launches; only English and Simplified Chinese are provided; native file dialogs follow the Windows UI language.
- UI automation is desktop-only and not part of hosted CI.

## Change workflow

1. Read the brief and current release-readiness report.
2. Confirm the change is in scope; otherwise record it as deferred/ask the owner.
3. Change code in small, reviewable steps aligned with the project boundaries.
4. Add/update deterministic tests. Visible changes require real UI evidence and light/dark screenshots when relevant.
5. Run Debug **and** Release builds, unit tests, and UI tests (or explicitly record `NOT RUN` with a reason).
6. Re-run the 4K benchmark/UI performance test for pipeline/renderer changes; update `docs/reports/performance.md`.
7. Run the dependency vulnerability audit; update `docs/reports/dependencies.md` if packages change.
8. Update README/phase reports/acceptance docs. Do not leave stale numbers or claims.
9. Never commit personal images, credentials, machine-private configuration, or full user paths. Environment logs/TRX files are untracked for this reason.

## Evidence layout

- `artifacts/acceptance/` — real screenshots and exported PNGs (tracked evidence)
- `artifacts/performance/` — benchmark/UI performance JSON + Markdown
- `artifacts/phase-0/`, `artifacts/test-results/`, `artifacts/dependency-audit.txt` — local evidence (untracked; contain machine paths)
- `artifacts/msix/` — local MSIX build (ignored; ~96 MB)
- `docs/design/screenshots/` — curated screenshots referenced by README/docs

## Current test totals

- 71 deterministic Core/Imaging/Rendering tests.
- 8 UI automation tests: core workflow, import/swap/clear, 2 export tests, empty/error state, 2 localization tests, 4K performance.
- Total: 79. UI tests are non-parallel and force `--lang en-US`.

## License

License selection is pending the project owner. Do not assume MIT/Apache or any other license. See `LICENSE` and `docs/decisions/DEFERRED.md`.
