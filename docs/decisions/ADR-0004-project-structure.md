# ADR-0004: Solution structure and deliberate deviations

- Status: Accepted
- Date: 2026-10-22

## Context

The brief proposes four production projects (`App`, `Core`, `Imaging`, `Rendering`), three test projects, and a specific folder layout. The implementation must stay reviewable and must not introduce frameworks for their own sake.

## Decision

Keep the proposed production split and dependency direction:

```text
App → Rendering → Core
App → Imaging → Core
```

Recorded deviations:

1. **Added `tests/VisualDiffLab.Rendering.Tests`** for `ViewTransform` and `CanvasGeometry` unit tests instead of putting rendering math tests into `Core.Tests`. `ViewTransform` lives in `Rendering` per the brief.
2. **No third-party MVVM framework.** `ObservableObject` is a small local implementation; commands are Click handlers that delegate to `MainViewModel` because file pickers and `ContentDialog` need window-bound code.
3. **Single main window.** No separate `Views/` folder: the MVP has one workspace window and one inspector. Creating user controls purely to populate folders would add indirection without reducing complexity.
4. **`Core` also contains pixel composition for exports** (`Export/DifferenceOverlayRenderer`, `SideBySideRenderer`). These are pure BGRA operations and are deterministic/testable without WIC or WinUI; only PNG encoding lives in `Imaging`.
5. **Added `tools/VisualDiffLab.Benchmark`** to reproduce CPU-bound measurements without the UI. It is not part of the app.
6. **Added `tests/VisualDiffLab.UITests`** with a `--a/--b` startup hook. The hook exists for verification and is documented as non-user-facing; it is not a product feature.
7. **Added MSIX properties conditionally** (`VisualDiffLabPackaged=true`) so the same project produces an unpackaged development build by default and a packaged build for release verification.
8. **`global.json` pins SDK 10.0.402**, installed per-user on the development machine; the brief's VS 2026 toolchain is not verified.

## Consequences

- `dotnet build` for the app requires a platform and RID (`-p:Platform=x64 -r win-x64`); plain `dotnet build VisualDiffLab.sln` without these properties is not the supported command and is documented accordingly.
- Test projects are separate so Core/Imaging/Rendering tests can run without a desktop while UI tests remain an explicit gate.
- No extra NuGet packages beyond Windows App SDK, Win2D, xUnit, test SDK, and FlaUI (UI tests only). Every package version is centrally pinned in `Directory.Packages.props`.
