# Part Types Classification & Expected Data Resolution Report

This document defines how the JCI Construction Verifier classifies Autodesk Inventor parts, extracts their actual specifications, resolves their expected values, and applies overrides/fallbacks.

---

## 1. Summary of Part Classifications & expected Specs

The tool divides parts into two main verification areas: **Roof/Wall Casing** (Casing details) and **Base/Floor Frame** (Base details). Parts are classified primarily by looking up their **Stock Number** (from `Stock Number` or `MODEL_NUMBER` properties) in the `PartClassifications` config database inside `materials_config.json`. If a part's stock number is not in the configuration database, it falls back to description-based legacy keyword rules.

The classification categories, expectation sources, and checked parameters are summarized below:

| Part Type | Category | Identified by (stock number, then description keyword) | Expected Gauge & Material |
| :--- | :--- | :--- | :--- |
| **Skin** | Casing | `091-30117-081/083/177`; "skin", "panel", "post" | The surface's own side in the config (roofs: Top; walls: their side) |
| **Liner** | Casing | `091-30117-082/084`; "liner" | The surface's own side's liner |
| **Channel** | Casing | `091-30117-064/065/085/086` and others; "C:SC" | Standard parts (`064/065/085/086`): 16 GA `STL GALV2/3/4` by panel thickness. Sloped outdoor roof channels (`068/069/070`), brackets (`046/077/078`) and `048`: plain 16 GA `STL GALV`. The config's `formedChannelMaterialType` is not used, so an Outdoor-configured unit with standard roof channels is flagged |
| **Roof Cap** (detailers: "roof trim") | Casing (roof) | `091-30117-001/058/074`; "roof corner cap", "roof cap" | The roof's **Trim** row (check: *Roof Trim Gauge & Material*). It starts as `Roof_RoofCap_MaterialGauge_Default` (16) in the **wall exterior** material; 20 GA whenever the trim material is `ALM EMB` |
| **Roof Accessory** | Casing (roof) | `091-30117-022/076`, `091-30119-007`; "roof super angle", "peaked roof split cover", "roof seal-off angle" | `Roof_RoofSealOffAngle_*` engineering defaults (16 GA STL GALV) |
| **Tiering Angle** | Casing (roof) | `091-30117-057`; "tiering angle" | 14 GA STL GALV |
| **Attachment Angle** | Casing (wall) | `091-30117-049/053`; "attachment angle", "angle, formed" | 16 GA in the liner material |
| **Seal-Off Angle** | Casing (wall) | `091-30119-006`; "seal-off angle" | The wall's exterior skin gauge and material |
| **Wall Corner Cap / Liner** | Casing (wall) | `091-30117-072` / `073` | 16 GA (cap: 20 ALM EMB, 14 ALM SHT) in the skin / liner material |
| **Trim** | Casing | `091-30117-011/195/196`; remaining roof/wall trim keywords | Display only (not automated) |
| **Misc Trim / Split Cover** | Casing | `091-30117-075/087/089`; "misc trim" | Display only |
| **Floor Sheet** | Base | `091-30117-056/124`; "floor", "deck" | `defaultConstructionOptions_FloorMaterialGauge/Type` |
| **Sub-Floor** | Base | `091-30117-080/061/062`; "subfloor", "sub-floor", "sub floor" | `unitBase.unitBaseGeometryList[].subFloorMaterialGauge/Type` |
| **Perimeter Angle** | Base | `091-30117-054/055`; "perimeter angle" | Base with an upturned lip (UTL): 10 GA `ALM SHT` (aluminum floor), 10 GA in the floor's stainless code, e.g. `SST304D` (stainless floor), 12 GA `STL GALV` (galvanized floor). Otherwise `UnitBase_PerimeterAngle_*` (16 GA STL GALV) |
| **Formed Channel** | Base | `091-30117-051`; "Channel, Formed" | `formedChannelMaterialGauge/Type` (fallback 10 GA hot roll) |
| **Structural Channel / Angle** | Base | `091-30117-186/188` / `187/189` | `STL C CHNL`/`STL ANGLE` or aluminum, from `structuralMaterialType` |
| **Floor Drain Support Angle** | Base | `091-30117-079` with profile "x 1.5 x 1.5" or "x 2 x 2" | 10 GA `STL HOT ROLL` (steel base) or `ALM SHT` (aluminum base). The engineering `UnitBase_FloorDrainSupportMaterial` constant (16 GA STL GALV) matched no real part and is not used |
| **Floor Attachment Angle** | Base | `091-30117-079` with profile "x 2.5 x 1" | `UnitBase_AluminumFloorAttachmentAngle_*` (14 GA ALM SHT) |
| **Not in config** | — | Lifting lugs, filler plates, drain pans, coil supports, grates, custom `079` angle profiles (e.g. 1.75 x 1.75), … | Not verified |

---

## 2. Expected Data Resolution & Default Fallbacks

### Casing Wall Thickness & Thermal Breaks
- **Thickness Mapping**: Extracted from the segment's side-specific casing thicknesses (Top, Left, Right, Front, Rear). If none are defined, it falls back to the skid's `nominalSurfaceThickness`.
- **Thermal Break**: Extracted from the segment's `HousingStyle`. Sets the default dropdown to `"Yes"` if `"ThermalBreak"`, else `"No"`.

### Where expectations come from
Expectations come from the unit's configuration and engineering data, never from the scanned parts, so a
surface whose parts are consistently wrong is still flagged. These rules were confirmed against 56 real
ISG units with `ucv-scan` (2026-10-21).

- **Side-specific casing**: each roof/wall surface's `surfaceID` maps to a `unitSurfaceList` entry with its
  `unitSide`. Skin and liner come from that side's `skinMaterialType_<Side>` / `linerMaterialType_<Side>`
  (roofs use Top). The old majority vote across all sides is only a fallback when the side is unknown.
- **Engineering constants**: `DOCUMENT_ENGINEERING_JSON` → `rules.engineeringConstantList` holds per-part
  standards such as `UnitBase_PerimeterAngle_MaterialType`, `Roof_RoofCap_MaterialGauge_Default` and
  `Roof_RoofSealOffAngle_SealOffAngle_MaterialType_Default`. They are available to the builder as
  `EngineeringData.Constants`.
- **Upturned lip (UTL)**: a base is UTL when its own `unitBase.unitBaseGeometryList[].upturnedLipHeight` (or its
  skid's `unitBaseList[].upturnedLipHeight`) is set. This matched the perimeter angle type on 89 of 90 scanned
  bases. Bulkhead "UTL" descriptions are not used: bulkhead data is unreliable and missed 10 of 40 lip bases.
  The config marks the whole base surface even when the lip is on only some sides, so **every** perimeter
  angle on a lip base expects the UTL angle. Standard 16 GA STL GALV angles on the non-lip sides show as
  mismatches by design (see `Docs/known_issues.md`); accepting either angle would hide a UTL angle that
  wrongly came out as 16 GA STL GALV.
- **Plate gauges**: plate materials keep their thickness label (0.125 ALM DIA stays "0.125", not 10 GA).
- **Modified parts**: SQ parts (description "SQ PART - …" or `MOM_DATA.PART_SOURCE = SQPart`) and parts
  edited through the verifier (`IS_HARD_EDITED`) are flagged *modified*. A freshly generated unit has none.

## 3. Special Logic, Overrides & Mappings

### A. Template Material Overrides
- When a part is classified as a **Formed Channel** (either via Stock Number mapping or description fallback) and its raw database material (`YCMATL`) is `"STL GALV"`, the tool overrides it to `"STL HOT ROLL"` during property reading. This prevents standard galvanized steel templates from flagging false errors since base formed channels are always Hot Rolled steel when the base is steel.

### B. Precise Thickness Resolution (YCMATL Fallback)
- If custom properties or `YCMATL` are blank, JCI assigns unique, highly precise decimal thicknesses to resolve the exact material/paint code:
  - `0.05601` ➔ `16 GA STL GALV`
  - `0.05604` ➔ `16 GA STL GALV PPC` (painted)
  - `0.05605`, `0.05606`, `0.05607` ➔ `16 GA STL GALV2 / 3 / 4`
- The `MaterialsConfig.ResolveFromThickness` function parses these float parameters and reversely resolves both gauge and material combinations.

### C. Normalization & Gauge Mapping
- **Normalization**: User input and model values are trimmed, converted to uppercase, and stripped of quotes (`"` or `'`) before comparisons to prevent syntax mismatches.
- **Gauge Mappings**: Resolves decimal thicknesses to gauges (e.g. `0.056` ➔ `16`, `0.028` ➔ `22`) via mappings loaded from `materials_config.json` and `materials_thickness_map.json`.
- **Precedence**: Stock Number mapping takes precedence over legacy keyword matches. For fallbacks, `Misc Trim` stock number match takes precedence over generic descriptions so custom-engineered parts do not trigger standard trim mismatches.
- **No Override for Trim**: Trim materials are NOT auto-overridden from galvanized (`STL GALV`) to painted (`STL GALV PPC`), as some trim remains unpainted.
