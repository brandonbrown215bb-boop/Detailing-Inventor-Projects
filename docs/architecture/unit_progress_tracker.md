---
kind: architecture
verified_at_commit: 1ef64f8da50d760a714c84045c2afa53a774b29b
scope:
  - Pigeon/UnitProgressTracker/src/**
---

# UnitProgressTracker Architecture

## Purpose
`UnitProgressTracker` is a WPF application and domain core library for reading Inventor `.iam` assembly geometry, matching surface bounding boxes with BOM item shells, tracking progress states, and generating markdown audit reports.

## Boundaries & Modules

### 1. Core Domain (`UnitProgressTracker.Core`)
- **`AsyncGeometryScanner`**: Asynchronously traverses Inventor `.iam` assemblies or fallback mock structures with `Task<List<GeometryBox>>`, `CancellationToken`, and progress reporting (`IProgress<int>`).
- **`BomShellEngine`**: Matches geometry bounding boxes with imported BOM items.
- **`ExcelBomImporter`**: Imports Excel BOM spreadsheets using `ExcelDataReader`.
- **`MarkdownExporter`**: Generates markdown audit summary reports of surface completion states.
- **`StatusStateManager` / `ProjectStateService`**: Manages custom surface status workflows and persistence.

### 2. Desktop Interface (`UnitProgressTracker.Wpf`)
- **`MainViewModel`**: MVVM state container handling scan execution, surface filtering, status edits, and report generation.
- **`Surface3DViewport`**: Interactive 3D view using `HelixToolkit.Wpf` with dark theme styling, opacity slider, wireframe toggle, and selection highlights.
- **`DarkTheme.xaml`**: Modern dark palette design system.

### 3. Test Suite (`UnitProgressTracker.Tests`)
- Comprehensive xUnit test suite (341+ passing tests) validating scanning, matching, state persistence, exporters, and viewmodel bindings.

## Invariants & Controls
- **Local-First**: Works without external network or cloud dependencies.
- **COM Interop Safety**: Missing or uninitialized Inventor COM handles fall back gracefully to mock scanners.
- **Async Execution**: Long-running scans execute off the main WPF UI thread with cancellation support.
