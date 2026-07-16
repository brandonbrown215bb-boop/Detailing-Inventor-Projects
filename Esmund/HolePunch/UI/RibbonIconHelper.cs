using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using stdole;

namespace SkinChannelPunch.UI
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
                string path = Path.Combine(dir, "assets", "skin-punch-" + size + ".png");
                if (File.Exists(path))
                {
                    using (var fileImage = Image.FromFile(path))
                    {
                        return new Bitmap(fileImage);
                    }
                }
            }
            catch
            {
            }

            return CreateFallbackIcon(size);
        }

        /// <summary>Simple hand-punch silhouette if PNG assets are missing.</summary>
        private static Image CreateFallbackIcon(int size)
        {
            var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.Transparent);
                graphics.SmoothingMode = size >= 24 ? SmoothingMode.AntiAlias : SmoothingMode.None;

                using (var fill = new SolidBrush(Color.FromArgb(255, 120, 120, 125)))
                using (var dark = new SolidBrush(Color.FromArgb(255, 45, 45, 50)))
                using (var outline = new Pen(Color.FromArgb(255, 30, 30, 35), Math.Max(1f, size / 16f)))
                {
                    // Rough plier punch: two handles + head (diagonal).
                    float s = size;
                    PointF[] upper = new[]
                    {
                        new PointF(s * 0.18f, s * 0.72f),
                        new PointF(s * 0.42f, s * 0.48f),
                        new PointF(s * 0.78f, s * 0.22f),
                        new PointF(s * 0.88f, s * 0.28f),
                        new PointF(s * 0.52f, s * 0.55f),
                        new PointF(s * 0.28f, s * 0.82f),
                    };
                    PointF[] lower = new[]
                    {
                        new PointF(s * 0.12f, s * 0.55f),
                        new PointF(s * 0.40f, s * 0.42f),
                        new PointF(s * 0.72f, s * 0.18f),
                        new PointF(s * 0.82f, s * 0.12f),
                        new PointF(s * 0.48f, s * 0.38f),
                        new PointF(s * 0.22f, s * 0.48f),
                    };

                    graphics.FillPolygon(fill, upper);
                    graphics.FillPolygon(fill, lower);
                    graphics.DrawPolygon(outline, upper);
                    graphics.DrawPolygon(outline, lower);

                    float pivot = s * 0.12f;
                    graphics.FillEllipse(dark, (s * 0.42f) - (pivot / 2f), (s * 0.42f) - (pivot / 2f), pivot, pivot);
                    graphics.FillEllipse(dark, s * 0.72f, s * 0.14f, s * 0.10f, s * 0.10f);
                }
            }

            return bitmap;
        }
    }
}
