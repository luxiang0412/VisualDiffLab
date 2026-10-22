# Phase 0 report — environment and technical validation

Date: 2026-10-22
OS: Windows 11 Pro build 26200 (25H2), x64, interactive desktop, non-administrator user
Baseline from brief: VS 2026 18.11.1, .NET 10.0.13, Windows App SDK 2.5.1, Windows SDK 10.0.26100.9457, WinUI 3 + Win2D

## Completed content

- Inspected the machine before writing code.
- Found **no .NET SDK** installed (only runtimes 6/8/10). Installed .NET SDK **10.0.402** per-user with `scripts/dotnet-install.ps1` into `%USERPROFILE%\.dotnet`.
- Found **Visual Studio 2022 17.8.3** (not VS 2026/2026 18.11.1). The build uses the .NET CLI and NuGet tooling; VS is not required.
- Installed Windows SDK headers available: 10.0.22621.0. The project targets `10.0.26100.0`; the .NET SDK supplies the 26100 reference pack and the MSIX tooling comes from NuGet (`Microsoft.Windows.SDK.BuildTools.MSIX`).
- Windows Developer Mode is enabled; session is interactive; NuGet is reachable.
- Created a disposable WinUI 3 + Win2D spike (`spike/WinUiSpike`) with Windows App SDK 2.5.1 and Win2D 1.4.0.
- Built and ran the spike: native WinUI 3 window, Win2D `CanvasControl`, filled rectangle and text rendering, normal close.
- Wrote decision records: `ADR-0001-ui-framework.md`, `ADR-0002-rendering.md`.
- Recorded environment evidence in `artifacts/phase-0/` with `scripts/collect-phase0.ps1`.

## Modified files

- `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `.gitignore`
- `spike/WinUiSpike/*` (disposable validation prototype)
- `scripts/dotnet-install.ps1`, `scripts/collect-phase0.ps1`, `scripts/capture-window.ps1`
- `docs/decisions/ADR-0001-ui-framework.md`, `docs/decisions/ADR-0002-rendering.md`
- `artifacts/phase-0/*`

## Commands

```powershell
powershell -ExecutionPolicy Bypass -File scripts/dotnet-install.ps1 -Channel 10.0 -InstallDir "$env:USERPROFILE\.dotnet"
powershell -ExecutionPolicy Bypass -File scripts/collect-phase0.ps1
dotnet build spike/WinUiSpike/WinUiSpike.csproj -c Debug -p:Platform=x64 -r win-x64
Start-Process spike/WinUiSpike/bin/x64/Debug/net10.0-windows10.0.26100.0/win-x64/WinUiSpike.exe
powershell -ExecutionPolicy Bypass -File scripts/capture-window.ps1 -ProcessName WinUiSpike -OutputPath artifacts/phase-0/spike-win2d.png -Foreground
```

## Test results

- Automated tests at Phase 0: **0 passed / 0 failed / 0 not run** (no test projects yet).
- Build: spike succeeded with 1 analyzer warning (CA1822) and 0 errors.

## Real run verification

- Spike window launched with title `WinUI/Win2D Spike`; screenshot `artifacts/phase-0/spike-win2d.png` shows the Win2D-drawn rectangle and text.
- Window close left no visible error; process exited.
- No 4K image decode, zoom/pan, overlay/selection drawing, background compute or MSIX packaging was verified in the Phase 0 spike. Those were validated later in Phases 2–5 and are documented there. This is a recorded scope deviation from the brief's Phase 0 list.

## Performance data

None at Phase 0. The first real baseline was measured on the completed app; see `docs/reports/performance.md`. Current 4K results: decode 61.4 ms median (benchmark), diff 42 ms median in the app, working set 473 MB.

## Known issues and risks

- The brief's exact toolchain (VS 2026, Windows SDK 28000) is not installed. Builds and packaging succeeded with .NET SDK 10.0.402 + NuGet tooling.
- Win2D 1.4.0 declares a dependency on WinUI 1.8; it runs against WinUI 2.3.9 from Windows App SDK 2.5.1. Verified by spike and full app; future upgrades need re-validation.
- Only Windows 11 25H2 (build 26200) is available. Windows 11 26H2 is NOT RUN.
- MSIX target-version constraints were verified later (Phase 5), not during Phase 0.
- Spike is disposable and intentionally excluded from `VisualDiffLab.sln`.

## Next phase

Implement `VisualDiffLab.Core` comparison rules, ignore-region semantics, statistics and deterministic test vectors (Phase 1).
