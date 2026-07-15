namespace SkinChannelPunch.Core
{
    internal static class PatternConstants
    {
        public const double InchesToCm = 2.54;

        /// <summary>Vertical external wall v1 defaults (inches).</summary>
        public const double TopInsetIn = 7.0;
        public const double BottomInsetIn = 1.25;
        public const double MaxCenterToCenterIn = 36.0;
        public const double HoleDiameterIn = 0.203;
        public const double ChannelFlangeWidthIn = 1.5;
        public const double FlangeHalfWidthIn = 0.75;

        /// <summary>Clearance from the channel flange edge toward the skin panel (inches).</summary>
        public const double RowInsetFromFlangeIn = FlangeHalfWidthIn;
    }
}
