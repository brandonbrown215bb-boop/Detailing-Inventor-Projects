using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using stdole;

namespace Highlighter.UI
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
            return PictureDispConverter.ToPictureDisp(LoadOrFallback(16));
        }

        public static IPictureDisp CreateLargeIcon()
        {
            return PictureDispConverter.ToPictureDisp(LoadOrFallback(32));
        }

        private static Image LoadOrFallback(int size)
        {
            try
            {
                string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty;
                string path = Path.Combine(dir, "assets", "highlighter-" + size + ".png");
                if (!File.Exists(path))
                {
                    // Dev: relative to project assets next to dll (bin\Release\net48\assets)
                    path = Path.Combine(dir, "highlighter-" + size + ".png");
                }

                if (File.Exists(path))
                {
                    // Clone so file handle is released.
                    using (var src = Image.FromFile(path))
                    {
                        return new Bitmap(src);
                    }
                }
            }
            catch
            {
            }

            return CreateFallbackGlyph(size);
        }

        private static Image CreateFallbackGlyph(int size)
        {
            var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.Transparent);
                using (var brush = new SolidBrush(Color.FromArgb(255, 255, 220, 0)))
                {
                    g.FillRectangle(brush, size * 0.2f, size * 0.1f, size * 0.35f, size * 0.8f);
                }
            }

            return bitmap;
        }
    }
}
