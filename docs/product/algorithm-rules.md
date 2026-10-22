# Comparison and export rules

Status: implemented and covered by deterministic tests. This document is the source of truth for behavior; if code and this document disagree, that is a bug.

## 1. Input normalization

- Supported source formats: **PNG, JPEG, BMP**. Other codecs (GIF, TIFF, HEIF, WebP, …) are rejected with an `Unsupported image format` message even if Windows Imaging Component can decode them.
- Decoding uses Windows Imaging Component (`BitmapDecoder`):
  - pixel format: **BGRA8**, row-major, top-down;
  - alpha: **premultiplied**;
  - color management: **ColorManageToSRgb**;
  - EXIF orientation: **RespectExifOrientation** (verified with an orientation-6 JPEG: 8×4 becomes 4×8).
- Treated as normalized sRGB. No color-profile metadata is preserved in comparisons or exports.
- Source files are read-only and never modified.
- Limits: 16,384 px per side, 80,000,000 decoded pixels per image. Comparison canvases are limited to 100,000,000 pixels. Inputs beyond the limits produce a user-facing error instead of an out-of-memory crash.
- Premultiplied alpha is deliberate: fully transparent pixels have zeroed RGB and cannot create false differences from hidden color values. The test suite covers this.

## 2. Comparison canvas

For image A (`WA × HA`) and image B (`WB × HB`):

```text
W = max(WA, WB)
H = max(HA, HB)
```

- Both images are aligned at canvas `(0,0)`.
- Images are never scaled, shifted, rotated or registered automatically.
- For each canvas pixel:
  - both images have a pixel → compare channels;
  - only A has a pixel → `OnlyInA` (removed), counted as different;
  - only B has a pixel → `OnlyInB` (added), counted as different;
  - the canvas never contains a position where neither image has a pixel.
- Ignore regions are applied in canvas coordinates. Overlapping regions use their union; pixels are counted as ignored once.
- `ComparedPixelCount` = canvas pixels not covered by any ignore region.
- `DifferentPixelCount` = compared pixels classified `Changed`, `OnlyInA` or `OnlyInB`.
- `DifferenceRatio = DifferentPixelCount / ComparedPixelCount`. If `ComparedPixelCount == 0`, the UI shows **N/A** and no division occurs.

## 3. Pixel difference rule

- Channels compared: **B, G, R, A** after normalization.
- `maxDelta = max(|ΔB|, |ΔG|, |ΔR|, |ΔA|)`.
- A pixel is different when `maxDelta > threshold` (strictly greater).
- Threshold range: **0–64** in 8-bit channel units. Default: **16**.
- Threshold 0 marks any non-zero normalized channel difference.
- Classification per pixel (one byte):
  - `Same = 0`
  - `Changed = 1`
  - `OnlyInA = 2`
  - `OnlyInB = 3`
  - `Ignored = 4`

Determinism: the engine is single-threaded and uses no randomness or locale-dependent operations. Repeated runs with the same inputs and options produce byte-identical classification masks.

## 4. Ignore regions

- Created by dragging in region mode; corners are normalized and clipped to the canvas.
- Stored in **comparison-canvas pixels**, never screen pixels. Zoom, pan, DPI changes and window resizing therefore cannot move a region relative to the image.
- Overlaps are allowed; the mask uses the union.
- Removing/adding supports undo (last operation, LIFO). `Ctrl+Z` undoes the most recent region add or delete.
- Regions affect statistics, the heatmap and both exports. They do not modify source images.
- A drag with zero area produces no region and leaves no pending selection.

## 5. Fixed colors

| Meaning | Color | Overlay alpha |
|---|---|---|
| Changed pixels | `#E3008C` (magenta) | 217/255 ≈ 85% |
| Only in B (added) | `#00A2FF` (blue) | 217/255 |
| Only in A (removed/missing) | `#FFB900` (amber) | 217/255 |
| Difference-export base background | `#1F1F1F` | opaque |
| Side-by-side background | `#2B2B2B` | opaque |
| Side-by-side label bar | `#3A3A3A` | opaque |

Changed colors are composited over image A, so the final exported pixel is not a single constant RGB. The fixed source color and 85% alpha are constants in `DiffColors` and are covered by export tests.

Ignored pixels are always rendered/exported as the plain A base (or neutral background where A has no pixel).

## 6. Exports

### Difference overlay PNG

- Size: comparison canvas `W × H`.
- Base: image A, flattened over `#1F1F1F` when A is transparent.
- Changed pixels: magenta at 85% over the base.
- B-only pixels: added blue.
- A-only pixels: removed amber.
- Ignored pixels: no diff color.
- Output is opaque BGRA8 and contains no window chrome, cursor or toolbar.
- Pixel composition is done by `Core` (`DifferenceOverlayRenderer.ComposeExport`) and encoded with WIC PNG; the same classification mask used for stats and the heatmap is used here.

### Side-by-side PNG

- Size: `WA + 16 + WB` wide, `32 + max(HA, HB)` high.
- 16 px neutral gap between images; 32 px label bar with fixed bitmap glyphs `A` and `B`; no font dependency.
- Natural pixel sizes; images are not stretched. Missing areas use `#2B2B2B`.
- Transparent image pixels are flattened over `#2B2B2B`.
- Limit: 120,000,000 output pixels; larger outputs are rejected before allocation with a user-facing error.

Both exports show `width × height px` in a confirmation dialog before the save picker opens. A failed save preserves the comparison and allows retry.

## 7. View transform

- `Zoom` is measured in **viewport DIPs per image pixel**.
- `100%` means one image pixel per **physical screen pixel**: `Zoom = 96 / rasterizationScale` (e.g. 0.5 at 200% DPI). The status bar displays the physical ratio, so it reads 100% at this setting.
- `Fit` uses 24 DIP padding, centers the comparison canvas and clamps zoom to `[0.01, 32]`.
- Wheel zoom keeps the image point under the pointer fixed, subject to pan clamping.
- When content is smaller than the viewport it is centered; otherwise panning is clamped so content cannot be completely lost.
- Side-by-side uses one shared transform with two viewports; A is drawn in the left viewport and B in the right with the same zoom and offset. Regions are drawn in both halves.
- Mode changes preserve the image point at the viewport center where possible.
- Coordinates rendered and coordinates hit-tested use the same `ViewTransform`, so overlays and selections cannot drift.

## 8. Asynchronous comparison

- Comparisons run on the thread pool with a monotonically increasing version and a cancellation token.
- Threshold changes are debounced by 120 ms.
- A result is applied only if its version is still current. Cancelled/stale results never overwrite newer statistics.
- While a comparison is running, statistics show placeholders (`Comparing…`) and export is disabled, so old numbers cannot masquerade as new ones.
- Heatmap upload is only performed while Heatmap mode is visible; leaving Heatmap releases its overlay buffer and bitmap.

## 9. Test vectors

Generated in code (no visual judgment required):

- identical images; one-pixel difference;
- difference exactly equal to the threshold (same) and exactly one above (different);
- largest channel decides;
- transparent pixels with different hidden RGB (same after premultiplication);
- alpha differs with same RGB (different);
- A larger than B and B larger than A (added/removed counts);
- 0×0 input rejected; buffer-size mismatch rejected;
- ignore region covering the whole canvas (ratio N/A);
- partial, out-of-bounds, overlapping ignore regions;
- extreme aspect ratio and boundary pixels;
- deterministic repeat; cancellation;
- corrupt files, unsupported GIF, locked file, missing file, 4K decode;
- export colors, ignored suppression, side-by-side padding/labels, size limits.
