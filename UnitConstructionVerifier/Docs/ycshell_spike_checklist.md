# YCShell Spike: Morning Checklist (Step 0)

Branch `spike/ycshell-bridge`. Plan: `batch_regenerate_upload_plan.md`. Quest `Q-20261022-221452-dd97`.

The spike adds a **YCShell Spike** button to UCV's QA Tools > Construction panel (Assembly ribbon). It opens a report
window. The window **never uploads**, and it only regenerates surfaces whose path contains `Spike Copy`.

## Before you start

1. Close Inventor.
2. Install the spike build, which was already built in Release:
   `UnitConstructionVerifier\deploy.bat`. The spike build has the same version number as v1.0.30, so to go back,
   check out `main`, build and deploy again.
3. Start Inventor and log in to the MOM tools as usual, so YCShell is fully loaded.
4. Open `C:\Users\jbrow263\ISG\ISG Test Units\Hole Pattern Tiger Team\Unit - Spike Copy\FINAL.IAM`.
   - **Do not** open the copy's `Base\FINAL.IAM`. It points at the original unit's base files.
   - **Do not** press Upload anywhere.
5. Optional: if you want the orphaned surface in the test, relink `391Z010068-0091.IAM` in the copy's FINAL and save
   (the copy only).

## Run

1. Press **YCShell Spike**. The window refreshes on open. Every line is also written to
   `%TEMP%\UnitConstructionVerifier\ycshell_spike_<time>.txt`.
2. Read the top of the report:
   - [ ] **Connected to YCShell ...**, or **NOT CONNECTED**. If it is not connected, stop here and send the log. The
     trace lines show whether YCShell is in the same AppDomain and which event handlers were visible. That decides
     whether the reflection approach can work at all.
   - [ ] Every member line reads `ok`. Any `MISS` means the installed YCShell differs from our source copy.
3. Read the surface list:
   - [ ] **Question 1:** "Active unit references N sub-assemblies directly; M are in YCShell's list". Expect 32 of 32
     (31 if 0091 is not relinked). Note any `NOT IN YCSHELL LIST` lines.
   - [ ] "Active unit is in YCShell's list: ... composite=True" (question 2, first half).
   - [ ] Changed surfaces are at the top with `CHANGED n`. Expect 0003, 0006, 0009, 0055, 0059, 0065, 0073, 0074,
     0078 and 0080 (plus 0091 if relinked).
4. Regenerate one surface. **0055** is a good first choice (1 changed part).
   - Select it, then press **Regenerate selected** and confirm.
   - Answer YCShell's own message boxes as you normally would. Note each one you get.
   - [ ] The report shows "Regenerate returned after N s". This answers question 5 (time per surface, including
     your clicks).
   - [ ] **Question 3:** compare the "Before/After: active edit document" lines. Did `ReturnToTop` leave you where
     you expected?
   - [ ] Compare the before and after part status. YCShell saves as it regenerates: replaced channels become `New`
     parts, and the surface is saved.
   - [ ] In Inventor, look at the regenerated channels on 0055. Do they match what pressing YCShell's own button gives?
5. Optional: regenerate a larger one (0074, 8 changed parts) for a second timing.
6. Regenerated surfaces are already saved by YCShell. That is fine here, because this is the copy.

## Regenerate changed (step 2)

Branch `ucv/batch-regenerate-upload`. Build and deploy it as above, then open the spike copy's `FINAL.IAM`.

1. Open the verifier (**Verify & Edit Specs**) and let it scan. The footer shows **Regenerate changed (N)…**.
   - [ ] Without records, expect about 7: 0019 and 0055 (spike leftovers), plus 0073, 0074, 0078 and 0080 ("can't
     tell", orange). Surfaces with only skin, liner or subfloor changes must not be ticked.
2. Press it.
   - [ ] The review panel lists every surface with its reason; ticks and colours as above.
   - [ ] Untick all but **two small surfaces** for the first run (for example 0019 and 0055).
3. Press **Regenerate 2 surfaces**.
   - [ ] A small "Regenerating channel features" window appears top-right, above Inventor, with **Stop after this
     surface**.
   - [ ] YCShell's own progress shows for each surface.
   - [ ] The "Channel Features have been recreated/replaced" box closes by itself.
   - [ ] Any Yes/No question waits for you.
4. At the end:
   - [ ] The summary lists both surfaces with times.
   - [ ] The unit is re-checked.
   - [ ] The button count drops: those surfaces now have channel records (`.ucv_channel_fingerprints.json` next to
     FINAL).
5. **Stop test:** run again with three or more surfaces ticked and press Stop during the first.
   - [ ] The first finishes; the rest are listed as "Not run (stopped)".
6. **Pattern test:** pattern a channel on a regenerated surface, then Re-check unit.
   - [ ] That surface needs Regenerate again: "channel added: …".

## Send back

- The `ycshell_spike_*.txt` log.
- The message boxes YCShell showed, and your answers.
- Anything that looked different from pressing YCShell's button by hand.
