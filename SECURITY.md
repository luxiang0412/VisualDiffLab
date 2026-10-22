# Security policy

## Supported versions

The MVP is version `0.1.x`. No public signed release exists yet; security fixes will be applied to the latest source on the default branch.

## Reporting a vulnerability

Do **not** open a public issue for a security problem. Contact the project maintainers privately with:

- affected version/commit,
- a description of the issue and impact,
- reproduction steps or a proof of concept,
- any suggested mitigation.

A public contact address and PGP key must be added by the project owner before public release. Until then, report through the private channel used to deliver this repository.

## Scope and threat model

Visual Diff Lab is a local desktop application that opens untrusted image files. Areas of interest:

- **Image decoding**: uses Windows Imaging Component (WIC) through `BitmapDecoder`; only PNG/JPEG/BMP codecs are accepted. Decoded size is limited (16,384 px per side, 80 M pixels) and comparison canvas to 100 M pixels to avoid memory-exhaustion crashes.
- **Local processing only**: no network, telemetry, accounts, cloud sync or upload code paths.
- **File access**: files are read only; exports are written only to the location chosen in the native save dialog.
- **Logs**: operation type, format/dimensions, durations and exception type. No pixel data and no full user paths. Logs live in `%LOCALAPPDATA%\VisualDiffLab\logs`.
- **No elevation**: the app does not request administrator privileges.

Out of scope for this project: vulnerabilities in Windows/WIC itself, third-party cloud providers shown by the file picker, and issues caused by running forged binaries.

## Dependency policy

NuGet versions are pinned centrally and checked with `dotnet list package --vulnerable --include-transitive`; results are recorded in `artifacts/dependency-audit.txt`. Do not ignore a reported vulnerability without a written risk decision.
