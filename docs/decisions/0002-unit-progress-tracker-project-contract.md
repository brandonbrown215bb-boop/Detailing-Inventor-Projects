# 2. Unit Progress Tracker Project and Reconciliation Contract

* **Status**: Accepted
* **Date**: 2026-08-08

## Context and Problem Statement

The native WPF Unit Progress Tracker has multiple persistence and scan paths, while
the pre-production Pigeon and Esmund prototypes use different saved-file shapes.
Implementation could not safely proceed until the team decided which file is
authoritative, how rescans preserve detailer work, which display state travels with a
project, and which Esmund parity items block promotion.

The detailed acceptance boundary is maintained in
`Pigeon/UnitProgressTracker/DETAILER_WORKFLOW_CONTRACT.md`.

## Decision Drivers

* A detailer must be able to reopen a portable project without its Inventor source.
* Save and scan failures must preserve the last usable project.
* Geometry changes must not silently inherit or discard tracking.
* Pre-production test artifacts must not force permanent migration complexity.
* Promotion criteria must distinguish data-safety work from later visual parity.

## Decision

1. A discriminated version 4 `.uptproj` file is the only production project-file
   contract. It contains renderable geometry, tracking, project definitions, BOM
   state, retired records, source provenance, project-owned display state, and camera
   position.
2. Pigeon version 2 and Esmund version 3 files are pre-production test artifacts.
   The production reader rejects them before changing the active project; no direct
   importer or conversion tool is required.
3. Opening a project never implicitly rescans its source. Rescan, Add, and Replace
   operate on candidate state and apply accepted results atomically.
4. A renumbered surface may receive existing tracking only after detailer
   confirmation. Ambiguous identity is never transferred automatically.
5. A missing surface remains pending until the detailer overrides the missing
   result, marks the surface unnecessary, or replaces it.
6. New or changed geometry is checked for protrusion into other project geometry.
   An intrusion produces a non-blocking warning and a persistent surface flag that
   clears only after a later geometry check proves the intrusion is rectified.
7. Sticker styling, bulkhead-channel positioning, opening and door rendering, and
   opening/bulkhead hover details do not block root promotion. They remain explicit
   post-promotion quests and may not be represented as complete without evidence.

## Consequences

* The first production writer and reader can target one unambiguous schema instead
  of maintaining two unused migration paths.
* Existing prototype files used for testing must be recreated as version 4 fixtures;
  attempting to open them produces an actionable unsupported-format message.
* Project round-trip tests must include camera position and unresolved intrusion
  flags in addition to geometry and tracking state.
* Reconciliation needs explicit review states for confirmed renumbers, missing
  surfaces, geometry intrusions, conflicts, and file-level failures.
* Root promotion can occur after the approved data-safety and trustworthy-workflow
  gates pass, while the four visual-parity quests continue afterward.

## Superseded Detail in ADR 0001

ADR 0001 remains authoritative for the C# .NET 8 WPF runtime architecture. Its
prototype-era "dual persistence strategy" and portable `.json` description are
superseded by this ADR's version 4 `.uptproj` contract.
