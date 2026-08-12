# Step 14 Workstation Smoke

Use the complete `.artifacts/publish/win-x64` directory created by
`prepare_step14.ps1`. Run this on the intended detailing workstation and, for the
fresh-machine row, from a clean folder that is not the repository checkout.

## Evidence header

| Field | Result |
| --- | --- |
| Tester and date | |
| Machine / Windows version | |
| Inventor version and build | |
| Apprentice availability/version | |
| Package source commit | |
| Package path | |
| Test IAM / unit | |

Use an isolated user-settings root if the workstation has existing tracker data:

```powershell
$env:UNIT_PROGRESS_TRACKER_DATA_ROOT = Join-Path $env:TEMP 'UPT-Step14-Smoke'
& '.\UnitProgressTracker.Wpf.exe'
```

Keep screenshots or exact error text for every failed row. Do not mark a row passed
because the application continued after silently skipping data.

## A. Fresh launch and portable project

- [ ] Launch the self-contained executable on the clean target without installing a
      separate .NET runtime. Record startup time and any Windows security prompt.
- [ ] Open a supported unit IAM/folder. Confirm non-empty geometry, surface list,
      statuses, and source diagnostics.
- [ ] Change status, checklist, note, display number, visibility, list/view options,
      BOM data, and camera. Save a v4 `.uptproj`.
- [ ] Copy the project away from the source and disconnect/rename the source folder.
      Reopen it. Confirm offline mode and that embedded geometry plus all edited state
      remains usable (`UPT-C-001`).
- [ ] With unsaved edits active, attempt to open another project and Cancel. Confirm the
      current project and dirty marker remain unchanged (`UPT-C-002`).

## B. Rejection and failure preservation

- [ ] Open a Pigeon v2 fixture, an Esmund v3 fixture, corrupt JSON, and a newer-version
      fixture. Each message must identify the problem/action; none may replace the
      active project (`UPT-C-003` through `UPT-C-005`).
- [ ] Point a scan at one unreadable/bad source among valid sources. Confirm scanned,
      accepted, skipped, and failed identifiers are reviewable and the last usable
      project remains intact (`UPT-C-011`).

## C. Inventor/Apprentice lifecycle and reconciliation

- [ ] Scan an unchanged supported IAM. Accept the proposal and confirm geometry refresh
      without loss of status, checklist, notes, visibility, display number, or history
      (`UPT-C-007`).
- [ ] Present a unique renumber candidate. Decline once and confirm no transfer; repeat
      and accept, confirming the preserved lineage/tracking (`UPT-C-008`).
- [ ] Exercise a scan containing a new surface and a missing surface. Verify override,
      unnecessary/retire, and pending-for-replacement choices are explicit
      (`UPT-C-009`).
- [ ] Cancel a scan after discovery begins. Confirm project, BOM, settings, and dirty
      state are unchanged (`UPT-C-010`).
- [ ] Replace one surface from IAM and add surfaces from a folder containing a valid
      source, duplicate, and failing source. Confirm only accepted geometry changes and
      every conflict is named (`UPT-C-015`, `UPT-C-016`).
- [ ] Use protruding geometry in rescan/add/replace. Confirm the action is allowed, both
      surfaces are named, and the unresolved warning survives save/reopen until a clean
      recheck resolves it (`UPT-C-021`).

## D. Tracking, filters, visibility, and Options

- [ ] Create different status definitions in two projects, switch, and reopen. Confirm
      no cross-project leakage (`UPT-C-012`).
- [ ] Edit the checklist template and add a surface. Confirm only the new surface gets
      the template and existing completed work is preserved (`UPT-C-013`).
- [ ] Retire a surface, save/reopen, then restore it. Confirm geometry, notes, checklist,
      status, and lineage return (`UPT-C-017`).
- [ ] Click a status legend item. Confirm list and viewport show the same status ID;
      click it again to clear. Compose it with text and visibility filters. Confirm a
      filtered-out selection clears and the project does not become dirty.
- [ ] Toggle one surface, selected surface, a mixed group, and Show All. Confirm labels,
      counts, viewport, dirty marker, save, and reopen agree (`UPT-C-018`).
- [ ] In Options, change project display choices and application theme, then Save. Move
      the camera and save/reopen. Open Options again, edit/reset, and Cancel. Confirm the
      first saved project values and camera remain, cancelled values do not apply, and
      theme follows the user rather than the project (`UPT-C-019`).

## E. BOM and outputs

- [ ] Edit BOM rows, save, switch projects, and reopen both. Confirm isolation and
      persistence (`UPT-C-014`).
- [ ] Export the audit Markdown and inspect surface/status/checklist totals and source
      diagnostics.
- [ ] Create shell folders in a disposable destination. Confirm expected directories,
      skipped misplaced rows, and actionable failures; do not use a production folder.

## Sign-off

| Gate | Pass / Fail | Evidence or defect link |
| --- | --- | --- |
| Fresh self-contained launch | | |
| Portable/offline project | | |
| Failure preservation | | |
| Inventor/Apprentice lifecycle | | |
| Reconciliation/add/replace | | |
| Tracking/filter/options round-trip | | |
| BOM/export/shell folders | | |

Promotion remains blocked until every required row passes or has an explicitly approved
classification in `STEP14_PROMOTION_EVIDENCE.md`.
