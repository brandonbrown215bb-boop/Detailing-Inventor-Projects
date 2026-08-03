Patch v1.0.32 — shared parts across skids (duplicate refs)
==========================================================

When the same 391- part appears on multiple skids (e.g. 391-60233-349 on Skid 1 and Skid 4):
- Lowest skid number gets the model export folder
- Higher skids get a list entry + stub folder tagged [ref Skid N]
- Create folders writes MODEL_REFERENCE.txt in stub folders pointing to the primary path
- Open folder on a ref entry opens the primary model location

Re-import BOM after patching to rebuild the export list.
