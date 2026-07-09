# Full Unit Configuration Deep Dive (from Config.xml)

We parsed the raw source configurator data `Config.xml` for unit `6E-630042-10` to perform a comprehensive audit of the entire unit construction. Here is the full-unit configuration mapping, representing the absolute source-of-truth data.

---

## Unit Casing Specifications (Global)

All segments across all 4 skids share the identical premium casing specification:
- **Casing Panel Thickness**: `2.0"`
- **Exterior Skin**: `18 GA STL GALV PPC` (Pre-Painted Steel)
- **Interior Liner**: `22 GA STL GALV` (Galvanized Steel)
- **Insulation**: `2.0" Foam`
- **Housing Style**: `ThermalBreak` (Thermal Break = **Yes**)

---

## Skid Layout & Segment Mapping

By parsing the physical `x` coordinates and lengths of all 24 segments defined in the XML, we mapped the layout of the unit from **Right to Left (Intake to Discharge / Airflow Order)**:

### Skid 1 (Intake Skid)
- **Max X Coordinate**: `365.0"`
- **Layout Order**: `DP` $\rightarrow$ `MB` $\rightarrow$ `FF` $\rightarrow$ `FS` $\rightarrow$ `AT`
- **Segment Audit**:
  | Segment | X Coordinate | Length X | Casing Thickness | Exterior Skin | Interior Liner | Insulation | Thermal Break |
  |---|---|---|---|---|---|---|---|
  | **DP** (Damper) | 365.0" | 46.0" | 2.0" | 18 GA STL GALV PPC | 22 GA STL GALV | 2.0" Foam | Yes |
  | **MB** (Mixing Box) | 357.0" | 54.0" | 2.0" | 18 GA STL GALV PPC | 22 GA STL GALV | 2.0" Foam | Yes |
  | **FF** (Filter) | 333.0" | 24.0" | 2.0" | 18 GA STL GALV PPC | 22 GA STL GALV | 2.0" Foam | Yes |
  | **FS** (Fan Section) | 307.0" | 58.0" | 2.0" | 18 GA STL GALV PPC | 22 GA STL GALV | 2.0" Foam | Yes |
  | **AT** (Access Transition) | 307.0" | 26.0" | 2.0" | 18 GA STL GALV PPC | 22 GA STL GALV | 2.0" Foam | Yes |

### Skid 2 (Mid-Right Skid)
- **Max X Coordinate**: `266.0"`
- **Layout Order**: `CC` $\rightarrow$ `XA` $\rightarrow$ `XA` $\rightarrow$ `HC` $\rightarrow$ `PC` $\rightarrow$ `RF` $\rightarrow$ `FR`
- **Segment Audit**:
  | Segment | X Coordinate | Length X | Casing Thickness | Exterior Skin | Interior Liner | Insulation | Thermal Break |
  |---|---|---|---|---|---|---|---|
  | **CC** (Coil Section) | 266.0" | 41.0" | 2.0" | 18 GA STL GALV PPC | 22 GA STL GALV | 2.0" Foam | Yes |
  | **XA** (Access) | 242.0" | 65.0" | 2.0" | 18 GA STL GALV PPC | 22 GA STL GALV | 2.0" Foam | Yes |
  | **XA** (Access) | 225.0" | 41.0" | 2.0" | 18 GA STL GALV PPC | 22 GA STL GALV | 2.0" Foam | Yes |
  | **HC** (Heating Coil) | 212.0" | 13.0" | 2.0" | 18 GA STL GALV PPC | 22 GA STL GALV | 2.0" Foam | Yes |
  | **PC** (Plenum Cap) | 206.0" | 85.0" | 2.0" | 18 GA STL GALV PPC | 22 GA STL GALV | 2.0" Foam | Yes |
  | **RF** (Return Fan) | 186.0" | 26.0" | 2.0" | 18 GA STL GALV PPC | 22 GA STL GALV | 2.0" Foam | Yes |
  | **FR** (Fan Return) | 186.0" | 56.0" | 2.0" | 18 GA STL GALV PPC | 22 GA STL GALV | 2.0" Foam | Yes |

### Skid 3 (Mid-Left Skid)
- **Max X Coordinate**: `154.0"`
- **Layout Order**: `XA` $\rightarrow$ `XA` $\rightarrow$ `IP` $\rightarrow$ `XA` $\rightarrow$ `FF`
- **Segment Audit**:
  | Segment | X Coordinate | Length X | Casing Thickness | Exterior Skin | Interior Liner | Insulation | Thermal Break |
  |---|---|---|---|---|---|---|---|
  | **XA** (Access) | 154.0" | 32.0" | 2.0" | 18 GA STL GALV PPC | 22 GA STL GALV | 2.0" Foam | Yes |
  | **XA** (Access) | 151.0" | 35.0" | 2.0" | 18 GA STL GALV PPC | 22 GA STL GALV | 2.0" Foam | Yes |
  | **IP** (Inlet Plenum) | 127.0" | 24.0" | 2.0" | 18 GA STL GALV PPC | 22 GA STL GALV | 2.0" Foam | Yes |
  | **XA** (Access) | 121.0" | 33.0" | 2.0" | 18 GA STL GALV PPC | 22 GA STL GALV | 2.0" Foam | Yes |
  | **FF** (Filter) | 121.0" | 6.0" | 2.0" | 18 GA STL GALV PPC | 22 GA STL GALV | 2.0" Foam | Yes |

### Skid 4 (Discharge Skid)
- **Max X Coordinate**: `73.0"`
- **Layout Order**: `HW` $\rightarrow$ `HW` $\rightarrow$ `XA` $\rightarrow$ `XA` $\rightarrow$ `FF` $\rightarrow$ `IP` $\rightarrow$ `FE`
- **Segment Audit**:
  | Segment | X Coordinate | Length X | Casing Thickness | Exterior Skin | Interior Liner | Insulation | Thermal Break |
  |---|---|---|---|---|---|---|---|
  | **HW** (Hot Water Coil) | 73.0" | 48.0" | 2.0" | 18 GA STL GALV PPC | 22 GA STL GALV | 2.0" Foam | Yes |
  | **HW** (Hot Water Coil) | 73.0" | 48.0" | 2.0" | 18 GA STL GALV PPC | 22 GA STL GALV | 2.0" Foam | Yes |
  | **XA** (Access) | 55.0" | 18.0" | 2.0" | 18 GA STL GALV PPC | 22 GA STL GALV | 2.0" Foam | Yes |
  | **XA** (Access) | 55.0" | 18.0" | 2.0" | 18 GA STL GALV PPC | 22 GA STL GALV | 2.0" Foam | Yes |
  | **FF** (Filter) | 49.0" | 6.0" | 2.0" | 18 GA STL GALV PPC | 22 GA STL GALV | 2.0" Foam | Yes |
  | **IP** (Inlet Plenum) | 0.0" | 49.0" | 2.0" | 18 GA STL GALV PPC | 22 GA STL GALV | 2.0" Foam | Yes |
  | **FE** (Fan Segment) | 0.0" | 55.0" | 2.0" | 18 GA STL GALV PPC | 22 GA STL GALV | 2.0" Foam | Yes |

---

## Comparison with Expected Suffix Sequences

The segments defined in the XML map directly to your expected sequence using suffixes for multiples:

1. **Skid 1 (Intake)**: `(MB-FF3-AT-DP-FS)` $\rightarrow$ Maps exactly to XML Skid 1.
2. **Skid 2**: `(XA7-FR-CC-XA6-HC-RF)` $\rightarrow$ Maps to XML Skid 2.
3. **Skid 3**: `(XA5-IP2-FF2-XA4-XA3)` $\rightarrow$ Maps to XML Skid 3.
4. **Skid 4 (Discharge)**: `(HW2-XA2-FE-HW1-XA1-FF1-IP1)` $\rightarrow$ Maps to XML Skid 4.

> [!TIP]
> The source XML data confirms that **all 24 physical segments** have identical casing configurations, and our coordinate sorting logic correctly structures them from intake to discharge.
