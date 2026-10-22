using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace UnitConstructionVerifier.UI
{
    /// <summary>Small layout and ordering rules for the verifier window, kept free of WPF for testing.</summary>
    public static class WindowLayout
    {
        /// <summary>
        /// Places a companion window beside its owner without leaving the screen: to the right when it fits,
        /// otherwise to the left, otherwise flush with the right screen edge.
        /// </summary>
        public static double CompanionLeft(double ownerLeft, double ownerWidth, double width, double screenLeft, double screenWidth, double gap = 12)
        {
            double screenRight = screenLeft + screenWidth;
            double right = ownerLeft + ownerWidth + gap;
            if (right + width <= screenRight) return right;

            double left = ownerLeft - gap - width;
            if (left >= screenLeft) return left;

            return Math.Max(screenLeft, screenRight - width);
        }

        /// <summary>Keeps a window's top edge on screen.</summary>
        public static double CompanionTop(double ownerTop, double height, double screenTop, double screenHeight)
            => Math.Max(screenTop, Math.Min(ownerTop, screenTop + screenHeight - height));

        /// <summary>
        /// Orders gauge options for the dropdowns: whole-number gauges ascending (10, 12 … 24), then decimal
        /// plate thicknesses ascending, then anything else in its original order. Nothing is dropped.
        /// </summary>
        public static List<string> OrderGauges(IEnumerable<string> gauges)
        {
            var list = gauges.ToList();
            var whole = new List<(int Value, string Text)>();
            var decimals = new List<(double Value, string Text)>();
            var other = new List<string>();

            foreach (string g in list)
            {
                string t = g?.Trim() ?? string.Empty;
                if (t.Length > 0 && t[0] != '0' && t.All(char.IsDigit) && int.TryParse(t, NumberStyles.None, CultureInfo.InvariantCulture, out int n))
                    whole.Add((n, g!));
                else if (t.IndexOf('.') >= 0 &&double.TryParse(t, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double d))
                    decimals.Add((d, g!));
                else
                    other.Add(g!);
            }

            return whole.OrderBy(w => w.Value).Select(w => w.Text)
                .Concat(decimals.OrderBy(d => d.Value).Select(d => d.Text))
                .Concat(other)
                .ToList();
        }
    }
}
