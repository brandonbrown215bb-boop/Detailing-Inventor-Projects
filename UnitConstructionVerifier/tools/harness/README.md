# Verifier window test harnesses

Small console programs for checking `VerifierWindow` changes without driving Brandon's Inventor session.
They are not part of `UnitConstructionVerifier.sln`. Build each one into a folder outside the repo:

```
dotnet build -o <temp folder>
```

| Harness | What it does | Usage |
|---|---|---|
| `render/` | Opens `VerifierWindow` on made-up sample data (4 roofs, 3 walls, 1 base) with no Inventor, using Inventor's WPF compatibility setting, and saves PNGs of several states, including the staged-edit grid and the write review. The quickest way to check a visual change, including the 1080×660 minimum size. | `UcvHarness.exe <outDir> [WIDTHxHEIGHT]` |
| `compat-load/` | Loads a built `UnitConstructionVerifier.dll` with `Assembly.LoadFrom` and no WPF `Application`, the way Inventor does, then constructs the window. Copy the interop, Newtonsoft and JSON files next to the DLL first. | `LoadFromHarness.exe <path to UnitConstructionVerifier.dll>` |
| `inventor-highlight/` | Starts a **separate** Inventor (with a PID guard), opens an assembly and replays the highlight calls through reflection, logging each step and Inventor's memory. | `InvRepro.exe <iam> <old\|new\|boxclicks> <logFile>` |

## Inventor hosting traps

Each of these cost a round trip during the v1.0.24–1.0.28 window work.

- **No WPF `Application` inside Inventor.** An exception escaping a window handler ends Inventor with nothing logged.
  `VerifierWindow` has a `Dispatcher.UnhandledException` guard that logs to `%TEMP%\UnitConstructionVerifier\ucv_debug.txt`.
  Read that log first when something misbehaves.
- **WPF runs in .NET 4.0 compatibility mode.** The `InactiveSelectionHighlight*BrushKey` keys equal `ControlBrushKey` /
  `ControlTextBrushKey` there, so defining both pairs in resources throws "Item has already been added" and the window never opens.
  Reproduce outside Inventor with `FrameworkCompatibilityPreferences.AreInactiveSelectionHighlightBrushKeysSupported = false`
  set before any WPF object is created (`render/` and `compat-load/` do this).
- **`VisualTreeHelper.GetParent` throws on `Run` and other content elements**, which arrive as `OriginalSource` when the
  mouse is over text. Use `UI/TreeSearch.FindAncestor`.
- **Inventor takes keys before WPF sees them** (Ctrl+C, Ctrl+V, F8 and other accelerators). `PreviewKeyDown` never fires
  for them. `UI/ShortcutHook` installs a thread keyboard hook for the keys the verifier needs; add keys there.
- **You cannot click the window from outside.** UI Automation sees no content in the verifier, posted mouse messages do
  nothing, and `PrintWindow` captures white. Render it with `render/` instead, or reproduce in Inventor with the crash
  guard plus temporary `DebugLogger` lines.
- **Starting Inventor through COM** (`Activator.CreateInstance(Type.GetTypeFromProgID("Inventor.Application"))`) started a
  new process every time in testing, but still check its PID against Inventor processes that were already running and stop
  on a match: a user's session must never be driven or closed. The add-in is not active in that instance until
  `ApplicationAddIn.Activate()` is called. `Process.HasExited` on Inventor throws Access denied; check `Process.GetProcesses()`
  and clean up with `taskkill /PID … /F`.
- **`deploy.bat` waits for a key at the end.** From automation: ``"`n`n" | cmd /c ".\deploy.bat"`` in PowerShell. It renames
  a locked DLL so it can run while Inventor is open, but Inventor keeps the old copy until it restarts.
