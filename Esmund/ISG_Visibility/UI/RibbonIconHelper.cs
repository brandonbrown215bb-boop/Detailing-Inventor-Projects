using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using stdole;

namespace VisTog.UI
{
    internal static class RibbonIconHelper
    {
        private sealed class PictureDispConverter : AxHost
        {
            private PictureDispConverter()
                : base("5a630e2a-351d-4e26-89b2-04e4a7a8d50e")
            {
            }

            public static IPictureDisp ToPictureDisp(Image image)
            {
                return GetIPictureDispFromPicture(image) as IPictureDisp;
            }
        }

        public static IPictureDisp CreateStandardIcon()
        {
            return PictureDispConverter.ToPictureDisp(LoadOrCreateIcon(16));
        }

        public static IPictureDisp CreateLargeIcon()
        {
            return PictureDispConverter.ToPictureDisp(LoadOrCreateIcon(32));
        }

        private static Image LoadOrCreateIcon(int size)
        {
            try
            {
                string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty;
                string path = Path.Combine(dir, "assets", "isg-visibility-" + size + ".png");
                if (File.Exists(path))
                {
                    // Clone so the file isn't locked.
                    using (var fileImage = Image.FromFile(path))
                    {
                        return new Bitmap(fileImage);
                    }
                }
            }
            catch
            {
            }

            return CreateIcon(size);
        }

        private static Image CreateIcon(int size)
        {
            var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.Transparent);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

                int pad = Math.Max(1, (int)(size * 0.06));
                var rect = new Rectangle(pad, pad, size - (pad * 2), size - (pad * 2));

                using (var path = new GraphicsPath())
                {
                    path.AddEllipse(rect);
                    graphics.SetClip(path);

                    using (var brush = new LinearGradientBrush(
                        rect,
                        Color.FromArgb(255, 26, 111, 156),
                        Color.FromArgb(255, 70, 120, 150),
                        0f))
                    {
                        var blend = new ColorBlend
                        {
                            Colors = new[]
                            {
                                Color.FromArgb(255, 26, 111, 156),
                                Color.FromArgb(255, 42, 138, 184),
                                Color.FromArgb(255, 70, 120, 150)
                            },
                            Positions = new[] { 0f, 0.45f, 1f }
                        };
                        brush.InterpolationColors = blend;
                        graphics.FillEllipse(brush, rect);
                    }

                    using (var pen = new Pen(Color.FromArgb(180, 220, 240, 255), Math.Max(0.7f, size / 28f)))
                    {
                        float cx = rect.X + rect.Width / 2f;
                        float cy = rect.Y + rect.Height / 2f;
                        float rx = rect.Width / 2f;
                        float ry = rect.Height / 2f;
                        foreach (float t in new[] { -0.45f, 0f, 0.45f })
                        {
                            float y = cy + ry * t;
                            float halfW = (float)(Math.Sqrt(Math.Max(0, 1 - (t * t))) * rx);
                            graphics.DrawLine(pen, cx - halfW, y, cx + halfW, y);
                        }

                        foreach (float s in new[] { 0.35f, 0.7f })
                        {
                            float w = rx * 2f * s;
                            graphics.DrawEllipse(pen, cx - (w / 2f), rect.Y, w, rect.Height);
                        }
                    }

                    using (var land = new SolidBrush(Color.FromArgb(200, 46, 140, 90)))
                    {
                        float rx = rect.Width / 2f;
                        float ry = rect.Height / 2f;
                        float cy = rect.Y + ry;
                        graphics.FillEllipse(land, rect.X + rx * 0.2f, cy - ry * 0.3f, rx * 0.5f, ry * 0.45f);
                    }

                    graphics.ResetClip();
                }

                float fontSize = size >= 28 ? 9f : (size >= 20 ? 6.5f : 5f);
                using (var font = new System.Drawing.Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel))
                using (var sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                })
                {
                    var layout = new RectangleF(0, 0, size, size);
                    using (var outlinePath = new GraphicsPath())
                    {
                        outlinePath.AddString(
                            "ISG",
                            font.FontFamily,
                            (int)font.Style,
                            font.Size,
                            layout,
                            sf);

                        float outlineWidth = Math.Max(1.2f, size / 16f);
                        using (var outlinePen = new Pen(Color.Black, outlineWidth)
                        {
                            LineJoin = LineJoin.Round
                        })
                        {
                            graphics.DrawPath(outlinePen, outlinePath);
                        }

                        using (var fill = new SolidBrush(Color.White))
                        {
                            graphics.FillPath(fill, outlinePath);
                        }
                    }
                }
            }

            ApplyHorizontalFade(bitmap);
            return bitmap;
        }

        private static void ApplyHorizontalFade(Bitmap bitmap)
        {
            int width = bitmap.Width;
            int height = bitmap.Height;
            var data = bitmap.LockBits(
                new Rectangle(0, 0, width, height),
                ImageLockMode.ReadWrite,
                PixelFormat.Format32bppArgb);

            try
            {
                int stride = Math.Abs(data.Stride);
                int bytes = stride * height;
                var buffer = new byte[bytes];
                System.Runtime.InteropServices.Marshal.Copy(data.Scan0, buffer, 0, bytes);

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int i = (y * stride) + (x * 4);
                        byte a = buffer[i + 3];
                        if (a == 0)
                        {
                            continue;
                        }

                        double t = x / (double)Math.Max(1, width - 1);
                        double keep = 1.0 - (0.85 * Math.Pow(t, 1.15));
                        int newA = (int)Math.Round(a * keep);
                        if (newA < 0)
                        {
                            newA = 0;
                        }

                        if (newA > 255)
                        {
                            newA = 255;
                        }

                        buffer[i + 3] = (byte)newA;
                    }
                }

                System.Runtime.InteropServices.Marshal.Copy(buffer, 0, data.Scan0, bytes);
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }
    }
}
