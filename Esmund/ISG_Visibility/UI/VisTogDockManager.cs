using System;
using System.Windows.Forms;
using InvApp = Inventor.Application;
using Inventor;

namespace VisTog.UI
{
    internal sealed class VisTogHostForm : Form
    {
        private readonly IntPtr _parentHwnd;

        public VisTogHostForm(IntPtr parentHwnd)
        {
            _parentHwnd = parentHwnd;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopLevel = false;
            ControlBox = false;
            MaximizeBox = false;
            MinimizeBox = false;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams createParams = base.CreateParams;
                if (_parentHwnd != IntPtr.Zero)
                {
                    createParams.Parent = _parentHwnd;
                }

                return createParams;
            }
        }
    }

    internal sealed class VisTogDockManager : IDisposable
    {
        private const string ClientId = "{D2F8C4A1-6B3E-4F9D-A871-5E4C2B9D0F3A}";
        private const string InternalName = "ISG_VisibilityPanel";
        private const string DisplayTitle = "ISG Visibility";

        private readonly InvApp _app;
        private DockableWindow _dockWindow;
        private VisTogHostForm _hostForm;
        private VisTogPanel _panel;
        private bool _childAttached;
        private bool _eventsHooked;

        public VisTogDockManager(InvApp app)
        {
            _app = app ?? throw new ArgumentNullException(nameof(app));
        }

        public void Show(AssemblyDocument assemblyDocument)
        {
            EnsureDockWindow();
            EnsureChildControls();
            EnsureDocumentEvents();

            _panel.RefreshForActiveAssembly();

            // Always apply default width so a prior customized wide dock doesn't stick.
            _dockWindow.Width = VisTogPanel.DefaultDockWidth;
            if (!_dockWindow.IsCustomized)
            {
                _dockWindow.DockingState = DockingStateEnum.kDockRight;
            }

            _dockWindow.Visible = true;
        }

        public void Dispose()
        {
            UnhookDocumentEvents();

            if (_dockWindow != null)
            {
                try
                {
                    _dockWindow.Visible = false;
                    if (_childAttached)
                    {
                        _dockWindow.Clear();
                    }

                    _dockWindow.Delete();
                }
                catch
                {
                }

                _dockWindow = null;
            }

            if (_hostForm != null)
            {
                _hostForm.Close();
                _hostForm.Dispose();
                _hostForm = null;
            }

            _panel?.Dispose();
            _panel = null;
            _childAttached = false;
        }

        private void EnsureDocumentEvents()
        {
            if (_eventsHooked)
            {
                return;
            }

            try
            {
                _app.ApplicationEvents.OnActivateDocument += OnActivateDocument;
                _eventsHooked = true;
            }
            catch
            {
            }
        }

        private void UnhookDocumentEvents()
        {
            if (!_eventsHooked)
            {
                return;
            }

            try
            {
                _app.ApplicationEvents.OnActivateDocument -= OnActivateDocument;
            }
            catch
            {
            }

            _eventsHooked = false;
        }

        private void OnActivateDocument(
            _Document documentObject,
            EventTimingEnum beforeOrAfter,
            NameValueMap context,
            out HandlingCodeEnum handlingCode)
        {
            handlingCode = HandlingCodeEnum.kEventNotHandled;
            if (beforeOrAfter != EventTimingEnum.kAfter)
            {
                return;
            }

            try
            {
                _panel?.RefreshForActiveAssembly();
            }
            catch
            {
            }
        }

        private void EnsureDockWindow()
        {
            if (_dockWindow != null)
            {
                return;
            }

            UserInterfaceManager uiManager = _app.UserInterfaceManager;
            try
            {
                _dockWindow = uiManager.DockableWindows[InternalName];
            }
            catch
            {
                _dockWindow = uiManager.DockableWindows.Add(ClientId, InternalName, DisplayTitle);
            }

            _dockWindow.ShowTitleBar = false;
            _dockWindow.ShowVisibilityCheckBox = true;
            _dockWindow.DisabledDockingStates = 0;
            _dockWindow.SetMinimumSize(110, 100);
        }

        private void EnsureChildControls()
        {
            if (_childAttached)
            {
                return;
            }

            IntPtr dockHwnd = new IntPtr(_dockWindow.HWND);
            _hostForm = new VisTogHostForm(dockHwnd)
            {
                Dock = DockStyle.Fill
            };

            _panel = new VisTogPanel(_app)
            {
                Dock = DockStyle.Fill
            };
            _hostForm.Controls.Add(_panel);

            _hostForm.CreateControl();
            _hostForm.Show();

            _dockWindow.AddChild(_hostForm.Handle.ToInt32());
            _childAttached = true;
        }
    }
}
