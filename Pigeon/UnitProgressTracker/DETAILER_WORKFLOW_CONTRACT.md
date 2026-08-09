# Unit Progress Tracker Detailer Workflow Contract

* **Status:** Approved
* **Date:** 2026-08-08
* **Quest:** `Q-20260808-143336-2aa7`
* **Implementation plan:** [DETAILER_REMEDIATION_PLAN.md](./DETAILER_REMEDIATION_PLAN.md)
* **Decision record:** [ADR 0002](../../docs/decisions/0002-unit-progress-tracker-project-contract.md)

## Purpose

This contract defines what a detailer may rely on when creating, saving, reopening,
rescanning, and maintaining a Unit Progress Tracker project. It is the acceptance
boundary for the remediation quests. It does not claim that the current Pigeon WPF
implementation already behaves this way.

The contract preserves the useful behavior of the Esmund implementation while
keeping Pigeon a native WPF application. Esmund approved the product boundary on
2026-08-08, and Pigeon confirmed that renumbered tracking transfers require detailer
confirmation.

## Contract summary

1. A `.uptproj` file is the authoritative, portable snapshot of a project. It must
   contain enough geometry and tracking state to open and work offline. The source
   folder is optional provenance and a rescan source, not a requirement for opening.
2. Opening a project never scans Inventor files, rewrites the project, or silently
   replaces the current project. Rescan is a separate, explicit operation.
3. Save, rescan, add, replace, remove, and restore preserve detailer-entered state.
   Destructive reconciliation is reviewable before it is committed.
4. Project definitions travel with the project. Personal workstation preferences do
   not.
5. The canonical production format is a discriminated version 4 `.uptproj` file.
   Pigeon version 2 and Esmund version 3 files are pre-production test artifacts and
   are not supported migration inputs.

## Terms

| Term | Meaning |
| --- | --- |
| Project snapshot | The portable project file containing geometry, tracking, project definitions, BOM state, retired surfaces, and source provenance. |
| Source folder | The optional folder or assembly location used to discover current Inventor geometry. |
| Active surface | A surface participating in the current project and normal workflow. |
| Retired surface | A surface intentionally removed from active work while retaining its tracking and lineage. |
| Missing surface | A previously active surface not found by the latest scan; it is unresolved until a detailer accepts retirement, replacement, or restoration. |
| Surface identity | Stable identity used to reconnect scanned geometry with saved tracking. Surface number alone is not sufficient when renumbering occurs. |
| Reconciliation | The proposed set of exact matches, renames, additions, missing surfaces, and conflicts produced by a scan. |

## Project file contract

### Portability and authority

The project snapshot is the authoritative saved state. A copied project opens with
the same active and retired surfaces, visible geometry, status definitions, checklist
templates, checklist results, notes, display numbers and lineage, visibility choices,
BOM edits, unit configuration, and project-owned display choices.

If its source folder is missing or inaccessible, the project opens in offline mode.
Offline mode disables operations that require Inventor source data, explains why,
and leaves all saved data usable. It is not an error state and must not produce an
empty-but-successful project.

The source folder is retained as provenance and as the default rescan location. A
detailer may choose a different source for a later scan without losing the saved
snapshot.

### Ownership of settings

| Scope | Saved values |
| --- | --- |
| Project | Embedded geometry; active and retired surfaces; statuses and colors; checklist templates and results; notes; display numbers and rename history; surface/group visibility; BOM rows and manual edits; unit configuration; source provenance; list grouping/sort; viewer overlays, opacity, wireframe, labels, legend, sticker appearance, and camera position. |
| Application/user | Recent projects; autosave enabled/interval; theme, accent, font scale, contrast, and system-theme synchronization; window position and size; defaults offered when creating a new project. |
| Session only | Current selection; hover target; open tab or panel; transient search/filter text. |

Project-owned values take effect after a project successfully opens. Application
preferences remain unchanged. Editing project options marks the project dirty;
Cancel restores the pre-dialog project values and application appearance.

### Schema support

| Input | Required behavior |
| --- | --- |
| Pigeon version 2 `.uptproj` | Reject before changing the current project. Explain that the format was pre-production and is not a supported import. |
| Esmund version 3 portable `.json` | Reject before changing the current project. No converter or direct-import path is required because existing files are test data. |
| Canonical Pigeon project | Read and write a format discriminator and schema version 4. Save only after successful validation and atomic replacement. |
| Newer unsupported schema | Reject before changing the current project. State the detected and supported versions. |
| Corrupt or incomplete file | Reject before changing the current project. Report the failing section and a recoverable next action. |

Rejected files are never rewritten. Compatibility work for the pre-production
version 2 and version 3 shapes may be reconsidered only through a new product
decision; implementation quests must not add an undocumented best-effort migration.

## Workflow contract

### New Project / Open Folder

- Protect unsaved work before replacing the current project.
- Scan into a candidate project, not directly into the live project.
- If scanning is cancelled or fails, keep the prior project unchanged.
- On success, show the discovered surface count, skipped files, warnings, and source.
- A new project receives project-local copies of the user's default statuses and
  checklist template. Later default changes do not mutate existing projects.

### Open Project

- Protect unsaved work before replacing the current project.
- Load and validate the complete snapshot before committing it to the UI.
- Do not implicitly rescan, attach to Inventor, or require the saved source path.
- Restore the same tracking, definitions, BOM, retired records, geometry, and
  project-owned display choices.
- Show offline state explicitly when the source is unavailable. Missing required
  version 4 geometry is an invalid snapshot and is rejected before replacing the
  current project.

### Save / Save As / Autosave

- Save a complete snapshot atomically; failure leaves the last valid file intact.
- Save As changes the active project path only after the write succeeds.
- Autosave may write only when a project path exists, the project is dirty, and no
  scan or reconciliation is in flight.
- A failed save leaves the project dirty and gives an actionable error. It does not
  claim success or close through unsaved-change protection.

### Rescan

Rescan is a two-phase transaction: scan and propose, then review and apply. The live
project remains usable until the proposal is accepted.

| Scan result | Proposed treatment |
| --- | --- |
| Same stable identity and surface number | Refresh geometry; preserve all tracking and project state automatically. |
| Same unique geometry fingerprint, different surface number | Present as a rename/replacement candidate. Transfer tracking only after detailer confirmation. Record old and new numbers in lineage. |
| New or changed geometry fingerprint | Check the candidate against other project geometry. An intrusion produces a non-blocking warning and a persistent flag on the affected surface until a later geometry check proves the intrusion is rectified. |
| New unique surface | Propose Add with the project's default status and checklist template, including any geometry-intrusion warning. |
| Previously active surface not found | Mark Missing in the proposal. Preserve it and require the detailer to override the missing result, mark the surface unnecessary, or replace it; do not silently delete or retire it. |
| Duplicate number, duplicate fingerprint, or ambiguous match | Present a conflict. Never guess which record owns the tracking. |
| File-level parse/COM failure | Include the file and reason in the proposal. Do not interpret that failure as a missing surface. |
| Cancellation or fatal scan failure | Discard the candidate result and leave the current project byte-for-byte equivalent in memory. |

Applying a proposal is atomic. If validation fails, none of its additions, geometry
updates, renames, or retirements enter the project. The result summary records exact
matches, confirmed renames, additions, unresolved missing surfaces, conflicts, and
skipped/failed files. Geometry-intrusion warnings do not block application, but the
flag and the surfaces involved remain visible until a later check confirms that the
intrusion no longer exists.

### Add Surfaces

- Scan only the chosen files or assembly into a candidate set.
- Reject or resolve duplicate identities before mutation.
- Add accepted surfaces with the project-local default status and checklist.
- Run the geometry-intrusion check before acceptance and retain any non-blocking
  warning flag in project state.
- Preserve every existing active/retired surface, BOM row, and project definition.
- Show what was added, skipped, conflicted, or failed.

### Replace Surface

- Replace exactly one selected active surface with exactly one accepted candidate.
- Preserve status, checklist results, notes, visibility, and lineage unless the
  detailer explicitly chooses a clean record.
- Record both numbers when the replacement renumbers the surface.
- Run the geometry-intrusion check against the accepted replacement and retain any
  non-blocking warning flag in project state.
- Keep the original surface and geometry unchanged if selection, scan, validation,
  or save fails.

### Remove / Retire

- Remove means retire, not delete.
- Retirement preserves geometry, tracking, notes, checklist, display number,
  fingerprint, lineage, timestamps, and the reason when supplied.
- A retired surface does not appear in the normal active list, progress totals, or
  default exports, but remains inspectable and exportable as retired history.

### Restore

- Restore returns the same record to active work with its tracking and lineage intact.
- Use cached geometry when valid. If geometry is absent, request a source and do not
  create an empty active surface.
- Identity conflicts are resolved before mutation. Cancellation leaves the surface
  retired.

## Esmund parity boundary

The Esmund application is the behavioral baseline, not the runtime architecture.

### Required before root promotion

- Portable save/open and meaningful offline use.
- Preservation of tracking through rescan and renumbering.
- Incremental add, single-surface replace, retire, and restore.
- Per-project custom status definitions and checklist templates.
- One durable BOM state including manual edits.
- Clear diagnostics and transactional failure/cancellation behavior.
- Status filtering and visibility choices that affect both the list and viewport.

### Intentional differences

- Pigeon remains a native .NET 8 WPF application with direct Inventor integration.
- `.uptproj` version 4 is the only production project-file contract; legacy Pigeon
  version 2 and Esmund version 3 files remain unsupported test artifacts.
- Electron, Node.js, Python, and C# sidecars are not Pigeon runtime dependencies.

### Approved post-promotion work

- Sticker styling parity.
- Correct bulkhead-channel segment-relative positioning.
- Opening and door rendering.
- Opening and bulkhead hover details.

These items do not block root promotion. They remain visibly incomplete and tracked
by their quests; they may not be represented as working before their evidence exists.

## Executable acceptance scenarios

These scenario IDs are stable test/review handles. Automated tests may split a row
into smaller cases but must retain the ID in the test name, trait, or evidence note.

| ID | Scenario and expected result | Quest |
| --- | --- | --- |
| `UPT-C-001` | Save a project, move the `.uptproj` away from its source, disconnect the source, and reopen; geometry and project state remain usable offline. | `Q-20260807-155235-f9de` |
| `UPT-C-002` | Open a valid project while another dirty project is active; Cancel leaves the active project unchanged. | `Q-20260807-155235-f9de` |
| `UPT-C-003` | Open a pre-production Pigeon v2 file; the current project remains unchanged and the unsupported-format message identifies version 4 as the production format. | `Q-20260807-155235-f9de` |
| `UPT-C-004` | Open an Esmund v3 test file; the current project remains unchanged and no implicit conversion or partial import occurs. | `Q-20260807-155235-f9de` |
| `UPT-C-005` | Open a corrupt or newer project; the current project is unchanged and the version/section error is actionable. | `Q-20260808-143336-e20d` |
| `UPT-C-006` | Interrupt an atomic save; the previous project file remains valid and the project remains dirty. | `Q-20260808-143336-189d` |
| `UPT-C-007` | Rescan unchanged surfaces; geometry refreshes while status, checklist, notes, visibility, display number, and history remain equal. | `Q-20260808-143336-140c` |
| `UPT-C-008` | Rescan a unique fingerprint under a new number; a candidate is shown and tracking transfers only after confirmation. | `Q-20260808-143336-140c` |
| `UPT-C-009` | Rescan with new and missing surfaces; additions are reviewable, and each missing surface can be overridden, marked unnecessary, or replaced without losing saved state before acceptance. | `Q-20260808-143336-140c` |
| `UPT-C-010` | Cancel or fail a scan after partial discovery; active/retired surfaces, BOM, settings, and dirty state remain unchanged. | `Q-20260808-143336-140c` |
| `UPT-C-011` | Encounter duplicate numbers/fingerprints or a file parse failure; no automatic identity transfer occurs and each conflict names its source. | `Q-20260808-143336-e20d` |
| `UPT-C-012` | Customize statuses/colors, save, switch projects, and reopen; each project restores only its own definitions. | `Q-20260808-143336-03de` |
| `UPT-C-013` | Customize a checklist template and add a surface; the new surface gets the project template and existing results are not rewritten. | `Q-20260806-192703-61a8` |
| `UPT-C-014` | Edit BOM rows, save, switch projects, and reopen; edits survive and never leak into the other project. | `Q-20260808-143336-a698` |
| `UPT-C-015` | Replace one surface with a valid candidate; exactly one geometry record changes and tracking/lineage survive. | `Q-20260808-143336-80a4` |
| `UPT-C-016` | Add accepted surfaces; existing surfaces and tracking remain unchanged and duplicates are reported. | `Q-20260808-143336-00d1` |
| `UPT-C-017` | Retire then restore a surface; geometry, tracking, notes, checklist, and lineage round-trip intact. | `Q-20260808-143336-1e7a` |
| `UPT-C-018` | Change status filter, surface/group visibility, or camera position, save, and reopen; list and viewport agree and project-owned choices return. | `Q-20260806-192657-d6e1`, `Q-20260806-192651-0957`, `Q-20260808-143336-8ec5` |
| `UPT-C-019` | Apply Options then Cancel a second edit; only the applied project values persist, including camera position, and application theme/accessibility preferences do not leak into the project. | `Q-20260808-143336-8ec5` |
| `UPT-C-020` | Run promotion review; every required behavior has automated evidence plus target-workstation evidence where Inventor is required, and every deferral is named. | `Q-20260807-200122-ca4a` |
| `UPT-C-021` | Rescan, add, or replace with geometry that protrudes into another surface; application remains allowed, both surfaces are identified, and the warning flag survives save/reopen until a later check proves the intrusion is rectified. | `Q-20260808-143336-140c`, `Q-20260808-143336-00d1`, `Q-20260808-143336-80a4` |

## Approval record

Approved on 2026-08-08 from Esmund's written answers and Pigeon's follow-up
confirmation. The accepted decisions are:

1. no production compatibility or converter is required for pre-production Pigeon
   version 2 or Esmund version 3 files;
2. tracking transfer after a confirmed renumber requires detailer confirmation;
3. new or changed geometry is checked for intrusion, which warns and persists a flag
   without blocking the workflow;
4. missing surfaces remain pending until the detailer overrides, marks unnecessary,
   or replaces them;
5. camera position belongs to project state;
6. `.uptproj` version 4 is the canonical production file contract; and
7. sticker styling, bulkhead positioning, opening/door rendering, and overlay hover
   details are explicit post-promotion work.

ADR 0002 records the durable decision. This contract and current source/tests define
acceptance; the stale architecture note is orientation only until separately
refreshed and verified.
