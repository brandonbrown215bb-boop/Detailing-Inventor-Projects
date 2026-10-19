# File Grid — CLI agent handoff

**Last updated:** 2026-10-18 (evening batch)  
**Canonical dev folder (user machine):** `C:\Users\esmun\Documents\Cursor\file_grid`  
**This repo path:** `Esmund/FileGrid` in [Detailing-Inventor-Projects](https://github.com/brandonbrown215bb-boop/Detailing-Inventor-Projects)

Sync dev → repo before push:

```powershell
robocopy "C:\Users\esmun\Documents\Cursor\file_grid" "C:\Users\esmun\Documents\Cursor\Detailing-Inventor-Projects\Esmund\FileGrid" /MIR /XD node_modules dist out /XF "File Grid.lnk"
```

---

## What this app is

Electron + React + TypeScript (`electron-vite`). Visual canvas for **folder** and **file shortcut** boxes; folder boxes accept Explorer drops to **move** files into that folder. Named layouts, connect strings between boxes, settings for UI/dock/zoom.

**User data (not in repo):** `%APPDATA%\file-grid\`

- `layouts/` — JSON layouts  
- `settings.json` — app settings  
- `session.json` — last active layout id  

**Portable zip for work:** from project root, `npm run package:zip` → `dist\FileGrid-Electron-Windows.zip` (~1.4 MB). Coworker runs `Setup-FileGrid.bat` (needs Node LTS).

---

## Stack & commands

| Task | Command |
|------|---------|
| Dev | `npm install` then `npm run dev` |
| Production build | `npm run build` |
| Run built app | `npm run preview` |
| Package zip | `npm run package:zip` |
| Desktop shortcut | `npm run icon` then `npm run shortcut` |

Main process: `src/main/` (`index.ts`, `fileOps.ts`, `layouts.ts`, `settings.ts`, `folderTree.ts`)  
Renderer: `src/renderer/src/` (`App.tsx`, `components/`, `folderTreeLayout.ts`)  
Shared types: `src/shared/types.ts`, `normalizeLayout.ts`, `linkLineStyle.ts`  
Preload API: `src/preload/index.ts` → `window.fileGrid`

Layout schema version: **2** (`LAYOUT_SCHEMA_VERSION` in `types.ts`).

---

## Features shipped (this version)

### Boxes

- **Folder** (`kind: 'folder'`) — default; drop files onto box to move into folder.  
- **File** (`kind: 'file'`) — `folderPath` holds file path; small file icon lower-left; **Shift+left-click** or toolbar **Open file**; drops onto file boxes are ignored.  
- **+ Folder**, **+ File**, **Drop File**, **Drop Folder** / **Drop Tree** on toolbar.

### Drop modes (toolbar button)

- **Click** folder drop button toggles **Drop Folder** ↔ **Drop Tree** (persisted in `settings.folderDropMode`).  
- **Drop Folder:** one box per dropped folder.  
- **Drop Tree:** scan subfolders (see settings), **simple tree layout** (parent above children, siblings left-to-right), auto **strings** parent → child.  
- Blocked roots: drive letters, `Windows`, `Program Files`, etc. (`src/main/folderTree.ts`).

### Settings → Folder drop

- `folderTreeMaxDepth` (0–15, default 7) — root is depth 0.  
- `folderTreeMaxBoxes` (20–800, default **250**) — user asked not to change default cap without discussion.  
- `folderTreeExcludeNames` — e.g. `OldVersions`, `.git`, `node_modules`.

### View / selection

- **Home** — reset view to `DEFAULT_HOME_VIEW` `{ panX: 80, panY: 80, zoom: 1 }`.  
- **Z-order:** ↑/↓ one step; **Ctrl+↑** to front, **Ctrl+↓** to back; toolbar **To front** / **To back**. Only when box selected and not typing in an input.

### Other (existing)

- Connect strings, line styles, relink missing paths, conflict modal on move, dock position, zoom modes, quick edit on canvas, etc.

---

## Intentionally not done / deferred

- **Templates** — scrapped by user.  
- **Layered grid (depth columns)** tree layout — replaced by simple tree.  
- **Tree link styling** — all auto tree links use same solid style as manual links; user may want dashed/lighter later.  
- **Tree layout tuning** — spacing constants in `folderTreeLayout.ts` (`GW`, `GH`, `SIBLING_GAP`, `LEVEL_GAP`); may need settings if wide Inventor trees feel cramped.  
- **Pre-drop confirm** for huge tree scans (>100 boxes) — discussed, not implemented.  
- **Multi-select tree drop policy** — currently each dropped root = separate tree stacked vertically.  
- **WPF portable** (`FileGrid.Portable/`) — not parity-tracked with Electron for this batch.

---

## Key files for common tasks

| Change | Where |
|--------|--------|
| Tree scan / blocklist | `src/main/folderTree.ts` |
| Tree positions + edges | `src/renderer/src/folderTreeLayout.ts` |
| Tree drop + auto links | `App.tsx` → `addTreesFromPaths` |
| File/folder drop buttons | `components/FileDropButton.tsx`, `FolderDropButton.tsx` |
| Box UI | `components/FolderBox.tsx` |
| Settings schema | `src/shared/types.ts`, `src/main/settings.ts`, `SettingsPanel.tsx` |
| IPC | `src/main/index.ts`, `src/preload/index.ts` |

---

## Git workflow

Repo: `https://github.com/brandonbrown215bb-boop/Detailing-Inventor-Projects.git`  
File Grid lives under **`Esmund/FileGrid/`** only — do not commit unrelated `Esmund/Highlighter` changes unless user asks.

```powershell
cd C:\Users\esmun\Documents\Cursor\Detailing-Inventor-Projects
git add Esmund/FileGrid
git status
git commit -m "..."
git push origin main
```

User rule: **only commit when asked**; morning session may ask for fixes only.

---

## Likely morning follow-ups

1. **Tree spacing** on real Inventor subfolder drops — adjust gaps or add settings.  
2. **Auto tree strings** visually busy — optional dashed/color for `links` created in `addTreesFromPaths` only.  
3. **README.md** is partly stale (no file boxes, tree, Home, z-order) — update if user wants docs aligned.  
4. **Registry** — `C:\Users\esmun\Documents\Cursor\docs\registry.yaml` has no File Grid entry yet; add if user archives/projects policy requires it.

---

## Testing checklist (quick)

1. `npm run build` — must pass.  
2. Drop Tree on a **small** subfolder — boxes + parent-child strings.  
3. Drop File — file boxes; Shift+click opens.  
4. Ctrl+↑ on buried box — jumps to front.  
5. `npm run package:zip` — zip appears under `dist/`.
