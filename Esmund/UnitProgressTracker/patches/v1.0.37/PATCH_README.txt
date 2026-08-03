Patch v1.0.37 — fix replace scan Inventor path matching
========================================================

Fix: replace/add scan showed false "no sidecar .json / CONFIG_JSON" error
when Inventor actually read the IAM but returned a resolved path that did
not match the picked path (Windows path casing / normalization).

Changes:
  • Same Inventor read path as full Open Folder / Rescan scan
  • Canonical path matching between Python reader and UI
  • Real Inventor error messages surfaced (not hidden by sidecar fallback)
  • Removed CONFIG_JSON folder and sidecar .json fallbacks entirely

Apply: run Apply-Patch.bat, then npm start.
