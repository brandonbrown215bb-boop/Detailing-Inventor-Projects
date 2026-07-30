# Reference Guide: Writing Config-Driven Part Verification Rules

This guide explains how to define, edit, and extend part classification and expected-spec verification rules inside [materials_config.json](file:///c:/Users/jbrow263/OneDrive%20-%20Johnson%20Controls/Documents/Inventor%20Projects/UnitConstructionVerifier/UnitConstructionVerifier/materials_config.json).

---

## 1. Where Rules Live

All rules are defined in the `PartRules` JSON array at the root of `materials_config.json`:

```json
{
  "Gauges": [...],
  "Materials": [...],
  "PartRules": [
    { ... rule 1 ... },
    { ... rule 2 ... }
  ],
  "PartClassifications": {
    "091-30117-066": "Base Accessory"
  }
}
```

---

## 2. Rule Property Reference

Each rule object supports the following fields:

| Field | Type | Required | Description |
|---|---|---|---|
| **`Classification`** | string | Yes | The classification label assigned to the part (e.g. `"Seal-Off Angle"`). |
| **`StockNumbers`** | string[] | No | List of exact part stock numbers (model numbers) that map to this rule. |
| **`DescriptionKeywords`** | string[] | No | List of description substrings (case-insensitive) that match this rule. |
| **`GaugeSource`** | string | Yes | Expression defining the expected gauge. (Leave empty `""` to ignore gauge checking). |
| **`MaterialSource`** | string | Yes | Expression defining the expected material. |
| **`VerificationMode`** | string | No | `"mismatch"` (default): flags errors in grid; `"display"`: shows actual specs only (no errors). |
| **`Section`** | string | No | Grouping section name. `"Casing"` (default) or `"Base"`. |
| **`FieldName`** | string | No | Column header displayed in the WPF details grid (defaults to `Classification`). |

---

## 3. Matching Priority (How a part is classified)

When the verifier scans a part, it determines its classification in the following order:

1. **Exact Stock Number Lookup**: Checks if the part's Stock Number/Model Number matches an entry in any rule's `StockNumbers` array.
2. **Generic Stock Number Table**: Checks the legacy `PartClassifications` dictionary (used for `Base Accessory` and legacy definitions).
3. **Description Keyword Match**: Checks if the part's description contains any keyword listed in a rule's `DescriptionKeywords` array (case-insensitive substring lookup). Runs in the order defined in the JSON file.
4. **Fallback**: If nothing matches, the classification defaults to `"Unknown"`.

---

## 4. Expected Value Syntax (`GaugeSource` and `MaterialSource`)

The expected values are resolved dynamically using several prefix strategies:

### A. Fixed Literals (`fixed:`)
Always uses the literal string provided.
- Syntax: `fixed:<value>`
- Examples: `"fixed:16"`, `"fixed:STL HOT ROLL"`

### B. Borrowed Surface Fields (`borrow:`)
Pulls the value from a property on the surface row.
- Syntax: `borrow:<FieldName>`
- Examples: `"borrow:InteriorLinerMaterial"`, `"borrow:FloorGauge"`

### C. Conditional Expressions (`if:`)
Evaluates and selects an expected spec based on a condition matched against another row field. This is very useful when specifications vary depending on casing or base material (e.g. steel vs. aluminum).
- Syntax: `if:<Condition>?<TrueSpec>:<FalseSpec>`
- Supported Conditions:
  - `<FieldName>_contains:<Keyword>` (case-insensitive substring match)
  - `<FieldName>=<Value>` (case-insensitive exact string match)
- Examples:
  - `"if:ExteriorSkinMaterial_contains:ALM?fixed:14:fixed:16"` (evaluates to expected gauge `14` if the exterior skin material is aluminum; otherwise expected gauge `16`).
  - `"if:BaseStructuralMaterial_contains:ALM?fixed:ALM ANGLE:fixed:STL ANGLE"`
- Nesting: The true and false branches can themselves be nested conditionals or standard prefixes (like `fixed:` or `borrow:`).

### D. Fallback Chains (`||`)
If a borrowed field might be empty or missing depending on whether the part is on a Roof or a Wall, you can chain multiple sources together using `||`. The engine uses the first source that resolves to a non-empty value.
- Syntax: `borrow:<FieldA> || borrow:<FieldB>`
- Example: `"borrow:TrimSkinGauge || borrow:ExteriorSkinGauge"` (checks `TrimSkinGauge` first; if empty/missing, falls back to `ExteriorSkinGauge`).

---

## 5. Available Borrow Fields

When using `borrow:`, you can reference the following field names:

### For Casing Parts (Roof / Wall)
- **`ExteriorSkinGauge`**
- **`ExteriorSkinMaterial`**
- **`InteriorLinerGauge`**
- **`InteriorLinerMaterial`**
- **`ChannelSkinGauge`**
- **`ChannelSkinMaterial`**
- **`TrimSkinGauge`** *(Roof only)*
- **`TrimSkinMaterial`** *(Roof only)*

### For Base / Floor Parts (Base)
- **`BaseStructuralMaterial`** *(automatically maps to `"STL C CHNL"` or `"ALM C CHNL"`)*
- **`BaseStructuralAngleMaterial`** *(automatically maps to `"STL ANGLE"` or `"ALM ANGLE"`)*
- **`FormedChannelGauge`**
- **`FormedChannelMaterial`**
- **`FormedChannelGaugeAndMaterial`**
- **`FloorGauge`**
- **`FloorMaterial`**
- **`FloorGaugeAndMaterial`**
- **`SubFloorGauge`**
- **`SubFloorMaterial`**
- **`SubFloorGaugeAndMaterial`**
- **`PerimeterAngleGauge`**
- **`PerimeterAngleMaterial`**
- **`PerimeterAngleGaugeAndMaterial`**

---

## 6. Concrete Examples

### Example 1: Wall Seal-Off Angle (Borrow + Fixed)
- Description matches `"wall seal-off angle"`
- Gauge is always fixed at 16 GA
- Material must match the wall's liner material

```json
{
  "Classification": "Seal-Off Angle",
  "DescriptionKeywords": [ "wall seal-off angle" ],
  "GaugeSource": "fixed:16",
  "MaterialSource": "borrow:InteriorLinerMaterial",
  "VerificationMode": "mismatch",
  "Section": "Casing",
  "FieldName": "Seal-Off Angle Gauge & Material"
}
```

### Example 2: Sub-Floor Sheet (Stock Numbers + Description Keywords)
- Matches if Model is `"091-30117-080"` OR description contains `"SUBFLOOR"`
- Gauge and Material are borrowed from the row's Sub-Floor specs

```json
{
  "Classification": "Sub-Floor",
  "DescriptionKeywords": [ "SUBFLOOR" ],
  "StockNumbers": [ "091-30117-080", "091-30117-061", "091-30117-062" ],
  "GaugeSource": "borrow:SubFloorGauge",
  "MaterialSource": "borrow:SubFloorMaterial",
  "VerificationMode": "mismatch",
  "Section": "Base",
  "FieldName": "Sub-Floor Gauge & Material"
}
```

### Example 3: Wall Corner Liner (Conditional Rule Example)
- Matches if Stock Number is `"091-30117-073"` or description contains `"wall corner liner"`
- Material is borrowed from the unit's `InteriorLinerMaterial`
- Gauge expects `14` if liner contains `"ALM"`; otherwise expects `16`

```json
{
  "Classification": "Wall Corner Liner",
  "DescriptionKeywords": [ "wall corner liner" ],
  "StockNumbers": [ "091-30117-073" ],
  "GaugeSource": "if:InteriorLinerMaterial_contains:ALM?fixed:14:fixed:16",
  "MaterialSource": "borrow:InteriorLinerMaterial",
  "VerificationMode": "mismatch",
  "Section": "Casing",
  "FieldName": "Wall Corner Liner Gauge & Material"
}
```

### Example 4: Split Cover (Display Only)
- Matches on Stock Numbers
- Displayed in the grid for user reference, but never triggers a mismatch error

```json
{
  "Classification": "Split Cover",
  "StockNumbers": [ "091-30117-075", "091-30117-087", "091-30117-089" ],
  "VerificationMode": "display",
  "Section": "Casing",
  "FieldName": "Trim Gauge & Material"
}
```
