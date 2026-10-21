# ucv-scan — headless unit scanner

Reads unit folders **read-only** through Inventor Apprentice (Inventor does not need to be running) and
runs the verifier's own builder and engine on every unit, exactly as the add-in would.

Use it to:

- check a rule or config change against every real unit before shipping it;
- produce snapshot baselines for `UnitConstructionVerifier.Tests/Snapshots` without clicking
  **Save Snapshot** in Inventor;
- investigate how units are actually built (all scanned part properties are in the snapshots).

## Build and run

```bat
dotnet build UnitConstructionVerifier.sln -c Release
UnitConstructionVerifier.Scan\bin\Release\net48\ucv-scan.exe "C:\Users\<you>\ISG" --out C:\temp\ucv-scan
```

Requires Inventor 2020 (for Apprentice) on the machine. Scanning about 650 IAMs takes roughly 10 minutes.

To try a rule or `materials_config.json` change, re-run the rules over an earlier scan's snapshots instead
of rescanning (takes seconds, opens no Inventor files):

```bat
ucv-scan.exe --replay C:\temp\ucv-scan --out C:\temp\ucv-scan-after
```

Compare the two `summary.csv` files, or diff individual `.outcome.json` files.

## What it reads

Every `*.iam` under the given folders (skipping `OldVersions`) that carries `DOCUMENT_CONFIG_JSON` is a
surface. Surfaces are grouped into a **unit** by the folder that holds the surface folders (a skid or
unit folder). For each surface it reads the config JSON, the engineering JSON (`DOCUMENT_ENGINEERING_JSON`)
and every part's iProperties and `MOM_DATA`, using the same parsers as the add-in
(`ConfigJsonExtractor.Parse`, `EngineeringJsonExtractor.Parse`, `IptPropertyMapper`).

Differences from the add-in: Apprentice has no active material or live sheet metal thickness, so those two
fallbacks are skipped. Saved overrides (Save Overrides sidecar) are not applied, the same as snapshots.

## Output

| File | Contents |
|---|---|
| `<unit>.unit_snapshot.json` | Builder/engine inputs (same format as **Save Snapshot**) |
| `<unit>.outcome.json` | Built construction rows and one outcome per part |
| `summary.csv` | Per unit: surfaces, parts, pass, mismatch, not checked, not in config, display only, modified |

To use a unit as a regression baseline, copy its `.unit_snapshot.json` into
`UnitConstructionVerifier.Tests/Snapshots` and follow the README there.
