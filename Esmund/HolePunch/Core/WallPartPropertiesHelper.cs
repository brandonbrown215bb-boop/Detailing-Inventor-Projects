using Inventor;

namespace SkinChannelPunch.Core
{
    internal static class WallPartPropertiesHelper
    {
        public static string TryGetStockNumber(ComponentOccurrence occurrence)
        {
            PartDocument partDocument = occurrence?.Definition?.Document as PartDocument;
            if (partDocument == null)
            {
                return null;
            }

            return TryGetStockNumber(partDocument);
        }

        public static string TryGetStockNumber(PartDocument partDocument)
        {
            if (partDocument == null)
            {
                return null;
            }

            string stock = TryGetPropertyValue(partDocument, "Design Tracking Properties", "Stock Number");
            if (!string.IsNullOrWhiteSpace(stock))
            {
                return stock.Trim();
            }

            stock = TryGetPropertyValue(partDocument, "Inventor User Defined Properties", "Stock Number");
            if (!string.IsNullOrWhiteSpace(stock))
            {
                return stock.Trim();
            }

            stock = TryGetPropertyValue(partDocument, "Design Tracking Properties", "Stock");
            return string.IsNullOrWhiteSpace(stock) ? null : stock.Trim();
        }

        public static string DescribeOccurrence(ComponentOccurrence occurrence)
        {
            string stock = TryGetStockNumber(occurrence);
            string role = WallStockConstants.ClassifyRole(stock);
            if (!string.IsNullOrWhiteSpace(stock) && !string.IsNullOrWhiteSpace(role))
            {
                return $"{role} ({stock})";
            }

            if (!string.IsNullOrWhiteSpace(stock))
            {
                return stock;
            }

            return "unknown stock";
        }

        private static string TryGetPropertyValue(PartDocument partDocument, string setName, string propertyName)
        {
            try
            {
                PropertySets propertySets = partDocument.PropertySets;
                PropertySet propertySet = propertySets[setName];
                Property property = propertySet[propertyName];
                return property.Value?.ToString();
            }
            catch
            {
                return null;
            }
        }
    }
}
