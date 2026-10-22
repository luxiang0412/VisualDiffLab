# ADR-0002: Canvas rendering

- Status: Accepted
- Date: 2026-10-22
- Deciders: implementation agent, pending product-owner review

## Context

The product needs a large canvas with four modes, smooth zoom/pan, a difference heatmap, ignore-region overlays, and high-DPI behavior. Candidate approaches:

1. Win2D `CanvasControl` immediate-mode drawing.
2. XAML `Image` elements with transforms, clipping and `Canvas` overlays.
3. Direct2D/DirectWrite interop.

## Decision

Use **Win2D 1.4.0 `CanvasControl`** for the comparison surface, with a Win2D `CanvasDrawingSession` renderer in `VisualDiffLab.Rendering`:

- Decoded images are uploaded once as `CanvasBitmap` (`B8G8R8A8UIntNormalized`, DPI 96).
- All four modes are drawn immediately, sharing `ViewTransform` and `CanvasGeometry`.
- The heatmap overlay is a premultiplied BGRA bitmap built from the same `DiffResult` mask used for statistics and exports.
- The overlay bitmap and its scratch buffer are reused across threshold changes and rebuilt only while Heatmap mode is visible; this keeps 4K working set under the target.
- Device-lost handling recreates bitmaps in `CreateResources` from the retained `ImageDocument` pixels and current diff.
- 100% is defined as one image pixel per physical screen pixel using `rasterizationScale` (verified at 200% DPI).

## Why not the alternatives

- **XAML Image elements**: viable, but wipe clipping, heatmap overlay upload, precise hit-testing, and side-by-side shared transforms would require more composition/visual-tree churn. Win2D gives a single coordinate system and direct control over the exact pixels.
- **Direct2D interop**: more native interop and lifetime risk than needed; Win2D already wraps the same rendering stack.

## Compatibility finding

Win2D 1.4.0 (`Microsoft.Graphics.Win2D`) declares a dependency on `Microsoft.WindowsAppSDK.WinUI 1.8.x`; with Windows App SDK 2.5.1 the WinUI package resolves to 2.3.9. A minimal spike plus the full app built and ran successfully on this combination with Windows 11 25H2. NuGet versions are centrally pinned in `Directory.Packages.props`.

Risk: Win2D is maintained separately from Windows App SDK. Future Windows App SDK upgrades must re-run the spike and the UI test suite; do not upgrade blindly. If Win2D becomes incompatible, the documented fallback is XAML `Image` rendering with `SoftwareBitmapSource`, accepting more UI-layer complexity.

## Consequences

- Rendering is GPU-accelerated and stays responsive at 4K (mode switch first frame max 16 ms measured).
- The app must handle canvas device loss, DPI changes, and explicit disposal on close; this is implemented in `CanvasInteraction.cs`.
- Rendering code is Windows-only and therefore tested by rendering/geometry unit tests plus real UI screenshots, not a headless renderer test.
- The canvas text labels use a fixed 12 px Win2D text format; system text scaling does not resize these labels (known accessibility limitation, recorded in the release readiness report).

## Evidence

- `artifacts/phase-0/spike-win2d.png`
- `docs/reports/performance.md`
- `tests/VisualDiffLab.Rendering.Tests`
- `tests/VisualDiffLab.UITests/PerformanceUiTests.cs`
