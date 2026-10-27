using System;
using System.Drawing;
using System.Windows.Forms;
using Inventor;
using UnitConstructionVerifier.Extraction;
using UnitConstructionVerifier.Models;
using UnitConstructionVerifier.UI;
using InvApp = Inventor.Application;
using InvBtnDef = Inventor.ButtonDefinition;
using InvBtnSink = Inventor.ButtonDefinitionSink_OnExecuteEventHandler;
using InvNVM = Inventor.NameValueMap;

namespace UnitConstructionVerifier.Operations
{
    public class ThicknessHoverCommand : IDisposable
    {
        private const string CommandClientId = "{1d4b8923-ae63-4cf0-af0e-7c1c9a14890f}";
        private const string CommandInternalName = "id_BtnThicknessViewer";
        private const string CommandDisplayName = "Thickness Inspector";

        private readonly InvApp _inventorApp;
        private InvBtnDef? _buttonDef;
        private InvBtnSink? _onExecuteHandler;

        private InteractionEvents? _interactionEvents;
        private SelectEvents? _selectEvents;
        private MouseEvents? _mouseEvents;
        private TooltipForm? _tooltipForm;
        private bool _isActive;

        public ThicknessHoverCommand(InvApp app)
        {
            _inventorApp = app ?? throw new ArgumentNullException(nameof(app));
        }

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
                CommandTypesEnum.kQueryOnlyCmdType,
                CommandClientId,
                "Hover over parts to inspect material group and gauge thickness",
                "Thickness Inspector",
                smallIcon,
                largeIcon,
                ButtonDisplayEnum.kAlwaysDisplayText
            );

            _onExecuteHandler = new InvBtnSink(OnExecute);
            _buttonDef.OnExecute += _onExecuteHandler;

            return _buttonDef;
        }

        private void OnExecute(InvNVM context)
        {
            if (_buttonDef == null) return;

            if (_buttonDef.Pressed)
            {
                Stop();
            }
            else
            {
                _buttonDef.Pressed = true;
                Start();
            }
        }

        // ── Interaction Command Lifecycle ────────────────────────────────────

        public void Start()
        {
            if (_isActive) return;

            try
            {
                MaterialsConfig.Initialize();
                _tooltipForm = new TooltipForm();
                _interactionEvents = _inventorApp.CommandManager.CreateInteractionEvents();
                _interactionEvents.InteractionDisabled = false;

                // Configure select events
                _selectEvents = _interactionEvents.SelectEvents;
                ConfigureFilters();

                // Configure mouse events
                _mouseEvents = _interactionEvents.MouseEvents;

                // Subscribe to events
                _interactionEvents.OnTerminate += InteractionEvents_OnTerminate;
                _selectEvents.OnPreSelect += SelectEvents_OnPreSelect;
                _selectEvents.OnStopPreSelect += SelectEvents_OnStopPreSelect;
                _mouseEvents.OnMouseMove += MouseEvents_OnMouseMove;

                _interactionEvents.Start();
                _isActive = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to start Material Thickness Inspector: " + ex.Message, 
                                "Thickness Inspector Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Stop();
            }
        }

        public void Stop()
        {
            if (!_isActive) return;

            _isActive = false;

            if (_buttonDef != null)
            {
                _buttonDef.Pressed = false;
            }

            if (_tooltipForm != null)
            {
                _tooltipForm.Hide();
                _tooltipForm.Dispose();
                _tooltipForm = null;
            }

            // Unsubscribe and release objects
            if (_interactionEvents != null)
            {
                try
                {
                    _interactionEvents.Stop();
                    _interactionEvents.OnTerminate -= InteractionEvents_OnTerminate;
                    
                    if (_selectEvents != null)
                    {
                        _selectEvents.OnPreSelect -= SelectEvents_OnPreSelect;
                        _selectEvents.OnStopPreSelect -= SelectEvents_OnStopPreSelect;
                    }
                    if (_mouseEvents != null)
                    {
                        _mouseEvents.OnMouseMove -= MouseEvents_OnMouseMove;
                    }
                }
                catch { }

                _interactionEvents = null;
                _selectEvents = null;
                _mouseEvents = null;
            }
        }

        public void Dispose()
        {
            Stop();
            if (_buttonDef != null && _onExecuteHandler != null)
            {
                try { _buttonDef.OnExecute -= _onExecuteHandler; } catch { }
                _onExecuteHandler = null;
            }
            _buttonDef = null;
        }

        private void ConfigureFilters()
        {
            if (_selectEvents == null) return;

            _selectEvents.ClearSelectionFilter();

            if (_inventorApp.ActiveDocument == null) return;

            DocumentTypeEnum docType = _inventorApp.ActiveDocument.DocumentType;
            if (docType == DocumentTypeEnum.kAssemblyDocumentObject)
            {
                _selectEvents.AddSelectionFilter(SelectionFilterEnum.kAssemblyLeafOccurrenceFilter);
            }
            else if (docType == DocumentTypeEnum.kPartDocumentObject)
            {
                _selectEvents.AddSelectionFilter(SelectionFilterEnum.kPartBodyFilter);
            }
        }

        private void InteractionEvents_OnTerminate()
        {
            Stop();
        }

        private void SelectEvents_OnPreSelect(
            ref object PreSelectEntity,
            out bool DoHighlight,
            ref Inventor.ObjectCollection MorePreSelectEntities,
            Inventor.SelectionDeviceEnum SelectionDevice,
            Inventor.Point ModelPosition,
            Inventor.Point2d ViewPosition,
            Inventor.View View)
        {
            DoHighlight = true; 

            if (PreSelectEntity == null)
            {
                if (_tooltipForm != null) _tooltipForm.Hide();
                return;
            }

            try
            {
                PartComponentDefinition? partDef = null;
                ComponentOccurrence? occ = null;

                if (PreSelectEntity is ComponentOccurrence occurrence)
                {
                    occ = occurrence;
                    if (occ.Definition is PartComponentDefinition pd)
                    {
                        partDef = pd;
                    }
                }
                else if (PreSelectEntity is SurfaceBody body)
                {
                    if (body.ComponentDefinition is PartComponentDefinition pd)
                    {
                        partDef = pd;
                    }
                }

                if (partDef == null)
                {
                    if (_tooltipForm != null) _tooltipForm.Hide();
                    return;
                }

                double? measuredInches = null;
                if (partDef is SheetMetalComponentDefinition smDef)
                    measuredInches = Convert.ToDouble(smDef.Thickness.Value) / 2.54;

                var part = IptPropertyMapper.Map(new PartDocumentSource((PartDocument)partDef.Document), string.Empty);
                string tooltipText = ThicknessTooltip.Build(part, measuredInches);

                if (_tooltipForm != null)
                {
                    _tooltipForm.SetTooltip(tooltipText, System.Windows.Forms.Cursor.Position);
                }
            }
            catch
            {
                if (_tooltipForm != null) _tooltipForm.Hide();
            }
        }

        private void SelectEvents_OnStopPreSelect(
            Inventor.Point ModelPosition,
            Inventor.Point2d ViewPosition,
            Inventor.View View)
        {
            if (_tooltipForm != null)
            {
                _tooltipForm.Hide();
            }
        }

        private void MouseEvents_OnMouseMove(
            MouseButtonEnum Button,
            ShiftStateEnum ShiftKeys,
            Inventor.Point ModelPosition,
            Point2d ViewPosition,
            Inventor.View View)
        {
            if (_tooltipForm != null && _tooltipForm.Visible)
            {
                _tooltipForm.Location = new System.Drawing.Point(
                    System.Windows.Forms.Cursor.Position.X + 15,
                    System.Windows.Forms.Cursor.Position.Y + 15
                );
            }
        }

        // ── Programmatic Icon Generation ──────────────────────────────────────

        private static Image CreateIcon(int size)
        {
            Bitmap bmp = new Bitmap(size, size);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(System.Drawing.Color.Transparent);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                // 1. Draw Ruler
                float ry = size * 0.24f;
                float rh = size * 0.22f;
                float rx = size * 0.08f;
                float rw = size * 0.84f;

                System.Drawing.Color rulerColor = System.Drawing.Color.FromArgb(240, 190, 45);
                System.Drawing.Color rulerBorderColor = System.Drawing.Color.FromArgb(170, 120, 10);
                System.Drawing.Color tickColor = System.Drawing.Color.FromArgb(70, 50, 10);

                using (Brush rulerBrush = new SolidBrush(rulerColor))
                using (Pen rulerPen = new Pen(rulerBorderColor, size >= 32 ? 1.5f : 1.0f))
                using (Pen tickPen = new Pen(tickColor, size >= 32 ? 1.5f : 1.0f))
                {
                    g.FillRectangle(rulerBrush, rx, ry, rw, rh);
                    g.DrawRectangle(rulerPen, rx, ry, rw, rh);

                    // Draw vertical tick marks (alternating lengths)
                    int tickCount = 8;
                    float tickSpacing = rw / tickCount;
                    for (int i = 1; i < tickCount; i++)
                    {
                        float tx = rx + i * tickSpacing;
                        float tickHeight = (i % 2 == 0) ? (rh * 0.6f) : (rh * 0.4f);
                        g.DrawLine(tickPen, tx, ry, tx, ry + tickHeight);
                    }
                }

                // 2. Draw Magnifying Glass
                float mx = size * 0.65f;
                float my = size * 0.65f;
                float mr = size * 0.19f;

                // Handle pointing down-right
                float handleStartDist = mr;
                float handleEndDist = size * 0.38f;
                double handleAngle = Math.PI / 4; // 45 degrees
                float hxStart = mx + handleStartDist * (float)Math.Cos(handleAngle);
                float hyStart = my + handleStartDist * (float)Math.Sin(handleAngle);
                float hxEnd = mx + handleEndDist * (float)Math.Cos(handleAngle);
                float hyEnd = my + handleEndDist * (float)Math.Sin(handleAngle);

                // Handle (dark grey/black)
                using (Pen handlePen = new Pen(System.Drawing.Color.FromArgb(60, 60, 60), size >= 32 ? 3.5f : 2.0f))
                {
                    handlePen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                    handlePen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                    g.DrawLine(handlePen, hxStart, hyStart, hxEnd, hyEnd);
                }

                // Lens Fill (semi-transparent blue)
                using (Brush lensBrush = new SolidBrush(System.Drawing.Color.FromArgb(120, 180, 220, 255)))
                {
                    g.FillEllipse(lensBrush, mx - mr, my - mr, mr * 2, mr * 2);
                }

                // Rim (dark grey)
                using (Pen rimPen = new Pen(System.Drawing.Color.FromArgb(50, 50, 50), size >= 32 ? 2.0f : 1.0f))
                {
                    g.DrawEllipse(rimPen, mx - mr, my - mr, mr * 2, mr * 2);
                }

                // Glass Highlight
                using (Pen highlightPen = new Pen(System.Drawing.Color.FromArgb(200, 255, 255, 255), size >= 32 ? 1.5f : 0.8f))
                {
                    g.DrawArc(highlightPen, mx - mr * 0.6f, my - mr * 0.6f, mr * 1.2f, mr * 1.2f, 180, 90);
                }
            }
            return bmp;
        }

        // Helper class to convert .NET Image to IPictureDisp using AxHost
        private class PictureDispConverter : AxHost
        {
            private PictureDispConverter() : base("5a630e2a-351d-4e26-89b2-04e4a7a8d50e") { }

            public static stdole.IPictureDisp ToIPictureDisp(Image image)
            {
                return (stdole.IPictureDisp)GetIPictureDispFromPicture(image);
            }
        }
    }
}
