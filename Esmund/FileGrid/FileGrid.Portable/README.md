# File Grid — Portable (C# / WPF)

Windows-native companion to the Electron **File Grid** app in the parent folder. Same JSON layout format (`layouts/*.json`, `settings.json`, `session.json`). The Electron app is unchanged.

## Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (to build)
- Windows 10/11 (WPF)

## Run from source

```powershell
cd FileGrid.Portable
dotnet run
```

## Build a portable single-file exe

```powershell
cd FileGrid.Portable
.\publish-portable.ps1
```

Output: `publish\FileGrid.exe` (self-contained, win-x64). Copy to a folder with coworkers:

- `FileGrid.exe`
- `portable.txt` (optional marker; creates/uses `data\` next to the exe)
- Or let each user run once — data goes to `%APPDATA%\FileGridPortable` without `portable.txt`

## Data locations

| Mode | Path |
|------|------|
| Portable | `<folder>\data\` (when `portable.txt` exists or `data\` already exists) |
| Installed-style | `%APPDATA%\FileGridPortable\` |

Electron uses `%APPDATA%\file-grid\` — layouts can be copied between apps if you copy the `layouts` folder and JSON files (same schema).

## Feature parity (portable build)

Included: grid pan/zoom, folder boxes, multi-folder drop on toolbar, file drop to move, conflict dialog (size/date), layouts, settings, box strings/links, shift+click open folder.

Electron-only for now: rich conflict thumbnails, web font bundles, some UI polish.

## Project layout

- `Models/` — JSON DTOs (camelCase, matches Electron)
- `Services/` — file move, layout/settings storage
- `Views/` — grid surface, dialogs
- `MainWindow.xaml` — toolbar + wiring
