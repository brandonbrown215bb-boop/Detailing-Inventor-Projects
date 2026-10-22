using System;
using System.Drawing;
using System.Windows.Forms;
using UnitConstructionVerifier.UI;
using InvApp = Inventor.Application;
using InvBtnDef = Inventor.ButtonDefinition;
using InvBtnSink = Inventor.ButtonDefinitionSink_OnExecuteEventHandler;
using InvNVM = Inventor.NameValueMap;

namespace UnitConstructionVerifier.Operations
{
    /// <summary>
    /// Ribbon button for the Step 0 YCShell spike (spike/ycshell-bridge branch only). Opens one
    /// <see cref="YcShellSpikeWindow"/> at a time.
    /// </summary>
    internal sealed class YcShellSpikeCommand : IDisposable
    {
        private const string CommandClientId = "{B1C2D3E4-F5A6-7890-BCDE-F01234567891}";
        private const string CommandInternalName = "UCV_YcShellSpike";
        private const string CommandDisplayName = "YCShell Spike";

        private readonly InvApp _app;
        private InvBtnDef? _buttonDef;
        private InvBtnSink? _onExecuteHandler;
        private YcShellSpikeWindow? _window;

        internal YcShellSpikeCommand(InvApp app)
        {
            _app = app;
        }

        internal InvBtnDef CreateDefinition(InvApp app)
        {
            Inventor.ControlDefinitions defs = app.CommandManager.ControlDefinitions;

            // Avoid duplicate registration across sessions
            try { return (InvBtnDef)(Inventor.ControlDefinition)defs[CommandInternalName]; }
            catch { /* not yet created */ }

            _buttonDef = (InvBtnDef)defs.AddButtonDefinition(
                CommandDisplayName,
                CommandInternalName,
                Inventor.CommandTypesEnum.kQueryOnlyCmdType,
                CommandClientId,
                "Batch regenerate/upload spike: check UCV can reach YCShell",
                "Lists the surfaces YCShell has loaded and regenerates one surface in a Spike Copy unit. Never uploads.",
                PictureDispConverter.ToIPictureDisp(CreateIcon(16)),
                PictureDispConverter.ToIPictureDisp(CreateIcon(32)),
                Inventor.ButtonDisplayEnum.kAlwaysDisplayText);

            _onExecuteHandler = new InvBtnSink(OnExecute);
            _buttonDef.OnExecute += _onExecuteHandler;

            return _buttonDef;
        }

        private void OnExecute(InvNVM context)
        {
            try
            {
                if (_window is { IsLoaded: true })
                {
                    _window.Activate();
                    return;
                }

                _window = new YcShellSpikeWindow(_app);
                _window.Closed += (_, _) => _window = null;
                _window.Show();
            }
            catch (Exception ex)
            {
                DebugLogger.Log(ex, "YcShellSpikeCommand.OnExecute");
                System.Windows.MessageBox.Show($"YCShell spike failed to open:\n\n{ex.Message}", "YCShell Spike");
            }
        }

        private static Image CreateIcon(int size)
        {
            var bmp = new Bitmap(size, size);
            using (Graphics g = Graphics.FromImage(bmp))
            using (var font = new Font("Segoe UI", size * 0.55f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var brush = new SolidBrush(Color.FromArgb(200, 90, 20)))
            {
                g.Clear(Color.Transparent);
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("YC", font, brush, new RectangleF(0, 0, size, size), format);
            }

            return bmp;
        }

        public void Dispose()
        {
            try { _window?.Close(); } catch { }
            _window = null;

            if (_buttonDef is not null && _onExecuteHandler is not null)
            {
                try { _buttonDef.OnExecute -= _onExecuteHandler; } catch { }
            }
            _buttonDef = null;
            _onExecuteHandler = null;
        }

        // Converts a .NET Image to IPictureDisp using AxHost
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
