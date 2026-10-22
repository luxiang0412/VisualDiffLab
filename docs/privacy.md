# Privacy

Visual Diff Lab is designed as a local-first desktop tool.

## What is processed

- Images you explicitly open are decoded and compared **on your device**.
- No image, pixel data, histogram, filename or file path is transmitted anywhere by the application.
- There is no account, login, sync, telemetry, analytics, crash upload or background network access in the product code.

## What is stored

- **Nothing persistent by default.** The app does not keep project files, history, recent-file lists or settings between launches.
- **Exports** are written only when you choose a location in the native save dialog.
- **Diagnostics log**: `%LOCALAPPDATA%\VisualDiffLab\logs\app.log`, capped at 2 MB with one rotation. Each line contains:
  - timestamp, level, operation type (startup, image decoded, comparison, export, error),
  - image **format and dimensions**, durations, counts,
  - exception type and HRESULT on failure.

  The log deliberately does **not** contain file names, full user paths, pixel data, image contents or exported file names.
- The app does not modify input files. Exports never overwrite an input unless you explicitly choose the same path and confirm the overwrite in the system dialog.

## Windows and third-party behavior

- File dialogs are provided by Windows. If you browse or open a file from a cloud provider (OneDrive, network share, etc.), the **operating system or provider** may download/synchronize it; that is outside this app.
- The packaged build uses the Windows App SDK and Win2D runtime components. They do not add telemetry to this app.
- No administrator privileges are requested.

## Removing local data

Close the app and delete `%LOCALAPPDATA%\VisualDiffLab` to remove diagnostics logs. Exported images remain wherever you saved them.

## Changes

This document must be updated if telemetry, accounts, cloud processing, automatic updates or any network feature is ever proposed. Such features require an explicit product decision and are not part of the MVP.
