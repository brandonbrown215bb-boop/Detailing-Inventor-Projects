# UnitProgressTracker Subproject Conventions & Grounding

## Architecture & Responsibilities
- **Core Domain (`UnitProgressTracker.Core`)**: `.NET 8.0`. Contains domain models (`SurfaceModel`, `SurfaceRecordModel`), services (`AsyncGeometryScanner`, `BomShellEngine`, `ExcelBomImporter`, `MarkdownExporter`, `ProjectStateService`, `StatusStateManager`).
- **WPF Application (`UnitProgressTracker.Wpf`)**: `.NET 8.0-windows`. Modern dark-theme WPF UI (`DarkTheme.xaml`) using `HelixToolkit.Wpf` (v3.1.2) for 3D viewport rendering.
- **Test Suite (`UnitProgressTracker.Tests`)**: xUnit test suite for core domain, scanners, exporters, and viewmodels.

## Grounded Preferences & Invariants
- **Modular Design & Single Responsibility**: Favor lightweight, testable, decomposed modules over monolithic code. Keep scanners, exporters, importers, state managers, and viewmodels strictly separated by responsibility.
- **UI & Experience**: Enforce local-first dark theme styling. Long-running assembly scanning must execute asynchronously off the UI thread with explicit progress reporting (`IProgress<int>`, `ProgressBar` overlay) and cancellation support (`CancellationToken`).
- **Validation Standard**: Mandate 100% test pass rate (341+ tests passing, 0 warnings/errors) via `dotnet test` before marking quest checkpoints or completing tasks.
- **COM Safety**: Ensure Inventor COM interop handles null/missing documents gracefully (`InventorComReader.cs`).
- **Derived Indexes**: Treat `.codegraph/` and semantic caches as disposable rebuildable indexes; source code and unit tests remain the single source of truth.
