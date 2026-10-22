# Phase 5 / release readiness report

Date: 2026-10-22
Version: 0.1.0 (MSIX Identity `VisualDiffLab`, version `0.1.0.0`, x64)

## Completed content

- MSIX build via the same app project with `-p:VisualDiffLabPackaged=true`; manifest targets Windows 11 25H2 (`MinVersion=10.0.26100.0`, `MaxVersionTested=10.0.26200.0`) with `runFullTrust`.
- Package layout and unsigned MSIX produced at `artifacts/msix/VisualDiffLab.App_0.1.0.0_x64_Test/VisualDiffLab.App_0.1.0.0_x64.msix` (≈96 MB, self-contained Windows App SDK/Win2D).
- Install verification via extracted loose layout + `Add-AppxPackage -Register` (Developer Mode). Signed MSIX install is **NOT VERIFIED** (no certificate).
- Upgrade verification: 0.1.0.0 → 0.1.1.0 registration, launch of 0.1.1.0, then uninstall; final 0.1.0.0 package re-built, re-registered, launched, and uninstalled.
- Uninstall verification: `Get-AppxPackage VisualDiffLab` empty afterwards.
- `README.md`, `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, `SECURITY.md`, `CHANGELOG.md`, `docs/privacy.md`.
- `LICENSE` contains an explicit **not selected** notice; license choice is pending the project owner (see `docs/decisions/DEFERRED.md`).
- GitHub Actions workflows authored: `.github/workflows/ci.yml` (build, unit tests, vulnerability audit, artifacts) and `.github/workflows/package.yml` (manual/tag MSIX build). Issue templates added.
- Phase 0–4 reports, performance report, algorithm rules, design notes/references, testing/acceptance notes.

## Modified files

- `src/VisualDiffLab.App/Package.appxmanifest`, `VisualDiffLab.App.csproj`, `Assets/*`
- `.github/**`, `README.md`, `LICENSE`, `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, `SECURITY.md`, `CHANGELOG.md`
- `docs/**`, `docs/design/screenshots/**`
- `docs/reports/*`

## Commands

```powershell
dotnet build src/VisualDiffLab.App/VisualDiffLab.App.csproj -c Release -p:Platform=x64 -r win-x64 -p:VisualDiffLabPackaged=true
# extract MSIX to a layout, then (Developer Mode):
Add-AppxPackage -Register "<layout>\AppxManifest.xml"
# launch via shell:AppsFolder\<PackageFamilyName>!App
Remove-AppxPackage (Get-AppxPackage VisualDiffLab).PackageFullName
```

## Test and verification results

| Check | Result |
|---|---|
| Core/Imaging/Rendering tests | 71/71 passed |
| UI automation tests | 8/8 passed in Release run (79 total with 71 unit/integration tests) |
| Dependency vulnerability audit | No vulnerable packages (9 projects, transitive included) |
| MSIX build | Succeeded (0 errors, 1 symbol-tool warning) |
| Loose-layout install 0.1.0.0 | Succeeded |
| Packaged launch | Succeeded; process path inside package layout; WinUI window visible (`phase-5-msix-final-launch.png`) |
| Upgrade 0.1.0.0 → 0.1.1.0 | Succeeded; `Version 0.1.1.0` registered and launched |
| Uninstall | Succeeded; package no longer registered |
| Signed MSIX install / clean machine | **NOT RUN** (no signing certificate) |
| Microsoft Store certification | **NOT RUN** (no Partner Center identity) |
| GitHub Actions execution | **NOT RUN** (workflows authored locally; not executed on GitHub) |

## Real run evidence

- `artifacts/acceptance/phase-5-msix-final-launch.png` — packaged app launched from `artifacts/msix/layout` after final 0.1.0.0 registration.
- `artifacts/acceptance/phase-5-msix-installed-launch.png` — earlier install verification.
- Full acceptance screenshots: `artifacts/acceptance/`, copied to `docs/design/screenshots/`.
- Exported PNGs: `artifacts/acceptance/exports/`.

## Published artifacts

- Source: this workspace.
- Unpackaged Release executable: `src/VisualDiffLab.App/bin/x64/Release/net10.0-windows10.0.26100.0/win-x64/VisualDiffLab.exe`.
- Unsigned test MSIX: `artifacts/msix/VisualDiffLab.App_0.1.0.0_x64_Test/VisualDiffLab.App_0.1.0.0_x64.msix`.
- Test/benchmark reports: `artifacts/test-results/`, `artifacts/performance/`.

## Known issues and blockers

1. **License not selected** — must be resolved by the owner before any public release.
2. **Package is unsigned** — install only verified in Developer Mode via loose registration. A signing certificate and clean-machine install are required for real distribution.
3. **Windows 11 26H2** and **ARM64** are NOT RUN.
4. GitHub Actions workflows are unexecuted; the UI test gate is local/self-hosted only.
5. Narrator/high-contrast/text-scaling audit is outstanding; canvas label text does not scale.
6. No Store identity/listing; no auto-update channel.
7. Side-by-side mode uses one shared transform; this is documented and intentional.
8. Drag-and-drop import is implemented but not covered by automation; the file-picker path is verified. ICC-profile conversion was not exercised with a custom ICC fixture.
9. The language choice is session-only (defaults to the Windows language on each launch); only English and Simplified Chinese exist, and native file dialogs follow the Windows UI language.

## Final acceptance checklist (brief §11)

### Functionality

- [x] Import two images through the real Open dialog, swap, reselect, clear — verified by UI test + screenshots; drag-and-drop is implemented but NOT RUN as an automated/manual drag test.
- [x] Four modes operate for real — verified by UI test, history + screenshots.
- [x] Synchronized zoom/pan, Fit, 100% — verified by rendering tests + UI (100%, pan, Wipe arrows).
- [x] Threshold updates statistics/heatmap/exports consistently — versioned pipeline, UI test, export pixel checks.
- [x] Ignore regions create/select/delete/undo — UI test, images.
- [x] Size-mismatch behavior matches the specification — unit + UI + export pixel counts.
- [x] Both PNG exports correct — UI-driven save + decode/pixel verification.

### Quality

- [x] Core/Imaging unit tests pass (44 + 12; Rendering 15 additional).
- [x] Key UI flows pass UI automation (6 tests) — no manual-only claim.
- [x] Corrupt file, threshold cancel/stale, invalid format, minimum size tested. Permission-denied/disk-full export NOT RUN.
- [x] No known blocking crash or data-corruption issue.
- [x] Dependencies/licenses/vulnerability audit recorded (`artifacts/dependency-audit.txt`); Win2D/Windows App SDK licenses noted.

### Visual & accessibility

- [x] Real screenshots for empty state, four modes, ignore regions, export state.
- [x] Dark and light themes.
- [x] Narrow window, maximize/restore, high DPI (200%, 4K).
- [ ] Narrator/high-contrast/text-scaling audit — **NOT RUN**.
- [x] Keyboard shortcuts, focusable canvas, accessible names/IDs implemented and mostly exercised.
- [x] English/Chinese UI switch with localized dialogs/status/accessible names; verified by two UI tests.
- [x] No visible control overlap/clipping at 1024×640; inspector scrolls.

### Performance & release

- [x] Reproducible 4K performance report with method and machine details.
- [x] Resource release/cancellation implemented; close-during-compute not automated.
- [x] Release build and MSIX build succeed.
- [ ] Clean-machine **signed** MSIX install/upgrade/uninstall — **NOT RUN**; loose registration verified.
- [x] README, contribution guide, privacy, changelog, testing docs complete.
- [ ] LICENSE selected — **blocked on owner**.
- [ ] CI results on GitHub — workflows authored, **NOT RUN**.
- [x] Local test evidence available under `artifacts/`.

## Release recommendation

The MVP is functionally complete and locally verified as an unsigned developer build. It is **not ready for public distribution** until the license is selected, the MSIX is signed and clean-machine tested, the accessibility audit is completed, and CI has run on GitHub. These are recorded blockers, not hidden assumptions.
