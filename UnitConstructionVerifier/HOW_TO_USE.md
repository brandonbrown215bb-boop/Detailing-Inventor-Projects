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

The verifier is **modeless** — you can pan and rotate the model in Inventor while it stays open.

### Docked or in its own window

- **Dock** (header, right end) moves the verifier into a panel inside Inventor, on the right of the model the first
  time. Drag the panel's title bar to dock it left or at the bottom, or to float it; Inventor remembers where.
  **Pop out** moves it back to its own window. The selected surface, filters, staged edits and unsaved expectations
  move with it. The next unit opens the way you last left it.
- Narrower than about 1040 px (a side dock, or a small window) the verifier switches to one column: surfaces on top,
  **Expected construction** collapsed until you click it (its fields read top to bottom), and the parts grid shows
  Status, Part, Expected and In part. Description, Type and Checks come back when it is wider.
- The dock's own ✕ only hides it. Run the command again on the same unit to bring it back as it was. Running it on a
  different unit closes the open one first, asking about unsaved changes.
- The verifier keeps checking the unit it was opened on. When Inventor switches to a different assembly, a yellow bar
  says so; **Check this unit** closes the current check (asking about unsaved changes) and checks the one Inventor is
  on. Opening one of the unit's own surface sub-assemblies does not count as another unit. Selecting a surface in the
  verifier switches Inventor back to the verifier's unit, so the highlight lands on the right model.
- Closing Inventor does not ask about unsaved changes in a docked verifier. Click **Save expectations** first.

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
- On the Roof tab, **Trim** is roof trim: the roof caps and roof corner caps. Changing it changes what those parts
  are checked against (shown as *Roof Trim Gauge & Material*). It starts from the wall exterior, the roof's side
  skins. Other trim stock parts (091-30117-011/195/196) are not automated and are listed as **Display only**.
- **Copy to all roofs… / Copy to all walls…** lists which other surfaces would change and which fields
  (for example `Exterior gauge 16 → 18`) before copying. Surfaces that already match are left alone; if they all
  match, nothing is asked and the footer says so.
- **Upturned lip** and **Curb rest** are unit-wide settings, under **Unit** on the Base & Floor tab.
- Expectations come from the unit's own configuration and engineering data: each roof/wall uses its own side of the unit, sub-floor comes from the base configuration, and perimeter angles on bases with an upturned lip (UTL) are 10 GA aluminum/stainless or 12 GA STL GALV by floor type (16 GA STL GALV otherwise). On a base whose lip runs on only some sides, the standard angles on the non-lip sides show as mismatches by design; see `Docs/known_issues.md`. See `Docs/part_types_classification.md`.

### Parts grid

- **All / Issues / Not checked** filter the grid; each shows its count.
- **Status** is a colored chip: **✓ Pass**, **✗ Mismatch**, **Display only**, **– Not checked**, or **● Staged**.
  Hover the chip for the reason or, on a staged row, the old and new value.
- On a staged row, **In part** shows the part's current value and the staged one: `20 GA STL GALV → 18 GA STL GALV`
  (hover if the cell is too narrow). Double-clicking it in Edit parts still edits the staged value.
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
- **Regenerate changed (N)…** and **Upload changed (N)…** run YCShell's Regenerate Channel Features and Upload
  Changes on every surface that needs it (see below). N is the number of surfaces that need it.
- **Save expectations** keeps expectation changes for this unit in the sidecar file next to the IAM.
- **Re-check unit** re-reads the unit from Inventor, then runs every check again. It follows surfaces MOM renamed on
  upload (see below).
- **⋯** holds **Surface number history…** and **Save snapshot for regression tests…** (see below).

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
- **Review & write to parts…** opens a review over the window listing every staged change: part, property, the
  value now, and the value it will be. Untick any change you do not want written yet; unticked changes stay
  staged. **Write N changes** writes the ticked ones in one Inventor transaction (part files must be checked out
  of Vault); **Cancel** writes nothing. While it writes, a progress panel shows which part it is on
  (Inventor opens, updates and saves each part in turn, which takes a while on many parts), then the verifier
  re-checks its own unit.
- If a staged edit cannot be written to a part property, the review shows it struck through and unticked.

### Unsaved changes

- Changing an expectation (or the lip/curb settings) shows **● Unsaved expectation changes** in the footer until you click **Save expectations**.
- Closing the verifier with unsaved expectation changes or staged part edits asks first: **Yes** saves expectations and closes, **No** closes without saving, **Cancel** keeps the window open. Staged part edits are never written on close.
- Expectation dropdowns always show the surface's stored value, even when it is not in the standard list (it appears at the top of the list). A blank value stays blank; Thermal Break no longer shows "No" for a blank value.

---

## Regenerate changed and Upload changed

These run YCShell's own **Regenerate Channel Features** and **Upload Changes** for every surface of the unit that
needs it, instead of opening and clicking each surface. UCV only drives YCShell, which does the work as usual.
Requirements:

- The JCI MOM (YCShell) add-in loaded, and you logged in to the MOM tools.
- The unit open in Inventor.
- No staged part edits: write or discard them first.

### Which surfaces count as changed

UCV reads the change markers YCShell keeps in the files, live, each time you press a button:

| Button | A surface is picked when… |
|---|---|
| **Upload changed** | anything changed since its last upload: a changed or new part, an added occurrence (for example a patterned channel), or moved or deleted components |
| **Regenerate changed** | its **channels** were added (including patterned), reshaped or moved since they were last regenerated |

- A skin, liner or subfloor change, or a gauge or material edit made in UCV, needs **Upload** but not
  **Regenerate**.
- **Bases are never offered for Regenerate.** YCShell regenerates only walls and roofs.
- **Orange rows are picked but uncertain.** Either YCShell flagged moved or deleted components without saying which,
  or a channel was edited by an older UCV version that kept no record. They are ticked anyway: better to regenerate
  one surface too many.
- **After UCV regenerates a surface,** it remembers the surface's channels (`.ucv_channel_fingerprints.json` next
  to the unit). The surface isn't offered again until a channel is added, moved or changed.

### Regenerate changed

1. Press **Regenerate changed (N)…**. A review lists every wall and roof with the reason, changed ones ticked.
   Untick any you want to skip.
2. Leave **Close YCShell's information boxes automatically** on. UCV then closes YCShell's "Channel Features have
   been recreated/replaced" box for you. **Yes/No questions from YCShell always wait for you.**
3. Press **Regenerate N surfaces**. The surfaces run one after another, each with YCShell's own progress window. A
   small window in the top right shows progress and has **Stop after this surface**.
   - A surface with suppressed parts is skipped; unsuppress them and run it again.
   - **YCShell saves each surface as it regenerates it.** Closing Inventor without saving does not undo it.
4. At the end, the unit is re-checked and a summary lists each surface: Done, Done with your answers, Not
   regenerated by YCShell, Skipped, Failed, or Not run (stopped).

Expect roughly 5 seconds to 2 minutes per surface.

### Upload changed

1. Press **Upload changed (N)…**. A review lists every surface with its changes.
   - A surface whose channels still need Regenerate is orange: "still needs Regenerate". Run Regenerate changed
     first, or upload anyway.
   - The unit IAM goes along with the upload, so YCShell can swap in surfaces MOM renames.
2. Press **Upload N surfaces**. UCV first runs YCShell's own checks on every ticked surface: suppressed parts, and
   structural parts with holes. If anything fails, nothing is uploaded and you get the full list.
3. **One YCShell "Upload Changes To MOM" window** opens with all the ticked surfaces. Check it and press **Upload**
   there, exactly as for one surface. Close it when it says the upload is complete.
   - Part # can show **N/A** for surfaces MOM renamed: YCShell has already swapped them for the new file. That is
     expected.
4. The unit is re-checked and a result lists each surface:
   - **uploaded**;
   - **renamed by MOM and relinked in the unit**, for example 0021 → 0048. No manual relink is needed;
   - **renamed but not found in the unit**: relink it by hand and save;
   - **still changed**: not uploaded, or the upload failed. See YCShell's upload log under
     `%LOCALAPPDATA%\Autodesk\ISG\MOM_YCShell\Logs`.

Expect about a minute per surface plus a few minutes overall. 8 surfaces took about 15 minutes, with one press.

### Renamed surfaces and number history

When MOM gives a surface a new number, its expectations, channel record and surface row follow it. That happens
when the verifier opens, on Re-check, and after Upload changed. **⋯ > Surface number history…** lists every number
each surface has had, with dates (for example 0021 → 0048 → 0049 → 0122), and copies the list to the clipboard. It
is kept in `.ucv_surface_history.json` next to the unit.

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

While the verifier has focus, docked or in its own window, every key goes to the verifier: typing, arrows, Tab,
**Ctrl+C**, **Ctrl+V** and **F8** work as they would in any window, and Inventor's own shortcuts do not fire. Alt
shortcuts still go to Inventor.

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
| Regenerate / Upload changed says YCShell can't be reached | Load the JCI MOM add-in and log in to the MOM tools; details are in the UCV debug log |
| "YCShell version not supported: …" | YCShell was updated and a part UCV relies on changed; the buttons stay off until UCV is updated |
| A surface you changed isn't offered | See *Known issues: Regenerate / Upload changed* |

---

## Related files

| File | Purpose |
|------|---------|
| `RELEASE_NOTES.md` | Version history |
| `Docs/project_review.md` | Roadmap to the v1 release (section 0) |
| `Docs/config_rules_guide.md` | Adding part classification rules |
| `Docs/known_issues.md` | Operational troubleshooting |
| `UnitConstructionVerifier/materials_config.json` | Materials / thickness mapping |
