using System;
using System.Windows.Forms;
using InvApp = Inventor.Application;
using Inventor;
using VisTog.Data;
using VisTog.UI;

namespace VisTog.Commands
{
    public sealed class OpenVisTogCommand : CommandBase
    {
        private static VisTogDockManager _dockManager;

        public OpenVisTogCommand(InvApp app, ButtonDefinition buttonDefinition)
            : base(app, buttonDefinition)
        {
        }

        protected override void Execute(NameValueMap context)
        {
            if (!TryGetActiveAssemblyDocument(out AssemblyDocument assemblyDocument))
            {
                InventorUiHelper.ShowMessage(
                    App,
                    "Open an assembly before using ISG Visibility.",
                    "ISG Visibility");
                return;
            }

            try
            {
                VisTogRulesCatalog.Load();
            }
            catch (Exception ex)
            {
                InventorUiHelper.ShowMessage(
                    App,
                    "ISG Visibility failed to load its rules file:" + System.Environment.NewLine + System.Environment.NewLine + ex.Message,
                    "ISG Visibility",
                    MessageBoxIcon.Error);
                return;
            }

            try
            {
                if (_dockManager == null)
                {
                    _dockManager = new VisTogDockManager(App);
                }

                _dockManager.Show(assemblyDocument);
            }
            catch (Exception ex)
            {
                InventorUiHelper.ShowMessage(
                    App,
                    "ISG Visibility could not open its panel:" + System.Environment.NewLine + System.Environment.NewLine + ex.Message,
                    "ISG Visibility",
                    MessageBoxIcon.Error);
            }
        }

        internal static void DisposeDockManager()
        {
            _dockManager?.Dispose();
            _dockManager = null;
        }
    }
}
