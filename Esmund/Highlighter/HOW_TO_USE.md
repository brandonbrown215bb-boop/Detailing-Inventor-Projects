# Highlighter — How to use

**Current version: 1.4.8.0** (2026-07-30)

Inventor add-in: toggle colored edge highlights on wall/roof skins and liners and base floors. Matched parts are ghosted (transparent) so only the outline remains visible.

**Requirements:** Windows, [.NET SDK](https://dotnet.microsoft.com/download) (`dotnet` on PATH), Autodesk Inventor 2020+.

---

## Install

1. **Close Inventor completely.**
2. Open this folder (`Esmund/Highlighter`) on the target PC.
3. Run **`build-and-install.bat`** (builds from source and installs), or **`install.bat`** if `pack\Highlighter.dll` already exists from a zip.
4. Restart Inventor.
5. **Tools → Add-Ins** — enable **Highlighter**, check **Load on Startup**.

The installer wipes any previous Highlighter or CutHighlight install, copies the DLL to `%APPDATA%\Autodesk\ApplicationPlugins\Highlighter\`, and writes `Highlighter.addin` under each `Inventor 20xx\Addins\` folder.

`install.bat` pauses on error so you can read the message.

---

## Using the panel

Open **Highlighter** from the add-ins ribbon. Toggle types (Wall Skins, Wall Liners, Roof Skins, etc.) and pick colors.

| Mode | Behavior |
|------|----------|
| **All** | Highlight matching parts across the active assembly |
| **Selective** | Pick surface(s), press **Enter** — only that zone stays visible while highlights apply |
| **Normal** | Clear highlights and restore visibility |

---

## How parts are matched

Classification uses **Design Tracking Properties → Stock Number** only (same property VisTog reads). One lookup per part — filenames and descriptions are not used.

| Stock number | Type |
|--------------|------|
| `091-30117-081` | Wall skin |
| `091-30117-082` | Wall liner |
| `091-30117-083` | Roof skin |
| `091-30117-084` | Roof liner |
| `091-30117-056` | Base floor |
| `091-30117-080` | Base subfloor |

Corner stock `091-30117-073` is **not** a liner and is intentionally excluded.

### Assembly tree walk

| Active document | Walk behavior |
|-----------------|---------------|
| **Skid** (work-order assembly, not `391Z`) | Only descend into **`391Z` surface subassemblies**; liners and skins live inside those |
| **`391Z` surface** open alone | Walk the full surface assembly tree |

---

## Manual build

```bat
build.bat
powershell -NoProfile -ExecutionPolicy Bypass -File .\install.ps1 -BuildDir .\bin\Release\net48
```

---

## Version history

### v1.4.8.0 (2026-07-30)

- **Restored full ghost (Opacity 0)** — skins/liners with collected edges are fully invisible again (not semi-transparent)
- **Ghost only when highlight succeeds** — parts with no edge geometry stay visible (fixes missed liners)
- Edge fallbacks + visible-chain unchanged from v1.4.7

### v1.4.7.0 (2026-07-30)

- **Ghost only when highlight succeeds** — no longer hides a part unless edge geometry was collected
- **Transparent ghosting only** — removed OverrideOpacity=0 (was hiding solids without leaving highlight edges)
- Broader edge fallback for bent/non-planar skins and liners

### v1.4.6.0 (2026-07-30)

- **Liner ghost highlights** — liners often have `Visible=false` in the assembly; force `Visible=true` before opacity-0 ghosting so edge highlights match skins
- Collect outline edges before ghosting; fall back to part-definition bodies when occurrence bodies are empty

### v1.4.5.0 (2026-07-30) — keeper release

- **Stock Number only** — replaced filename/part-number substring matching from v1.4.3
- **391Z skid walk** — from an open skid, enter only `391Z` surface subassemblies to find liners/skins (VisTog-style)
- **Direct occurrence references** — no path collect/resolve round-trip (fixes highlights when skid is open)
- **Install cleanup** — wipes old Highlighter/CutHighlight installs before copying (Ce3 installer pattern)
- Added `HOW_TO_USE.md`, `INSTALL.txt`, `build-and-install.bat`

### v1.4.3.0

- Stable base shared with coworkers: selective scope, prehighlight off, opacity-0 ghosting
