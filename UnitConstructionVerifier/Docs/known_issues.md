# Known issues & troubleshooting

Operational notes for the Unit Construction Verifier add-in. Not code defects unless noted as such.

---

## Ctrl+V in Verifier pastes old Inventor clipboard (v1.0.18.0+)

**Reported:** 2026-07-23  
**Status:** Mitigated in v1.0.26.0: while the Verifier has focus it takes Ctrl+C, Ctrl+V and F8 before Inventor (see *Fix* below). The description below is how it behaved before.

### Symptom

With the Verifier window open, the user selects a surface or part in the list (locator box highlights correctly). They press **Ctrl+C** — nothing obvious happens in Inventor. They press **Ctrl+V** while still in the Verifier — Inventor tries to **paste a previously copied assembly occurrence**, sometimes opening a missing-reference or “path not current” dialog. The path shown may be unrelated to the current pick (e.g. an old compile folder or `.ps1` file visible in the browse tree).

### Cause

1. **v1.0.18.0** stopped using Inventor `SelectSet` for default highlight (locator box instead). **Ctrl+C no longer copies the highlighted row into Inventor’s assembly clipboard** — there is no live component selection to copy.

2. The Verifier is **modeless** and highlighting may **activate** the unit/surface IAM (`ResolveHighlightAssembly` → `document.Activate()`). Inventor can still handle **assembly keyboard shortcuts** (including **Ctrl+V**) even when focus appears to be on the Verifier.

3. **Ctrl+V** therefore applies whatever was **already on Inventor’s clipboard** from an earlier **Inventor** copy (Ctrl+C in the graphics window or browser), not from the Verifier list.

4. The Verifier has **no Ctrl+V handler**. Ctrl+C on a selected part or surface copies its **number** as text (v1.0.25), and the right-click **Copy part number / Copy surface number** items do the same via `Clipboard.SetText`. None of these put assembly occurrences on the clipboard.

### What is *not* happening

- The locator box does not populate `SelectSet`.
- The Verifier does not invoke compile/unblock `.ps1` scripts.
- **Open in Inventor** (context menu) is separate; it uses scanned `FilePath` / `SourceSurfaceIam` and shows our **“File Not Found”** MessageBox if the path is invalid.

### Workarounds for users

- Before using the Verifier, avoid leaving a stale **Inventor component copy** on the clipboard; copy something neutral in Notepad, or set **Highlight → Off** / close the Verifier and clear selection if confused.
- If paste was accidental, cancel Inventor’s resolve/paste dialog; no change was made by the Verifier itself.
- Right-click a row and use **Copy part number / Copy surface number** when you need the number on the text clipboard.

### Fix (v1.0.26.0)

Inventor handles its shortcuts in its own message loop before the key is dispatched to the WPF window, so `PreviewKeyDown`
never saw Ctrl+C or F8 (confirmed in Inventor 2020, 2026-10-22). `UI/ShortcutHook.cs` installs a thread `WH_KEYBOARD` hook
while the Verifier is open. When keyboard focus is inside the Verifier window it removes Ctrl+C, Ctrl+V and F8 from the
queue and runs them in the window: copy/paste text in text fields, copy part or surface numbers elsewhere, Ctrl+V does
nothing outside text fields, F8 is **Next issue**. All other keys, and these keys while focus is in Inventor, pass through.

### Possible further mitigations (not implemented)

- Take more Inventor shortcuts the same way if they cause trouble while the Verifier has focus (Ctrl+X, Ctrl+Z, Delete).
- Avoid **document.Activate()** on every list selection when the user is working entirely in the Verifier.
- Show a one-line hint in the UI that assembly paste is Inventor-driven, not list-driven.

### Related version history

| Version | Highlight behavior | Ctrl+C in Verifier + Inventor active |
|---------|-------------------|--------------------------------------|
| ≤ v1.0.17.0 | Native `SelectSet` | Could copy highlighted occurrence into Inventor clipboard (accidental duplicate paste risk) |
| v1.0.18.0–v1.0.25.0 | Locator box / X-Ray | Ctrl+C does not refresh Inventor assembly clipboard; Ctrl+V may paste **previous** Inventor copy |
| v1.0.26.0+ | Locator box / X-Ray | Verifier takes Ctrl+C (copies numbers as text) and Ctrl+V (text fields only) while it has focus |

See also: `HOW_TO_USE.md` and `RELEASE_NOTES.md`.

---

## Perimeter angles on bases with an upturned lip on only some sides

**Status:** By design.

### Symptom

On a base with an upturned lip (UTL), some perimeter angles pass as 10 GA ALM SHT (or 12 GA STL GALV /
10 GA stainless) while others show **✗ Mismatch** with 16 GA STL GALV as the actual value.

### Cause

The configuration marks the whole base surface as UTL (`upturnedLipHeight`), even when the lip runs on only
one side. It does not say which sides, so the verifier expects the UTL angle on every perimeter angle of that
base.

### What to do

Check the flagged angles: if they are on a side with no upturned lip, 16 GA STL GALV is correct and the
mismatch can be ignored. If they are on the lip side, the part is wrong. The verifier deliberately does not
accept both values, because that would hide a UTL angle that came out as 16 GA STL GALV.

