using System;
using System.Collections.Generic;

namespace SkinChannelPunch.Core
{
    internal sealed class VerticalPatternResult
    {
        public IReadOnlyList<double> PositionsIn { get; }
        public double SpanIn { get; }
        public string Message { get; }

        public VerticalPatternResult(IReadOnlyList<double> positionsIn, double spanIn, string message)
        {
            PositionsIn = positionsIn;
            SpanIn = spanIn;
            Message = message;
        }
    }

    /// <summary>
    /// Skin-only vertical span: bottom + inset through top - inset.
    /// Middle holes only when usable span >= max center-to-center.
    /// </summary>
    internal static class VerticalPatternCalculator
    {
        public static VerticalPatternResult Compute(
            double skinBottomIn,
            double skinTopIn,
            double bottomInsetIn,
            double topInsetIn,
            double maxCenterToCenterIn)
        {
            double first = skinBottomIn + bottomInsetIn;
            double last = skinTopIn - topInsetIn;
            double usable = last - first;

            if (usable < 0)
            {
                return new VerticalPatternResult(
                    Array.Empty<double>(),
                    usable,
                    "Skin span is too short for the top and bottom insets.");
            }

            var positions = new List<double> { first };

            if (usable >= maxCenterToCenterIn)
            {
                int intervals = Math.Max(1, (int)Math.Ceiling(usable / maxCenterToCenterIn));
                double pitch = usable / intervals;
                for (int i = 1; i < intervals; i++)
                {
                    positions.Add(first + (pitch * i));
                }
            }

            if (positions.Count == 0 || Math.Abs(positions[positions.Count - 1] - last) > 1e-6)
            {
                positions.Add(last);
            }

            return new VerticalPatternResult(
                positions,
                usable,
                $"{positions.Count} hole(s) over {usable:0.###} in usable span.");
        }
    }
}
