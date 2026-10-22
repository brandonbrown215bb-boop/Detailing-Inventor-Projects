using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace UnitConstructionVerifier.UI
{
    /// <summary>
    /// Message boxes and owned windows for a view that may sit in its own <see cref="Window"/> (pop-out) or in an
    /// Inventor dockable window (no WPF <see cref="Window"/> at all). Docked, the owner is Inventor's main frame, so
    /// dialogs stay modal to Inventor and on top of it.
    /// </summary>
    internal static class Dialogs
    {
        private const uint GaRoot = 2;

        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);

        public static MessageBoxResult Show(DependencyObject view, string text, string caption = "",
            MessageBoxButton button = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.None)
        {
            if (Window.GetWindow(view) is Window window)
            {
                return MessageBox.Show(window, text, caption, button, icon);
            }

            // MessageBoxButton and MessageBoxImage use the Win32 MB_* values, and MessageBoxResult the ID* values.
            return (MessageBoxResult)MessageBoxW(OwnerHandle(view), text, caption, (uint)button | (uint)icon);
        }

        public static bool? ShowDialog(DependencyObject view, Microsoft.Win32.CommonDialog dialog) =>
            Window.GetWindow(view) is Window window ? dialog.ShowDialog(window) : dialog.ShowDialog();

        /// <summary>Makes <paramref name="owned"/> stay above the view's window (or Inventor, when docked).</summary>
        public static void SetOwner(DependencyObject view, Window owned)
        {
            if (Window.GetWindow(view) is Window window)
            {
                owned.Owner = window;
            }
            else
            {
                new WindowInteropHelper(owned).Owner = OwnerHandle(view);
            }
        }

        /// <summary>The top-level window the view is in: its own window, or Inventor's frame when docked.</summary>
        public static IntPtr OwnerHandle(DependencyObject view)
        {
            if (view is Visual visual && PresentationSource.FromVisual(visual) is HwndSource source)
            {
                return GetAncestor(source.Handle, GaRoot);
            }

            return IntPtr.Zero;
        }
    }
}
