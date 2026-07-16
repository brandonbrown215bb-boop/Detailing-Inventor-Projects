using System;
using Inventor;

namespace Highlighter.Commands
{
    public abstract class CommandBase : IDisposable
    {
        protected readonly Application App;
        private readonly ButtonDefinition _buttonDefinition;

        protected CommandBase(Application app, ButtonDefinition buttonDefinition)
        {
            App = app;
            _buttonDefinition = buttonDefinition;
            _buttonDefinition.OnExecute += OnExecute;
        }

        private void OnExecute(NameValueMap context)
        {
            Execute(context);
        }

        protected abstract void Execute(NameValueMap context);

        public virtual void Dispose()
        {
            _buttonDefinition.OnExecute -= OnExecute;
        }
    }
}
