# ADR-0003: UI automation approach

- Status: Accepted
- Date: 2026-10-22

## Context

The brief asks for real UI automation of the core workflow and prefers evaluating **Appium Windows Driver**. Microsoft documentation notes that the classic **WinAppDriver is no longer actively developed**. The product also requires a high-DPI desktop, native file dialogs, and a WinUI 3 window.

## Decision

Use **FlaUI 5.0.0 + UIA3** in `tests/VisualDiffLab.UITests` to drive the real WinUI automation tree:

- Launch the real Release executable with the `--a`/`--b` test hook.
- Use UIA patterns (`SelectionItem`, `RangeValue`, `Invoke`, `Toggle`) for modes, sliders and buttons.
- Use real mouse input for canvas drags (ignore regions) and real keyboard input for shortcuts.
- Navigate the native **Save As** common dialog (hosted as a `#32770` child window inside the WinUI window) via UIA, then verify the exported PNGs by decoding them with the product's own `ImageLoader`.
- Capture evidence with DWM extended-frame screenshots (DPI-aware test host).
- Read the app's own diagnostics log for first-frame mode switch and comparison timings.

Tests are opt-in and require an interactive desktop. They are not part of the default CI job.

## Why not Appium Windows Driver

- It depends on WinAppDriver, which Microsoft no longer actively develops.
- It adds a Node/Appium server and driver installation to every test machine.
- The team already needs a Windows desktop session for WinUI; UIA3 has no additional runtime dependency.
- FlaUI's UIA3 provides the same underlying Windows UI Automation tree and supports the native file dialog.

This is a recorded deviation from the brief's preference, based on maintenance status and environment constraints. If Appium becomes required later, the existing scenario and assertions should be ported rather than duplicated.

## Important test-host detail

UIA geometry and mouse coordinates are only reliable when the test process is **per-monitor DPI aware**. `UiTestBootstrap` calls `SetProcessDpiAwarenessContext(PER_MONITOR_AWARE_V2)` in a module initializer. Without it, rectangles are virtualized and clicks land in the wrong place (observed during development).

## Limitations / NOT RUN

- UI tests were run locally on the interactive desktop, not in GitHub Actions.
- They are not executed on headless hosted runners; the workflow template is a separate, self-hosted/desktop gate (`docs/testing/manual-acceptance.md`).
- File-picker automation covers the save dialog; the open dialog is exercised manually (startup hook used for tests).
- Narrator/high-contrast automated checks are not implemented.

## Evidence

- `tests/VisualDiffLab.UITests/CoreWorkflowUiTests.cs`
- `tests/VisualDiffLab.UITests/ExportUiTests.cs`
- `tests/VisualDiffLab.UITests/EmptyStateUiTests.cs`
- `tests/VisualDiffLab.UITests/PerformanceUiTests.cs`
- `artifacts/acceptance/`
