using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using InvApp = Inventor.Application;
using Inventor;
using Highlighter.Commands;
using Highlighter.Core;
using Highlighter.UI;

namespace Highlighter
{
    // Same ClientId as Cut Highlight successor so manifests upgrade cleanly.
    [Guid("C4E8A1F0-2B5D-4C8E-9A1F-6D3B5E7C8A90")]
    [ComVisible(true)]
    public class StandardAddInServer : ApplicationAddInServer
    {
        private const string ClientId = "{C4E8A1F0-2B5D-4C8E-9A1F-6D3B5E7C8A90}";
        private const string ButtonInternalName = "Highlighter_OpenPanel";
        private const string PanelInternalName = "id_PanelHighlighter";

        private InvApp _app;
        private ButtonDefinition _button;
        private HighlightController _controller;
        private OpenPanelCommand _command;

        public void Activate(ApplicationAddInSite addInSiteObject, bool firstTime)
        {
            _app = addInSiteObject.Application;

            try
            {
                // Remove prior Cut Highlight ribbon id if present.
                try
                {
                    ((ButtonDefinition)_app.CommandManager.ControlDefinitions["CutHighlight_HighlightCuts"]).Delete();
                }
                catch
                {
                }

                CreateButton();
                SetupUi();
                _controller = new HighlightController(_app);
                _command = new OpenPanelCommand(_app, _button, _controller);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Error activating Highlighter: " + ex.Message,
                    "Highlighter",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        public void Deactivate()
        {
            if (_command != null)
            {
                _command.Dispose();
                _command = null;
            }

            _controller = null;

            if (_button != null)
            {
                try { _button.Delete(); } catch { }
                _button = null;
            }

            _app = null;
        }

        public void ExecuteCommand(int commandID)
        {
        }

        public object Automation => null;

        private void CreateButton()
        {
            ControlDefinitions defs = _app.CommandManager.ControlDefinitions;
            try { ((ButtonDefinition)defs[ButtonInternalName]).Delete(); } catch { }

            _button = defs.AddButtonDefinition(
                "Highlighter",
                ButtonInternalName,
                CommandTypesEnum.kQueryOnlyCmdType,
                ClientId,
                "Open Highlighter panel — toggle skins, liners, floors by type and color",
                "Highlighter",
                RibbonIconHelper.CreateStandardIcon(),
                RibbonIconHelper.CreateLargeIcon(),
                ButtonDisplayEnum.kAlwaysDisplayText);
        }

        private void SetupUi()
        {
            TryAddToRibbon("Assembly", "id_TabTools", "id_TabModel");
            TryAddToRibbon("Part", "id_TabTools", "id_TabSheetMetal", "id_TabModel");
        }

        private void TryAddToRibbon(string ribbonName, params string[] tabCandidates)
        {
            try
            {
                Ribbon ribbon = _app.UserInterfaceManager.Ribbons[ribbonName];
                RibbonTab tab = null;
                foreach (string name in tabCandidates)
                {
                    try
                    {
                        tab = ribbon.RibbonTabs[name];
                        break;
                    }
                    catch
                    {
                    }
                }

                if (tab == null)
                {
                    tab = ribbon.RibbonTabs[1];
                }

                // Remove old Cut Highlight panel if present.
                try { tab.RibbonPanels["id_PanelCutHighlight_" + ribbonName].Delete(); } catch { }

                string panelName = PanelInternalName + "_" + ribbonName;
                try { tab.RibbonPanels[panelName].Delete(); } catch { }

                RibbonPanel panel = tab.RibbonPanels.Add("Highlighter", panelName, ClientId);
                panel.CommandControls.AddButton(_button, true);
            }
            catch
            {
            }
        }
    }
}
