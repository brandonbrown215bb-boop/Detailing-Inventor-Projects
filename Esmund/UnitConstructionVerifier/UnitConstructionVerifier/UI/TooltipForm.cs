using System;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace UnitConstructionVerifier.UI
{
    public class TooltipForm : Form
    {
        private Label lblText;

        [DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(
            int nLeftRect,
            int nTopRect,
            int nRightRect,
            int nBottomRect,
            int nWidthEllipse,
            int nHeightEllipse
        );

        [DllImport("gdi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteObject(IntPtr hObject);

        public TooltipForm()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.BackColor = Color.FromArgb(40, 40, 40); // Sleek dark gray
            this.DoubleBuffered = true;
            this.StartPosition = FormStartPosition.Manual;

            lblText = new Label();
            lblText.ForeColor = Color.FromArgb(245, 245, 245); // Off-white for high contrast and readability
            lblText.Font = new Font("Segoe UI", 10F, FontStyle.Regular);
            lblText.Location = new Point(12, 10);
            lblText.AutoSize = true;
            this.Controls.Add(lblText);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                // WS_EX_TRANSPARENT (0x20) - Click-through
                // WS_EX_NOACTIVATE (0x08000000) - Does not take focus
                // WS_EX_TOPMOST (0x00000008) - Force top-most
                cp.ExStyle |= 0x00000020 | 0x08000000 | 0x00000008;
                return cp;
            }
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        public void SetTooltip(string text, Point cursorPosition)
        {
            if (lblText.Text != text)
            {
                lblText.Text = text;
            }

            // Adjust form size to fit text + padding
            int newWidth = lblText.PreferredWidth + 24;
            int newHeight = lblText.PreferredHeight + 20;
            
            if (this.Size.Width != newWidth || this.Size.Height != newHeight)
            {
                this.Size = new Size(newWidth, newHeight);

                // Re-apply rounded corners
                IntPtr ptr = CreateRoundRectRgn(0, 0, this.Width, this.Height, 10, 10);
                if (this.Region != null)
                {
                    this.Region.Dispose();
                }
                this.Region = Region.FromHrgn(ptr);
                DeleteObject(ptr);
            }

            // Position tooltip offset from cursor
            // 15px right and 15px down prevents overlap with the pointer
            this.Location = new Point(cursorPosition.X + 15, cursorPosition.Y + 15);

            if (!this.Visible)
            {
                this.Show();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            // Draw a subtle border inside the rounded region to make it pop
            using (Pen borderPen = new Pen(Color.FromArgb(80, 80, 80), 1.5f))
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                // Use a smaller rectangle to draw the inside border
                e.Graphics.DrawPath(borderPen, GetRoundedRectPath(new Rectangle(0, 0, Width - 1, Height - 1), 10));
            }
        }

        private System.Drawing.Drawing2D.GraphicsPath GetRoundedRectPath(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            Size size = new Size(diameter, diameter);
            Rectangle arc = new Rectangle(bounds.Location, size);
            System.Drawing.Drawing2D.GraphicsPath path = new System.Drawing.Drawing2D.GraphicsPath();

            if (radius == 0)
            {
                path.AddRectangle(bounds);
                return path;
            }

            // Top-left arc
            path.AddArc(arc, 180, 90);

            // Top-right arc
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);

            // Bottom-right arc
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);

            // Bottom-left arc
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);

            path.CloseFigure();
            return path;
        }
    }
}
