# Unit Progress Tracker

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

## Supported JSON geometry paths

- `configuration.roof.geometryList[]`
- `configuration.wall.geometryList[]`
- `configuration.unitBase.unitBaseGeometryList[].geometry`

## Run

```bash
cd C:\Users\esmun\Documents\Cursor\UnitSurfaceViewer
npm install
npm start
```

## Data files

| Location | Purpose |
|----------|---------|
| `{project-folder}/.unit-surface-viewer/surface-data.json` | Per-project surface status, checklist, notes |
| `%APPDATA%/unit-surface-viewer/options.json` | Global status colors and checklist template |

## Test data

Sample CONFIG_JSON folders in Ce3:

- `Ce3\ISG\6-26_isg\20116\configs\CONFIG_JSON`
- `Ce3\xml_data\20078\CurrentSurfaces\CONFIG_JSON`
