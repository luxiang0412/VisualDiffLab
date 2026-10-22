# ADR-0001: UI framework

- Status: Accepted
- Date: 2026-10-22
- Deciders: implementation agent, pending product-owner review

## Context

The brief requires a native Windows desktop application with Fluent Design, four comparison modes, a canvas-first layout, accessibility, light/dark/system themes, and a supported packaging path. The reference baseline is Windows 11 25H2+, .NET 10 LTS, Windows App SDK 2.5.1, and WinUI 3.

The actual development machine did **not** have Visual Studio 2026 or any .NET SDK installed. .NET SDK 10.0.402 was installed per-user. Visual Studio 2022 17.8.3 was present but was not used for the build. Windows 11 build 26200 (25H2) is the only OS available for runtime verification.

## Decision

Use **WinUI 3 + XAML** with **Windows App SDK 2.5.1**, targeting `net10.0-windows10.0.26100.0` and minimum OS `10.0.26100.0`:

- Single main window; native title bar (not custom-drawn).
- `CommandBar` (with dynamic overflow), `SelectorBar` for modes, inspector panel, status bar.
- MVVM-lite: `MainViewModel` + `INotifyPropertyChanged`; no third-party MVVM framework. Canvas interaction and platform dialogs live in the window code-behind because they are inherently platform-bound.
- `FileOpenPicker`/`FileSavePicker` initialized with the window handle for unpackaged and packaged runs.
- Theme via `ElementTheme` on the root element with System/Light/Dark options; native title bar theme follows where supported.

## Alternatives considered

- **WPF**: mature, but does not meet the Fluent/WinUI platform requirement and would make the Windows 11 visual acceptance substantially weaker.
- **Avalonia**: cross-platform, but the brief explicitly targets native Windows/Fluent and WinUI 3.
- **WinUI 2 / UWP**: outdated for this product; desktop packaging and file-system access are less direct.
- **Custom title bar**: rejected; the brief requires native window behavior unless justified.

## Consequences

- The app is Windows 11 25H2+ only. That matches the product support policy.
- Unpackaged development builds require the Windows App SDK runtime or self-contained deployment. Builds are self-contained in this repository.
- WinUI 3 requires an interactive desktop; UI tests cannot run on headless CI.
- The lack of Visual Studio 2026 did not block building, but the exact VS 2026 toolchain from the brief is not verified.

## Evidence

- `artifacts/phase-0/spike-win2d.png` (WinUI 3 + Win2D window)
- `artifacts/acceptance/phase-2-side-by-side-light.png`
- `artifacts/acceptance/phase-4-narrow-window.png`
- UI tests in `tests/VisualDiffLab.UITests`
