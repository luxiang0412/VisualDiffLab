# Contributing to Visual Diff Lab

Thank you for helping. This repository follows the product and engineering rules in the development brief: small, reviewable changes, measured claims, and no scope creep.

## Before you start

- The license is **not yet selected** (`LICENSE`). Do not assume a license or publish binaries.
- Discuss new features first. Anything outside the MVP must be added to `docs/decisions/DEFERRED.md` and approved by the product owner.
- Do not add NuGet/npm dependencies casually. Record purpose, license, maintenance status, version and alternatives in the PR. Versions are centrally pinned in `Directory.Packages.props`.

## Build

```powershell
dotnet restore .\VisualDiffLab.sln
dotnet build .\VisualDiffLab.sln -c Release -p:Platform=x64
dotnet build .\src\VisualDiffLab.App\VisualDiffLab.App.csproj -c Release -p:Platform=x64 -r win-x64
```

The app is a WinUI 3 desktop app; plain `dotnet build` without a platform is not a supported app build. Run it on Windows 11 25H2 (build 26100+) or later.

## Test

```powershell
dotnet test .\tests\VisualDiffLab.Core.Tests -c Release -p:Platform=x64
dotnet test .\tests\VisualDiffLab.Imaging.Tests -c Release -p:Platform=x64
dotnet test .\tests\VisualDiffLab.Rendering.Tests -c Release -p:Platform=x64

# Interactive desktop required; uses the real UI and input injection.
$env:VDL_APP_CONFIG = "Release"
dotnet test .\tests\VisualDiffLab.UITests -c Debug -p:Platform=x64
dotnet test .\tests\VisualDiffLab.UITests -c Debug -p:Platform=x64 --filter "FullyQualifiedName~PerformanceUiTests"
```

Rules:

- Algorithm changes must add deterministic tests in `VisualDiffLab.Core.Tests`.
- Rendering math changes must add tests in `VisualDiffLab.Rendering.Tests`.
- User-visible workflow changes must update/extend `VisualDiffLab.UITests` and the acceptance screenshots.
- Do not mark a check as passed without evidence. Use `NOT RUN` plus a reason when it was not executed.
- Do not fabricate performance numbers. Record machine, OS build, image sizes, iterations and method.

## Evidence and reports

- Update the relevant `docs/reports/phase-*.md` or `docs/testing/manual-acceptance.md`.
- Do not commit personal pictures, user paths, credentials or machine-specific configuration.
- Logs must never contain pixel data or full user paths.

## Code style

- Nullable reference types enabled; keep the build free of new warnings.
- Keep Core free of WinUI/Win2D/WIC references.
- Keep the dependency direction `App → Rendering → Core` and `App → Imaging → Core`.
- Prefer platform controls and theme resources; do not restyle Fluent controls without a recorded reason.
- Large bitmaps: avoid new per-frame allocations and document ownership/disposal.

## Pull request checklist

- [ ] Scope matches an approved task; deferred ideas recorded.
- [ ] Builds Debug + Release.
- [ ] Unit/integration tests pass; UI tests pass or an explicit NOT RUN reason is recorded.
- [ ] Performance implications measured if the change touches the pipeline/renderer.
- [ ] Screenshots updated for visible changes (light and dark where relevant).
- [ ] No secrets, personal paths or private images.
- [ ] Docs/reports updated.

## Code of conduct

Participation is covered by `CODE_OF_CONDUCT.md`.
