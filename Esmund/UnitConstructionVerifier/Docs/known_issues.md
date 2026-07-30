# Known issues & troubleshooting

Operational notes for the Unit Construction Verifier add-in. Not code defects unless noted as such.

---

## Ctrl+V in Verifier pastes old Inventor clipboard (v1.0.18.0+)

**Reported:** 2026-07-23  
**Status:** Expected behavior with modeless UI; no fix unless users report it often.

### Symptom

With the Verifier window open, the user selects a surface or part in the list (locator box highlights correctly). They press **Ctrl+C** — nothing obvious happens in Inventor. They press **Ctrl+V** while still in the Verifier — Inventor tries to **paste a previously copied assembly occurrence**, sometimes opening a missing-reference or “path not current” dialog. The path shown may be unrelated to the current pick (e.g. an old compile folder or `.ps1` file visible in the browse tree).

### Cause

1. **v1.0.18.0** stopped using Inventor `SelectSet` for default highlight (locator box instead). **Ctrl+C no longer copies the highlighted row into Inventor’s assembly clipboard** — there is no live component selection to copy.

2. The Verifier is **modeless** and highlighting may **activate** the unit/surface IAM (`ResolveHighlightAssembly` → `document.Activate()`). Inventor can still handle **assembly keyboard shortcuts** (including **Ctrl+V**) even when focus appears to be on the Verifier.

3. **Ctrl+V** therefore applies whatever was **already on Inventor’s clipboard** from an earlier **Inventor** copy (Ctrl+C in the graphics window or browser), not from the Verifier list.

4. The Verifier has **no custom Ctrl+C / Ctrl+V handlers**. Grid **Ctrl+C** only copies visible cell text (WPF default). The **⧉** buttons copy part/surface **text** via `Clipboard.SetText`, not assembly occurrences.

### What is *not* happening

- The locator box does not populate `SelectSet`.
- The Verifier does not invoke compile/unblock `.ps1` scripts.
- **Open in Inventor** (context menu) is separate; it uses scanned `FilePath` / `SourceSurfaceIam` and shows our **“File Not Found”** MessageBox if the path is invalid.

### Workarounds for users

- Before using the Verifier, avoid leaving a stale **Inventor component copy** on the clipboard; copy something neutral in Notepad, or use **Normal** / close the Verifier and clear selection if confused.
- If paste was accidental, cancel Inventor’s resolve/paste dialog; no change was made by the Verifier itself.
- Use **⧉** on a row when you need the part or surface **number** on the text clipboard.

### Possible future mitigations (not implemented)

- Swallow **Ctrl+V** (and optionally **Ctrl+C**) on the Verifier window so shortcuts do not reach Inventor while it has focus.
- Avoid **document.Activate()** on every list selection when the user is working entirely in the Verifier.
- Show a one-line hint in the UI that assembly paste is Inventor-driven, not list-driven.

### Related version history

| Version | Highlight behavior | Ctrl+C in Verifier + Inventor active |
|---------|-------------------|--------------------------------------|
| ≤ v1.0.17.0 | Native `SelectSet` | Could copy highlighted occurrence into Inventor clipboard (accidental duplicate paste risk) |
| v1.0.18.0+ | Locator box / X-Ray | Ctrl+C does not refresh Inventor assembly clipboard; Ctrl+V may paste **previous** Inventor copy |

See also: `STABLE_BASE.md` (feature summary).
