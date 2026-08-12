---
kind: architecture
scope:
  - Pigeon/UnitProgressTracker/src/**
---

# UnitProgressTracker Architecture

## Purpose

`UnitProgressTracker` is a local-first WPF tool for detailers. It reads Inventor
assembly geometry, embeds a portable snapshot in a version 4 `.uptproj`, tracks
surface work and BOM edits, and keeps that saved work usable when the source
assembly is unavailable.

The approved behavior and ownership rules live in
`Pigeon/UnitProgressTracker/DETAILER_WORKFLOW_CONTRACT.md`. ADR 0002 records the
version 4 persistence decision. Source and tests remain authoritative.

## Boundaries

### Core domain and persistence

`UnitProgressTracker.Core` owns JSON-safe models and deterministic services. The
important seams are:

- `ProjectSerializer` validates the canonical format/version and performs atomic
  persistence. Unsupported Pigeon v2, Esmund v3, corrupt, and newer files return
  typed load failures rather than partially replacing the active project.
- `ProjectStateModel` is the project aggregate: embedded geometry, tracking,
  retired records, project status definitions, checklist template/results, BOM,
  unit configuration, provenance, unresolved intrusion flags, project display
  choices, and camera state.
- `ProjectStateService` owns state-preserving surface lifecycle transactions:
  checklist synchronization, reviewed renumber transfer, replacement, add, retire,
  and restore.
- `GeometryScanner` reports accepted, skipped, and failed source files. Inventor COM
  and Apprentice access are external adapters; their lifecycle still requires a
  supported-workstation smoke test.
- `RescanReconciler`, `GeometryFingerprinter`, and
  `GeometryIntrusionChecker` propose identity and geometry changes. Ambiguous
  renumbers require confirmation. Intrusions warn and persist without blocking.
- BOM import, shell planning, and markdown export remain core services so they can
  be tested without the WPF shell.

### Desktop orchestration

`UnitProgressTracker.Wpf` owns dialogs, command routing, and rendering.

- `MainViewModel` coordinates the current aggregate. It applies search, status,
  visibility, sort, and grouping as one non-mutating projection shared by the list
  and viewport. Project mutations mark the aggregate dirty.
- `SurfaceModel` notifies visibility changes. `SurfaceGroupViewModel` derives
  visible, hidden, or mixed group state and delegates persistence/dirty handling
  back to `MainViewModel`.
- `OptionsViewModel` edits deep copies. `MainViewModel.ApplyOptions` is the commit
  boundary: project-owned display/status/checklist values enter the project,
  application-owned theme values enter `AppSettings`, and runtime callbacks update
  the shell only after Save.
- `Surface3DViewport` renders the filtered surface projection, raises selection,
  hover, and camera-change events, and does not own durable state. Camera state is
  written to the project by `MainViewModel`.
- `AppSettingsService` owns recent projects, autosave defaults, and theme/accessibility
  preferences under the user data root. Those values are excluded from `.uptproj`.

### Tests and fixtures

`UnitProgressTracker.Tests` exercises the core services and WPF view models with an
isolated data root. `Fixtures/v4-complete-project.uptproj` is the reusable portable
round-trip fixture. Inventor lifecycle and fresh-machine launch evidence cannot be
substituted by unit tests.

## Data and control flow

1. A folder scan produces explicit geometry plus skipped/failed diagnostics.
2. Reconciliation compares the scan with the current aggregate without mutating it.
3. The detailer reviews renumber, missing, replacement, add, and intrusion outcomes.
4. An accepted transaction updates geometry and tracking together and marks dirty.
5. Save snapshots the project aggregate atomically; reopen reconstructs active and
   retired surfaces from embedded data before considering the source folder.
6. Search and filters build a session-only projection. Visibility remains a
   project-owned property and therefore participates in save/reopen.

## Invariants and sharp edges

- `.uptproj` version 4 is the only production contract. Pigeon v2 and Esmund v3 are
  explicitly rejected; no silent upgrade path exists.
- A failed open, scan, add, replace, or reconcile operation preserves the last usable
  aggregate and its dirty state.
- Surface-number similarity is not identity. Tracking moves only through a reviewed,
  unique match.
- Missing surfaces stay reviewable; renumbered tracking is never silently transferred.
- Geometry intrusion is non-blocking but remains flagged until a later check proves it
  is resolved.
- Theme/accessibility is application-owned. Camera, list/viewer preferences, status
  definitions, checklist templates, BOM, and surface/group visibility are project-owned.
- Filtering is session-only and must not dirty or mutate project state.
- Sticker styling, bulkhead positioning, opening/door rendering, and rich overlay hover
  details are post-promotion work and must not be represented as complete.

## Validation

See `docs/operations/validation.md` and
`Pigeon/UnitProgressTracker/STEP14_WORKSTATION_SMOKE.md`.

This note was refreshed and inspected against remediation source commit `dccba91`.
The frontmatter verification marker is maintained by Agent Ground.
