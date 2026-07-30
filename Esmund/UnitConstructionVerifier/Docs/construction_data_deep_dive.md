# Unit Construction Data Deep Dive

We conducted a deep-dive audit of the data extracted from all 9 roof surface configurations in the `Roof_Full` assembly. Here is the summary of the extracted properties and how they align with your physical sequence.

## Skid Sequence & Physical Coordinates

By inspecting the physical coordinate fields (`geometryList`) in each configuration file, we mapped the physical position of each skid along the unit's X-axis (left-to-right on the factory floor):

| Skid Number | Skid ID (GUID) | Segment Sequence | X Start Coordinate | Physical Layout Position |
|---|---|---|---|---|
| **Skid 3** | `070f66d7-4be3-4452-993a-fcd3d4b3569d` | `(MB FF-3 AT DP FS)` | **307.0"** | Rightmost (Intake / Inlet) |
| **Skid 2** | `bc56bab9-52b8-4f09-a368-8a2f36c901cc` | `(XA-7 FR CC XA-6 HC RF)` | **186.0"** | Mid-Right |
| **Skid 1** | `9ae77aed-9b08-4fd2-af67-7ab356e62116` | `(XA-5 IP-2 FF-2 XA-4 XA-3)` | **121.0"** | Mid-Left |
| **Skid 4** | `Unknown / Null` | `(HW-2 XA-2 FE HW-1 XA-1 FF-1 IP-1)` | **1.0"** | Leftmost (Supply / Discharge) |

> [!NOTE]
> **Skid 4** is stored with `skidNumber = null` in the database configuration, which was causing the verifier to skip it entirely in previous runs.
> By switching to **X-coordinate descending sorting**, we naturally order the skids in the exact airflow sequence you expected:
> `Skid 3` -> `Skid 2` -> `Skid 1` -> `Skid 4`

---

## Segment Construction Details Audit

Below is the side-by-side audit of all **23 segments** in the expected physical sequence. 

- **Populated Segments**: Segments that have casing panels in this Roof sub-assembly.
- **Sequence Fallback Segments**: Segments (like Dampers, Access Transitions, or Filter Sections) that do not have their own separate roof configurations in this assembly. They inherit the skid's nominal casing thickness (`2.0"` or `2.77"`) as a fallback:

| Skid | Segment | Source File | Casing Thickness | Exterior Skin | Interior Liner | Insulation | Thermal Break |
|---|---|---|---|---|---|---|---|
| **Skid 3** | **MB** | *Sequence Fallback* | 2.77" | - | - | - | - |
| **Skid 3** | **FF-3** | *Sequence Fallback* | 2.77" | - | - | - | - |
| **Skid 3** | **AT** | *Sequence Fallback* | 2.77" | - | - | - | - |
| **Skid 3** | **DP** | `391Z010111-0151` | 2.0" | 18 GA STL GALV PPC | 22 GA STL GALV | 2.0" Foam | Yes |
| **Skid 3** | **FS** | `391Z010111-0151` | 2.0" | 18 GA STL GALV PPC | 22 GA STL GALV | 2.0" Foam | Yes |
| **Skid 2** | **XA-7** | *Sequence Fallback* | 2.0" | - | - | - | - |
| **Skid 2** | **FR** | *Sequence Fallback* | 2.0" | - | - | - | - |
| **Skid 2** | **CC** | `391Z010111-0150` | 2.0" | 18 GA STL GALV PPC | 22 GA STL GALV | 2.0" Foam | Yes |
| **Skid 2** | **XA-6** | `391Z010111-0150` | 2.0" | 18 GA STL GALV PPC | 22 GA STL GALV | 2.0" Foam | Yes |
| **Skid 2** | **HC** | `391Z010111-0150` | 2.0" | 18 GA STL GALV PPC | 22 GA STL GALV | 2.0" Foam | Yes |
| **Skid 2** | **RF** | `391Z010111-0150` | 2.0" | 18 GA STL GALV PPC | 22 GA STL GALV | 2.0" Foam | Yes |
| **Skid 1** | **XA-5** | *Sequence Fallback* | 2.77" | - | - | - | - |
| **Skid 1** | **IP-2** | *Sequence Fallback* | 2.77" | - | - | - | - |
| **Skid 1** | **FF-2** | *Sequence Fallback* | 2.77" | - | - | - | - |
| **Skid 1** | **XA-4** | `391Z010111-0146` | 2.0" | 18 GA STL GALV PPC | 22 GA STL GALV | 2.0" Foam | Yes |
| **Skid 1** | **XA-3** | `391Z010111-0146` | 2.0" | 18 GA STL GALV PPC | 22 GA STL GALV | 2.0" Foam | Yes |
| **Skid 4** | **HW-2** | *Sequence Fallback* | 2.0" | - | - | - | - |
| **Skid 4** | **XA-2** | *Sequence Fallback* | 2.0" | - | - | - | - |
| **Skid 4** | **FE** | *Sequence Fallback* | 2.0" | - | - | - | - |
| **Skid 4** | **HW-1** | `391Z010111-0145` | 2.0" | 18 GA STL GALV PPC | 22 GA STL GALV | 2.0" Foam | Yes |
| **Skid 4** | **XA-1** | `391Z010111-0145` | 2.0" | 18 GA STL GALV PPC | 22 GA STL GALV | 2.0" Foam | Yes |
| **Skid 4** | **FF-1** | `391Z010111-0145` | 2.0" | 18 GA STL GALV PPC | 22 GA STL GALV | 2.0" Foam | Yes |
| **Skid 4** | **IP-1** | `391Z010111-0145` | 2.0" | 18 GA STL GALV PPC | 22 GA STL GALV | 2.0" Foam | Yes |

---

## Technical Audit Conclusion

1. **Material Extraction**: Dominant skin materials (e.g. `18 GA STL GALV PPC`) and liner materials (e.g. `22 GA STL GALV`) are being correctly computed and formatted from the active segment lists.
2. **Casing Thicknesses**: The verifier correctly extracts the actual wall thickness (e.g. `2.0"`) and nominal casing thickness (e.g. `2.77"` for sloped roof ends) instead of pulling sheet metal gauge thicknesses.
3. **Sorting**: Unified the sorting of Casing, Base, and Floor rows. Everything is now ordered from **Intake to Discharge** (X descending).
4. **Deploying the Fix**: The new sorting and sequence code is fully compiled and ready. Once you are done working in Inventor, close it and let me know, and I will copy the updated binaries to deploy the changes.
