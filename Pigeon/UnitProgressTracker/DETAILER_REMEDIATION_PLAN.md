# UnitProgressTracker Detailer Remediation Plan

- **Status:** Proposed implementation sequence
- **Scope:** `Pigeon/UnitProgressTracker`
- **Reference implementation:** `Esmund/UnitProgressTracker` is parity evidence, not a code-sharing requirement.
- **Coordination:** Every numbered step maps to one committed card under `.questboard/quests/`.

## Purpose

Make the native WPF Unit Progress Tracker safe for a detailer to use across the full working cycle:

1. open or scan a unit;
2. record status, checklist, notes, visibility, numbering history, and BOM decisions;
3. save and close;
4. reopen with or without the original source folder;
5. rescan, replace, add, retire, and restore surfaces without losing prior work;
6. export and create shell folders from the same project state the UI displays.

The current application has useful scan, render, BOM-import, and tracking pieces, but the pieces do not yet share one durable project state. This plan stabilizes that state before adding more viewport behavior.

## Product boundaries

- The WPF application remains local-first and Windows-only.
- Project data must not require a cloud service, PAT, account, or network connection.
- The original Electron application may be inspected to establish detailer-visible behavior and saved-file compatibility expectations. Its implementation is not copied into the WPF runtime.
- Existing `.uptproj` files must either load safely or produce an actionable compatibility message. They must never be silently interpreted as a successful blank project.
- A failed or cancelled operation must preserve the last usable in-memory project.
- Source geometry, tracking records, BOM data, status definitions, and display preferences need explicit ownership. No workflow should maintain a second unofficial copy.
- `ProjectStateService` and other domain services are useful only when the shipped WPF path calls them; isolated unit tests are not parity evidence.
- Promotion from `Pigeon/` to the repository root is outside every implementation quest until the final parity gate passes.

## Delivery rules

- Claim the mapped quest before editing its scope.
- Keep each quest small enough to review independently, but do not mark a quest done when its acceptance test depends on an unfinished predecessor.
- Add a failing regression test before changing behavior when the defect can be reproduced without Inventor.
- Use sanitized, repository-owned fixtures. Tests must not read another developer's profile, scratch directory, or production `%APPDATA%` file.
- Keep `MainViewModel` from absorbing new domain rules. New state transitions belong behind cohesive, testable boundaries.
- A quest may move to `review` after local automated evidence exists. Inventor-dependent quests remain in `review` until target-workstation smoke evidence is recorded.
- Documentation is updated from verified behavior at the end, not used as proof that behavior exists.

## Sequence and Quest map

| Step | Gate | Quest | Outcome |
| ---: | --- | --- | --- |
| 0 | Contract | `Q-20260808-143336-2aa7` | Define the detailer workflow and project-state compatibility contract |
| 1 | Test foundation | `Q-20260808-143336-189d` | Restore a hermetic, zero-warning validation gate |
| 2 | Data safety | `Q-20260807-155235-f9de` | Round-trip a usable project, including offline geometry |
| 3 | Data safety | `Q-20260808-143336-140c` | Preserve tracking and the last usable state through rescan |
| 4 | Data safety | `Q-20260808-143336-03de` | Preserve custom status definitions per project |
| 5 | Data safety | `Q-20260806-192703-61a8` | Apply and preserve project checklist templates |
| 6 | Data safety | `Q-20260808-143336-a698` | Persist manual BOM edits and isolate BOM state between projects |
| 7 | Daily workflow | `Q-20260808-143336-80a4` | Replace one surface without losing tracking history |
| 8 | Daily workflow | `Q-20260808-143336-00d1` | Add surfaces incrementally without replacing the project |
| 9 | Daily workflow | `Q-20260808-143336-1e7a` | Retire and restore surfaces with audit history intact |
| 10 | Daily workflow | `Q-20260808-143336-e20d` | Show actionable project, scan, and COM diagnostics |
| 11 | Trustworthy UI | `Q-20260806-192657-d6e1` | Make legend filtering affect both the list and viewport |
| 12 | Trustworthy UI | `Q-20260806-192651-0957` | Make surface and group visibility observable and persistent |
| 13 | Trustworthy UI | `Q-20260808-143336-8ec5` | Make Options transactional and behaviorally complete |
| 14 | Visual parity | `Q-20260806-192913-57d1` | Apply supported sticker settings to rendered labels |
| 15 | Visual parity | `Q-20260807-155233-d335` | Correct bulkhead-channel segment-relative positioning |
| 16 | Visual parity | `Q-20260807-155234-9859` | Render openings and doors in the 3D context |
| 17 | Visual parity | `Q-20260807-154317-3047` | Provide useful opening and bulkhead hover details |
| 18 | Promotion | `Q-20260807-200122-ca4a` | Verify parity evidence and decide whether Pigeon can move to root |

Steps within a gate may be parallelized only when they do not modify the same state contract. Gates remain ordered.

---

## Step 0 — Define the detailer workflow and compatibility contract

- **Quest:** `Q-20260808-143336-2aa7`
- **Depends on:** None

### Detailer outcome

The team shares one written meaning for Save, Open, Rescan, Add, Replace, Remove, Restore, and offline use before changing the schema that supports them.

### Acceptance criteria

- The contract states whether `.uptproj` is a portable project or a tracking overlay. If both modes are supported, the file and UI distinguish them.
- Project-scoped and application-scoped settings are listed explicitly.
- Supported input schema versions and the behavior for newer, older, or malformed files are defined.
- Rescan rules define how exact matches, renamed geometry, new surfaces, missing surfaces, duplicates, cancellation, and partial failure affect tracking.
- Esmund parity items are classified as required, intentionally changed, or deferred.
- Each observable scenario is represented by a named automated or manual validation case assigned to a later quest.

### Evidence required for review

- Approved contract or ADR.
- Scenario matrix linked from the quest.
- No production behavior claim based only on the stale architecture note.

## Step 1 — Restore the validation gate

- **Quest:** `Q-20260808-143336-189d`
- **Depends on:** Step 0 for final scenario names; fixture isolation can begin immediately.

### Detailer outcome

The team can tell whether a remediation actually preserves detailer work on any development machine.

### Acceptance criteria

- No test references a developer-specific absolute path or external scratch directory.
- MRU and settings tests use an isolated writable data root and never change the user's production profile.
- The full solution builds with zero warnings.
- All existing tests pass before behavioral remediation is considered complete; any deliberately corrected expectation is documented in the quest.
- A reusable project round-trip fixture contains geometry, tracking, BOM, status definitions, checklist templates, retirement history, and preferences.
- Validation commands work from a clean checkout without relying on prior `bin`, `obj`, or profile state.

## Step 2 — Round-trip a usable project

- **Quest:** `Q-20260807-155235-f9de`
- **Depends on:** Steps 0–1

### Detailer outcome

Saving and reopening returns the detailer to a usable project, including a populated 3D viewport when the original IAM folder is unavailable.

### Acceptance criteria

- A saved project carries every geometry and identity field needed by the surface list, detail panel, viewport, bulkhead overlays, and subsequent reconciliation.
- Status, checklist, notes, visibility, display number, previous numbers, fingerprints, BOM, unit configuration, and supported preferences survive the same round trip.
- Reopening offline restores renderable surfaces and clearly identifies offline mode.
- Reopening with a valid source folder does not silently replace saved tracking with scanner defaults.
- Malformed or unsupported files leave the current project untouched and show an actionable error.
- Existing supported project versions are migrated or rejected according to Step 0.

## Step 3 — Preserve tracking through rescan

- **Quest:** `Q-20260808-143336-140c`
- **Depends on:** Steps 0–2

### Detailer outcome

A detailer can rescan changed IAM data without losing status, checklist, notes, visibility, or numbering history.

### Acceptance criteria

- The current visible project is retained until a scan and reconciliation result is ready to apply.
- Cancellation, fatal scan failure, or rejected partial results leave the prior project usable and unchanged.
- Exact surface matches preserve tracking.
- Renumber candidates use confirmed identity evidence and remain reviewable; ambiguous matches are never transferred silently.
- New surfaces receive the current project checklist template and default status.
- Missing surfaces follow the approved retirement rule and retain audit history.
- One integration test proves scan → edit → rescan → save → reopen without tracking loss.

## Step 4 — Preserve custom status definitions

- **Quest:** `Q-20260808-143336-03de`
- **Depends on:** Step 2

### Detailer outcome

Custom workflow states retain their names, colors, fill behavior, and surface assignments after save and reopen.

### Acceptance criteria

- Status definitions have a single project-owned source of truth.
- Adding, editing, reordering, and deleting a status produces a durable and testable result.
- Deleting a status that is still assigned requires an explicit resolution or preserves it as an identifiable unknown state.
- Markdown export and the 3D viewport resolve status meaning from the same definitions.
- Unknown state IDs are visible to the detailer rather than silently rendered as an ordinary fallback.

## Step 5 — Apply and preserve checklist templates

- **Quest:** `Q-20260806-192703-61a8`
- **Depends on:** Steps 2–3

### Detailer outcome

New surfaces start with the project's intended audit checklist while existing completion work remains intact.

### Acceptance criteria

- Checklist templates are stored at the scope chosen in Step 0.
- Fresh scans and incremental additions initialize new records from the active template.
- Template edits do not reset checked values on existing surfaces without explicit confirmation.
- Save, reopen, rescan, add, replace, retire, restore, and export preserve checklist labels and completion state.
- Duplicate labels and case variants have deterministic behavior.

## Step 6 — Persist one BOM state

- **Quest:** `Q-20260808-143336-a698`
- **Depends on:** Step 2

### Detailer outcome

The BOM shown on screen, saved in the project, and used to create shell folders is the same BOM.

### Acceptance criteria

- Import, manual add, inline add, delete, and removal update one project-owned BOM aggregate.
- Save and reopen preserve every accepted manual edit and omission.
- Opening a project with no BOM clears the previous project's displayed entries, filters, selection, and folder plan.
- Shell-folder creation includes accepted manual entries and excludes removed entries.
- Failed import or invalid unit configuration does not replace the last valid BOM.
- Tests cover switching between two projects with different BOMs in one application session.

## Step 7 — Replace one surface safely

- **Quest:** `Q-20260808-143336-80a4`
- **Depends on:** Steps 2–5

### Detailer outcome

The detailer can replace one changed IAM surface without rescanning the full unit or losing its tracking history.

### Acceptance criteria

- Replace requires one selected active surface and exactly one valid scanned replacement.
- Cancel, zero results, multiple results, duplicate active identity, and COM failure leave the project unchanged.
- Same-identity replacement refreshes geometry while preserving tracking.
- Changed identity follows the approved transfer/retirement rules and records lineage.
- The list, detail panel, viewport, dirty state, and saved project agree immediately after replacement.

## Step 8 — Add surfaces incrementally

- **Quest:** `Q-20260808-143336-00d1`
- **Depends on:** Steps 2–5

### Detailer outcome

The detailer can add newly created surfaces without replacing the active project.

### Acceptance criteria

- Existing surfaces, selection, tracking, BOM, and project path remain intact.
- Duplicate surfaces are skipped or explicitly resolved before changes are applied.
- Accepted surfaces receive geometry, identity metadata, default status, and the current checklist template.
- Partial or failed scans present a reviewable result before changing the project.
- Add is covered separately from full Open Folder and Rescan behavior.

## Step 9 — Retire and restore surfaces

- **Quest:** `Q-20260808-143336-1e7a`
- **Depends on:** Steps 2–5

### Detailer outcome

A removed surface remains auditable and can be restored without recreating its detailer work.

### Acceptance criteria

- Remove records who/when is not required, but it records when, source identity, fingerprint, tracking snapshot, and transfer reason.
- Retired surfaces do not reappear in the active list after save and reopen.
- The Removed section is rehydrated from persisted state.
- Restore returns geometry and tracking to the active project or explains why geometry must be reacquired.
- Renumber history and retirement history remain distinct and understandable.
- Remove and restore participate in dirty tracking and unsaved-change protection.

## Step 10 — Make failures visible and recoverable

- **Quest:** `Q-20260808-143336-e20d`
- **Depends on:** Steps 2–3; may proceed alongside Steps 4–9.

### Detailer outcome

When a project or surface cannot be read, the detailer knows what failed, what was preserved, and what to do next.

### Acceptance criteria

- Invalid project JSON, unsupported versions, inaccessible folders, unreadable files, missing geometry, and COM/Apprentice failures have distinct user-facing outcomes.
- Partial scans report scanned, accepted, skipped, and failed files with reviewable identifiers.
- Silent per-file failures no longer masquerade as a complete successful scan.
- Diagnostics avoid credentials and sensitive configuration content.
- The last usable project is preserved whenever the requested operation cannot be safely applied.

## Step 11 — Make status filtering real

- **Quest:** `Q-20260806-192657-d6e1`
- **Depends on:** Step 4

### Detailer outcome

Clicking a status in the legend shows the same matching surfaces in the list and viewport.

### Acceptance criteria

- Filtering compares status IDs, not unrelated text fields.
- Clicking the active legend filter clears it.
- Text search, status filter, visibility filter, and grouping compose predictably.
- Selection behavior is defined when the selected surface is filtered out.
- Counts and empty-state messaging reflect the filtered result.
- Filtering does not mutate or dirty project data.

## Step 12 — Make visibility observable and persistent

- **Quest:** `Q-20260806-192651-0957`
- **Depends on:** Steps 2–3

### Detailer outcome

Surface and group visibility controls immediately agree with the list, counts, viewport, save state, and reopened project.

### Acceptance criteria

- Surface property changes notify every bound view that consumes them.
- A group with mixed visibility has a defined visual state and action.
- Individual, selected-surface, group, and Show All actions update counts and controls immediately.
- Visibility changes mark the project dirty and participate in autosave/unsaved-change prompts.
- Save and reopen reproduce the same visibility state.

## Step 13 — Make Options transactional and honest

- **Quest:** `Q-20260808-143336-8ec5`
- **Depends on:** Steps 2, 4, and 5

### Detailer outcome

Every exposed option either changes the product as labeled or is removed until supported. Cancel means cancel.

### Acceptance criteria

- Opening and cancelling Options leaves the project and running UI unchanged.
- Reset Defaults does not affect the project until Save is accepted.
- Saved options apply immediately and survive reopen at the scope defined in Step 0.
- Name mode, sort mode, grid, skid labels, legend, hover information, opacity, wireframe, sticker style, theme, font scale, focus visibility, and FPS controls are each wired and tested or removed from the dialog.
- Runtime controls and persisted preferences use one source of truth.

## Step 14 — Apply sticker settings

- **Quest:** `Q-20260806-192913-57d1`
- **Depends on:** Step 13

### Detailer outcome

Surface labels remain legible and configurable at useful model scales and viewing angles.

### Acceptance criteria

- Supported font, text color, background, border, scale, and orientation settings affect rendered stickers.
- Invalid values produce safe defaults and actionable validation.
- Settings apply to newly loaded and already visible surfaces.
- A visual smoke matrix covers representative roof, wall, base, and dense-skid views.

## Step 15 — Correct bulkhead-channel positioning

- **Quest:** `Q-20260807-155233-d335`
- **Depends on:** Steps 2–3

### Detailer outcome

Bulkhead channels appear at the correct segment-relative location rather than being displaced by the surface's absolute origin.

### Acceptance criteria

- Channel positions are validated against representative multi-segment surfaces with non-zero origins.
- Existing correct zero-origin cases remain unchanged.
- Calculated channel geometry survives save, reopen, rescan, and replacement.
- Target data or Inventor evidence is attached for cases that cannot be proven synthetically.

## Step 16 — Render openings and doors

- **Quest:** `Q-20260807-155234-9859`
- **Depends on:** Steps 2–3

### Detailer outcome

The viewport gives the detailer enough spatial context to verify openings and doors against the surface geometry.

### Acceptance criteria

- Supported opening and door types have a consistent visual representation and legend.
- Indicators align with their owning surface and unit side.
- Missing or incomplete opening data does not prevent the base surface from rendering.
- Visibility, filtering, selection, save/reopen, and replacement keep overlays synchronized.

## Step 17 — Add useful overlay hover details

- **Quest:** `Q-20260807-154317-3047`
- **Depends on:** Steps 15–16

### Detailer outcome

Hovering an opening, door, or bulkhead overlay identifies the engineering item without obscuring the viewport.

### Acceptance criteria

- Hit testing distinguishes surface bodies, openings, doors, and bulkhead channels.
- The tooltip shows only available, relevant identifiers and dimensions.
- Tooltip behavior respects the project preference and disappears reliably.
- Dense geometry remains usable without stale or misleading hover content.

## Step 18 — Verify parity and promotion readiness

- **Quest:** `Q-20260807-200122-ca4a`
- **Depends on:** All required prior steps

### Detailer outcome

Pigeon is promoted only after the team has evidence that it can replace the established workflow without losing detailer work.

### Acceptance criteria

- The Step 0 parity matrix is updated with pass, accepted difference, or explicitly deferred status for every workflow.
- Automated build and test evidence is green with zero warnings.
- Self-contained publish and fresh-machine launch smoke pass.
- Save/reopen offline, rescan reconciliation, add, replace, retire/restore, BOM edits, filters, options, export, and shell-folder creation pass an end-to-end detailer smoke.
- Inventor/Apprentice behavior is validated on the supported detailing workstation environment.
- Architecture documentation is refreshed from the final source and verified with Agent Ground.
- Promotion to root is a separate, deliberate change after review approval; it is not bundled into remediation implementation.

## Completion evidence template

Every quest handoff to `review` should record:

- exact files changed;
- tests added or changed and why;
- exact validation commands and results;
- saved-file versions exercised;
- whether an Inventor workstation was required;
- known limitations or deferred parity decisions;
- one exact next action for the reviewer.

## Current baseline captured by the review

- Reviewed source revision: `c6cb18d763bd`.
- Automated run: 360 tests discovered; 355 passed and 5 failed.
- Build emitted two nullable warnings from the test project.
- One scanner test depends on another developer's scratch path.
- MRU tests use the production profile settings path and are not workspace-isolated.
- No target Inventor smoke evidence was produced during the review.
- `docs/architecture/unit_progress_tracker.md` is stale relative to the current source and must not be treated as completion evidence.

This baseline is evidence for prioritization, not a permanent metric. Each implementing quest must record its own current results.
