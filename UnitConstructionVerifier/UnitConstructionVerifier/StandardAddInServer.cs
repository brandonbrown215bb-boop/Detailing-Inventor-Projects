using System;
using System.Runtime.InteropServices;
using Inventor;

namespace UnitConstructionVerifier
{
    /// <summary>
    /// Inventor Add-In entry point. Inventor instantiates this class via COM
    /// when the add-in loads. Registers the ribbon button and the command.
    /// </summary>
    [Guid("B1C2D3E4-F5A6-7890-BCDE-F01234567891")]
    [ClassInterface(ClassInterfaceType.AutoDual)]
    public class StandardAddInServer : ApplicationAddInServer
    {
        private Application? _inventorApp;
        private VerifierCommand? _verifierCommand;
        private Operations.ThicknessHoverCommand? _hoverCommand;

        // ── ApplicationAddInServer ────────────────────────────────────────────

        public void Activate(ApplicationAddInSite addInSiteObject, bool firstTime)
        {
            DebugLogger.CleanUpOldLogs();
            _inventorApp = addInSiteObject.Application;
            _verifierCommand = new VerifierCommand(_inventorApp);
            _hoverCommand = new Operations.ThicknessHoverCommand(_inventorApp);

            RibbonManager.CreateUI(_inventorApp, _verifierCommand, _hoverCommand);
        }

        public void Deactivate()
        {
            _verifierCommand?.Dispose();
            _verifierCommand = null;

            _hoverCommand?.Dispose();
            _hoverCommand = null;

            _inventorApp = null;
        }

        public void ExecuteCommand(int commandID) { }

        public object Automation => null!;
    }
}
