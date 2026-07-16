using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using InvApp = Inventor.Application;
using Inventor;
using SkinChannelPunch.Commands;
using SkinChannelPunch.UI;

namespace SkinChannelPunch
{
    [Guid("A7C4E2B1-9F3D-4A6E-8C1D-2E5F6A8B9C0D")]
    [ComVisible(true)]
    public class StandardAddInServer : ApplicationAddInServer
    {
        private const string ClientId = "{A7C4E2B1-9F3D-4A6E-8C1D-2E5F6A8B9C0D}";
        private const string ButtonInternalName = "SkinChannelPunch_Punch";
        private const string PanelInternalName = "id_PanelSkinChannelPunch";
        private const string HighlightButtonInternalName = "SkinChannelPunch_HighlightCuts";

        private InvApp _app;
        private ButtonDefinition _punchButton;
        private PunchSkinHolesCommand _punchCommand;

        public void Activate(ApplicationAddInSite addInSiteObject, bool firstTime)
        {
            _app = addInSiteObject.Application;

            try
            {
                // Remove Highlight Cuts if a prior Hole Punch build registered it here.
                try
                {
                    ((ButtonDefinition)_app.CommandManager.ControlDefinitions[HighlightButtonInternalName]).Delete();
                }
                catch
                {
                }

                CreateButton();
                SetupUi();
                _punchCommand = new PunchSkinHolesCommand(_app, _punchButton);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Error activating Hole Punch: " + ex.Message,
                    "Hole Punch",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        public void Deactivate()
        {
            if (_punchCommand != null)
            {
                _punchCommand.Dispose();
                _punchCommand = null;
            }

            if (_punchButton != null)
            {
                try
                {
                    _punchButton.Delete();
                }
                catch
                {
                }

                _punchButton = null;
            }

            _app = null;
        }

        public void ExecuteCommand(int commandID)
        {
        }

        public object Automation => null;

        private void CreateButton()
        {
            ControlDefinitions controlDefinitions = _app.CommandManager.ControlDefinitions;

            try
            {
                ((ButtonDefinition)controlDefinitions[ButtonInternalName]).Delete();
            }
            catch
            {
            }

            _punchButton = controlDefinitions.AddButtonDefinition(
                "Hole\nPunch",
                ButtonInternalName,
                CommandTypesEnum.kQueryOnlyCmdType,
                ClientId,
                "Punch vertical skin holes anchored to a channel",
                "Punch skin holes from channel",
                RibbonIconHelper.CreateStandardIcon(),
                RibbonIconHelper.CreateLargeIcon(),
                ButtonDisplayEnum.kAlwaysDisplayText);
        }

        private void SetupUi()
        {
            try
            {
                Ribbon ribbon = _app.UserInterfaceManager.Ribbons["Assembly"];
                TryRemoveLegacyCe3Panel(ribbon);

                RibbonTab toolsTab = TryGetRibbonTab(ribbon, "id_TabTools")
                    ?? TryGetRibbonTab(ribbon, "id_TabModel")
                    ?? ribbon.RibbonTabs[1];

                try
                {
                    toolsTab.RibbonPanels[PanelInternalName].Delete();
                }
                catch
                {
                }

                // Remove Part-ribbon panel left by v1.8.4 Highlight Cuts
                try
                {
                    Ribbon partRibbon = _app.UserInterfaceManager.Ribbons["Part"];
                    RibbonTab partTools = TryGetRibbonTab(partRibbon, "id_TabTools")
                        ?? TryGetRibbonTab(partRibbon, "id_TabSheetMetal")
                        ?? TryGetRibbonTab(partRibbon, "id_TabModel");
                    if (partTools != null)
                    {
                        try { partTools.RibbonPanels[PanelInternalName + "_Part"].Delete(); } catch { }
                    }
                }
                catch
                {
                }

                RibbonPanel panel = toolsTab.RibbonPanels.Add("Hole Punch", PanelInternalName, ClientId);
                panel.CommandControls.AddButton(_punchButton, true);
            }
            catch
            {
            }
        }

        private static void TryRemoveLegacyCe3Panel(Ribbon ribbon)
        {
            try
            {
                RibbonTab ce3Tab = ribbon.RibbonTabs[Ce3RibbonHelper.TabInternalName];
                try
                {
                    RibbonPanel legacy = ce3Tab.RibbonPanels[PanelInternalName];
                    legacy.Delete();
                }
                catch
                {
                }
            }
            catch
            {
            }
        }

        private static RibbonTab TryGetRibbonTab(Ribbon ribbon, string internalName)
        {
            try
            {
                return ribbon.RibbonTabs[internalName];
            }
            catch
            {
                return null;
            }
        }
    }
}
