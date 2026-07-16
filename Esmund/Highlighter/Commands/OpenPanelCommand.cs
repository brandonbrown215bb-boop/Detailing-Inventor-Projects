using System.Windows.Forms;
using InvApp = Inventor.Application;
using Inventor;
using Highlighter.Core;
using Highlighter.UI;

namespace Highlighter.Commands
{
    /// <summary>
    /// Opens the Highlighter type/color panel (modeless).
    /// </summary>
    public sealed class OpenPanelCommand : CommandBase
    {
        private readonly HighlightController _controller;
        private HighlighterPanelForm _panel;

        public OpenPanelCommand(InvApp app, ButtonDefinition buttonDefinition, HighlightController controller)
            : base(app, buttonDefinition)
        {
            _controller = controller;
        }

        protected override void Execute(NameValueMap context)
        {
            if (_panel == null || _panel.IsDisposed)
            {
                _panel = new HighlighterPanelForm(App, _controller);
                _panel.PlaceNearInventor();
            }

            if (!_panel.Visible)
            {
                _panel.Show();
            }

            try
            {
                _panel.RefreshButtonStates();
                _panel.BringToFront();
                _panel.Activate();
            }
            catch
            {
            }
        }

        public override void Dispose()
        {
            try
            {
                if (_panel != null && !_panel.IsDisposed)
                {
                    _panel.ForceClose();
                    _panel.Dispose();
                }
            }
            catch
            {
            }

            _panel = null;
            _controller?.Dispose();
            base.Dispose();
        }
    }
}
