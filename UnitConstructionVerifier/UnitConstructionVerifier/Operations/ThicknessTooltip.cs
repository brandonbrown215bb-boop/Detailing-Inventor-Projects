using System;
using System.Collections.Generic;
using System.Globalization;
using UnitConstructionVerifier.Engine;
using UnitConstructionVerifier.Models;

namespace UnitConstructionVerifier.Operations
{
    /// <summary>
    /// Text for the Thickness Inspector hover. Reads the part the same way the verifier does, so the
    /// tooltip and the verifier window never disagree about a part's gauge and material.
    /// </summary>
    public static class ThicknessTooltip
    {
        /// <summary>
        /// How far the modelled sheet metal thickness may drift from the recorded thickness before the
        /// tooltip shows it. Same tolerance <see cref="MaterialsConfig.ResolveFromThickness(string, string, out string, out string)"/>
        /// uses for a closest gauge match.
        /// </summary>
        public const double MeasuredTolerance = 0.00015;

        /// <param name="part">The part as <see cref="Extraction.IptPropertyMapper"/> reads it.</param>
        /// <param name="measuredInches">Live sheet metal thickness, or null for a part that isn't sheet metal.</param>
        public static string Build(IptProperties part, double? measuredInches)
        {
            var lines = new List<string>();

            // An unmatched gauge is a raw decimal ("0.0612 GA STL GALV" in the verifier); show the material alone.
            bool gaugeKnown = HasKnownGauge(part);
            string first = gaugeKnown
                ? VerificationEngine.FormatGaugeAndMaterial(part.MtlGauge, part.YCMATL)
                : MaterialsConfig.MapMaterial(part.YCMATL);
            lines.Add(first.Length > 0 ? first : "No gauge or material recorded");

            double? design = ParseInches(part.Thickness);
            lines.Add(design.HasValue ? $"{design.Value:0.000}\"" : "No thickness recorded");

            if (!gaugeKnown && !string.IsNullOrWhiteSpace(part.MtlGauge))
                lines.Add("No gauge match");

            if (design.HasValue && measuredInches.HasValue &&
                Math.Abs(measuredInches.Value - design.Value) > MeasuredTolerance)
                lines.Add($"Measured {measuredInches.Value:0.0000}\"");

            return string.Join("\n", lines);
        }

        private static bool HasKnownGauge(IptProperties part)
        {
            if (string.IsNullOrWhiteSpace(part.MtlGauge)) return false;
            string mapped = MaterialsConfig.MapGauge(part.MtlGauge, part.YCMATL);
            return MaterialsConfig.Gauges.Contains(mapped);
        }

        private static double? ParseInches(string value)
        {
            string clean = (value ?? string.Empty).Replace("\"", "").Replace("in", "").Trim();
            return double.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out double inches) && inches > 0
                ? inches
                : (double?)null;
        }
    }
}
