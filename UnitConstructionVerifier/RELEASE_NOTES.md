# Unit Construction Verifier — Release Notes

Root `UnitConstructionVerifier/` is the release line. Roadmap: `Docs/project_review.md` section 0.

## v1.0.24.0 (2026-10-21) — window no longer loses edits

From the UI review of the verifier window (data risks D1–D4 and I3).

- Expectation dropdowns keep a surface's stored value when it is not a standard option. Before, the
  dropdown showed blank and changing any other field on that surface saved `""` over the hidden value.
  Base height, base material and Thermal Break use the same lists, and a blank Thermal Break no longer
  shows as "No".
- Unsaved expectation changes show in the footer, and closing the window with unsaved expectations or
  staged part edits asks before discarding them.
- The **Actual Part Spec** column is read-only until Edit Mode is turned on (it was editable on a fresh
  window), and rows whose parameter cannot be written back cannot be edited or synced. Edits that cannot
  be written are listed in the write confirmation instead of being dropped silently.

## v1.0.23.0 (2026-10-21) — rules from real units

Rules were checked against 56 real ISG units (7,583 parts) with the new `ucv-scan` tool: mismatches went
from 1,300 to 135 and Not checked from 699 to 23.

- Roof and wall skin/liner expectations use the surface's **own side** of the unit (roofs: Top) instead
  of a majority vote across all sides. Painted wall sides no longer make unpainted roofs fail.
- Roof channels follow the wall convention: standard parts (`064/065/085/086`) are 16 GA `STL GALV2/3/4` by
  panel thickness; sloped outdoor roof channels (`068/069/070`) are plain `STL GALV`.
- New rules: **Roof Cap** (16 GA in the wall exterior material, 20 GA for ALM EMB), **Roof Accessory**
  (super angle, peaked split cover, roof seal-off angle: 16 GA STL GALV), **Tiering Angle** (14 GA STL GALV),
  **Attachment Angle** (16 GA in the liner material). Stock `079` angles are split by profile: **Floor Drain
  Support Angle** (1.5x1.5 / 2x2: 10 GA in the base alloy), **Floor Attachment Angle** (2.5x1: engineering
  14 GA ALM SHT); other `079` profiles
  (custom angles) are not verified.
- Wall seal-off angles now expect the exterior skin (matched every scanned part); the old rule expected
  16 GA in the liner material.
- Sub-floor expectations come from `unitBase.unitBaseGeometryList[].subFloorMaterialType/Gauge`.
- Perimeter angles on bases with an upturned lip (UTL, `upturnedLipHeight` in the base config): 10 GA ALM SHT on aluminum floors, 10 GA
  in the floor's stainless code (e.g. SST304D) on stainless floors, 12 GA STL GALV on galvanized floors. Otherwise the engineering standard
  `UnitBase_PerimeterAngle_*` (16 GA STL GALV).
- All engineering constants (`DOCUMENT_ENGINEERING_JSON`) are now read and kept in snapshots.
- Plate gauges keep their thickness label: a 0.125 ALM DIA floor reads "0.125 GA ALM DIA", not 10 GA
  (the gauge lookup now prefers the exact material over the closest aluminum sheet entry).
- Parts changed after generation (SQ parts, verifier edits) are flagged **modified** in the grids and in
  the Run Verification summary.
- New `UnitConstructionVerifier.Scan` project (`ucv-scan.exe`): headless, read-only scan of unit folders,
  and `--replay` to re-run rules over saved snapshots. The add-in and the scanner share one set of parsers.

## v1.0.22.0 (2026-10-21) — no silent passes

- Every part now gets an outcome: **Pass**, **Mismatch**, **Display only**, **Not checked** (with the
  reason) or **Not in config**. Previously parts with no surface row, parts in the wrong surface type
  and parts with a blank expected value were skipped without notice.
- Not-checked parts appear in grey in the surface grids (hover for the reason), and Run Verification
  reports how many parts were checked and how many could not be.
- The parts grids and Run Verification now use one shared check, so they can no longer disagree.
- Base expectations come from the engineering config instead of the scanned parts. Formed channel
  reads `formedChannelMaterialType`/`formedChannelMaterialGauge`; structural material reads
  `structuralMaterialType`. Sub-floor and perimeter angle have no config source and start blank.
- Classification: the longest matching description keyword wins (e.g. "Floor Perimeter Angle" is a
  Perimeter Angle, not a Floor Sheet); "sub-floor" and "sub floor" spellings are recognized.
- `091-30117-072` is only a Wall Corner Cap and `091-30117-073` only a Wall Corner Liner;
  `091-30117-187`/`189` are Structural Angle everywhere. Rule conflicts are logged on load.
- Snapshot baselines now record each part's outcome instead of only mismatches.

## v1.0.21.0 (2026-10-21) — snapshot regression tests

- New **Save Snapshot** button in the verifier footer saves the unit's scan data (config JSON,
  engineering data, IPT scan, captured before saved overrides) to `<unit>.unit_snapshot.json`.
- `SnapshotRegressionTests` replays every snapshot in `UnitConstructionVerifier.Tests/Snapshots`
  through the builder and engine and compares the outcome against an approved baseline.
  See `UnitConstructionVerifier.Tests/Snapshots/README.md`.

## v1.0.20.0 (2026-10-21) — release baseline

- `compile.bat` and `create_dist.bat` now build and package **Release**, matching `deploy.bat`.
  Before this, `build-and-install.bat` compiled Debug but deployed whatever Release build was last made.
- `HOW_TO_USE.md` now describes this copy (it previously described the Esmund fork) and documents
  what write-back stamps on edited parts.
- Added `Docs/known_issues.md` (Ctrl+V / clipboard behavior) and `PartPropertyEditsMapper` tests.
- Removed tracked `.bak` files and the committed dist zip; `create_dist.bat` regenerates the zip.

## v1.0.19.0

- `MOM_DATA` attribute set is read first and kept in sync on write-back
  (`MATERIAL_TYPE_CODE`, `ORIGINAL_SHEET_METAL_THICKNESS`).
- Edited parts are stamped `IS_HARD_EDITED`, `PART_SOURCE = SQPart`, `DOCUMENT_STATUS = Changed_SQ`
  and given the `EditedAppearance` color from `materials_config.json`.
- Edit Mode write-back works for config-driven parameter names (`Exterior Gauge & Material`, etc.).
- IPT changes save even when the part is already open in Inventor.

## v1.0.18.0

- Default highlight is an oriented locator box instead of Inventor `SelectSet`; X-Ray unchanged.

## v1.0.17.0

- Part and surface highlight work when a surface sub-assembly IAM is opened directly.

## v1.0.16.0 and earlier

- Modeless verifier window, clipboard copy buttons, X-Ray highlight, wireframe preview,
  resizable grids, config-driven `PartRules` engine (including Seal-Off Angle).
