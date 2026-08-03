Patch v1.0.34 — unit Config.xml import for BOM segment placement
================================================================

Problem
-------
BOM skid bracket strings (e.g. [(DP1/MB-FE)]) do not always match the Segment
column (e.g. FR - Fan (Return)). Bracket-only matching skipped valid rows.

Solution
--------
Import the unit Config.xml (one per unit) on the BOM tab after importing the BOM.
UPT reads shippingSkidList from Config — same model Ce3 uses — and maps each
BOM row using Skid number + Segment code (FR, DP-2, etc.).

Workflow
--------
1. Apply patch (Apply-Patch.bat)
2. Import BOM xlsx
3. Import Config.xml (from unit folder, e.g. 6E-610038-03\Config.xml)
4. Verify export list / create Inventor folders

Com 20172 shipping skids (from Config)
--------------------------------------
  Skid 01: FR-MB-DP-1
  Skid 02: RF-CC-1-XA-1-XA-2-XA-3
  Skid 03: HW-1-XA-4-CC-2-HW-2-XA-5-XA-6-XA-7
  Skid 04: XA-8-FS-DP-2-IG-IP-XA-9

Test case: 391-60233-349 on Skid 1 (FR) and Skid 4 (DP-2) — both should
appear after Config import; Skid 1 primary, Skid 4 ref stub (v1.0.32 logic).

Notes
-----
- Config summary is saved in the project (not the full XML)
- Re-importing BOM keeps loaded Config
- Without Config, UPT still tries bracket matching and shows a hint if rows skip

Future
------
Config may drive more UPT features (segment metadata, validation, etc.).
