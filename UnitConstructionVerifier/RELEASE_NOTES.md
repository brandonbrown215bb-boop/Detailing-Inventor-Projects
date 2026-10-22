# Unit Construction Verifier — Release Notes

Root `UnitConstructionVerifier/` is the release line. Roadmap: `Docs/project_review.md` section 0.

## v1.0.29.0 (2026-10-22) — dock the verifier inside Inventor

- **Dock / Pop out** (header) moves the verifier between its own window and a panel docked inside Inventor (right of
  the model by default; drag to left, bottom or floating). Nothing is lost when it moves, and the next unit opens the
  way you last left it.
- **Narrow layout:** below about 1040 px the verifier uses one column (surfaces above the detail, expectation forms
  folded and collapsed, a shorter parts grid), so it fits a side dock. The pop-out window can now be made that small too.
- **Keys:** all keys aimed at the verifier now reach it before Inventor's shortcuts, docked or not (ADR 0003). This
  replaces the v1.0.26 Ctrl+C / Ctrl+V / F8 hook; behaviour of those three keys is unchanged.
- Running the command on a different unit while the verifier is open now closes the open one (asking about unsaved
  changes) and opens the new unit. Before, it brought back the old unit.
- Known limitation: closing Inventor does not ask about unsaved changes in a docked verifier.

## v1.0.28.0 (2026-10-22) — roof Trim row drives roof caps

- **Fixed:** changing a roof's **Trim** gauge or material did not change what roof caps (091-30117-001/058/074) were
  checked against. They used a hidden roof cap value taken from the wall exterior, so they never flagged and were never
  staged (seen on 091Z010068-0105). Roof caps are what detailers call roof trim, so they now follow the roof's Trim row
  (check name *Roof Trim Gauge & Material*); `ALM EMB` is still always 20 GA. The Trim row starts with the same values
  as before, so existing units check the same until you change it.
- The trim stock parts 091-30117-011/195/196 are not automated and are now **Display only** instead of checked.

## v1.0.27.0 (2026-10-22) — review before writing (UI review Option B)

- **Review & write to parts…** now opens a review listing each staged change (part, property, now, will be)
  instead of a message box that named only the files. Untick a change to leave it staged; only ticked changes
  are written. Changes that cannot be written show struck through.
- **Staged rows** show the part's current value next to the staged one in **In part** (`20 → 22`), so the real
  value no longer disappears until you hover the status chip.
- **Copy to all roofs/walls** lists the surfaces and fields that would change, copies only to surfaces that differ,
  and skips the prompt when every other surface already matches.

## v1.0.26.0 (2026-10-22) — keyboard shortcuts inside Inventor

- **Fixed:** Ctrl+C and F8 did nothing in the verifier inside Inventor, because Inventor took the keys first.
  The verifier now takes **Ctrl+C**, **Ctrl+V** and **F8** while it has focus. Ctrl+C copies the selected part or
  surface numbers (or selected text in a text field); F8 is **Next issue**.
- **Ctrl+V** pastes into text fields only. Elsewhere in the verifier it does nothing, so Inventor no longer pastes an
  old copied component into the model while you are working in the verifier.

## v1.0.25.0 (2026-10-21) — window redesign (UI review Option A)

The verifier window keeps its layout but shows problems before you go looking and stops interrupting with dialogs.

- **Status everywhere:** the header shows mismatches across the unit, tabs show a count per surface type, and
  each surface has a status dot and count. Status re-runs on its own when an expectation changes.
  **Next issue** (F8) jumps to the next surface with a mismatch.
- **Re-check unit** (was Run Verification Check) shows its summary in a banner with **Go to first issue**
  instead of a message box. Saves, copies and "copied to all" show a short note in the footer.
- **Highlight** is one labelled choice: **Box / X-Ray / Off** (replaces the X-Ray checkbox and Normal button).
  Off now stays off until you pick Box or X-Ray.
- **Edit parts** switch replaces the Edit Mode checkbox. Staged edits collect in a footer tray that stays visible
  until they are written or discarded; Re-check and Save stay available while editing.
- **Expected construction** uses labelled Gauge / Material columns that no longer rewrap, and can be collapsed.
  Upturned lip and curb rest moved from the header to Base & Floor → Unit. Gauges are listed in numeric order.
- **Parts grid:** colored status chips, All / Issues / Not checked filters, not-checked reasons inline after the
  description, columns renamed (Checks, Expected, In part). The copy buttons are gone: right-click for
  **Copy part number** (all selected rows) or press Ctrl+C.
- **Search** filters surfaces by surface number, segment or part number.
- **Fixed:** right-click **Open in Inventor** on a part row did nothing (the menu never saw the row).
  The wireframe preview opened partly off-screen; it now opens on whichever side of the verifier fits.
  Selected surfaces and tabs were hard to read, grids had a light row-header strip and light scrollbars,
  and not-checked rows were not grey.
- **Copy to all roofs/walls** asks before replacing other surfaces' expectations.
- **Crash guard:** an error inside the verifier window is now logged to `ucv_debug.txt` and shown as a note
  in the footer. Inventor gives add-in windows no WPF `Application`, so before this any uncaught error
  closed Inventor without a log entry.
- Two problems found while testing in Inventor: the window failed to open because Inventor runs WPF in
  .NET 4.0 compatibility, where two system colour keys collide; and clicking a part's description text
  closed Inventor (the click handler could not walk up from a text run). Both are fixed and covered by tests.
- Save Snapshot moved to the footer **⋯** menu.

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
