using System.Windows;
using FileGrid.Portable.Models;

namespace FileGrid.Portable.Helpers;

public static class GridMath
{
    public static double Step(LayoutState layout) => layout.CellSize * layout.View.Zoom;

    public static (int Gx, int Gy) ScreenToGrid(LayoutState layout, double sx, double sy)
    {
        var step = Step(layout);
        return (
            (int)Math.Round((sx - layout.View.PanX) / step),
            (int)Math.Round((sy - layout.View.PanY) / step)
        );
    }

    public static Rect GridRectToScreen(LayoutState layout, GridBox box)
    {
        var step = Step(layout);
        return new Rect(
            Math.Round(layout.View.PanX + box.Gx * step),
            Math.Round(layout.View.PanY + box.Gy * step),
            Math.Round(box.Gw * step),
            Math.Round(box.Gh * step));
    }

    public static Point BorderPoint(Rect rect, Point toward)
    {
        var cx = rect.X + rect.Width / 2;
        var cy = rect.Y + rect.Height / 2;
        var dx = toward.X - cx;
        var dy = toward.Y - cy;
        if (Math.Abs(dx) < 0.001 && Math.Abs(dy) < 0.001) return new Point(cx, cy);
        var hw = rect.Width / 2;
        var hh = rect.Height / 2;
        var scale = Math.Max(Math.Abs(dx) / hw, Math.Abs(dy) / hh);
        return new Point(cx + dx / scale, cy + dy / scale);
    }
}
