# Deferred items and open product decisions

Items below are intentionally **not implemented** in the MVP. They require a product decision and must not be added opportunistically.

## Product decisions pending

- **Open-source license**: the project owner must choose a license. `LICENSE` currently contains a "not selected" notice; no permission is granted until the owner adds a license.
- **Code signing / Store identity**: the MSIX package is unsigned. Production signing, certificate ownership, Partner Center identity and Store listing are decisions for the owner.
- **Minimum OS wording**: implemented and documented as Windows 11 25H2 (build 26100). Testing on a second 26H2 machine is still required before claiming 26H2 support.

## Environment gaps to close before public release

- Clean-machine install of a **signed** MSIX (current verification uses loose-layout registration on a Developer Mode machine).
- Windows 11 **26H2** execution (host is 25H2 build 26200).
- **ARM64** build/run verification (TFM and RID are configured, not tested).
- GitHub Actions execution on GitHub (workflows authored, not run here).
- Narrator, high-contrast, and system text-scaling review; canvas label font size is currently fixed and does not scale.
- Full keyboard-only walkthrough with focus-visual screenshots (shortcuts and UIA names are implemented, but a formal a11y audit is outstanding).

## Explicit non-goals (from the brief)

- Browser automation, Playwright execution, automatic screenshots.
- AI analysis, cloud processing, accounts, sync, telemetry.
- Video/GIF timelines, animated comparisons.
- Folder batch comparison, project management, history database.
- PSD/RAW/PDF and other professional formats.
- Automatic registration (pan/zoom/rotation).
- General-purpose image editing.
- OCR, defect classification, or correctness judgments.
- Plugin/scripting/extension architecture.

## Localization (implemented scope vs deferred)

Implemented: English and Simplified Chinese in-app UI, Windows-language default, immediate switch, localized dialogs/status/errors/accessible names, key-parity validation.

Deferred:
- Persisting the language choice across launches (and persisting theme alongside it).
- Additional languages and translator review workflow.
- Localizing the MSIX shell display name/description via `.resw` resources.
- Forcing native file pickers to the in-app language (Windows controls this).
- Right-to-left layout support.

## Optional future work (not approved)

- Signed MSIX + auto-update.
- File association / "Open with" for image pairs.
- Remembering the last folder and theme between sessions.
- Side-by-side viewport splitter and independent scroll (would change the documented shared-transform behavior).
- Export of heatmap-only PNG.
- Localization infrastructure.
