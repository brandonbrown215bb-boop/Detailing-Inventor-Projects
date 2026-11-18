using System;
using Inventor;

namespace UnitConstructionVerifier.Operations
{
    /// <summary>Finds an appearance in a part or assembly, copying it in from Inventor's libraries when missing.</summary>
    internal static class AppearanceLookup
    {
        /// <param name="localAssets">The document's <c>AppearanceAssets</c>.</param>
        /// <param name="document">The part or assembly the library copy goes into.</param>
        public static Asset? FindOrCopy(Inventor.Application? app, AssetsEnumerator localAssets, object document, string name)
        {
            // A copied library asset keeps an internal Name (e.g. "1:Pink"); only DisplayName matches the
            // config, and copying it in a second time throws.
            foreach (Asset asset in localAssets)
            {
                if (string.Equals(asset.DisplayName, name, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(asset.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return asset;
                }
            }

            if (app == null) return null;

            try { return app.ActiveAppearanceLibrary.AppearanceAssets[name].CopyTo(document); }
            catch { }

            try { return app.ActiveMaterialLibrary.AppearanceAssets[name].CopyTo(document); }
            catch { }

            return null;
        }
    }
}
