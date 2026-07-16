namespace SkinChannelPunch.Core
{
    internal static class WallStockConstants
    {
        public const string SkinStock = "091-30117-081";
        public const string VerticalChannelStock = "091-30117-065";
        public const string SlopedRoofChannelStock = "091-30117-070";

        public static string ClassifyRole(string stockNumber)
        {
            if (string.IsNullOrWhiteSpace(stockNumber))
            {
                return null;
            }

            string normalized = stockNumber.Trim();
            if (normalized.IndexOf(SkinStock, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Skin";
            }

            if (normalized.IndexOf(VerticalChannelStock, System.StringComparison.OrdinalIgnoreCase) >= 0
                || normalized.IndexOf(SlopedRoofChannelStock, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "VerticalChannel";
            }

            return null;
        }

        public static bool IsKnownVerticalChannelStock(string stockNumber)
        {
            if (string.IsNullOrWhiteSpace(stockNumber))
            {
                return false;
            }

            string normalized = stockNumber.Trim();
            return normalized.IndexOf(VerticalChannelStock, System.StringComparison.OrdinalIgnoreCase) >= 0
                || normalized.IndexOf(SlopedRoofChannelStock, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
