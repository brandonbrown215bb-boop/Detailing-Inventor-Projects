# Unit Construction Verifier — How to use

**Esmund fork** — Inventor add-in for verifying unit construction against scanned expectations: surfaces, parts, properties, and visual highlight.

This copy lives under `Esmund/UnitConstructionVerifier` and is **separate from** the root `UnitConstructionVerifier/` folder in this repo (co-worker baseline).

**Requirements:** Windows, Autodesk Inventor 2020, .NET Framework 4.8 SDK (Visual Studio Build Tools or VS).

---

## Build and install

From this folder (`Esmund/UnitConstructionVerifier`):

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
- Use **Edit Mode** flows in the UI as documented in the window — apply only after reviewing the grid.

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
| Highlight missing on direct IAM open | Ensure you’re on this fork (v1.0.17+ feature) |
| Property write-back failed | Confirm IPT is sheet metal and parameter names match config JSON |
| Accidental paste dialog | Cancel in Inventor; see clipboard section above |

---

## Related files

| File | Purpose |
|------|---------|
| `STABLE_BASE.md` | Fork feature summary and version notes |
| `Docs/known_issues.md` | Operational troubleshooting |
| `UnitConstructionVerifier/materials_config.json` | Materials / thickness mapping |
