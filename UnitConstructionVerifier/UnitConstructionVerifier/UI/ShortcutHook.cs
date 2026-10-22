using System;
using System.Runtime.InteropServices;

namespace UnitConstructionVerifier.UI
{
    /// <summary>Verifier shortcuts that Inventor would otherwise take before WPF sees the key.</summary>
    public enum VerifierShortcut { None, Copy, Paste, NextIssue }

    /// <summary>
    /// Inventor handles its own shortcuts (Ctrl+C, Ctrl+V, function keys) in its message loop, before the key is
    /// dispatched to our window, so <c>PreviewKeyDown</c> never fires for them. A thread keyboard hook runs earlier:
    /// while the verifier has focus it removes those keys from the queue and hands them to the window instead.
    /// </summary>
    public sealed class ShortcutHook : IDisposable
    {
        private const int WhKeyboard = 2;
        private const int HcAction = 0;
        private const int VkControl = 0x11;
        private const int VkShift = 0x10;
        private const int VkMenu = 0x12;
        private const uint GaRoot = 2;

        private delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll")]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);

        [DllImport("user32.dll")]
        private static extern IntPtr GetFocus();

        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        private readonly Func<IntPtr> _ownerHandle;
        private readonly Action<VerifierShortcut> _onShortcut;
        private readonly HookProc _proc; // kept in a field so the GC cannot collect the delegate Windows calls
        private IntPtr _hook;

        /// <param name="ownerHandle">The verifier's top-level window handle; keys count only while focus is inside it.</param>
        /// <param name="onShortcut">Called once per key press from inside the hook, on the UI thread. Post the
        /// real work (Dispatcher.BeginInvoke) rather than doing it while Windows is still in the hook chain.</param>
        public ShortcutHook(Func<IntPtr> ownerHandle, Action<VerifierShortcut> onShortcut)
        {
            _ownerHandle = ownerHandle;
            _onShortcut = onShortcut;
            _proc = HookCallback;
        }

        /// <summary>Installs the hook on the calling (UI) thread. Returns false if Windows refused it.</summary>
        public bool Install()
        {
            if (_hook != IntPtr.Zero) return true;
            _hook = SetWindowsHookEx(WhKeyboard, _proc, IntPtr.Zero, GetCurrentThreadId());
            if (_hook == IntPtr.Zero)
            {
                DebugLogger.Log($"ShortcutHook: SetWindowsHookEx failed ({Marshal.GetLastWin32Error()})");
                return false;
            }

            DebugLogger.Log("ShortcutHook: installed");
            return true;
        }

        public void Dispose()
        {
            if (_hook == IntPtr.Zero) return;
            UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }

        /// <summary>Maps a key to a verifier shortcut. Key-ups and auto-repeats map to the same shortcut so the
        /// whole press is removed, but only the first key-down should run it (see <paramref name="runs"/>).</summary>
        public static VerifierShortcut Classify(int virtualKey, bool ctrl, bool shift, bool alt, bool keyUp, bool repeat,
            out bool runs)
        {
            VerifierShortcut shortcut = VerifierShortcut.None;
            if (!alt && !shift)
            {
                if (ctrl && virtualKey == 'C') shortcut = VerifierShortcut.Copy;
                else if (ctrl && virtualKey == 'V') shortcut = VerifierShortcut.Paste;
                else if (!ctrl && virtualKey == 0x77 /* F8 */) shortcut = VerifierShortcut.NextIssue;
            }

            runs = shortcut != VerifierShortcut.None && !keyUp && !repeat;
            return shortcut;
        }

        private IntPtr HookCallback(int code, IntPtr wParam, IntPtr lParam)
        {
            // An exception escaping a hook callback would take Inventor down with it.
            try
            {
                if (code == HcAction && FocusIsInOwner())
                {
                    long flags = lParam.ToInt64();
                    bool keyUp = (flags & 0x80000000L) != 0;
                    bool repeat = (flags & 0x40000000L) != 0;
                    VerifierShortcut shortcut = Classify(wParam.ToInt32(), IsDown(VkControl), IsDown(VkShift),
                        IsDown(VkMenu), keyUp, repeat, out bool runs);

                    if (shortcut != VerifierShortcut.None)
                    {
                        if (runs) _onShortcut(shortcut);
                        return (IntPtr)1; // non-zero: the key is discarded, so Inventor never sees it
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log(ex, "ShortcutHook");
            }

            return CallNextHookEx(_hook, code, wParam, lParam);
        }

        private bool FocusIsInOwner()
        {
            IntPtr owner = _ownerHandle();
            IntPtr focus = GetFocus();
            return owner != IntPtr.Zero && focus != IntPtr.Zero && GetAncestor(focus, GaRoot) == owner;
        }

        private static bool IsDown(int virtualKey) => (GetKeyState(virtualKey) & 0x8000) != 0;
    }
}
