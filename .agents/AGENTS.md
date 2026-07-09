# JCI AHU Construction Data Rules

Guidelines for parsing, sorting, and validating Air Handling Unit (AHU) segment construction configurations from JCI database structures (JSON, XML, or Inventor properties).

## 1. Segment Sequence & Physical Flow Sorting
- **Physical Layout Order**: Always sort skids and segments physically from **Intake to Discharge / Airflow Flow Direction** (X-coordinate descending). Do not sort by ID or database sequence indexes.
- **Sequence Source**: Extract the complete sequence of segments for each skid using the `skidSegmentSequence` configuration parameter (e.g. `(MB FF-3 AT DP FS)`). Use this sequence to generate the expected casing grid rows, including segments that may not have direct casing configs.

## 2. Active Casing Wall Verification
- **Wall Thickness Dependency**: Casing specifications (skin material/gauge, liner material/gauge, insulation, paint) are side-specific (Top, Bottom, Left, Right, Front, Rear).
- **Existence Check**: A side's configuration properties are only active if that side has a defined wall thickness (`WallThickness_* > 0` or similar). Ignore materials or gauges on sides with null or zero thickness, as they represent open/internal boundaries.
- **Thickness Fallbacks**: If a segment does not specify local wall thicknesses, fall back to the skid's `nominalSurfaceThickness` casing thickness.

## 3. Base Frame & Sub-Floor Part Classification & Verification
- **Sub-Floor Classification**: Identify sub-floor sheets if the part's description contains `"SUBFLOOR"` (case-insensitive) or its model number is `"091-30117-080"`. Route them exclusively to the Sub-Floor Gauge/Material verification check.
- **Structural vs. Formed Base Channels**: 
  - Treat structural C-channels (description starts with `"CHN:STRUCT"`) and formed sheet metal channels (description contains `"Channel, Formed"`) as distinct categories with separate expectations.
  - Verify structural channels against the dominant structural frame material (mapped to `"STL C CHNL"` or `"ALM C CHNL"`).
  - Verify formed channels against the dominant formed channel material (e.g. `"10 GA STL HOT ROLL"`).
- **Ignore Base Frame Accessories**: Ignore perimeter angles, lifting lugs, filler plates, or any other base accessories during frame material and gauge verification. Only verify main structural and formed channels.
- **Template Material Override**: If a part is classified as a formed channel, and its custom properties are blank, falling back to the standard Inventor template material `"STL GALV"`, override it to `"STL HOT ROLL"` as base frame formed channels are always Hot Rolled steel when base is steel.
- **Nominal Thickness Mappings**: Map nominal decimal thicknesses to their standard gauge numbers (e.g. `"0.127"` ➔ `"10"`, `"0.056"` ➔ `"16"`, `"0.028"` ➔ `"22"`) during formatting.
- **Ignore Paint and Insulation**: Do not perform verification checks or generate mismatches for paint parameters (Base Paint, Floor Paint, Exterior Paint) or floor insulation, as these are outside the user's portion of the manufacturing verification process.

## 5. Roof Trim & Casing Spec Resolution
- **Trim vs. Misc Trim Precedence**: Prioritize `Misc Trim` matching (specific descriptors or stock numbers like `091-30119-007` and `091-30117-076`) over generic description keyword matching (like `sq part - trim` or `roof cap`) so that custom-engineered parts route to `Misc Trim` instead of standard `Trim`.
- **No Trim Material Auto-Overrides**: Do not auto-override standard galvanized steel (`STL GALV`) to painted galvanized steel (`STL GALV PPC`) for Trim or Misc Trim parts, as some trim remains unpainted.
- **Gauge Parameter Normalization**: When reading gauge parameters from custom properties (which may be formatted as floats like `16.00000`), normalize them to standard integer gauge numbers (e.g., `16`).
- **Spec Resolution via Precise Thickness**: Since JCI assigns distinct, precise decimal thicknesses to different material/paint combinations (e.g. `0.05604` is unique to `16 GA STL GALV PPC` vs `0.05601` for `16 GA STL GALV`), use these decimal values to resolve the exact material/paint codes when YCMATL/custom properties are blank.

## 4. Deployment and Assembly Hot-Reload
- **Final Deployment Step**: After compiling the `UnitConstructionVerifier` project, deploy the output binary `UnitConstructionVerifier.dll`, its manifest `UnitConstructionVerifier.addin`, the dependency library `Newtonsoft.Json.dll`, and the configuration database files (`materials_config.json`, `materials_thickness_map.json`) to the target add-ins directory: `%APPDATA%\Autodesk\Inventor 2020\Addins\`.
- **Handling Active Locks (Hot-Reload)**: If Autodesk Inventor is running, the DLL file will be locked by the process. To deploy without closing Inventor:
  1. Rename the existing in-place DLL to `UnitConstructionVerifier.dll.old` (or `UnitConstructionVerifier.dll.old_<timestamp>` to avoid collisions with other locked backup files).
  2. Copy the new `UnitConstructionVerifier.dll`, manifest, configurations, and dependency files into the directory.
  3. The updated add-in will load automatically upon restarting Inventor.

