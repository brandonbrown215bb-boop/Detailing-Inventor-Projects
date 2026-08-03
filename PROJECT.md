# Project: UnitProgressTracker Porting Project

## Architecture
- Module/package boundaries, data flow, shared interfaces
- `UnitProgressTracker.Core` (net8.0): Domain models (`BomRow`, `SurfaceModel`, `StatusState`, `GeometryBox`, `ProjectStateModel`), services (`ExcelBomImporter`, `BomShellEngine`, `ProjectSerializer`, `GeometryScanner`, `InventorComReader`, `MarkdownExporter`).
- `UnitProgressTracker.Wpf` (net8.0-windows): ViewModels (`MainViewModel`, `StatusStateEditorViewModel`), Views (`MainWindow.xaml`, `StatusStateEditorDialog.xaml`), Controls (`Surface3DViewport.cs`), Themes, Converters.
- `UnitProgressTracker.Tests` (net8.0 xUnit): Test project for Core models, services, BOM parsing, project state serialization, async scanner, viewport logic, and E2E integration.

## Feature Inventory
| # | Feature | Description | Milestone | Source |
|---|---------|-------------|-----------|--------|
| 1 | Excel BOM Import (.xlsx/.csv) | Native C# Excel reader using ClosedXML/ExcelDataReader for 11-col BOM | M1 | R1 |
| 2 | Prefix Tier & Excluded Filter | Filter 391/291/etc, drop hardware/factors, filter 9 exclusion patterns | M1 | R1 |
| 3 | Skid Sequence & Misplaced Panels | Parse [FR-MB] reversed order, flag <-- misplaced coil lines, SQ doors | M1 | R1 |
| 4 | WPF BOM DataGrid & Folder Engine | WPF BOM view, manual row edit, folder plan preview, shell folder creation | M1 | R1 |
| 5 | ProjectStateModel & Atomic Save | Schema v2 project state serialization via ProjectSerializer.cs atomic write | M2 | R2 |
| 6 | Auto-Save & Dirty State Tracking | IsDirty flag, auto-save timer, recent projects (MRU) list, File menu | M2 | R2 |
| 7 | Geometry Fingerprint & History | Renumbering candidate auto-suggest, retired surface history tracking | M2 | R2 |
| 8 | Dynamic Status State Manager | Custom colors, fillType (solid vs wireframe), status state editor UI | M3 | R3 |
| 9 | Audit Checklist & Notes | Per-surface interactive checklist bindings, multiline notes editor | M3 | R3 |
| 10 | Surface Visibility & Markdown Export | Hide/show surface toggling, MarkdownExporter.cs report generator | M3 | R3 |
| 11 | Async Scanner Engine | Async ScanIamFolderAsync / ScanIamFileAsync in GeometryScanner & COM reader | M4 | R4 |
| 12 | WPF Progress Bar Overlay | Modal/status progress bar, 0-100%, cancellation token support | M4 | R4 |
| 13 | Viewport Pick & Highlight Sync | Bidirectional surface selection sync between list/3D viewport | M5 | R5 |
| 14 | 3D Billboard Text Stickers | Render short label stickers above surface bounding boxes | M5 | R5 |
| 15 | Viewport Opacity & Wireframe | Slider opacity control, global/per-surface wireframe & fill modes | M5 | R5 |
| 16 | Camera Zoom Extents Reset | Fit View and Reset Camera extents animation | M5 | R5 |

## Milestones
| # | Name | Scope | Dependencies | Status |
|---|------|-------|-------------|--------|
| M1 | Excel BOM Import & Shell Engine | Native Excel reader, BOM filtering, skid sequence, WPF BOM DataGrid & Shell Engine | none | IN_PROGRESS |
| M2 | Atomic Project File (.uptproj) & History | ProjectStateModel, atomic save, IsDirty/auto-save, MRU, surface renumbering history | M1 | PLANNED |
| M3 | Audit Checklist, Status & Markdown Export | Dynamic status state editor, interactive checklist bindings, visibility toggle, MarkdownExporter | M2 | PLANNED |
| M4 | Async Inventor IAM File Scanner | Async scanning engine, WPF progress bar overlay, CancellationToken support | M2 | PLANNED |
| M5 | Interactive WPF 3D Viewport Enhancements | Selection highlight sync, 3D billboard stickers, opacity slider, wireframe toggle, camera reset | M3 | PLANNED |

## Interface Contracts
### Core Services ↔ WPF ViewModels
- `ExcelBomImporter`: `BomPlanImportResult ImportBom(string filePath)`
- `BomShellEngine`: `ShellFolderPlan BuildPlan(IEnumerable<BomRow> rows, string skidSequenceOverride)`
- `ProjectSerializer`: `void SaveAtomic<T>(string filePath, T data)`, `T Load<T>(string filePath)`
- `GeometryScanner`: `Task<List<SurfaceModel>> ScanIamFolderAsync(string folderPath, IProgress<ProgressReport> progress, CancellationToken cancellationToken)`
- `MarkdownExporter`: `string ExportToMarkdown(ProjectStateModel project)`

## Code Layout
`Pigeon/UnitProgressTracker/`
- `src/UnitProgressTracker.Core/`
  - `Models/` (`BomRow.cs`, `SurfaceModel.cs`, `StatusState.cs`, `GeometryBox.cs`, `ProjectStateModel.cs`, `RenumberHistoryRecord.cs`)
  - `Services/` (`BomShellEngine.cs`, `ExcelBomImporter.cs`, `ProjectSerializer.cs`, `GeometryScanner.cs`, `InventorComReader.cs`, `MarkdownExporter.cs`)
- `src/UnitProgressTracker.Wpf/`
  - `ViewModels/` (`MainViewModel.cs`, `StatusStateEditorViewModel.cs`)
  - `Views/` (`MainWindow.xaml`, `StatusStateEditorDialog.xaml`)
  - `Controls/` (`Surface3DViewport.cs`)
  - `Themes/` (`DarkTheme.xaml`)
  - `ValueConverters.cs`
- `tests/UnitProgressTracker.Tests/`
  - `BomShellEngineTests.cs`, `ExcelBomImporterTests.cs`, `ProjectSerializerTests.cs`, `GeometryScannerTests.cs`, `MarkdownExporterTests.cs`, `E2ETests.cs`
