# 3. Unit Construction Verifier: docked and pop-out hosting

* **Status**: Accepted (hosting and keyboard). Narrow-layout scope still open.
* **Date**: 2026-10-22

## Context and Problem Statement

Detailers want the verifier available docked inside Inventor as well as in its current floating window. Inventor
2020 add-ins dock UI through `UserInterfaceManager.DockableWindows`, which hosts a child window handle
(`DockableWindow.AddChild`). The verifier is WPF and runs inside Inventor's process with no WPF `Application` and
no WPF message loop.

Inventor's message loop handles its own shortcuts before keys are dispatched. For the floating window that already
cost Ctrl+C and F8 (fixed in v1.0.26 by `UI/ShortcutHook`). A docked window is a child of Inventor's frame, which
is worse. In the spike on `spike/dockable-window`, tested in Inventor on 2026-10-22, letters, Backspace, arrows
and Tab never reached a docked WPF panel. Number-pad key-downs arrived but produced no characters. Only Ctrl+C,
Ctrl+V and F8 could be rescued by the shortcut hook.

## Decision Drivers

* Typing, grid editing, Tab navigation and copy/paste must work docked as well as they do floating.
* One verifier view, not two copies of a 1,400-line window.
* No change to how Inventor handles keys when focus is in Inventor itself.

## Decision

1. **Host:** the verifier content becomes one WPF view hosted either in a `Window` (pop-out) or in an `HwndSource`
   child window added to an Inventor `DockableWindow` (docked). In the spike, the docked panel followed floating,
   resizing and docking changes.
2. **Keyboard:** while focus is inside the verifier's window (the window itself or any child of it), a thread
   `WH_GETMESSAGE` hook passes key messages straight to WPF. It covers `WM_KEYDOWN`, `WM_KEYUP`, `WM_CHAR` and
   `WM_DEADCHAR`. The hook calls `ComponentDispatcher.RaiseThreadMessage`, then `TranslateMessage` and
   `DispatchMessage` itself, and replaces the original with `WM_NULL`, so Inventor's accelerators never see it.
   Alt combinations (`WM_SYS*`) stay with Inventor. In the spike this made typing, Backspace, arrows, Tab
   navigation, grid editing, Ctrl+C and F8 reach WPF. Shortcuts are then ordinary WPF key handling
   (`PreviewKeyDown`) in both hosts, and the narrower `ShortcutHook` is retired.

## Consequences

* With Ctrl+C now reaching WPF, the `DataGrid` built-in copy runs unless the view handles Ctrl+C itself. The view
  must keep copying part numbers, as `RunShortcut` does today.
* Inventor single-key shortcuts do not fire while the user types in the verifier. That is intended.
* Ctrl+V and Ctrl+Z were not captured in the spike's log. Confirm both once the real view is docked.
* The current window has a minimum width of 1080 px, which does not fit a side dock. Whether to build a narrow
  layout, or allow only bottom and floating docking at first, is still open.
