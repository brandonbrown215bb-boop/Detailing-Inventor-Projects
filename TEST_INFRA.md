# E2E Test Infra: UnitProgressTracker

## Test Philosophy
- Opaque-box, requirement-driven E2E test suite.
- Derived strictly from requirements R1 through R5 in `ORIGINAL_REQUEST.md`.
- Systematic testing across Tiers 1–4 (Feature Coverage, Boundary/Corner, Cross-Feature, Real-World Application).

## Feature Inventory & Test Coverage Requirements

| # | Feature Area | Description | Tier 1 (Coverage) | Tier 2 (Boundary) | Tier 3 (Cross) | Tier 4 (Real-World) |
|---|--------------|-------------|:-----------------:|:-----------------:|:--------------:|:-------------------:|
| F1 | R1: Excel BOM & Shell Folder Engine | Native Excel import, 391 part mapping, skid sequence, misplaced coils, excluded parts, WPF BOM table & Shell Folder Engine | 5 | 5 | ✓ | ✓ |
| F2 | R2: Atomic Project File (.uptproj) | ProjectStateModel, atomic save/load, auto-save timer, recent projects, dirty state, surface renumbering history | 5 | 5 | ✓ | ✓ |
| F3 | R3: Surface Audit & MD Export | Custom status state manager, per-surface checklists, notes, surface visibility toggling, MarkdownExporter | 5 | 5 | ✓ | ✓ |
| F4 | R4: Async IAM File Scanner | Async InventorComReader & GeometryScanner, WPF progress bar overlay, cancellation support | 5 | 5 | ✓ | ✓ |
| F5 | R5: Interactive 3D Viewport | HelixToolkit selection sync, custom billboard stickers, opacity slider, wireframe toggle, zoom extents reset | 5 | 5 | ✓ | ✓ |

## Test Runner Strategy
- Framework: xUnit / NUnit / MSTest under .NET 8 (`dotnet test`)
- Target Project: `Pigeon/UnitProgressTracker/tests/UnitProgressTracker.Tests/UnitProgressTracker.Tests.csproj`
- Invocation Command: `dotnet test Pigeon/UnitProgressTracker/tests/UnitProgressTracker.Tests/UnitProgressTracker.Tests.csproj --configuration Release`
- Pass Condition: 100% tests pass with exit code 0.

## Tier Breakdown & Goals
- **Tier 1**: 25 happy-path test cases (5 per feature F1–F5).
- **Tier 2**: 25 boundary/edge/corner test cases (5 per feature F1–F5).
- **Tier 3**: 5 cross-feature combination test cases (pairwise interaction flows).
- **Tier 4**: 5 real-world application E2E scenario test cases.
- **Total Minimum**: 60 test cases.
