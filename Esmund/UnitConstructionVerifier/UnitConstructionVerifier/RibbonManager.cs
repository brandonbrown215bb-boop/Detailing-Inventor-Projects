using System;

// Fully qualify all Inventor types to avoid conflicts with System.Windows.Application
// that WPF's XAML compiler injects into the temporary build project.

namespace UnitConstructionVerifier
{
    /// <summary>
    /// Creates the "Unit Construction Verifier" ribbon panel and button in
    /// the Inventor ribbon UI.
    /// </summary>
    internal static class RibbonManager
    {
        private const string TabInternalName   = "UCV_Tab";
        private const string TabDisplayName    = "QA Tools";
        private const string PanelInternalName = "UCV_Panel";
        private const string PanelDisplayName  = "Construction";

        internal static void CreateUI(Inventor.Application app, VerifierCommand verifierCmd, Operations.ThicknessHoverCommand hoverCmd)
        {
            try
            {
                Inventor.Ribbons ribbons = app.UserInterfaceManager.Ribbons;

                // Setup Assembly ribbon (contains both commands)
                ConfigureRibbon(app, ribbons, "Assembly", verifierCmd, hoverCmd);

                // Setup Part ribbon (contains only Thickness Hover command)
                ConfigureRibbon(app, ribbons, "Part", null, hoverCmd);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UCV] RibbonManager.CreateUI: {ex.Message}");
            }
        }

        private static void ConfigureRibbon(
            Inventor.Application app, 
            Inventor.Ribbons ribbons, 
            string ribbonName, 
            VerifierCommand? verifierCmd, 
            Operations.ThicknessHoverCommand hoverCmd)
        {
            try
            {
                Inventor.Ribbon? ribbon = TryGetRibbon(ribbons, ribbonName);
                if (ribbon is null) return;

                Inventor.RibbonTab   tab   = FindOrCreateTab(ribbon, TabInternalName, TabDisplayName);
                Inventor.RibbonPanel panel = FindOrCreatePanel(tab, PanelInternalName, PanelDisplayName);

                if (verifierCmd != null)
                {
                    Inventor.ButtonDefinition verifierDef = verifierCmd.CreateDefinition(app);
                    try { panel.CommandControls.AddButton(verifierDef, true, true, string.Empty, false); }
                    catch { }
                }

                if (hoverCmd != null)
                {
                    Inventor.ButtonDefinition hoverDef = hoverCmd.CreateDefinition(app);
                    try { panel.CommandControls.AddButton(hoverDef, true, true, string.Empty, false); }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UCV] ConfigureRibbon ({ribbonName}): {ex.Message}");
            }
        }

        private static Inventor.Ribbon? TryGetRibbon(Inventor.Ribbons ribbons, string name)
        {
            try { return ribbons[name]; }
            catch { return null; }
        }

        private static Inventor.RibbonTab FindOrCreateTab(
            Inventor.Ribbon ribbon, string internalName, string displayName)
        {
            foreach (Inventor.RibbonTab t in ribbon.RibbonTabs)
                if (t.InternalName == internalName) return t;
            return ribbon.RibbonTabs.Add(displayName, internalName, Guid.NewGuid().ToString());
        }

        private static Inventor.RibbonPanel FindOrCreatePanel(
            Inventor.RibbonTab tab, string internalName, string displayName)
        {
            foreach (Inventor.RibbonPanel p in tab.RibbonPanels)
                if (p.InternalName == internalName) return p;
            return tab.RibbonPanels.Add(displayName, internalName, Guid.NewGuid().ToString());
        }
    }
}
