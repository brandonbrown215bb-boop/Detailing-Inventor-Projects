# Implementation Plan: MOM_DATA Extraction, Multi-Layer Write-Back & Hard-Edit Visual Indicator

Update `UnitConstructionVerifier` (`IptPropertyReader.cs`, `IptPropertyWriter.cs`, and `MaterialsConfig.cs`) to:
1. Use `MOM_DATA` AttributeSet as the primary authoritative property extraction source.
2. Synchronize edits back to `MOM_DATA` (`MATERIAL_TYPE_CODE` & `ORIGINAL_SHEET_METAL_THICKNESS`) during write-back.
3. Apply a **configurable visual "hard-edited" Appearance indicator** to parts modified via UCV Edit Mode, configured via `materials_config.json`.

---

## User Review Required

> [!IMPORTANT]
> - **Visual Hard-Edit Indicator**: When a user saves property edits via UCV Edit Mode (`IptPropertyWriter.cs`), UCV will automatically set the part's Inventor `ActiveAppearance` to a universal "changed" appearance asset (e.g. `"Yellow"` or `"Magenta"`), giving immediate visual feedback in the CAD 3D model.
> - **JSON Configuration**: The target hard-edit appearance name is configurable in `materials_config.json` under `"EditedAppearance": "Yellow"`.
> - **MOM_DATA Write-Back Alignment**: Edits update both `User Defined iProperties` and the `MOM_DATA` AttributeSet (`MATERIAL_TYPE_CODE` & `ORIGINAL_SHEET_METAL_THICKNESS`), keeping CAD properties, UCV verifier, and downstream MOM DXF/nesting tools 100% in sync.

## Open Questions

None. All technical details, JSON schema extensions, and Inventor Appearance APIs have been integrated.

---

## Proposed Changes

### Configuration Layer

#### [MODIFY] [materials_config.json](file:///c:/Users/jbrow263/OneDrive%20-%20Johnson%20Controls/Documents/Inventor%20Projects/UnitConstructionVerifier/UnitConstructionVerifier/materials_config.json)

- Add `"EditedAppearance": "Yellow"` setting at top level.

#### [MODIFY] [MaterialsConfig.cs](file:///c:/Users/jbrow263/OneDrive%20-%20Johnson%20Controls/Documents/Inventor%20Projects/UnitConstructionVerifier/UnitConstructionVerifier/Models/MaterialsConfig.cs)

- Add static property `public static string EditedAppearance { get; set; } = "Yellow";` loaded from `materials_config.json`.

---

### Core Extraction Layer

#### [MODIFY] [IptPropertyReader.cs](file:///c:/Users/jbrow263/OneDrive%20-%20Johnson%20Controls/Documents/Inventor%20Projects/UnitConstructionVerifier/UnitConstructionVerifier/Extraction/IptPropertyReader.cs)

- In `ReadPartProperties(PartDocument doc, string ownerIamPath)`:
  1. Inspect `doc.AttributeSets` for `"MOM_DATA"`.
  2. If `"MOM_DATA"` exists, extract `MATERIAL_TYPE_CODE`, `ORIGINAL_SHEET_METAL_THICKNESS`, `MODEL_NUMBER`, and `PART_SOURCE` as authoritative properties.
  3. If `"MOM_DATA"` is missing, fall back to reading `User Defined Properties` (`YCMATL`, `Thickness`, `INPUT_PARAMETER_Mtl_Gauge`) and `Design Tracking Properties`.
  4. Read `IS_HARD_EDITED` flag from User Defined Properties.

---

### Write-Back & Visual Appearance Layer

#### [MODIFY] [IptPropertyWriter.cs](file:///c:/Users/jbrow263/OneDrive%20-%20Johnson%20Controls/Documents/Inventor%20Projects/UnitConstructionVerifier/UnitConstructionVerifier/Operations/IptPropertyWriter.cs)

- In `UpdatePartProperties(string iptPath, PartPropertyEdits edits, out string errorMessage)`:
  1. **MOM_DATA AttributeSet Write-Back**:
     - Ensure `AttributeSet` `"MOM_DATA"` exists on `doc.AttributeSets`; create it via `doc.AttributeSets.Add("MOM_DATA")` if missing.
     - Write `edits.YCMATL` to `AttributeSet["MOM_DATA"]["MATERIAL_TYPE_CODE"]` (create attribute if missing).
     - Write `edits.Thickness` to `AttributeSet["MOM_DATA"]["ORIGINAL_SHEET_METAL_THICKNESS"]` (create attribute if missing).
  2. **User Defined iProperty & Hard-Edit Flag Write-Back**:
     - Write `edits.YCMATL` to `YCMATL`.
     - Write `edits.Thickness` to `Thickness`.
     - Write `edits.MtlGauge` to `INPUT_PARAMETER_Mtl_Gauge`.
     - Write `WriteUserProperty(userDefined, "IS_HARD_EDITED", "TRUE")`.
  3. **Visual Hard-Edit Appearance Sync**:
     - Retrieve configured appearance name from `MaterialsConfig.EditedAppearance` (e.g. `"Yellow"`).
     - Try finding appearance in `doc.Appearances` or `_app.ActiveAppearanceLibrary` / `_app.ActiveMaterialLibrary`.
     - If found, assign `doc.ActiveAppearance = appearanceAsset` and update Design Tracking `Appearance` property.
     - If missing, continue gracefully without throwing exception or failing the edit.

---

### Data Models & Tests

#### [MODIFY] [VerificationResult.cs](file:///c:/Users/jbrow263/OneDrive%20-%20Johnson%20Controls/Documents/Inventor%20Projects/UnitConstructionVerifier/UnitConstructionVerifier/Models/VerificationResult.cs)

- Add diagnostic properties to `IptProperties`:
  - `public bool IsHardEdited { get; set; }`

#### [MODIFY] [ApprenticePropertyReaderTests.cs](file:///c:/Users/jbrow263/OneDrive%20-%20Johnson%20Controls/Documents/Inventor%20Projects/UnitConstructionVerifier/UnitConstructionVerifier.Tests/ApprenticePropertyReaderTests.cs)

- Add tests verifying `MOM_DATA` extraction, hard-edit flag recognition, and `EditedAppearance` configuration loading.

---

## Verification Plan

### Automated Tests
1. Run `dotnet test UnitConstructionVerifier\UnitConstructionVerifier.sln` to ensure all unit tests pass.

### Manual Verification
1. Edit a part's gauge or material in UCV Edit Mode, click **Write Changes**, and verify in Inventor that the part's visual appearance turns **Yellow** (or configured color).
2. Change `"EditedAppearance": "Magenta"` in `materials_config.json`, edit another part, and verify the visual appearance updates to **Magenta**.
3. Run `deploy.bat` to deploy binaries and manifests to `%APPDATA%\Autodesk\Inventor 2020\Addins\`.
