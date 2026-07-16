using System;
using System.Drawing;
using System.Windows.Forms;

namespace VisTog.UI
{
    /// <summary>
    /// Flat button with true vertical/horizontal text centering (WinForms FlatStyle sits text high).
    /// </summary>
    internal sealed class CenteredFlatButton : Button
    {
        public bool PreferEllipsis { get; set; } = true;
        public bool ShrinkTextToFit { get; set; }

        public CenteredFlatButton()
        {
            FlatStyle = FlatStyle.Flat;
            UseVisualStyleBackColor = false;
            UseCompatibleTextRendering = false;
            SetStyle(
                ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.UserPaint
                | ControlStyles.ResizeRedraw,
                true);
            UpdateStyles();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Rectangle bounds = ClientRectangle;

            Color back = Enabled ? BackColor : ControlPaint.Dark(BackColor);
            using (var fill = new SolidBrush(back))
            {
                g.FillRectangle(fill, bounds);
            }

            int border = FlatAppearance.BorderSize;
            if (border > 0)
            {
                Color borderColor = FlatAppearance.BorderColor;
                using (var pen = new Pen(borderColor, border))
                {
                    g.DrawRectangle(pen, border / 2, border / 2, bounds.Width - border, bounds.Height - border);
                }
            }

            if (Image != null)
            {
                int ix = bounds.X + (bounds.Width - Image.Width) / 2;
                int iy = bounds.Y + (bounds.Height - Image.Height) / 2;
                if (!string.IsNullOrEmpty(Text))
                {
                    ix = bounds.X + 4;
                }

                g.DrawImage(Image, ix, iy, Image.Width, Image.Height);
            }

            if (!string.IsNullOrEmpty(Text))
            {
                Font drawFont = Font;
                Font fitted = null;
                try
                {
                    if (ShrinkTextToFit && bounds.Width > 8)
                    {
                        float size = Font.Size;
                        while (size >= 8F)
                        {
                            fitted?.Dispose();
                            fitted = new Font(Font.FontFamily, size, Font.Style);
                            Size needed = TextRenderer.MeasureText(
                                Text,
                                fitted,
                                new Size(int.MaxValue, bounds.Height),
                                TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
                            if (needed.Width <= bounds.Width - 2)
                            {
                                break;
                            }

                            size -= 0.5F;
                        }

                        drawFont = fitted ?? Font;
                    }

                    TextFormatFlags flags = TextFormatFlags.HorizontalCenter
                        | TextFormatFlags.VerticalCenter
                        | TextFormatFlags.NoPrefix
                        | TextFormatFlags.SingleLine;
                    if (PreferEllipsis && !ShrinkTextToFit)
                    {
                        flags |= TextFormatFlags.EndEllipsis;
                    }

                    TextRenderer.DrawText(
                        g,
                        Text,
                        drawFont,
                        bounds,
                        ForeColor,
                        flags);
                }
                finally
                {
                    fitted?.Dispose();
                }
            }

            if (Focused && ShowFocusCues)
            {
                ControlPaint.DrawFocusRectangle(g, Rectangle.Inflate(bounds, -3, -3));
            }
        }
    }
}
