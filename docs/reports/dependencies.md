# Dependency review

Date: 2026-10-22
Tooling: `dotnet list VisualDiffLab.sln package --vulnerable --include-transitive` and `--outdated`
Raw local output (contains machine paths, do not commit): `artifacts/dependency-audit.txt`

## Direct packages

| Package | Version | License | Used by | Purpose / alternative |
|---|---|---|---|---|
| Microsoft.WindowsAppSDK | 2.5.1 | Microsoft license | App, Rendering | WinUI 3 runtime/controls. Required by the stated stack; no practical alternative for WinUI 3. |
| Microsoft.Graphics.Win2D | 1.4.0 | Microsoft license | App, Rendering | Immediate-mode canvas/heatmap/selection rendering. Fallback documented in ADR-0002. |
| Microsoft.Windows.SDK.BuildTools | 10.0.28000.2705 | Microsoft license | App, Rendering | Windows SDK projections/MSIX tooling. Required by Windows App SDK. |
| Microsoft.NET.Test.Sdk | 17.14.1 | MIT | tests | VSTest host/TRX logger. Deliberately not upgraded to 18.x (kept aligned with xUnit v2/VSTest). |
| xunit | 2.9.3 | Apache-2.0 | tests | Test framework. |
| xunit.runner.visualstudio | 3.1.5 | Apache-2.0 | tests | VSTest adapter. 4.x targets xUnit v3; not needed. |
| FlaUI.UIA3 | 5.0.0 | MIT | UITests only | Windows UI Automation wrapper. Chosen over Appium/WinAppDriver (ADR-0003). Test-only, never shipped. |

Transitive Windows App SDK components (Base, Foundation, InteractiveExperiences, WinUI, DWrite, Widgets, AI, ML, Search, Runtime) and `Microsoft.Windows.SDK.BuildTools.MSIX` are pulled by `Microsoft.WindowsAppSDK`; all are Microsoft packages with the same terms.

There are **no npm/Node dependencies**. Node.js is not required to build/test the product.

## Vulnerability audit

Command (run 2026-10-22 against nuget.org):

```powershell
dotnet list VisualDiffLab.sln package --vulnerable --include-transitive
```

Result: **no vulnerable packages** reported for any of the nine projects (Core, Imaging, Rendering, App, four test projects, Benchmark), including transitive dependencies.

## Outdated audit

Only test infrastructure is behind:

- `Microsoft.NET.Test.Sdk` 17.14.1 → 18.11.0
- `xunit.runner.visualstudio` 3.1.5 → 4.0.1

Rationale for staying: the suite is on xUnit v2 + VSTest; the 4.x runner targets xUnit v3 and would require a coordinated test-framework migration. Production packages, Windows App SDK, Win2D and BuildTools are current as of the audit date.

## License status

- The repository license is **not selected** by the project owner; see `LICENSE`.
- All third-party packages used in the shipped app are Microsoft-licensed (Windows App SDK / Win2D / BuildTools). Test-only packages are MIT/Apache-2.0.
- Before public distribution, the owner must choose the project license and review the Microsoft terms for redistribution of self-contained Windows App SDK/Win2D binaries.

## Risks

- Win2D is maintained separately from Windows App SDK; version 1.4.0 declares a WinUI 1.8 dependency but resolves/works with WinUI 2.3.9. Future upgrades must re-run the spike, build and UI tests (ADR-0002).
- Self-contained MSIX is large (~96 MB) because the Windows App SDK/Win2D runtime and resources are included.
- Pinned versions are intentional; do not float them without re-running the full test/performance suite.
