using UnitConstructionVerifier.Persistence;

namespace UnitConstructionVerifier.Operations
{
    /// <summary>
    /// Maps grid "Checked Parameter" values to <see cref="PartPropertyEdits"/> fields for write-back.
    /// </summary>
    public static class PartPropertyEditsMapper
    {
        public static bool TryApply(PartPropertyEdits edits, string parameter, string value)
        {
            if (edits == null || string.IsNullOrWhiteSpace(parameter))
            {
                return false;
            }

            if (string.Equals(parameter, "Thickness", System.StringComparison.OrdinalIgnoreCase))
            {
                edits.Thickness = value?.Trim();
                return true;
            }

            if (IsGaugeAndMaterialParameter(parameter))
            {
                PersistenceManager.ParseGaugeAndMaterial(value ?? string.Empty, out string gauge, out string material);
                edits.MtlGauge = gauge;
                edits.YCMATL = material;
                return true;
            }

            if (IsMaterialOnlyParameter(parameter))
            {
                edits.YCMATL = value?.Trim();
                return true;
            }

            return false;
        }

        public static bool HasAnyValue(PartPropertyEdits edits)
        {
            return edits != null &&
                   (edits.Thickness != null || edits.YCMATL != null || edits.MtlGauge != null);
        }

        private static bool IsGaugeAndMaterialParameter(string parameter)
        {
            return parameter.IndexOf("Gauge & Material", System.StringComparison.OrdinalIgnoreCase) >= 0
                || string.Equals(parameter, "Formed Channel Material", System.StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsMaterialOnlyParameter(string parameter)
        {
            return parameter.IndexOf("Structural Material", System.StringComparison.OrdinalIgnoreCase) >= 0
                || string.Equals(parameter, "Base Material", System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
