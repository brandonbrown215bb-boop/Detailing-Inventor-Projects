using System;
using Inventor;

namespace UnitConstructionVerifier.Extraction
{
    /// <summary><see cref="IPartPropertySource"/> over a live Inventor part document.</summary>
    internal sealed class PartDocumentSource : IPartPropertySource
    {
        private readonly PartDocument _doc;

        public PartDocumentSource(PartDocument doc) => _doc = doc;

        public string FilePath => _doc.FullFileName;

        public string ReadProperty(string setName, string propertyName)
        {
            try { return _doc.PropertySets[setName][propertyName]?.Value?.ToString() ?? string.Empty; }
            catch { return string.Empty; }
        }

        public bool HasAttributeSet(string setName) => FindSet(setName) != null;

        public string ReadAttribute(string setName, string attributeName)
        {
            try
            {
                var set = FindSet(setName);
                if (set == null) return string.Empty;
                foreach (Inventor.Attribute attr in set)
                    if (string.Equals(attr.Name, attributeName, StringComparison.OrdinalIgnoreCase))
                        return attr.Value?.ToString() ?? string.Empty;
            }
            catch { }
            return string.Empty;
        }

        public string ActiveMaterialName()
        {
            try { return _doc.ActiveMaterial?.Name ?? string.Empty; }
            catch { return string.Empty; }
        }

        public string SheetMetalThicknessInches()
        {
            try
            {
                if (_doc.ComponentDefinition is SheetMetalComponentDefinition smDef)
                {
                    double inches = Convert.ToDouble(smDef.Thickness.Value) / 2.54;
                    return inches.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);
                }
            }
            catch { }
            return string.Empty;
        }

        private AttributeSet? FindSet(string setName)
        {
            try
            {
                foreach (AttributeSet set in _doc.AttributeSets)
                    if (string.Equals(set.Name, setName, StringComparison.OrdinalIgnoreCase))
                        return set;
            }
            catch { }
            return null;
        }
    }
}
