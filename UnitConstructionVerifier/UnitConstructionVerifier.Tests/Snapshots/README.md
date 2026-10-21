# Real-unit snapshots

Regression baselines for `SnapshotRegressionTests`. Each unit has two files:

| File | What it is |
|---|---|
| `<unit>.unit_snapshot.json` | Builder/engine inputs captured from Inventor (config JSON, engineering data, IPT scan) |
| `<unit>.approved.json` | The builder + engine outcome you have confirmed is correct |

## Adding a known-good unit

1. Open the unit IAM in Inventor and launch the verifier.
2. Click **Save Snapshot** and copy the saved `.unit_snapshot.json` into this folder.
3. Run `dotnet test UnitConstructionVerifier.Tests`. The new case reports *inconclusive* and writes
   `<unit>.received.json`.
4. Review the received file. If it matches the real unit, rename it to `<unit>.approved.json`.

## When a snapshot test fails

The test writes `<unit>.received.json` and names the first differing line. Diff it against the approved
file. If the change is a bug, fix the code. If it is an intended rule or config change, replace the
approved file with the received one and commit both together.

Snapshots are captured **before** saved overrides (Save Overrides sidecar) are applied, so they test the
builder's own expectations. Approved files also depend on `materials_config.json`.
