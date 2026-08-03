Patch v1.0.38 — fix replace scan not linking Inventor results
==============================================================

Fix: Inventor read succeeded but replace scan could not attach the returned
config to the picked IAM (Windows long-path prefix mismatch).

Also:
  • Python reader reads MOM_DATA → DOCUMENT_CONFIG_JSON first (same as Ce3)
  • Basename fallback when matching Inventor results to picked files
  • Better error when Inventor returns empty stdout

Apply: run Apply-Patch.bat, then npm start.
