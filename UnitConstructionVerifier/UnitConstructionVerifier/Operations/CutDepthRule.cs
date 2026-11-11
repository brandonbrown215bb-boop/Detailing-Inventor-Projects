using System;

namespace UnitConstructionVerifier.Operations
{
    /// <summary>
    /// Decides whether a cut's distance must follow a sheet metal thickness change, so a cut that went through the
    /// old sheet still goes through the new one.
    /// </summary>
    /// <remarks>
    /// YCShell's library cuts are symmetric: sketched on the sheet's face and cut both ways, so only half the
    /// distance enters the material (18 GA skins cut 0.09"). A symmetric cut must be linked to
    /// <c>Thickness * 2 ul</c>; linking it to <c>Thickness</c> cuts only halfway.
    /// </remarks>
    public static class CutDepthRule
    {
        public const string OneSided  = "Thickness";
        public const string Symmetric = "Thickness * 2 ul";

        /// <summary>Slack for comparing cut reach with sheet thickness, in cm (about 0.0004").</summary>
        public const double ToleranceCm = 0.001;

        /// <returns>The expression to give the cut's distance, or null to leave it alone.</returns>
        public static string? NewExpression(bool symmetric, string expression, double distanceCm,
                                            double oldThicknessCm, double newThicknessCm)
        {
            string linked = symmetric ? Symmetric : OneSided;
            if (SameExpression(expression, linked)) return null;

            // An earlier UCV write linked symmetric cuts to Thickness alone; they reach only halfway.
            if (symmetric && SameExpression(expression, OneSided)) return linked;

            double reach = symmetric ? distanceCm / 2 : distanceCm;
            if (reach < oldThicknessCm - ToleranceCm) return null;  // never went through: deliberate, leave it
            if (reach >= newThicknessCm - ToleranceCm) return null; // still goes through
            return linked;
        }

        /// <summary>
        /// True when a cut went through the old sheet, so on a flat sheet it can be made Through All. Cuts linked to
        /// either thickness expression count, whatever their distance reads mid-write.
        /// </summary>
        public static bool WentThrough(bool symmetric, string expression, double distanceCm, double oldThicknessCm)
        {
            if (SameExpression(expression, OneSided) || SameExpression(expression, Symmetric)) return true;
            double reach = symmetric ? distanceCm / 2 : distanceCm;
            return reach >= oldThicknessCm - ToleranceCm;
        }

        private static bool SameExpression(string a, string b) =>
            string.Equals((a ?? string.Empty).Replace(" ", ""), b.Replace(" ", ""), StringComparison.OrdinalIgnoreCase);
    }
}
