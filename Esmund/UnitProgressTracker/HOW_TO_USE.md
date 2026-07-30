# Unit Progress Tracker — How to use

Electron app for tracking unit build progress: 3D surface review, checklists, notes, and BOM shell export folder planning.

**Requirements:** Windows, [Node.js](https://nodejs.org/) (LTS), Autodesk Inventor (for IAM scan), Python with `pywin32` (same COM read as Pigeon).

---

## Install and run

1. Open PowerShell in this folder (`Esmund/UnitProgressTracker`).
2. Install dependencies:

   ```powershell
   npm install
   pip install pywin32
   ```

3. Start the app:

   ```powershell
   npm start
   ```

Always use `npm start`. Do **not** open `index.html` in a browser — the Electron bridge will not work.

If the folder came from a zip or OneDrive, unblock it first (folder **Properties → Unblock**) if scripts are blocked.

---

## First-time workflow

1. **File → Open Folder…** (or header **Open Folder…**) and pick the folder that contains your unit’s **391Z surface IAM** files (subfolders are scanned).
2. The app loads cached geometry when available. Use **File → Rescan** to refresh from Inventor (can take several minutes on large units; **Cancel scan** is available).
3. Use **File → Save project as…** to save a portable `.json` project file (geometry + tracking + BOM). After that, **File → Save project** updates that same file.

---

## Surfaces tab

The left panel lists every surface found in the scan.

| Action | How |
|--------|-----|
| Select a surface | Click a row in the list, or pick it in the 3D view |
| Detail panel | Checklist, status, and notes open on the right |
| Hide a surface | Double **right-click** in the 3D view |
| Show all hidden | **Show all** in the panel header |
| Fit view | **Fit View** in the header |

**3D navigation:** Orbit with mouse (configurable in **Options**). Optional FPS mode: WASD move, Space/ Ctrl up/down, Shift sprint.

**Status colors** and **checklist items** are configured under **Options**. Changes apply to all surfaces.

**List display:** Use Name / Sort / tag toggles in the surface list header.

---

## BOM tab

Switch the left panel to **BOM** for 391- assembly export planning.

| Action | How |
|--------|-----|
| Import BOM | **Import BOM…** or **File → Import BOM…** — pick a `BOM_FLAT` `.xlsx` (Sheet1) |
| Add entry manually | **Add entry…** — pick skid, then segment (segments match the selected skid) |
| Remove from export list | **Remove** on a row (re-import BOM restores removed imported lines) |
| Set export root | **Set shell root…** — parent folder where `Shell/` will be created |
| Create folders | **Create Shell folders…** — builds empty `Shell/Skid NN/NN XX/…` tree |
| Open export folder | **Open folder** on a row (requires shell root) |
| Checklist / notes | Click the **entry box** (part number / description area) — detail panel opens on the right |

**Filters:** Search, sort, skid, segment, and **SQ custom only** narrow the list.

**Alerts:** Coil panel lines with `Segment = <--` are skipped and listed in a red banner (fix in the source BOM).

Excluded assemblies (by description) include DOOR, latches, ISO PLT, drain items, etc. — see `src/bom-folder-maker.js` for the current list.

---

## File menu

| Item | Purpose |
|------|---------|
| Save project | Updates the active project file (after Save as) and folder cache |
| Save project as… | Portable `.json` with embedded geometry |
| Load project… | Open a saved project (offline capable) |
| Recent… | Reopen recent scan folders |
| Rescan | Re-read IAM folder via Inventor |
| Import BOM… | Same as BOM tab import |
| Import / Export JSON / Markdown | Surface tracking export (legacy interchange) |

---

## Data locations

| Data | Location |
|------|----------|
| Per-folder tracking (auto-save) | `{scan-folder}/.unit-surface-viewer/surface-data.json` |
| App options (colors, checklist template, BOM filters) | `%APPDATA%/unit-surface-viewer/options.json` |
| Portable project | Path you chose in **Save project as…** |

---

## Tips

- Open a saved project on a PC without Inventor if the `.json` includes embedded geometry.
- After **Save project as…**, use **Save project** to overwrite that file — not only the scan-folder cache.
- On the BOM tab, pick **skid** before **segment** when adding entries manually.
- Custom **SQ** assemblies show an amber **SQ custom** tag in the BOM list.

---

## Troubleshooting

| Issue | Try |
|-------|-----|
| Buttons dead / “app bridge unavailable” | Run with `npm start`, not browser |
| Scan hangs | **Cancel scan**, restart app, **Rescan** |
| BOM add dialog won’t type | Fixed in v1.0.25+ — ensure latest; FPS keys are disabled while dialogs are open |
| Shell folder not found | **Relocate shell root…** if the folder moved |

For Inventor COM errors, confirm Python `pywin32` and that Inventor can open the IAM files outside the app.
