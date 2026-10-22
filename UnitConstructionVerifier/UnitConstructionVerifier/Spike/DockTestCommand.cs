using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using UnitConstructionVerifier.UI;
using InvApp     = Inventor.Application;
using InvBtnDef  = Inventor.ButtonDefinition;
using InvBtnSink = Inventor.ButtonDefinitionSink_OnExecuteEventHandler;

namespace UnitConstructionVerifier.Spike
{
    /// <summary>
    /// Docking spike (dev only): a ribbon button that shows <see cref="DockTestPanel"/> in an Inventor dockable window,
    /// hosted through an <see cref="HwndSource"/> child window, with three keyboard modes to compare.
    /// </summary>
    internal sealed class DockTestCommand : IDisposable
    {
        private const string ClientId     = "{B1C2D3E4-F5A6-7890-BCDE-F01234567891}";
        private const string InternalName = "UCV_DockTest";
        private const string WindowName   = "UCV_DockTest_Window";

        private const int WsChild = 0x40000000, WsVisible = 0x10000000, WsClipChildren = 0x02000000, WsClipSiblings = 0x04000000;

        private readonly InvApp _app;
        private InvBtnDef? _buttonDef;
        private InvBtnSink? _onExecuteHandler;
        private Inventor.DockableWindow? _window;
        private HwndSource? _source;
        private DockTestPanel? _panel;
        private ShortcutHook? _shortcutHook;
        private KeyPassthroughHook? _passthroughHook;

        internal DockTestCommand(InvApp app) => _app = app;

        internal InvBtnDef CreateDefinition(InvApp app)
        {
            Inventor.ControlDefinitions defs = app.CommandManager.ControlDefinitions;
            try { _buttonDef = (InvBtnDef)(Inventor.ControlDefinition)defs[InternalName]; }
            catch
            {
                _buttonDef = (InvBtnDef)defs.AddButtonDefinition(
                    "Dock test (dev)", InternalName, Inventor.CommandTypesEnum.kNonShapeEditCmdType, ClientId,
                    "Docking spike: WPF panel in an Inventor dockable window", "Development test for a docked verifier. Not for detailers.",
                    Type.Missing, Type.Missing, Inventor.ButtonDisplayEnum.kAlwaysDisplayText);
            }

            _onExecuteHandler = new InvBtnSink(OnExecute);
            _buttonDef.OnExecute += _onExecuteHandler;
            return _buttonDef;
        }

        private void OnExecute(Inventor.NameValueMap context)
        {
            try
            {
                EnsureWindow();
                _window!.Visible = true;
                _panel!.Log("shown: " + StateReport());
            }
            catch (Exception ex)
            {
                DebugLogger.Log(ex, "DockTestCommand.OnExecute");
                MessageBox.Show("Dock test failed: " + ex.Message, "UCV dock test", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EnsureWindow()
        {
            if (_window != null && _source != null) return;

            Inventor.DockableWindows windows = _app.UserInterfaceManager.DockableWindows;
            try { _window = windows[WindowName]; }
            catch { _window = windows.Add(ClientId, WindowName, "UCV dock test"); }

            _window.ShowVisibilityCheckBox = true;
            _window.SetMinimumSize(250, 300);

            _panel = new DockTestPanel(StateReport);
            _panel.ModeChanged += SetKeyMode;

            var parameters = new HwndSourceParameters("UCVDockTest")
            {
                WindowStyle = WsChild | WsVisible | WsClipChildren | WsClipSiblings,
                ParentWindow = new IntPtr(_window.HWND),
                Width = Math.Max(_window.Width, 300),
                Height = Math.Max(_window.Height, 400),
            };
            _source = new HwndSource(parameters) { RootVisual = _panel, SizeToContent = SizeToContent.Manual };
            _window.AddChild(_source.Handle.ToInt32());

            ClearLogFile();
            _panel.Log($"created: host hwnd {_source.Handle.ToInt64():X}, dock hwnd {_window.HWND:X}");
        }

        private string StateReport()
        {
            if (_window == null) return "(no window)";
            return $"docking={_window.DockingState} visible={_window.Visible} dock={_window.Width}x{_window.Height} " +
                   $"panel={_panel?.ActualWidth:0}x{_panel?.ActualHeight:0} keyboardFocus={Keyboard.FocusedElement?.GetType().Name ?? "(none)"}";
        }

        private void SetKeyMode(DockKeyMode mode)
        {
            _shortcutHook?.Dispose();
            _shortcutHook = null;
            _passthroughHook?.Dispose();
            _passthroughHook = null;

            Func<IntPtr> owner = () => _source?.Handle ?? IntPtr.Zero;
            if (mode == DockKeyMode.Shortcuts)
            {
                _shortcutHook = new ShortcutHook(owner, shortcut =>
                    _panel!.Dispatcher.BeginInvoke(new Action(() => RunShortcut(shortcut))));
                _panel!.Log("shortcut hook installed: " + _shortcutHook.Install());
            }
            else if (mode == DockKeyMode.Passthrough)
            {
                _passthroughHook = new KeyPassthroughHook(owner);
                _panel!.Log("passthrough hook installed: " + _passthroughHook.Install());
            }
        }

        private void RunShortcut(VerifierShortcut shortcut)
        {
            _panel!.Log($"hook took {shortcut}");
            if (Keyboard.FocusedElement is IInputElement focused)
            {
                if (shortcut == VerifierShortcut.Copy) ApplicationCommands.Copy.Execute(null, focused);
                else if (shortcut == VerifierShortcut.Paste) ApplicationCommands.Paste.Execute(null, focused);
            }
        }

        private static void ClearLogFile()
        {
            try { System.IO.File.Delete(DockTestPanel.LogPath); } catch { }
        }

        public void Dispose()
        {
            _shortcutHook?.Dispose();
            _passthroughHook?.Dispose();
            _source?.Dispose();
            _source = null;
            if (_buttonDef != null && _onExecuteHandler != null) _buttonDef.OnExecute -= _onExecuteHandler;
        }
    }
}
