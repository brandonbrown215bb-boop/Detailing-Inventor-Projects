using System;
using InvApp = Inventor.Application;
using Inventor;
using VisTog.UI;

namespace VisTog.Commands
{
    public abstract class CommandBase : IDisposable
    {
        protected readonly InvApp App;
        private readonly ButtonDefinition _buttonDefinition;

        protected CommandBase(InvApp app, ButtonDefinition buttonDefinition)
        {
            App = app;
            _buttonDefinition = buttonDefinition;
            _buttonDefinition.OnExecute += OnExecute;
        }

        private void OnExecute(NameValueMap context)
        {
            try
            {
                Execute(context);
            }
            catch (Exception ex)
            {
                InventorUiHelper.ShowMessage(
                    App,
                    ex.Message,
                    "VisTog",
                    System.Windows.Forms.MessageBoxIcon.Error);
            }
        }

        protected abstract void Execute(NameValueMap context);

        protected bool TryGetActiveAssemblyDocument(out AssemblyDocument assemblyDocument)
        {
            assemblyDocument = App.ActiveDocument as AssemblyDocument;
            return assemblyDocument != null;
        }

        public virtual void Dispose()
        {
            _buttonDefinition.OnExecute -= OnExecute;
        }
    }
}
