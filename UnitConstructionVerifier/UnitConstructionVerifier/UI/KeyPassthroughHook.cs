using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;
namespace UnitConstructionVerifier.UI
{
    /// <summary>
    /// Gives every key message aimed at the verifier to WPF directly, before Inventor's message loop can treat it as
    /// an Inventor shortcut (ADR 0003). A thread WH_GETMESSAGE hook sees the message as GetMessage returns it; for keys
    /// whose target is inside the owner window it lets WPF preprocess it, translates and dispatches it itself, then turns
    /// the original into WM_NULL so Inventor's accelerator handling gets nothing. Works for the pop-out window and for
    /// the docked view, whose window is a child of Inventor's frame.
    /// </summary>
    public sealed class KeyPassthroughHook : IDisposable
    {
        private const int WhGetMessage = 3;
        private const int PmRemove = 1;
        private const int WmNull = 0x0000;
        private const int WmKeyFirst = 0x0100; // WM_KEYDOWN
        private const int WmDeadChar = 0x0103; // WM_KEYDOWN, WM_KEYUP, WM_CHAR, WM_DEADCHAR; Alt (WM_SYS*) keys stay Inventor's

        private delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll")]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool TranslateMessage(ref MSG lpMsg);

        [DllImport("user32.dll")]
        private static extern IntPtr DispatchMessage(ref MSG lpmsg);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        private static extern bool IsChild(IntPtr hWndParent, IntPtr hWnd);

        private readonly Func<IntPtr> _ownerHandle;
        private readonly Action<int, IntPtr>? _onKey;
        private readonly HookProc _proc;
        private IntPtr _hook;

        /// <param name="onKey">Optional: message id and wParam (virtual key or character) of each key passed through.</param>
        public KeyPassthroughHook(Func<IntPtr> ownerHandle, Action<int, IntPtr>? onKey = null)
        {
            _ownerHandle = ownerHandle;
            _onKey = onKey;
            _proc = HookCallback;
        }

        public bool Install()
        {
            if (_hook != IntPtr.Zero) return true;
            _hook = SetWindowsHookEx(WhGetMessage, _proc, IntPtr.Zero, GetCurrentThreadId());
            return _hook != IntPtr.Zero;
        }

        public void Dispose()
        {
            if (_hook == IntPtr.Zero) return;
            UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }

        /// <summary>True when <paramref name="hwnd"/> is <paramref name="owner"/> or one of its child windows.</summary>
        internal static bool IsInside(IntPtr owner, IntPtr hwnd) =>
            owner != IntPtr.Zero && hwnd != IntPtr.Zero && (hwnd == owner || IsChild(owner, hwnd));

        private IntPtr HookCallback(int code, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (code >= 0 && wParam.ToInt32() == PmRemove)
                {
                    MSG msg = Marshal.PtrToStructure<MSG>(lParam);
                    if (msg.message >= WmKeyFirst && msg.message <= WmDeadChar && IsInside(_ownerHandle(), msg.hwnd))
                    {
                        _onKey?.Invoke(msg.message, msg.wParam);

                        // What WPF's own message loop would do: let WPF preprocess (access keys, tabbing out of the
                        // HwndSource), and only translate and dispatch what it did not handle.
                        MSG copy = msg;
                        if (!ComponentDispatcher.RaiseThreadMessage(ref copy))
                        {
                            TranslateMessage(ref copy);
                            DispatchMessage(ref copy);
                        }

                        msg.message = WmNull;
                        Marshal.StructureToPtr(msg, lParam, false);
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log(ex, "KeyPassthroughHook");
            }

            return CallNextHookEx(_hook, code, wParam, lParam);
        }
    }
}
