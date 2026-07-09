# Part Types Classification & Expected Data Resolution Report

This document defines how the JCI Construction Verifier classifies Autodesk Inventor parts, extracts their actual specifications, resolves their expected values, and applies overrides/fallbacks.

---

## 1. Summary of Part Classifications & expected Specs

The tool divides parts into two main verification areas: **Roof/Wall Casing** (Casing details) and **Base/Floor Frame** (Base details). The classification rules, expectation sources, and parameters checked are summarized below:

| Part Type | Category | Classification Logic (Description or Model Number) | Checked Parameter | Expected Spec Source |
| :--- | :--- | :--- | :--- | :--- |
| **Liner** | Casing | Description contains `"liner"` | Gauge & Material | Selected row's `InteriorGaugeAndMaterial` |
| **Trim** | Casing | Description contains `"roof corner cap"`, `"roof cap"`, or `"sq part - trim"` | Gauge & Material | Selected row's `TrimGaugeAndMaterial` (Roofs) or `ExteriorGaugeAndMaterial` (Walls) |
| **Misc Trim** | Casing | Description contains `"peaked roof split cover"` or `"roof seal-off angle"`; OR Model Number is `"091-30119-007"` or `"091-30117-076"` | Display only (no mismatch verification) | None |
| **Channel (Casing)** | Casing | Description starts with `"C:SC"` | Gauge & Material | Selected row's `ChannelGaugeAndMaterial` |
| **Skin** | Casing | Not classified as Liner, Trim, Misc Trim, or Channel; AND Description contains `"skin"`, `"panel"`, or `"post"` | Gauge & Material | Selected row's `ExteriorGaugeAndMaterial` |
| **Sub-Floor Sheet** | Base | Model Number is `"091-30117-080"`; OR Description contains `"SUBFLOOR"` | Gauge & Material | Selected row's `SubFloorGaugeAndMaterial` |
| **Floor Sheet** | Base | Description contains `"floor"` or `"deck"` | Gauge & Material | Selected row's `FloorGaugeAndMaterial` |
| **Structural Channel** | Base | Description starts with `"CHN:STRUCT"` | Material | Mapped from selected row's `BaseMaterial` (`STL C CHNL` if steel base, `ALM C CHNL` if aluminum base) |
| **Formed Channel** | Base | Description contains `"Channel, Formed"` | Gauge & Material | Selected row's `FormedChannelMaterial` |
| **Perimeter Angle** | Base | Description contains `"perimeter angle"` or `"angle, perimeter"`; OR contains both `"perimeter"` and `"angle"` | Gauge & Material | Selected row's `PerimeterAngleGaugeAndMaterial` |

---

## 2. Expected Data Resolution & Default Fallbacks

### Casing Wall Thickness & Thermal Breaks
- **Thickness Mapping**: Extracted from the segment's side-specific casing thicknesses (Top, Left, Right, Front, Rear). If none are defined, it falls back to the skid's `nominalSurfaceThickness`.
- **Thermal Break**: Extracted from the segment's `HousingStyle`. Sets the default dropdown to `"Yes"` if `"ThermalBreak"`, else `"No"`.

### Wall Channel Material Defaults
- Wall channels default to `16 GA` expected gauge.
- Expected material defaults to `STL GALV2` (2" panel thickness), `STL GALV3` (3" panel thickness), `STL GALV4` (4" panel thickness), or `STL GALV?` (flags mismatch) if unrecognized.

### Base / Floor Defaults
- **Base Structural Material**: If no parts are scanned, default to `STL C CHNL` or `ALM C CHNL` based on `StructuralMaterialType`. If parts are scanned, resolves to the dominant structural part's material.
- **Formed Channels**: Defaults to `10 GA STL HOT ROLL` (steel base) or `10 GA ALM HOT ROLL` (aluminum base).
- **Floor Sheet**: Initialized from `DefaultFloorMaterialGauge` and `DefaultFloorMaterialType`.
- **Sub-Floor Sheet / Perimeter Angles**: Initialized directly from the scanned sub-floor and perimeter angle parts under that skid base.

---

## 3. Special Logic, Overrides & Mappings

### A. Template Material Overrides
- When a part is classified as a **Formed Channel** and its raw database material (`YCMATL`) is `"STL GALV"`, the tool overrides it to `"STL HOT ROLL"` during property reading. This prevents standard galvanized steel templates from flagging false errors since base formed channels are always Hot Rolled steel when the base is steel.

### B. Precise Thickness Resolution (YCMATL Fallback)
- If custom properties or `YCMATL` are blank, JCI assigns unique, highly precise decimal thicknesses to resolve the exact material/paint code:
  - `0.05601` ➔ `16 GA STL GALV`
  - `0.05604` ➔ `16 GA STL GALV PPC` (painted)
  - `0.05605`, `0.05606`, `0.05607` ➔ `16 GA STL GALV2 / 3 / 4`
- The `MaterialsConfig.ResolveFromThickness` function parses these float parameters and reversely resolves both gauge and material combinations.

### C. Normalization & Gauge Mapping
- **Normalization**: User input and model values are trimmed, converted to uppercase, and stripped of quotes (`"` or `'`) before comparisons to prevent syntax mismatches.
- **Gauge Mappings**: Resolves decimal thicknesses to gauges (e.g. `0.056` ➔ `16`, `0.028` ➔ `22`) via mappings loaded from `materials_config.json` and `materials_thickness_map.json`.
- **Precedence**: `Misc Trim` stock number match takes precedence over generic descriptions so custom-engineered parts do not trigger standard trim mismatches.
- **No Override for Trim**: Trim materials are NOT auto-overridden from galvanized (`STL GALV`) to painted (`STL GALV PPC`), as some trim remains unpainted.
