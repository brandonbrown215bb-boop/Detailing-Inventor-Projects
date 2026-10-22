using System;
using System.Windows;
using System.Windows.Interop;

namespace UnitConstructionVerifier.UI
{
    /// <summary>
    /// Docked host: the verifier view inside an Inventor dockable window, through an <see cref="HwndSource"/> child
    /// window (ADR 0003). Inventor sizes the child to the dock and remembers where the user docked it. Closing the dock
    /// with its own ✕ only hides it; the view and its state stay until the presenter closes it.
    /// </summary>
    internal sealed class VerifierDockHost : IDisposable
    {
        private const string ClientId   = "{B1C2D3E4-F5A6-7890-BCDE-F01234567891}";
        private const string WindowName = "UCV_Verifier_Dock";

        private const int WsChild = 0x40000000, WsVisible = 0x10000000, WsClipChildren = 0x02000000, WsClipSiblings = 0x04000000;

        private readonly Inventor.Application _app;
        private Inventor.DockableWindow? _window;
        private HwndSource? _source;
        private KeyPassthroughHook? _keys;

        public VerifierDockHost(Inventor.Application app) => _app = app;

        public void Show(VerifierView view)
        {
            EnsureWindow();
            if (!ReferenceEquals(_source!.RootVisual, view)) _source.RootVisual = view;
            _window!.Visible = true;
        }

        /// <summary>Hides the dock and lets go of the view, so it can move to its own window or be closed.</summary>
        public void Detach()
        {
            if (_source != null) _source.RootVisual = null;
            if (_window != null) _window.Visible = false;
        }

        private void EnsureWindow()
        {
            if (_window != null && _source != null) return;

            Inventor.DockableWindows windows = _app.UserInterfaceManager.DockableWindows;
            bool created = false;
            try { _window = windows[WindowName]; }
            catch
            {
                _window = windows.Add(ClientId, WindowName, "Unit Construction Verifier");
                created = true;
            }

            // Empty without a unit loaded, so it is not offered in Inventor's list of windows to show.
            _window.ShowVisibilityCheckBox = false;
            _window.SetMinimumSize(420, 360);
            if (created) _window.DockingState = Inventor.DockingStateEnum.kDockRight;

            var parameters = new HwndSourceParameters("UCVVerifierDock")
            {
                WindowStyle = WsChild | WsVisible | WsClipChildren | WsClipSiblings,
                ParentWindow = new IntPtr(_window.HWND),
                Width = Math.Max(_window.Width, 360),
                Height = Math.Max(_window.Height, 420),
            };
            _source = new HwndSource(parameters) { SizeToContent = SizeToContent.Manual };
            _window.AddChild(_source.Handle.ToInt32());

            // Inventor treats keys aimed at a docked child as its own shortcuts; give them to WPF.
            IntPtr handle = _source.Handle;
            _keys = new KeyPassthroughHook(() => handle);
            _keys.Install();
        }

        public void Dispose()
        {
            _keys?.Dispose();
            _keys = null;
            if (_source != null)
            {
                _source.RootVisual = null;
                _source.Dispose();
                _source = null;
            }
        }
    }
}
