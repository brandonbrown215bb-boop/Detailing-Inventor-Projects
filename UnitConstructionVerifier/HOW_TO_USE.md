# Unit Construction Verifier — How to use

Inventor add-in for verifying unit construction against scanned expectations: surfaces, parts, properties, and visual highlight.

This root `UnitConstructionVerifier/` copy is the **release line** for detailers. `Esmund/UnitConstructionVerifier` is a separate fork and is not what detailers install.

**Requirements:** Windows, Autodesk Inventor 2020, .NET Framework 4.8 SDK (Visual Studio Build Tools or VS).

---

## Build and install

From this folder (`UnitConstructionVerifier`):

```bat
build-and-install.bat
```

That runs `compile.bat` then `deploy.bat`, copying the add-in to:

`%APPDATA%\Autodesk\Inventor 2020\Addins`

**Restart Inventor** after deploy.

Manual steps:

```bat
compile.bat
deploy.bat
```

If deploy fails because the DLL is locked, close Inventor and run again.

---

## Opening the verifier

After Inventor loads the add-in, run the **Unit Construction Verifier** command from the Add-Ins tab (exact ribbon label depends on your `.addin` manifest).

The verifier window is **modeless** — you can pan and rotate the model in Inventor while it stays open.

---

## Main window

### Surface list

- Lists surfaces from the construction scan for the active unit.
- **Click a row** to highlight that surface in Inventor.
- **⧉** buttons copy part or surface **text** to the Windows clipboard (use these instead of Ctrl+C for numbers).

### Highlight modes

| Mode | Behavior |
|------|----------|
| **Default (locator box)** | Oriented range box around the part/surface — does not use Inventor SelectSet |
| **X-Ray** | Detailed edge outline within the active surface |
| **Normal** | Restores native Inventor green prehighlight; also restores when the window closes |

Pick highlight color from the header swatch (shared with X-Ray palette).

### Expectations / parts grids

- Review expected vs actual construction data per surface.
- Each part row shows **✓ Pass**, **✗ Mismatch** (red), **Display only**, or **– Not checked** (grey italic). Hover a not-checked row for the reason: no expected value, the part sits in the wrong surface type, or its IAM has no surface row.
- Parts no rule in `materials_config.json` covers are not verified and not listed.
- **Run Verification Check** reports how many parts were checked and how many could not be.
- Parts changed after the unit was generated (SQ parts, or parts edited here) show **· modified** after their status. A freshly generated unit has none.
- Expectations come from the unit's own configuration and engineering data: each roof/wall uses its own side of the unit, sub-floor comes from the base configuration, and perimeter angles on bases with an upturned lip (UTL) are 10 GA aluminum/stainless or 12 GA STL GALV by floor type (16 GA STL GALV otherwise). On a base whose lip runs on only some sides, the standard angles on the non-lip sides show as mismatches by design; see `Docs/known_issues.md`. See `Docs/part_types_classification.md`.
- **Apply** actions push approved edits (e.g. casing, properties) to parts.
- Splitters between sections can be dragged to resize.

### Wireframe preview

- Opens a separate **modeless** preview window.
- Middle-drag to pan; shows sheet metal cuts and standard hole features.

---

## Edit mode and IPT write-back

When editing part properties from the verifier:

- Config-driven parameter names (e.g. **Exterior Gauge & Material**) sync to sheet metal IPTs.
- Changes save even if the part is already open in Inventor.
- Edited values go to the user-defined iProperties (`Thickness`, `YCMATL`, `INPUT_PARAMETER_*`) **and** the `MOM_DATA` attribute set (`ORIGINAL_SHEET_METAL_THICKNESS`, `MATERIAL_TYPE_CODE`). `MOM_DATA` is read first when present.
- Every edited part is stamped `IS_HARD_EDITED = TRUE`, `MOM_DATA.PART_SOURCE = SQPart`, `MOM_DATA.DOCUMENT_STATUS = Changed_SQ`, and gets the `EditedAppearance` color from `materials_config.json` (default **Yellow**).
- Write-back has not yet been signed off on real units; check edited parts in Inventor before releasing them.
- Use **Edit Mode** flows in the UI as documented in the window — apply only after reviewing the grid.
- Part values can be edited only while **Edit Mode** is on, and only on rows whose checked parameter can be written back (thickness, gauge & material, base/formed-channel material). Other rows stay read-only, and **Sync** skips them.
- If a staged edit cannot be written, the confirmation lists it as skipped instead of dropping it silently.

### Unsaved changes

- Changing an expectation (or the lip/curb settings) shows **● Unsaved expectation changes** in the footer until you click **Save Overrides**.
- Closing the verifier with unsaved expectation changes or staged part edits asks first: **Yes** saves expectations and closes, **No** closes without saving, **Cancel** keeps the window open. Staged part edits are never written on close.
- Expectation dropdowns always show the surface's stored value, even when it is not in the standard list (it appears at the top of the list). A blank value stays blank; Thermal Break no longer shows "No" for a blank value.

---

## Save Snapshot (regression baselines)

**Save Snapshot** in the footer writes `<unit>.unit_snapshot.json` (default: next to the IAM) with the scan data the verifier built its expectations from. Use it on units you know are correct, then add them to the tests as described in `UnitConstructionVerifier.Tests/Snapshots/README.md`. It does not change any Inventor files.

---

## Working with assemblies

- Highlight works when a **surface sub-assembly IAM is opened directly**, not only from the unit root.
- **Open in Inventor** (context menu) uses scanned file paths; you get a clear message if the file is missing.

---

## Clipboard and Ctrl+V (important)

Default highlight uses a **locator box**, not Inventor’s selection set.

- **Ctrl+C in the verifier does not** put the highlighted row on Inventor’s assembly clipboard.
- If the unit IAM is active, **Ctrl+V in Inventor** may still paste an **older** Inventor copy and trigger missing-reference dialogs.

**Workaround:** Use **⧉** for part/surface numbers. Avoid Ctrl+V in Inventor while verifying unless you know what’s on the clipboard.

Full notes: `Docs/known_issues.md`

---

## Checking many units without Inventor open

`ucv-scan` reads unit folders read-only and writes one snapshot and outcome per unit plus a `summary.csv`.
See `UnitConstructionVerifier.Scan/README.md`.

---

## Packaging

```bat
package-source.bat
```

Creates a source zip (respects `_package_exclude.txt`). Useful for transfer to another PC — rebuild with `build-and-install.bat` there.

---

## Troubleshooting

| Issue | Try |
|-------|-----|
| Add-in not in Inventor | Confirm deploy path, restart Inventor, check `.addin` in Addins folder |
| Deploy “file locked” | Close Inventor; deploy renames old DLL with timestamp |
| Highlight missing on direct IAM open | Ensure you’re on v1.0.17 or later |
| Property write-back failed | Confirm IPT is sheet metal and parameter names match config JSON |
| Accidental paste dialog | Cancel in Inventor; see clipboard section above |

---

## Related files

| File | Purpose |
|------|---------|
| `RELEASE_NOTES.md` | Version history |
| `Docs/project_review.md` | Roadmap to the v1 release (section 0) |
| `Docs/config_rules_guide.md` | Adding part classification rules |
| `Docs/known_issues.md` | Operational troubleshooting |
| `UnitConstructionVerifier/materials_config.json` | Materials / thickness mapping |
