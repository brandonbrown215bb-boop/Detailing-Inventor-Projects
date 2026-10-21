# Unit Construction Verifier — Release Notes

Root `UnitConstructionVerifier/` is the release line. Roadmap: `Docs/project_review.md` section 0.

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
