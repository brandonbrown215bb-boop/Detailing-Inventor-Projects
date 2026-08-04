# Unit Progress Tracker — How to use

**Current version: 1.0.43** (2026-08-04)

Electron app for tracking unit build progress: 3D surface review, checklists, notes, and BOM shell export folder planning.

**Requirements:** Windows, [Node.js](https://nodejs.org/) (LTS), Autodesk Inventor (for IAM scan), Python 3 with `pywin32` (`py -3 -m pip install pywin32`).

---

## Install and run

1. Open PowerShell in this folder (`Esmund/UnitProgressTracker`).
2. Install dependencies (first time only):

   ```powershell
   npm install
   py -3 -m pip install pywin32
   ```

3. Start the app:

   ```powershell
   npm start
   ```

   Or double-click **Start Unit Progress Tracker.vbs**.

Always use `npm start` or the VBS launcher. Do **not** open `index.html` in a browser — the Electron bridge will not work.

If the folder came from a zip or OneDrive, unblock it first (folder **Properties → Unblock**) if scripts are blocked.

---

## First-time workflow

1. **File → Open Folder…** and pick the folder that contains your unit's **391Z surface IAM** files (subfolders are scanned).
2. The app loads cached geometry when available. Use **File → Rescan** to refresh from Inventor (can take several minutes on large units; **Cancel scan** is available).
3. Use **File → Save project as…** to save a portable `.json` project file (geometry + tracking + BOM + status/checklist templates). After that, **File → Save project** updates that same file.

---

## Surfaces tab

| Action | How |
|--------|-----|
| Select a surface | Click a row in the list, or pick it in the 3D view |
| Detail panel | Checklist, status, and notes on the right |
| Replace surface | Pick a single 391Z `.iam` via Inventor (no full-folder rescan) |
| Add surface(s) | Add from folder |
| Remove surface | Retire from active list; view **Removed** list to restore |
| Renumber / history | **Renumber & history** in detail panel — change display number, link retired numbers, **Remove** bad history entries |
| Hide a surface | Double **right-click** in the 3D view |
| Fit view | **Fit View** in the header |

**3D navigation:** Orbit with mouse (configurable in **Options**). Optional FPS mode: WASD move, Space/Ctrl up/down, Shift sprint. **Ctrl+W does not close the app** (used for FPS forward).

---

## Status and checklist templates

| What | Where stored |
|------|----------------|
| Custom **Status** types and **Checklist** item labels (per job) | Portable project `.json` → `projectOptions` (v1.0.42+) |
| Which status/checkbox each surface uses | Project `.json` → `surfaces` |
| Global UI (layout, theme, viewer keys) | `%APPDATA%/unit-surface-viewer/options.json` |

After adding custom statuses or checklist items in **Options**, **Save project** so they travel with the job file. Loading an older project without `projectOptions` still works — placeholder rows are created for any status/checklist IDs referenced in the data.

---

## BOM tab

| Action | How |
|--------|-----|
| Import BOM | **Import BOM…** — `BOM_FLAT` `.xlsx` or **Config.xml** (segment→skid from config) |
| Add entry manually | **Add entry…** — pick skid, then segment |
| Create folders | **Create Shell folders…** — builds `Shell/Skid NN/NN XX/…` tree |
| Open export folder | **Open folder** on a row |

**Filters:** Search, sort, skid, segment, and **SQ custom only**.

---

## File menu

| Item | Purpose |
|------|---------|
| Save project / Save project as… | Portable `.json` with embedded geometry and projectOptions |
| Load project… | Open saved project (offline capable) |
| Recent… | Reopen recent scan folders |
| Rescan | Re-read IAM folder via Inventor |
| Import BOM… | BOM xlsx or Config.xml |

---

## Data locations

| Data | Location |
|------|----------|
| Per-folder tracking | `{scan-folder}/.unit-surface-viewer/surface-data.json` |
| App UI options | `%APPDATA%/unit-surface-viewer/options.json` |
| Portable project | Path from **Save project as…** (includes `projectOptions` when saved on v1.0.42+) |

---

## Troubleshooting

| Issue | Try |
|-------|-----|
| Buttons dead / bridge unavailable | Run with `npm start`, not browser |
| Scan fails on work laptop | Ensure `py -3` works; sidecar uses Python launcher on Windows |
| Replace shows no geometry | Confirm `.iam` path; ensure Inventor and py -3 sidecar work |
| Inventor COM errors | Confirm Inventor can open the IAM files; install `pywin32` |
| Custom statuses missing after Load project | Re-save project on v1.0.42+ after editing Options, or copy work-laptop `options.json` once |
| Renamed surfaces missing after reload | Upgrade to v1.0.41+; reload project file |
| Can't close app / stuck on exit | Upgrade to v1.0.43; use **File → Save project**, or reload project to clear dirty flag |

---

## Version history (recent)

| Version | Highlights |
|---------|------------|
| **1.0.43** | Autosave to project file when folder path unavailable; failed autosave no longer blocks close |
| **1.0.42** | `projectOptions` in project JSON — per-job status/checklist templates |
| **1.0.41** | Renumber reload fix; **Remove** on surface history rows |
| 1.0.40 | Replace confirm OK=pick `.iam`; py -3 launcher |
| 1.0.39 | Replace scan path fixes; MOM_DATA read |
| 1.0.34 | BOM Config.xml import |
| 1.0.23 | Prior stable base |
