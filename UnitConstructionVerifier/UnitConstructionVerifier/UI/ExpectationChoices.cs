using System;
using System.Collections.Generic;

namespace UnitConstructionVerifier.UI
{
    /// <summary>
    /// Option lists for the expectation dropdowns. A surface's stored value is always selectable: a value
    /// missing from the standard list used to show blank and was then saved back as "" when any other
    /// field on the surface changed.
    /// </summary>
    public static class ExpectationChoices
    {
        public static readonly IReadOnlyList<string> YesNo = new[] { "Yes", "No" };
        public static readonly IReadOnlyList<string> BaseHeights = new[] { "6\"", "8\"", "10\"", "12\"" };
        public static readonly IReadOnlyList<string> BaseMaterials = new[] { "STL C CHNL", "ALM C CHNL" };

        /// <summary>
        /// Returns the options to show and the item to select for <paramref name="stored"/>.
        /// A blank stored value selects nothing. A value matching a standard option (ignoring case and
        /// surrounding spaces) selects that option. Any other value is put first in a copy of the list,
        /// unchanged, and selected.
        /// </summary>
        public static IReadOnlyList<string> WithStoredValue(IReadOnlyList<string> standard, string? stored, out string? selected)
        {
            string value = stored?.Trim() ?? string.Empty;
            if (value.Length == 0)
            {
                selected = null;
                return standard;
            }

            foreach (string option in standard)
            {
                if (string.Equals(option?.Trim(), value, StringComparison.OrdinalIgnoreCase))
                {
                    selected = option;
                    return standard;
                }
            }

            var withStored = new List<string>(standard.Count + 1) { stored! };
            withStored.AddRange(standard);
            selected = stored;
            return withStored;
        }
    }
}
