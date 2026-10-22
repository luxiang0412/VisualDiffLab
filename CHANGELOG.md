# Changelog

All notable changes are recorded here. The project is pre-1.0; behavior may still change after product-owner review.

## [0.1.0] — 2026-10-22

### Added

- Native WinUI 3 workspace for comparing two local screenshots.
- PNG/JPEG/BMP decoding (EXIF orientation, ICC→sRGB, premultiplied BGRA8).
- Comparison canvas with max-dimension semantics and strict per-channel threshold (0–64).
- Side-by-side, Wipe, Overlay and Difference heatmap modes with shared zoom/pan, Fit and true 100%.
- Statistics: different pixels, compared pixels, ratio (`N/A` when nothing compared), ignored pixels, canvas coverage.
- Ignore rectangles in image coordinates with selection, delete and `Ctrl+Z` undo.
- Difference Overlay and Side-by-side PNG export with size confirmation and native save dialog.
- Light, Dark, Follow system themes; native title-bar theme follow where supported.
- English and Simplified Chinese UI with an in-session language switch (Follow system / English / 中文), 171 localized strings, and a key-parity validator. Language defaults to the Windows preferred language; it is not persisted across launches.
- Keyboard shortcuts (`Ctrl+O`, `Ctrl+E`, `1`–`4`, `F`, `0`, `Space+drag`, `Esc`, `Ctrl+Z`, `Delete`, Left/Right in Wipe).
- Diagnostic log with no paths/pixels; error dialogs for corrupt/unsupported/inaccessible files.
- 71 deterministic unit/integration tests and 8 real UI-automation tests.
- MSIX packaging (unsigned), loose-layout install/launch/upgrade/uninstall verification.
- Documentation: algorithm rules, ADRs, design notes/references, phase reports, privacy, acceptance guide, CI workflows.

### Known limitations

- License not selected by the owner.
- MSIX unsigned; signed clean-machine install and Store submission not performed.
- Windows 11 26H2 and ARM64 NOT RUN.
- Narrator/high-contrast/text-scaling audit outstanding; canvas chip text does not scale.
- GitHub Actions authored but not executed on GitHub.
