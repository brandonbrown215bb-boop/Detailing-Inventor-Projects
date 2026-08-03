Patch v1.0.35 — incremental surface scan + Removed list
========================================================

Includes v1.0.34 BOM Config.xml import (same patch bundle).

Incremental scan (no full 59-surface rescan)
--------------------------------------------
Replace one surface:
  1. Open unit folder (cache load — no Rescan)
  2. Select surface
  3. Renumber & history → Replace from folder…
  4. Pick folder with ONE 391Z IAM (Inventor read for that folder only)
  5. Old number retired; new geometry linked with same display number

Add new surface(s):
  File → Add surface(s) from folder…
  Pick one or more folders (multi-select). Skips duplicates already in project.

Remove surface:
  Renumber & history → Remove surface…
  Removed from main list and 3D; snapshot kept under Removed (collapsed, bottom of list).

Requires Inventor for folder scans. No CONFIG_JSON / pigeon export needed.

Apply: run Apply-Patch.bat against your UnitSurfaceViewer install, then npm start.
