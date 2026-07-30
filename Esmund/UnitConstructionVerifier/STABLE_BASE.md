# Unit Construction Verifier — Stable Base (Fork)

**Current version:** 1.0.19.0  
**Date:** 2026-07-23  

| Artifact | Path |
|----------|------|
| Fork stable zip (v1.0.16.0 feature freeze) | `CursorRemote/mobile/UnitConstructionVerifier_Source_v1.0.16.0_STABLE.zip` |
| Rolling dev zip | `CursorRemote/mobile/UnitConstructionVerifier_Source.zip` |
| Original stable base | `CursorRemote/mobile/UnitConstructionVerifier_Source_v1.0.7.0_STABLE.zip` |

## Included from v1.0.16.0 (fork stable)

- Modeless verifier window (Inventor pan/rotate while open)
- Clipboard copy buttons for surface and part numbers
- Default highlight: oriented **locator box** (part/surface local RangeBox) via ClientGraphics; no Inventor SelectSet (prevents accidental copy/paste)
- Surface/part locator box color from the header swatch (same palette as X-Ray)
- Optional **X-Ray** detailed edge outline within the active surface
- **Normal** button restores native green Inventor prehighlight; auto-restore on window close
- **Wireframe Preview**: modeless window, middle-drag pan, sheet metal cuts + standard HoleFeature holes
- Preview + verifier window stack restore (stay above Inventor when already in front)
- Resizable surface list / expectations / parts grid splitters; auto-sized expectation dropdowns; Apply Casing layout fix

## v1.0.19.0 (current dev)

- Fix Edit Mode write-back for config-driven parameter names (`Exterior Gauge & Material`, etc.) so gauge/material/thickness sync reaches sheet metal IPTs again
- Save IPT changes even when the part is already open in Inventor

## v1.0.18.0

- Replaced SelectSet highlighting with oriented locator boxes (default); X-Ray unchanged for detailed outlines

## v1.0.17.0

- Part and surface highlight when a **surface sub-assembly IAM is opened directly** (not only from the unit root assembly)

## Build / deploy

```bat
compile.bat
deploy.bat
```

Or `build-and-install.bat`. Restart Inventor after deploy.

## Repo path

`Detailing-Inventor-Projects/UnitConstructionVerifier`

## Known issues (troubleshooting)

### Ctrl+V in Verifier pastes old Inventor clipboard (v1.0.18.0+)

Default highlight uses a **locator box**, not Inventor `SelectSet`, so **Ctrl+C in the Verifier does not put the listed part/surface on Inventor’s assembly clipboard**. If the unit IAM is active, **Ctrl+V may still be handled by Inventor** and paste whatever was copied earlier in the graphics window — including stale paths or missing-reference dialogs that look unrelated to the current row.

**Workaround:** Clear accidental Inventor copies before verifying; use **⧉** for text part/surface numbers. Full write-up: `Docs/known_issues.md`.

