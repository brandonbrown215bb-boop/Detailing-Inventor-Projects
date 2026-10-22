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

### Header

- The unit file name, then a status pill: **✓ All N checked parts match** or **✗ N mismatches on M surfaces**.
  It updates on its own whenever an expectation changes; there is nothing to run first.
- **N not checked** appears when a rule covers a part but it could not be checked.
- **Next issue** jumps to the next surface with a mismatch, opens its tab, and selects the first
  mismatched part.
- **Highlight** chooses how the selected surface or part is shown in Inventor:

| Mode | Behavior |
|------|----------|
| **Box** (default) | Colored locator box around the part or surface. Does not use Inventor's selection set. |
| **X-Ray** | Detailed edge outline of the selected part within its surface. |
| **Off** | No verifier highlight; Inventor's normal green selection is restored (same as Highlighter's Normal). Stays off until you pick Box or X-Ray. Closing the window also restores normal selection. |

  The round swatch next to it picks the highlight color.
- **Edit parts** turns on part editing (see below).

### Surface tabs and list

- Tabs show a red count of mismatches per surface type, or a green ✓ when that type has none.
- Each surface has a status dot (red: mismatches, green: all checked parts match, grey: nothing checked) and a
  red count.
- **Find surface or part…** filters the list by surface number, segment, or the number of any part inside the
  surface.
- **Click a surface** to highlight it in Inventor. Right-click for **Copy surface number** and **Open in Inventor**.

### Expected construction

- Gauge and material sit in labelled columns that do not move when the window is resized. Click the
  **Expected construction** heading to collapse the block and give the parts grid more room.
- **Copy to all roofs… / Copy to all walls…** asks before replacing the other surfaces' expectations.
- **Upturned lip** and **Curb rest** are unit-wide settings, under **Unit** on the Base & Floor tab.
- Expectations come from the unit's own configuration and engineering data: each roof/wall uses its own side of the unit, sub-floor comes from the base configuration, and perimeter angles on bases with an upturned lip (UTL) are 10 GA aluminum/stainless or 12 GA STL GALV by floor type (16 GA STL GALV otherwise). On a base whose lip runs on only some sides, the standard angles on the non-lip sides show as mismatches by design; see `Docs/known_issues.md`. See `Docs/part_types_classification.md`.

### Parts grid

- **All / Issues / Not checked** filter the grid; each shows its count.
- **Status** is a colored chip: **✓ Pass**, **✗ Mismatch**, **Display only**, **– Not checked**, or **● Staged**.
  Hover the chip for the reason or, on a staged row, the old and new value.
- A not-checked part shows its reason after the description: no expected value, the part sits in the wrong
  surface type, or its IAM has no surface row.
- Parts changed after the unit was generated (SQ parts, or parts edited here) show **· modified** after their status. A freshly generated unit has none.
- Parts no rule in `materials_config.json` covers are not verified and not listed.
- Right-click a part for **Copy part number** (all selected rows), **Preview part**, and **Open in Inventor**.
- **Re-check unit** (footer) runs every check again and shows a summary banner with **Go to first issue**.
- The splitter between the surface list and the detail pane can be dragged.

### Wireframe preview

- **Preview part** is enabled once a part is selected. The preview opens beside the verifier, on whichever side
  fits on screen, as a separate **modeless** window.
- Middle-drag to pan; shows sheet metal cuts and standard hole features.

### Footer

- Short confirmations (saved, copied, copied expectations) appear at the bottom left instead of in a dialog.
- **Save expectations** keeps expectation changes for this unit in the sidecar file next to the IAM.
- **⋯** holds **Save snapshot for regression tests…** (see below).

---

## Edit mode and IPT write-back

When editing part properties from the verifier:

- Config-driven parameter names (e.g. **Exterior Gauge & Material**) sync to sheet metal IPTs.
- Changes save even if the part is already open in Inventor.
- Edited values go to the user-defined iProperties (`Thickness`, `YCMATL`, `INPUT_PARAMETER_*`) **and** the `MOM_DATA` attribute set (`ORIGINAL_SHEET_METAL_THICKNESS`, `MATERIAL_TYPE_CODE`). `MOM_DATA` is read first when present.
- Every edited part is stamped `IS_HARD_EDITED = TRUE`, `MOM_DATA.PART_SOURCE = SQPart`, `MOM_DATA.DOCUMENT_STATUS = Changed_SQ`, and gets the `EditedAppearance` color from `materials_config.json` (default **Yellow**).
- Write-back has not yet been signed off on real units; check edited parts in Inventor before releasing them.
- Turn on **Edit parts**, then double-click a value in **In part**, or use **Use expected for selected / for all
  mismatches**. Edits are staged: the footer tray shows how many, and nothing is written until
  **Review & write to parts…**. **Discard** drops them. The tray stays visible while edits are staged, even if
  you turn **Edit parts** off.
- Part values can be edited only while **Edit parts** is on, and only on rows whose checked parameter can be written back (thickness, gauge & material, base/formed-channel material). Other rows stay read-only, and **Sync** skips them.
- If a staged edit cannot be written, the confirmation lists it as skipped instead of dropping it silently.

### Unsaved changes

- Changing an expectation (or the lip/curb settings) shows **● Unsaved expectation changes** in the footer until you click **Save expectations**.
- Closing the verifier with unsaved expectation changes or staged part edits asks first: **Yes** saves expectations and closes, **No** closes without saving, **Cancel** keeps the window open. Staged part edits are never written on close.
- Expectation dropdowns always show the surface's stored value, even when it is not in the standard list (it appears at the top of the list). A blank value stays blank; Thermal Break no longer shows "No" for a blank value.

---

## Save Snapshot (regression baselines)

**Save snapshot for regression tests…** (footer **⋯** menu) writes `<unit>.unit_snapshot.json` (default: next to the IAM) with the scan data the verifier built its expectations from. Use it on units you know are correct, then add them to the tests as described in `UnitConstructionVerifier.Tests/Snapshots/README.md`. It does not change any Inventor files.

---

## Working with assemblies

- Highlight works when a **surface sub-assembly IAM is opened directly**, not only from the unit root.
- **Open in Inventor** (context menu) uses scanned file paths; you get a clear message if the file is missing.

---

## Clipboard and Ctrl+V (important)

Default highlight uses a **locator box**, not Inventor’s selection set.

While the verifier window has focus, it handles **Ctrl+C**, **Ctrl+V** and **F8** itself; Inventor does not see them.

- **Ctrl+C** on a selected part row or surface copies its number as text (same as right-click **Copy part number / Copy surface number**). In a text field it copies the selected text.
- **Ctrl+V** pastes only into text fields. Anywhere else in the verifier it does nothing, so Inventor cannot paste an old copied component into the model.
- Neither puts assembly occurrences on Inventor’s clipboard. Once you click back into Inventor, **Ctrl+V there** still pastes whatever Inventor last copied.

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
