using System;
using System.Runtime.InteropServices;
using InvApp = Inventor.Application;
using Inventor;
using VisTog.Commands;
using VisTog.UI;

namespace VisTog
{
    [Guid("D2F8C4A1-6B3E-4F9D-A871-5E4C2B9D0F3A")]
    [ComVisible(true)]
    public class StandardAddInServer : ApplicationAddInServer
    {
        private const string ClientId = "{D2F8C4A1-6B3E-4F9D-A871-5E4C2B9D0F3A}";
        private const string ButtonInternalName = "ISG_Visibility_OpenPanel";
        private const string LegacyButtonInternalName = "VisTog_OpenPanel";
        private const string PanelInternalName = "id_PanelISGVisibility";
        private const string DisplayName = "ISG Visibility";

        private InvApp _app;
        private ButtonDefinition _openButton;
        private OpenVisTogCommand _openCommand;

        static StandardAddInServer()
        {
            StartupLog.Write("ISG Visibility assembly loaded");
        }

        public void Activate(ApplicationAddInSite addInSiteObject, bool firstTime)
        {
            _app = addInSiteObject.Application;
            StartupLog.Write("Activate begin firstTime=" + firstTime);

            try
            {
                PersistLoadAutomatically(addInSiteObject);

                if (firstTime)
                {
                    CreateButton();
                    SetupUi();
                }
                else
                {
                    EnsureButton();
                    EnsureUi();
                }

                _openCommand = new OpenVisTogCommand(_app, _openButton);
                StartupLog.Write("Activate complete");
            }
            catch (Exception ex)
            {
                StartupLog.Write("Activate failed: " + ex);
            }
        }

        public void Deactivate()
        {
            StartupLog.Write("Deactivate begin");

            OpenVisTogCommand.DisposeDockManager();

            if (_openCommand != null)
            {
                _openCommand.Dispose();
                _openCommand = null;
            }

            _openButton = null;
            _app = null;
            StartupLog.Write("Deactivate complete");
        }

        public void ExecuteCommand(int commandID)
        {
        }

        public object Automation => null;

        private void PersistLoadAutomatically(ApplicationAddInSite addInSiteObject)
        {
            try
            {
                var addIn = addInSiteObject.Parent as ApplicationAddIn;
                if (addIn != null)
                {
                    addIn.LoadAutomatically = true;
                    StartupLog.Write("LoadAutomatically=true");
                }
            }
            catch (Exception ex)
            {
                StartupLog.Write("LoadAutomatically failed: " + ex.Message);
            }
        }

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

            try
            {
                ((ButtonDefinition)controlDefinitions[LegacyButtonInternalName]).Delete();
            }
            catch
            {
            }

            _openButton = controlDefinitions.AddButtonDefinition(
                DisplayName,
                ButtonInternalName,
                CommandTypesEnum.kQueryOnlyCmdType,
                ClientId,
                "Toggle assembly visibility by IPT stock number",
                "Open ISG Visibility panel",
                RibbonIconHelper.CreateStandardIcon(),
                RibbonIconHelper.CreateLargeIcon(),
                ButtonDisplayEnum.kAlwaysDisplayText);
        }

        private void EnsureButton()
        {
            ControlDefinitions controlDefinitions = _app.CommandManager.ControlDefinitions;

            try
            {
                _openButton = (ButtonDefinition)controlDefinitions[ButtonInternalName];
                StartupLog.Write("Reused existing button definition");
            }
            catch
            {
                StartupLog.Write("Creating button definition on non-first load");
                CreateButton();
            }
        }

        private void SetupUi()
        {
            TryAddPanelToRibbon("Assembly");
        }

        private void EnsureUi()
        {
            if (!TryAddPanelToRibbon("Assembly"))
            {
                StartupLog.Write("EnsureUi could not attach ribbon button");
            }
        }

        private bool TryAddPanelToRibbon(string ribbonName)
        {
            try
            {
                Ribbon ribbon = _app.UserInterfaceManager.Ribbons[ribbonName];
                RibbonTab tab = TryGetRibbonTab(ribbon, "id_TabTools")
                    ?? TryGetRibbonTab(ribbon, "id_TabModel")
                    ?? TryGetRibbonTab(ribbon, "id_GetStarted")
                    ?? ribbon.RibbonTabs[1];

                RibbonPanel panel;
                try
                {
                    panel = tab.RibbonPanels[PanelInternalName];
                }
                catch
                {
                    panel = tab.RibbonPanels.Add("ISG Visibility", PanelInternalName, ClientId);
                }

                if (!PanelHasButton(panel, ButtonInternalName))
                {
                    panel.CommandControls.AddButton(_openButton, true);
                }

                return true;
            }
            catch (Exception ex)
            {
                StartupLog.Write("TryAddPanelToRibbon failed: " + ex.Message);
                return false;
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

        private static bool PanelHasButton(RibbonPanel panel, string internalName)
        {
            CommandControls controls = panel.CommandControls;
            for (int i = 1; i <= controls.Count; i++)
            {
                try
                {
                    CommandControl control = controls[i];
                    if (control.ControlDefinition != null &&
                        string.Equals(control.ControlDefinition.InternalName, internalName, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                catch
                {
                }
            }

            return false;
        }
    }
}
