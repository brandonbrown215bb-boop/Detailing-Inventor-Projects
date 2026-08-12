# Step 14 Promotion Evidence

Status: **Prepared, not approved for promotion**

Prepared: 2026-08-11

Promotion quest: `Q-20260807-200122-ca4a`

The automated remediation gate is green. Fresh-machine launch, end-to-end detailer,
and supported Inventor/Apprentice evidence remain pending. Promotion to the repository
root is a later deliberate change.

## Reproducible automated evidence

Run `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\prepare_step14.ps1`
from this directory. The current preparation run produced:

- Release test result: **429 passed, 0 failed, 0 skipped**.
- Focused Steps 11-13 result: **11 passed**.
- Self-contained runtime: `win-x64`.
- Publish output: `.artifacts/publish/win-x64/UnitProgressTracker.Wpf.exe`.
- Publish inventory: **256 files, 153,268,354 bytes**.
- Source formatting check: `git diff --check` passed.

The publish output is ignored build evidence, not a source deliverable. Copy the whole
publish directory for the fresh-machine check; the executable is not a single-file app.

## Contract matrix

“Automated pass” means the current Release suite has evidence for the non-host portion
of the scenario. It does not silently stand in for a row marked workstation pending.

| ID | Promotion classification | Automated evidence | Workstation evidence |
| --- | --- | --- | --- |
| `UPT-C-001` | Automated pass; smoke pending | v4 portable/offline project tests | Reopen copied project with source disconnected |
| `UPT-C-002` | Automated pass; UI smoke pending | dirty-state/open preservation tests | Cancel dirty-project replacement dialog |
| `UPT-C-003` | Accepted difference; automated pass | explicit Pigeon v2 rejection tests | Confirm actionable message in published UI |
| `UPT-C-004` | Accepted difference; automated pass | explicit Esmund v3 rejection tests | Confirm no partial import in published UI |
| `UPT-C-005` | Automated pass; UI smoke pending | typed corrupt/newer/section diagnostics | Confirm last project remains visible |
| `UPT-C-006` | Automated pass | atomic save interruption tests | Optional filesystem interruption observation |
| `UPT-C-007` | Automated pass; Inventor pending | `Step3RescanTrackingTests` | Unchanged supported-IAM rescan |
| `UPT-C-008` | Automated pass; Inventor pending | reviewed renumber tests | Confirm and decline candidate transfer |
| `UPT-C-009` | Automated pass; Inventor pending | missing/new/review transaction tests | Exercise all missing-surface choices |
| `UPT-C-010` | Automated pass; Inventor pending | cancelled/failed scan preservation tests | Cancel a real scan after discovery starts |
| `UPT-C-011` | Automated pass; Inventor pending | `Step10DiagnosticsTests` and conflict tests | Trigger/read a real source failure |
| `UPT-C-012` | Automated pass; smoke pending | `Step4StatusDefinitionTests` | Switch between two projects with different states |
| `UPT-C-013` | Automated pass; smoke pending | `Step5ChecklistTemplateTests` | Add surface after template edit |
| `UPT-C-014` | Automated pass; smoke pending | `Step6BomIsolationTests` | Edit/switch/reopen two project BOMs |
| `UPT-C-015` | Automated pass; Inventor pending | `Step7ReplaceSurfaceTests` | Replace from supported IAM candidate |
| `UPT-C-016` | Automated pass; Inventor pending | `Step8AddSurfaceTests` | Add folder with valid, duplicate, and failed files |
| `UPT-C-017` | Automated pass; smoke pending | `Step9RetireRestoreTests` | Retire/save/reopen/restore |
| `UPT-C-018` | Automated pass; smoke pending | `Step11FilteringTests`, `Step12VisibilityTests`, `Step13OptionsTests` | Compare list/viewport and reopen state |
| `UPT-C-019` | Automated pass; smoke pending | transactional/scope tests in `Step13OptionsTests` | Save, reopen, then cancel a second edit |
| `UPT-C-020` | **Pending promotion review** | This matrix and automated gate | Requires all pending rows and reviewer approval |
| `UPT-C-021` | Automated pass; Inventor pending | rescan/add/replace intrusion tests | Confirm persistent non-blocking warning in host path |

## Approved post-promotion work

These are visible deferrals, not failed parity claims:

| Work | Quest | Classification |
| --- | --- | --- |
| Sticker settings and scale/orientation | `Q-20260806-192913-57d1` | Approved post-promotion |
| Bulkhead positioning | `Q-20260806-192905-882a` | Approved post-promotion |
| Opening and door rendering | `Q-20260806-192858-f184` | Approved post-promotion |
| Overlay hover detail completion | `Q-20260806-192645-79fe` | Approved post-promotion |

## Remaining gate evidence

- Complete and sign `STEP14_WORKSTATION_SMOKE.md` using the self-contained package.
- Attach supported Inventor/Apprentice version and lifecycle results.
- Commit the final source, inspect the refreshed architecture note, then run Agent
  Ground `verify`; the note is intentionally unverified while behavior is uncommitted.
- Review this matrix and explicitly approve promotion before moving Pigeon to root.
