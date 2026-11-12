# Test plan: roof openings after the YLength workaround

Status: **planned, not started** (2026-11-11). Nothing here is built. Run it on a test unit before any change ships.

## 1. Background

Detailers raise a roof surface's YLength (thickness) so its slope lands where it must, for example the vestibule
roof on job 20275 (surface 0050: 2" → 2.79", so the low side meets the main roof's 4.75" high side). The server then
regenerates the roof and **drops its `surfaceSegmentList`**, and with it the roof's openings (a roof's openings
live in its segment's `openingList`, filtered by `unitSide`).

Evidence, from reading all 121 roof configs under `C:\Users\jbrow263\ISG` and YCShell's own code:

- A roof has no segments exactly when its top (`y + yLength`) is above its skid's segment tops. Every such roof is
  a sloped roof with a non-standard thickness: 391Z010296-0050 (2.79), 391Z010109-0127 (2.8), 391Z010233-0077 and
  -0088 (2.25, test unit 20227).
- Without a segment, YCShell builds skins, liners and trim with fallback materials (STL GALV 20), leaves insulation
  empty (regions take their insulation type from the segment they overlap) and the paperwork BOM prints
  "Error getting segment and material."
- UCV v1.0.38 restores the segment and rebuilds the foam (see `HOW_TO_USE.md`, *Restore roof segments*). It does
  **not** rebuild parts or add openings. This plan covers what is left: openings, and possibly rebuilding the whole
  roof correctly.

## 2. What a roof opening is in YCShell

Not a part. `ISG_YC_Roof_Generator.CGenerator_YC_Roof` frames the roof around each opening, in about 40 methods:

| Step | YCShell method(s) |
|---|---|
| Split the channels around the opening into framed sub-regions | `ReprocessChannelsInSubRegionsThatFrameOpenings`, `regenerateChannelsInSubRegionThatFramesAnOpening` |
| Place the framing channels | `PlaceOpeningChannels` (inside `placeChannelsAndBrackets`) |
| Hole patterns on the framing channels, skin and liner side | `CreateOpeningHolePatternGroups_SkinSide/_LinerSide`, `AddOpening{Vertical,Horizontal}HolesToChannels_SkinSide/_LinerSide` |
| Cut-outs in every sheet the opening crosses | `AddOpening{Vertical,Horizontal}HolesTo{Skin,Liner}Sheets` (hole types `SkinOpening`, `LinerOpening`) |
| Foam weep holes in the opening channels | `AddFoamWeepHolesToOpeningChannels` |
| Remove attachment holes inside the opening's keep-out zone | `RemoveChannelAttachmentHolesFromOpeningExclusionRegions` |

Paperwork then dimensions each opening from its Left/Right/Top/Bottom framing channels (`AddOpeningDimensions`,
no null check: a missing frame stamps "Failed to add opening dimensions."). Insulation regions that are openings are
skipped (`IsRegionAnOpening`).

## 3. Option A: run YCShell's own generator on the restored config

`ISG_Common_Generator.CGenerator.Run(CGeneratorOptions, …)` is shipped on every detailer PC; it is the pipeline the
server runs. Given a config that has its segment back, it should build the roof the way the order meant it.

What `Run` does that matters:

1. Deletes and recreates `options.OutputFolder`. **Never point it at a job folder.**
2. Loads engineering rules from the shell-generator web service (`GetEngineeringRulesJSON`) and refreshes the model
   cache.
3. Starts its own `CInventorInstance`, hides it, disables user interaction. Unverified whether this attaches to the
   running Inventor or starts a new one: find out first, from `BoundaryInventorCS.CInventorInstance..ctor`.
4. `Generate()`: places every part. Part numbers come from the service (`GetPartMap_*`).
5. Insulation, sequence numbers, BOM map (`SaveBOMMapAndStructure`, service), embedded JSON, save, PDW, paperwork.
6. Zips the result and calls `SaveBOMMapFileData` and **`WarehouseJSON`**: the config and files are stored in MOM.

So option A is an official regenerate performed from a client. **Needs sign-off from the MOM owners** before any
trial, because steps 4–6 create records.

### A. Test steps

1. Get sign-off. Agree which MOM environment the trial may write to.
2. On test unit 20227, surface 391Z010233-0077: restore its segments with UCV first (v1.0.38), keep the config.
3. Read `CInventorInstance`'s constructors; decide whether `Run` can be called inside Inventor or needs a separate
   process (a small console host referencing YCShell's DLLs, like the server).
4. Call `Run` with `OutputFolder` = a new empty temp folder, `SkipPDWProcessing` / `SkipDrawingProcessing` first.
5. Compare the new roof with a sister roof that kept its segments: part list, materials, insulation, openings.
6. Repeat with drawings on. Check the paperwork with UCV's *Check paperwork*.

### A. Pass criteria

- Skins, liners and trim carry the segment's materials; insulation regions are Foam with the right volumes.
- Every order opening on the roof is framed, cut and dimensioned. The paperwork has no watermark.
- Nothing was written to MOM beyond what the owners agreed to.

## 4. Option B: add openings by hand, with parameters the detailer enters

For when option A is not allowed. UCV would place what YCShell's generator would have, from values the detailer
enters (pre-filled from the order's opening where there is one).

### B. Parameters the detailer would define

| Parameter | Source / default |
|---|---|
| Opening type, shape | Order opening (`openingType`, `openingShape`), else the detailer |
| Position X, Z and size (length, width) | Order opening geometry, else the detailer; validated inside the roof footprint |
| Framing: which sides get a channel, channel stock (indoor/outdoor, sloped vertical) | Roof type and slope; detailer confirms |
| Channel splits | The existing channel the opening crosses is cut into two around it |
| Sheet cut-outs | Every skin and liner the opening overlaps gets a `SkinOpening` / `LinerOpening` hole |
| Hole patterns, weep holes, keep-out | Engineering rules (`DOCUMENT_ENGINEERING_JSON`), not the detailer |

### B. How UCV could place parts

YCShell's **Add Library Part** (`CFrmPartLibrary.DoAdd`, then `Library_Add_OnExecute`) is:

1. Copy the library part's cached IPT and XLS into the surface folder as `<model>_<n>.IPT`.
2. Write the parameter values into the XLS (the part's `CALCULATOR` reference) and update.
3. Set `PART_SOURCE`, `LIBRARY_FILE_TYPE`, `PART_TYPE`, `PART_SUBTYPE`, sheet thickness; save, close.
4. Inventor's interactive Place command (the detailer clicks).

UCV could do steps 1–3 the same way and place the occurrence with a transform computed from the opening, with no
click. Sheet cut-outs are rows added to the sheet's `INPUT_PARAMETER_ISG_HOLE_n_*` parameters (`TYPE`, `SHAPE`,
`CENTER_POINT`, `SIZE`, `FEATURE_NAME`), as on the existing liners.

### B. Risks

- This re-implements part of the roof generator. It will drift when YCShell changes; edge cases (an opening over a
  splice, near a perimeter channel, on the slope) are where it will be wrong.
- Hole patterns and weep-hole spacing come from engineering constants UCV does not read today.
- Upload must accept the new parts as library parts (`DerivedFromLibraryPart`) and the paperwork must find the frame.

### B. Test steps

1. Pick a roof in test unit 20227 that has an order opening (check its skid's segments for `unitSide: Top`
   openings; 0050's vestibule segment has none).
2. Add the opening by hand in YCShell (Add Library Part + edits) and record every parameter used: this is the
   reference.
3. Prototype UCV's version on a copy; compare part by part with the reference, then run *Check paperwork* and the
   insulation check.
4. Upload to the test environment; confirm the paperwork dimensions the opening.

### B. Pass criteria

Same parts, parameters and holes as the hand-made reference; paperwork dimensions the opening; Upload succeeds.

## 5. Open questions

- Can MOM match roof segments by footprint instead (the upstream fix)? If yes, both options are unnecessary.
- Can the order tool set `lowSideThickness` without raising YLength? If the slope still lands, the segment never
  drops.
- Which of the four affected roofs actually had order openings? (0050: none.)
- For already-uploaded roofs whose parts are already right, how is corrected paperwork sent to MOM? YCShell's Upload
  skips a surface with no part changes ("Complete - No Change Detected").
