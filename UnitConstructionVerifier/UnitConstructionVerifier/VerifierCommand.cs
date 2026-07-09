using System;
using System.Drawing;
using System.Windows;
using UnitConstructionVerifier.Extraction;
using UnitConstructionVerifier.Models;
using UnitConstructionVerifier.Persistence;
using UnitConstructionVerifier.UI;
using InvApp      = Inventor.Application;
using InvAssyDoc  = Inventor.AssemblyDocument;
using InvNVM      = Inventor.NameValueMap;
using InvBtnDef   = Inventor.ButtonDefinition;
using InvBtnSink  = Inventor.ButtonDefinitionSink_OnExecuteEventHandler;
using InvCtrlDef  = Inventor.ControlDefinition;

namespace UnitConstructionVerifier
{
    /// <summary>
    /// Handles the ribbon button click. Extracts all construction data from
    /// the active Inventor document, then launches the WPF dialog.
    /// </summary>
    internal sealed class VerifierCommand : IDisposable
    {
        private const string CommandClientId     = "{B1C2D3E4-F5A6-7890-BCDE-F01234567891}";
        private const string CommandInternalName = "UCV_VerifyConstruction";
        private const string CommandDisplayName  = "Verify & Edit Specs";

        private readonly InvApp _app;
        private InvBtnDef?      _buttonDef;
        private InvBtnSink?     _onExecuteHandler;

        internal VerifierCommand(InvApp app) => _app = app;

        // ── Called by RibbonManager ───────────────────────────────────────────

        internal InvBtnDef CreateDefinition(InvApp app)
        {
            Inventor.ControlDefinitions defs = app.CommandManager.ControlDefinitions;

            // Avoid duplicate registration across sessions
            try { return (InvBtnDef)(Inventor.ControlDefinition)defs[CommandInternalName]; }
            catch { /* not yet created */ }

            stdole.IPictureDisp smallIcon = PictureDispConverter.ToIPictureDisp(CreateIcon(16));
            stdole.IPictureDisp largeIcon = PictureDispConverter.ToIPictureDisp(CreateIcon(32));

            _buttonDef = (InvBtnDef)defs.AddButtonDefinition(
                CommandDisplayName,
                CommandInternalName,
                Inventor.CommandTypesEnum.kShapeEditCmdType,
                CommandClientId,
                "Verify & Edit Unit Construction Specifications",
                "Reads DOCUMENT_CONFIG_JSON and IPT properties, then compares against user-entered expectations, allowing overrides.",
                smallIcon,
                largeIcon,
                Inventor.ButtonDisplayEnum.kAlwaysDisplayText);

            _onExecuteHandler = new InvBtnSink(OnExecute);
            _buttonDef.OnExecute += _onExecuteHandler;

            return _buttonDef;  // ButtonDefinition implements ControlDefinition
        }

        // ── Programmatic Icon Generation ──────────────────────────────────────

        private static Image CreateIcon(int size)
        {
            Bitmap bmp = new Bitmap(size, size);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                // 1. Draw Gear
                float cx = size * 0.42f;
                float cy = size * 0.42f;
                float rOut = size * 0.28f;
                float rIn = size * 0.20f;
                float rHole = size * 0.08f;

                Color gearColor = Color.FromArgb(100, 120, 140);
                Color gearBorderColor = Color.FromArgb(70, 85, 100);

                using (Brush gearBrush = new SolidBrush(gearColor))
                using (Pen gearPen = new Pen(gearBorderColor, size >= 32 ? 1.5f : 1.0f))
                {
                    // Draw Gear teeth
                    int nTeeth = 8;
                    for (int i = 0; i < nTeeth; i++)
                    {
                        double theta = i * (2 * Math.PI / nTeeth);
                        double toothWidthAngle = 0.28; // radians

                        PointF[] pts = new PointF[4];
                        pts[0] = new PointF(
                            cx + rIn * (float)Math.Cos(theta - toothWidthAngle),
                            cy + rIn * (float)Math.Sin(theta - toothWidthAngle)
                        );
                        pts[1] = new PointF(
                            cx + rOut * (float)Math.Cos(theta - toothWidthAngle * 0.5),
                            cy + rOut * (float)Math.Sin(theta - toothWidthAngle * 0.5)
                        );
                        pts[2] = new PointF(
                            cx + rOut * (float)Math.Cos(theta + toothWidthAngle * 0.5),
                            cy + rOut * (float)Math.Sin(theta + toothWidthAngle * 0.5)
                        );
                        pts[3] = new PointF(
                            cx + rIn * (float)Math.Cos(theta + toothWidthAngle),
                            cy + rIn * (float)Math.Sin(theta + toothWidthAngle)
                        );

                        g.FillPolygon(gearBrush, pts);
                        g.DrawPolygon(gearPen, pts);
                    }

                    // Fill main body
                    g.FillEllipse(gearBrush, cx - rIn, cy - rIn, rIn * 2, rIn * 2);
                    g.DrawEllipse(gearPen, cx - rIn, cy - rIn, rIn * 2, rIn * 2);

                    // Hollow out the center hole
                    var oldCompositing = g.CompositingMode;
                    g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                    using (Brush transBrush = new SolidBrush(Color.Transparent))
                    {
                        g.FillEllipse(transBrush, cx - rHole, cy - rHole, rHole * 2, rHole * 2);
                    }
                    g.CompositingMode = oldCompositing;

                    // Draw center hole inner border
                    g.DrawEllipse(gearPen, cx - rHole, cy - rHole, rHole * 2, rHole * 2);
                }

                // 2. Draw Magnifying Glass
                float mx = size * 0.70f;
                float my = size * 0.70f;
                float mr = size * 0.18f;

                // Handle pointing down-right
                float handleStartDist = mr;
                float handleEndDist = size * 0.40f;
                double handleAngle = Math.PI / 4; // 45 degrees
                float hxStart = mx + handleStartDist * (float)Math.Cos(handleAngle);
                float hyStart = my + handleStartDist * (float)Math.Sin(handleAngle);
                float hxEnd = mx + handleEndDist * (float)Math.Cos(handleAngle);
                float hyEnd = my + handleEndDist * (float)Math.Sin(handleAngle);

                // Handle (dark grey/black)
                using (Pen handlePen = new Pen(Color.FromArgb(60, 60, 60), size >= 32 ? 3.5f : 2.0f))
                {
                    handlePen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                    handlePen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                    g.DrawLine(handlePen, hxStart, hyStart, hxEnd, hyEnd);
                }

                // Lens Fill (semi-transparent blue)
                using (Brush lensBrush = new SolidBrush(Color.FromArgb(120, 180, 220, 255)))
                {
                    g.FillEllipse(lensBrush, mx - mr, my - mr, mr * 2, mr * 2);
                }

                // Rim (dark grey)
                using (Pen rimPen = new Pen(Color.FromArgb(50, 50, 50), size >= 32 ? 2.0f : 1.0f))
                {
                    g.DrawEllipse(rimPen, mx - mr, my - mr, mr * 2, mr * 2);
                }

                // Glass Highlight
                using (Pen highlightPen = new Pen(Color.FromArgb(200, 255, 255, 255), size >= 32 ? 1.5f : 0.8f))
                {
                    g.DrawArc(highlightPen, mx - mr * 0.6f, my - mr * 0.6f, mr * 1.2f, mr * 1.2f, 180, 90);
                }
            }
            return bmp;
        }

        // Helper class to convert .NET Image to IPictureDisp using AxHost
        private class PictureDispConverter : System.Windows.Forms.AxHost
        {
            private PictureDispConverter() : base("5a630e2a-351d-4e26-89b2-04e4a7a8d50e") { }

            public static stdole.IPictureDisp ToIPictureDisp(Image image)
            {
                return (stdole.IPictureDisp)GetIPictureDispFromPicture(image);
            }
        }

        // ── Button click ──────────────────────────────────────────────────────

        private void OnExecute(InvNVM context)
        {
            DebugLogger.Clear();
            DebugLogger.Log("OnExecute started");
            try
            {
                Inventor.Document? activeDoc = _app.ActiveDocument;
                if (activeDoc is null)
                {
                    DebugLogger.Log("Active document is null");
                    MessageBox.Show("No document is currently open.", "Unit Construction Verifier",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                DebugLogger.Log($"Active document: {activeDoc.FullFileName}");

                if (activeDoc is not InvAssyDoc asmDoc)
                {
                    DebugLogger.Log("Active document is not an AssemblyDocument");
                    MessageBox.Show("Please open an assembly (.IAM) file first.", "Unit Construction Verifier",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Extract data from the active assembly and all referenced sub-assemblies
                var extractor    = new ConfigJsonExtractor();
                var engExtractor = new EngineeringJsonExtractor();
                var iptReader    = new IptPropertyReader();

                DebugLogger.Log("Starting extractor.ExtractAll");
                System.Collections.Generic.List<ConfigData> configDatas = extractor.ExtractAll(asmDoc);
                DebugLogger.Log($"ExtractAll found {configDatas.Count} ConfigData payloads");

                DebugLogger.Log("Starting engExtractor.ExtractAll");
                EngineeringData engData = engExtractor.ExtractAll(asmDoc);
                DebugLogger.Log($"Curb support height: {engData.CurbSupportAngleHeight}");

                DebugLogger.Log("Starting iptReader.ScanAssembly");
                IptScanResult    iptResult  = iptReader.ScanAssembly(asmDoc);
                DebugLogger.Log($"ScanAssembly found {iptResult.Parts.Count} parts");

                // Build the domain model
                DebugLogger.Log("Building domain model");
                var builder = new ConstructionDataBuilder(configDatas, engData, iptResult);
                UnitConstructionData data = builder.Build();
                DebugLogger.Log($"Built model: RoofRows={data.RoofRows.Count}, WallRows={data.WallRows.Count}, BaseRows={data.BaseRows.Count}");

                // Load any previously saved overrides
                string iamPath = asmDoc.FullFileName;
                DebugLogger.Log($"Loading overrides for {iamPath}");
                PersistenceManager.LoadOverrides(iamPath, data);

                // Show the dialog (pass iptResult for verification)
                DebugLogger.Log("Showing VerifierWindow");
                var window = new VerifierWindow(data, iamPath, _app);
                window.SetIptScanResult(iptResult);
                window.ShowDialog();
                DebugLogger.Log("VerifierWindow closed");
            }
            catch (Exception ex)
            {
                DebugLogger.Log(ex, "VerifierCommand.OnExecute");
                MessageBox.Show($"Error: {ex.Message}\n\n{ex.StackTrace}",
                    "Unit Construction Verifier", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── IDisposable ───────────────────────────────────────────────────────

        public void Dispose()
        {
            if (_buttonDef is not null && _onExecuteHandler is not null)
                _buttonDef.OnExecute -= _onExecuteHandler;
            _buttonDef        = null;
            _onExecuteHandler = null;
        }
    }
}
