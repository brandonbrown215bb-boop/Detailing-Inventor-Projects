using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace UnitConstructionVerifier.Operations
{
    /// <summary>
    /// Restores a WPF window above Inventor after COM selection calls, without permanent Topmost.
    /// </summary>
    internal static class InventorWindowStackHelper
    {
        private static readonly IntPtr HwndTop = IntPtr.Zero;
        private const uint SwpNomove = 0x0002;
        private const uint SwpNosize = 0x0001;
        private const uint SwpNoactivate = 0x0010;
        private const uint SwpShowwindow = 0x0040;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int x,
            int y,
            int cx,
            int cy,
            uint uFlags);

        public static void Capture(
            Window window,
            ref bool keepAboveInventor,
            ref bool pendingStackRestore,
            ref bool pendingFocusRestore)
        {
            if (!window.IsVisible)
            {
                return;
            }

            if (window.IsActive)
            {
                pendingStackRestore = true;
                pendingFocusRestore = true;
            }
            else if (keepAboveInventor)
            {
                pendingStackRestore = true;
            }
        }

        public static bool TakePending(
            ref bool pendingStackRestore,
            ref bool pendingFocusRestore,
            out bool restoreFocus)
        {
            restoreFocus = pendingFocusRestore;
            bool pending = pendingStackRestore;
            pendingStackRestore = false;
            pendingFocusRestore = false;
            return pending;
        }

        public static void RestoreAboveInventor(Window window, bool restoreFocus)
        {
            if (!window.IsVisible)
            {
                return;
            }

            IntPtr handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero)
            {
                return;
            }

            window.Topmost = true;
            if (restoreFocus)
            {
                window.Activate();
            }
            else
            {
                SetWindowPos(
                    handle,
                    HwndTop,
                    0,
                    0,
                    0,
                    0,
                    SwpNomove | SwpNosize | SwpNoactivate | SwpShowwindow);
            }

            window.Topmost = false;
        }

        public static void ScheduleRestore(Window window, bool restoreFocus)
        {
            void TryRestore() => RestoreAboveInventor(window, restoreFocus);

            TryRestore();
            window.Dispatcher.BeginInvoke((Action)TryRestore, DispatcherPriority.ApplicationIdle);
            window.Dispatcher.BeginInvoke((Action)TryRestore, DispatcherPriority.ContextIdle);

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(75) };
            int attempts = 0;
            timer.Tick += (_, __) =>
            {
                attempts++;
                TryRestore();
                if (attempts >= 8)
                {
                    timer.Stop();
                }
            };
            timer.Start();
        }
    }
}
