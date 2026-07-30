# UnitConstructionVerifier — Deep Project Review

**Date:** 2026-07-09 | **Reviewed with:** Brandon Brown

---

## 1. Project Summary

The **UnitConstructionVerifier** is an Autodesk Inventor 2020 COM add-in (.NET/C# + WPF) that cross-checks physical AHU assembly parts against their engineering configuration database. It reads embedded `DOCUMENT_CONFIG_JSON` attributes from surface IAMs, scans IPT custom properties (`YCMATL`, gauge, stock number), builds an expected-vs-actual material spec comparison, and displays mismatches in a tabbed WPF panel with optional write-back to Inventor.

---

## 2. User & Operational Context

| Question | Answer |
|---|---|
| **Primary users** | Drafters/detailers — want green/red signal, not database internals |
| **Maintainer** | Solo (Brandon), maintained alongside primary job |
| **Distribution** | Personal machine only today; plan to distribute to other detailers eventually |
| **Unit complexity** | Multi-skid (2–4 skids) is the **normal** case, not an edge case |
| **Write-back trust** | Low — verifies manually; wants to eventually trust it |
| **Config file updates** | Rare — materials/gauges are stable and well-known |

---

## 3. Architecture Health Assessment

### ✅ Strengths

- **IAM boundary tracking** in `IptPropertyReader` is elegant — dynamically determines part ownership by checking for `DOCUMENT_CONFIG_JSON` presence as it walks the tree, no fragile naming conventions needed.
- **Bidirectional thickness map** (`materials_thickness_map.json`) is sophisticated — JCI's 5-digit precision scheme disambiguates material/paint combos that share nominal gauges.
- **Dominant material heuristic** in `ConstructionDataBuilder` is a smart solution for multi-sided configs where one side is open.
- **Backward-compatibility schema merging** in `PersistenceManager` shows good forward-thinking; the schema has already evolved once and was designed to handle it.
- **X-coordinate descending sort** correctly resolves the physical airflow order including the Skid 4 `null skidNumber` edge case.
- **15 focused unit tests** covering the exact business rules, all Inventor-COM-free.

### ⚠️ Technical Debt

| Item | File(s) | Severity | Notes |
|---|---|---|---|
| `FormatGaugeAndMaterial` / `Normalize` duplicated 3× | `ConstructionData.cs`, `VerificationEngine.cs`, `VerifierWindow.xaml.cs` | Medium | Should live in a shared `VerifierHelpers` static class |
| Wall Trim verification passes `ExteriorGaugeAndMaterial` as `expectedTrim` | `VerificationEngine.cs` L54-55 | Low | Architecturally confusing; logic is correct per spec but misleading for maintainers |
| `MaterialsConfig.Initialize()` re-parses JSON on every dialog open | `VerifierWindow.xaml.cs` | Low | Add a one-time init flag |
| Broad silent `catch {}` blocks | Extractors, `IptPropertyReader` | Medium | Not every catch logs; makes debugging hard. Add `DebugLogger.Log(ex, context)` to every catch |
| `MergeWallRows` / `MergeRoofRows` / `MergeBaseRows` are ~100 lines each with near-identical patterns | `PersistenceManager.cs` | Low | Generic merge helper with delegate map would halve the code |
| `MaterialsConfig` exposes mutable `public static Dictionary<>` | `MaterialsConfig.cs` | Low | Expose as `IReadOnlyDictionary` for safety |
| `SegmentTypeSuffix` typed as `object?` | `ConfigData.cs` L135 | Low | Should be `string?` with a JSON converter |

---

## 4. Feature Gaps (Known)

| Feature | Status | Priority |
|---|---|---|
| **Sequence-fallback segment rows** (MB, AT, FF segments without their own IAM) | Planned/incomplete — AGENTS.md references `skidSegmentSequence` but no row generation implemented | Medium |
| **Insulation verification** | Extracted + displayed; no mismatch check | Low (not in scope per AGENTS.md) |
| **Thermal break verification** | Extracted + displayed; no mismatch check | Low |
| **Paint verification** | Explicitly excluded by design | N/A |
| **Base Height / Curb Rest write-back** | Displayed only; no write path in `IptPropertyWriter` | Low |
| **Unknown-classification parts** | Silently skipped; no UI indicator | Medium |
| **Batch report export** (PDF/Excel) | Not implemented | Future |
| **Per-row pass/fail indicator** in surface list boxes | Not implemented | Medium |
| **Vault checkout integration** | Manual file system read-only check only | Future |
| **IDW BOM verification** | Not started | High (see roadmap) |

---

## 5. Confirmed Design Decisions & Rules

### 5A. Wall Seal-Off Angle Rule (NEW — to be implemented)
- **Classification**: New part type `Seal-Off Angle` (description contains `"seal-off angle"`)
- **Gauge expected**: Always **16 GA** (fixed)
- **Material expected**: Must match the wall row's **liner material** (not exterior skin)
- **Rule type**: This is a "fixed gauge + borrow material from sibling field" pattern — expect more like this as new units are encountered

### 5B. Part-Rule Extensibility (Design Direction Confirmed)
> Adding a new part-type rule **must be a config file change**, not a code change.

This means the classification engine needs a **config-driven rule schema** in `materials_config.json` that can express:
- The classification name
- How to identify the part (stock number list + description fallback keywords)
- The expected gauge source (`fixed:16`, or `borrow:InteriorLinerGauge`)
- The expected material source (`borrow:InteriorLinerMaterial`, or `borrow:ExteriorSkinMaterial`, etc.)
- Whether mismatches should be generated or display-only

### 5C. ThicknessHoverCommand (Actively Used)
- The second Inventor command on the Part ribbon is **actively used**
- Improvement goal: **simplify readability** — strip out info that is now redundant given the verifier's capabilities

---

## 6. Prioritized Roadmap

### 🔴 Priority 1 — Expand Part Classification Rules & Correctness

The biggest source of pain: builder logic makes wrong assumptions for part types that don't have explicit rules yet. The fix is two-pronged:

1. **Implement Wall Seal-Off Angle rule** immediately (concrete, well-defined)
2. **Design and implement a config-driven rule schema** in `materials_config.json` so new rules can be added without code changes — covering:
   - Fixed expected gauge overrides
   - "Borrow material from" references (`liner`, `exterior`, `channel`)
   - Display-only vs mismatch-generating behavior

### 🟠 Priority 2 — Snapshot-Based Regression Testing

**What it is:** When the verifier runs against a live Inventor assembly, it dumps the full extraction result (config JSON payloads + IPT scan result) to a `unit_snapshot.json` file next to the IAM. This snapshot can be loaded into unit tests without Inventor, making it possible to write regression tests that cover real-unit behavior.

**Why it matters:** As new rules are added, regression tests against saved snapshots will catch silent breakage in previously-verified units before deployment.

**Implementation sketch:**
- Add `SnapshotSerializer.Save(IptScanResult, List<ConfigData>, path)` and `Load(path)` methods
- Add tests in `UnitConstructionVerifier.Tests` that load snapshot + run builder + engine and assert against a known-good expected output

### 🟡 Priority 3 — Builder Unit Test Expansion

Expand `ConstructionDataBuilderTests.cs` to cover:
- Multi-skid assemblies with mixed nominal thicknesses
- Segments present in `skidSegmentSequence` but missing their own IAM (sequence fallback)
- Dominant material heuristic tie-breaking
- Seal-off angle expected value resolution

### 🟢 Priority 4 — ThicknessHoverCommand Simplification

Audit what information the tooltip currently shows and strip it to just the signal that's still useful now that the verifier window gives the full picture.

### 🔵 Priority 5 — IDW BOM Verification *(Future)*

A new verification layer that:
1. Locates the surface IDW and Skid Packet IDW for the active unit
2. Reads BOM rows from each IDW
3. Matches BOM part numbers against the active assembly
4. Flags: part numbers present in assembly but missing from BOM, or material annotations in the drawing that don't match verified specs
5. Optionally updates the IDW material fields

> [!IMPORTANT]
> IDW file location is variable — generated alongside the IAM at upload time but occasionally placed elsewhere. The tool will need a configurable search strategy (sibling folder first, then fallback to user browse).

### 🔵 Priority 6 — Distribution Documentation *(Future)*

Create a clean, step-by-step deployment guide for other detailers to follow manually. Cover:
- Prerequisites (Inventor 2020, .NET version)
- Deploy directory path (`%APPDATA%\Autodesk\Inventor 2020\Addins\`)
- Hot-reload procedure (rename old DLL, copy new one, restart Inventor)
- Rollback steps

---

## 7. Code Quality Quick Wins (Can be done any time)

These are low-risk, low-effort improvements that increase long-term maintainability:

1. **Extract `FormatGaugeAndMaterial` and `Normalize`** from the three files they're duplicated in into a single `VerifierHelpers.cs` static class.
2. **Gate the raw JSON temp dumps** with an `#if DEBUG` or a runtime `DebugLogger.IsVerbose` flag (even if current workstation security isn't a concern, it's good hygiene for when others use it).
3. **Add `DebugLogger.Log(ex, context)` to every bare `catch {}`** that currently swallows exceptions silently.
4. **Add one-time init flag** to `MaterialsConfig.Initialize()` to avoid re-parsing on every dialog open.

---

## 8. Open Questions for Future Sessions

- [ ] Which other part types follow the "fixed gauge + borrow material from field X" pattern? (Discoverable only from real units)
- [ ] What does the ThicknessHoverCommand currently show that should be removed?
- [ ] For IDW BOM verification — what is the typical folder naming convention and how reliably are IDWs co-located with their IAM?
- [ ] For the Skid Packet IDW — is there a consistent naming convention that links it to the top-level unit assembly?
