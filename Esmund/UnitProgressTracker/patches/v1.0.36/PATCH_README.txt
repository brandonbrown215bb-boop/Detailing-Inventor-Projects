Patch v1.0.36 — replace-from-IAM diagnostics + sidecar fallback
=================================================================

Includes v1.0.35 incremental scan and v1.0.34 BOM Config import.

Fix: misleading "No 391Z surface geometry found"
------------------------------------------------
v1.0.35 showed one generic message whether the .iam was missing OR Inventor
could not read DOCUMENT_CONFIG_JSON from it.

v1.0.36 reports:
  • No .iam found at that path (name must match 391Z*.iam)
  • Found N IAM(s) but Inventor/config failed — per-file error lines

Replace one surface
-------------------
  1. Open unit folder (cache load — no full Rescan)
  2. Select surface
  3. Renumber & history → Replace from IAM…
  4. OK = folder with .iam, Cancel = pick .iam file directly
  5. Old number retired; new geometry linked

IAM requirements
----------------
Inventor must be installed. The IAM needs either:
  • MOM_DATA → DOCUMENT_CONFIG_JSON (normal detailing export), OR
  • Same-name sidecar: 391Z010142-0123.json in the same folder, OR
  • 391Z010142-0123 DOCUMENT_CONFIG_JSON.txt

Apply: run Apply-Patch.bat against your UnitSurfaceViewer install, then npm start.
