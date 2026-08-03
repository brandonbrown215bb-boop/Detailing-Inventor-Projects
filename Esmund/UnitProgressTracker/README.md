# Unit Progress Tracker

**Current version: 1.0.40** (2026-07-30)

Electron app for tracking unit build progress: 3D surface review, per-surface checklists and notes, and BOM shell export folder planning.

## Features

- Open a folder (including subfolders) of surface JSON files named by surface number (e.g. `391Z010142-0003.json`)
- Renders each surface as axis-aligned boxes from `x`, `y`, `z`, `xLength`, `yLength`, `zLength` (inches, assembly origin)
- Pan, rotate, and zoom with OrbitControls (same interaction pattern as Ce3)
- Surface number labels on each surface
- Click a surface (3D or list) to open a detail panel with status, checklist, and notes
- Configurable status colors in Options (defaults: Current, Corrected, Built, Associated, Paperwork Corrected, Paperwork Uploaded, Done)
- Persists per-folder data in `.unit-surface-viewer/surface-data.json`
- Export all surface status/checklist/notes as JSON or Markdown
- BOM tab: Config.xml import; segment→skid placement; shell folder export
- Replace / add / retire surfaces without full-folder rescan (v1.0.35+)

## Supported JSON geometry paths

- `configuration.roof.geometryList[]`
- `configuration.wall.geometryList[]`
- `configuration.unitBase.unitBaseGeometryList[].geometry`

## Run

```bash
cd Esmund/UnitProgressTracker
npm install
py -3 -m pip install pywin32   # first time only
npm start
```

Or double-click `Start Unit Progress Tracker.vbs`.

## Data files

| Location | Purpose |
|----------|---------|
| `{project-folder}/.unit-surface-viewer/surface-data.json` | Per-project surface status, checklist, notes |
| `%APPDATA%/unit-surface-viewer/options.json` | Global status colors and checklist template |

## Documentation

- [HOW_TO_USE.md](./HOW_TO_USE.md) — full user guide
- [STABLE-BASE.txt](./STABLE-BASE.txt) — current build notes
- [SETUP.txt](./SETUP.txt) — quick install

## Version history (recent)

| Version | Date | Highlights |
|---------|------|------------|
| **1.0.40** | 2026-07-30 | Replace confirm: OK=pick .iam; py -3 launcher |
| 1.0.39 | 2026-07-30 | Replace scan path + Inventor read fixes |
| 1.0.34 | 2026-07-30 | BOM Config.xml import |
| 1.0.23 | 2026-07-29 | Prior stable base (BOM sort/filter) |
