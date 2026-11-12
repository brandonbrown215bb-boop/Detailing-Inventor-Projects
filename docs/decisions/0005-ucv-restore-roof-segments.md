# 5. Unit Construction Verifier: restore roof segments through YCShell

* **Status**: Accepted. Shipped in UCV v1.0.38.0; nominal thickness added in v1.0.39.0.
* **Date**: 2026-11-11

## Context and Problem Statement

Detailers raise a sloped roof's YLength so its slope lands correctly. The server then regenerates the roof without
its `surfaceSegmentList` (and so without its openings): on all 121 roofs found locally, a roof has no segments
exactly when its top is above its skid's segment tops. Without a segment YCShell leaves the insulation empty
(regions take their insulation type from the segment they overlap) and the paperwork cannot find the skin and liner
material. The ISG Bandage add-in "fixed" this by inventing a segment (`STL GALV` 16/20, `Foam`) and insulation
cells; on job 20275 that made the paperwork print 20 GA galvanized liners for 18 GA aluminum parts.

Until now UCV wrote only part files. This is the first time it changes a surface's `DOCUMENT_CONFIG_JSON`.

## Decision Drivers

* Correct, not just not failing: every value written must come from the order or from YCShell, never a default.
* YCShell stays read-only; its formats and rules stay authoritative.
* The detailer sees exactly what will change and can undo it.

## Considered Options

1. **Bandage approach.** Synthesize a segment and insulation in UCV.
2. **UCV edits the JSON itself.** Copy the segment in, write the attribute with Newtonsoft, leave insulation to the
   next Upload.
3. **Restore the order's segment, then let YCShell rebuild.** UCV copies the skid's own segment (shared by ID across
   the unit's surface configs) into the roof's config text, hands it to YCShell's parser
   (`SerializationExtensions.ToISGConfiguration`), and runs YCShell's insulation step the way its Upload does
   (`CUtility.CreateISGGenerator` → `GenerateInsulation` → `EmbedJSONInAssembly`).

## Decision

Option 3.

* A roof is restored only when it has no segment from its skid's order data. Segments are copied verbatim; ones that
  are not order data (no `skidID`, or an ID no other surface carries, e.g. the Bandage's) are removed.
* A segment is restored when it is on the roof's skid, overlaps the roof's X/Z footprint and reaches the roof's
  underside. Copies of one segment that differ between surfaces are not used.
* YCShell serializes and writes the config (JSON and its XML copy, engineering JSON) and builds the insulation from
  the roof's real channels. UCV writes no config JSON of its own.
* UCV saves a backup of the original config next to the surface IAM before writing, and saves only the surface IAM.
* Openings are not restored or added. The review lists the order's roof openings so the detailer knows.
* (v1.0.39) When the roof's `nominalSurfaceThickness` is one YCShell has no foam for (its housing insulation
  calculator, `YC_HousingInsulation.xlsx`, has only 2, 3 and 4), it is set to the segments' `wallThickness_Top` when
  they agree on one of those. Otherwise `CGenerator.ConvertMalformedInsulationToStub` turns every region into
  Foamboard. Only YCShell's insulation code reads the nominal thickness (`GenerateInsulation_Cavities`,
  `GenerateInsulationData_Cavities`, `GetRegionList`, `calculateFiberglassVolume`; on regions it only sizes
  structural-channel webs). The roof geometry and low/high thicknesses are kept. Seen on 20275 0050: v1.0.38 gave
  7 Foamboard regions at 2.79"; YCShell's calculator gives 2" foam (391-20002-210) at 2".

Option 1 was rejected because its values are guesses. Option 2 was rejected because YCShell's Upload skips a surface
with no part changes ("Complete - No Change Detected"), so the foam would never be rebuilt, and because UCV would
own a JSON writer for YCShell's format.

## Consequences

* The YCShell members are resolved and checked before use (`YcShellBridge`); if YCShell changes shape, the action
  reports which member is missing and writes nothing. Regenerate / Upload changed do not depend on them.
* Needs YCShell loaded with its engineering rules (logged in) and the roof loaded in YCShell.
* Parts built with fallback materials are not changed; UCV's verification flags them against the restored materials
  and the detailer writes them as usual. Expectations come from the config when the verifier opens, so it must be
  reopened after a restore.
* A roof already uploaded with correct parts is not re-sent by Upload. Getting corrected paperwork to MOM for it is
  open (see `UnitConstructionVerifier/Docs/roof_openings_local_generation_test_plan.md`).
* The real fix is upstream: MOM should match roof segments by footprint. When it does, this action finds nothing to
  do.
