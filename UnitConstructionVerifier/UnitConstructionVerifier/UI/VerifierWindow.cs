using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace UnitConstructionVerifier.UI
{
    /// <summary>Pop-out host: the verifier view in its own top-level window.</summary>
    public sealed class VerifierWindow : Window
    {
        private KeyPassthroughHook? _keys;
        private bool _detaching;

        public VerifierWindow(VerifierView view)
        {
            View = view;
            Title = "Unit Construction Verifier";
            Width = 1180;
            Height = 800;
            MinWidth = 420;
            MinHeight = 480;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = view.TryFindResource("BgPrimary") as Brush;
            Content = view;

            Activated += (_, __) => view.NoteHostActivated();
            Closing += (_, e) =>
            {
                if (!_detaching && !view.ConfirmClose()) e.Cancel = true;
            };

            // Inventor's message loop takes shortcut keys before WPF sees them; hand ours straight to WPF (ADR 0003).
            SourceInitialized += (_, __) =>
            {
                IntPtr handle = new WindowInteropHelper(this).Handle;
                _keys = new KeyPassthroughHook(() => handle);
                _keys.Install();
            };
            Closed += (_, __) =>
            {
                _keys?.Dispose();
                if (!_detaching) view.Shutdown();
            };
        }

        public VerifierView View { get; }

        /// <summary>True when the window closed only to hand its view to the dock (or to a close the presenter ran).</summary>
        public bool IsDetached => _detaching;

        /// <summary>Closes the window without closing the view, so the view can move to the dock.</summary>
        public VerifierView Detach()
        {
            _detaching = true;
            Content = null;
            Close();
            return View;
        }
    }
}
