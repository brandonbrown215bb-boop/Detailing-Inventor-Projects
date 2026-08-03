Unit Progress Tracker — Patch v1.0.29
=====================================

Fix: BOM shell folders now match coil segments in slash-style skid brackets
      (e.g. CC-1 from [XA1/CC1], CC-2 from [HW/CC2], DP-2 from [IG/DP2]).

Requires: existing v1.0.28 install (npm install already done).

Apply
-----
1. Download UnitProgressTracker_Patch_v1.0.29.zip from DL
2. Extract anywhere
3. Double-click Apply-Patch.bat
4. Paste path to your UnitSurfaceViewer folder when prompted
   (or drag that folder onto Apply-Patch.bat)
5. Restart the app — header should show v1.0.29
6. Re-import BOM if one was already loaded

Files updated
-------------
  src/bom-folder-maker.js
  package.json

No npm install needed. No full zip re-download.
