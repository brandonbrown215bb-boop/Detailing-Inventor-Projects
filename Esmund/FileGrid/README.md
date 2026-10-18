# File Grid

A desktop canvas for sorting files: white grid on black, folder boxes you can place, resize, and drop onto to **move** items in the real filesystem. Layouts persist as named save states.

## Run

```bash
npm install
npm run dev
```

### Desktop shortcut (Windows)

Icon assets live in `build/` (from `file_grid_icon_512x512.png`). Generate `.ico` and create a desktop shortcut:

```powershell
npm install
npm run icon
npm run shortcut
```

This creates **File Grid** on your desktop; it runs the Electron app in the background (no terminal window). Launchers: `scripts\Launch-FileGrid-Electron.vbs` or `.cmd`. For dev logs, run `npm run dev` in a terminal instead.

Re-run `npm run shortcut` after pulling launcher changes so the desktop link picks them up.

## Controls

| Action | Input |
|--------|--------|
| Pan | Middle-mouse drag, or right-button drag on the canvas |
| Zoom | Mouse wheel |
| Move box | Drag the box title bar |
| Resize | Drag the bottom-right handle (snaps to grid units) |
| Drop files | From Explorer onto a box (moves into that folder) |
| Name conflict | Compare thumbnails + size/date, then **Replace** / **Keep both** / **Skip** (optional apply to all) |
| Add box | **+ Folder box**, or drag a folder onto **Drop folder to add box** |
| Open folder | **Shift+click** a box (or toolbar **Open folder** when selected) |
| Settings | Text scale, hide paths, colors, fonts, rename/duplicate layout |
| Remove box | Select it → **Remove box**, or **Delete** |
| New sorting job | **New layout** or **Save as…** |

Layouts are stored under your app user data folder (`%APPDATA%\file-grid\layouts` on Windows).

## Build

```bash
npm run build
npm run preview
```

### Windows zip package (for coworkers)

Creates `dist/FileGrid-Electron-Windows.zip` with source, built `out/`, and `Setup-FileGrid.bat`:

```powershell
npm run package:zip
```

Recipients unzip, run **Setup-FileGrid.bat** once (needs Node.js LTS), then use the **File Grid** desktop shortcut. The shortcut runs the built app (`npm run preview`) without a visible terminal.

## Portable C# build (optional, for coworkers)

A **WPF .NET 8** version lives in [`FileGrid.Portable/`](FileGrid.Portable/README.md). It does not replace this Electron app; it uses the same layout JSON format. Build a single-file exe with:

```powershell
cd FileGrid.Portable
.\publish-portable.ps1
```
