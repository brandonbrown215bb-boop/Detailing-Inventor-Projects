# 1. Adoption of C# .NET 8 WPF Architecture for UnitProgressTracker

* **Status**: Accepted
* **Date**: 2026-08-03

## Context and Problem Statement

`UnitProgressTracker` was initially prototyped as an Electron desktop application (`Esmund/UnitProgressTracker`) utilizing HTML/CSS/JavaScript, Three.js WebGL rendering, and external Python/C# sidecars to interface with Autodesk Inventor COM APIs.

While web technologies provided fast UI mockups, distributing Electron bundled a ~100 MB Chromium/Node.js runtime, incurred high RAM footprint (>250 MB), and required out-of-process sidecar communication for Inventor COM interop.

## Decision Drivers

* Direct native interop with Autodesk Inventor COM APIs without external process sidecars.
* Low memory and disk footprint for fast launch on detailing workstations.
* Native 3D viewport rendering performance.
* Simplified single-file deployment (`dotnet publish -r win-x64 /p:PublishSingleFile=true`).

## Decided Options

1. **Core Application Framework**: **C# .NET 8 WPF** located in `Detailing-Inventor-Projects\Pigeon\UnitProgressTracker`.
2. **3D Viewport Engine**: **HelixToolkit.Wpf / HelixToolkit.Wpf.SharpDX** for hardware-accelerated WPF 3D graphics (Orbit/Pan/Zoom controls, edge rendering, billboard text stickers, specular/diffuse materials).
3. **Autodesk Inventor Integration**: **Dual-Mode COM Interop** (`oleaut32.dll` / `ole32.dll` P/Invoke) running background worker threads to read `DOCUMENT_CONFIG_JSON` iProperties directly from active Inventor instances without freezing the UI.
4. **Data Persistence**: **Dual Persistence Strategy** — auto-saving per-folder surface status, checklist, and notes data to `.unit-surface-viewer/surface-data.json`, while supporting portable `.json` single-file project exports with embedded 3D geometry for offline workstations.
5. **Distribution Strategy**: **Self-Contained Single-File Executable** published via `dotnet publish` to network shares or AddIns directories.

## Consequences

* **Positive**:
  * Eliminates Chromium and Node.js runtime overhead (~60 MB executable vs ~120 MB Electron installer).
  * Direct in-process or background thread COM interop with Autodesk Inventor.
  * Instant startup time and low RAM footprint (~60 MB RAM).
  * Standalone single-file deployment with zero client prerequisites.

* **Negative**:
  * Windows-only platform target (aligned with Autodesk Inventor ecosystem requirement).
