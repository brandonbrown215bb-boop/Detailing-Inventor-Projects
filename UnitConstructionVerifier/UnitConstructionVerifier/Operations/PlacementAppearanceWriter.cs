using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Inventor;

namespace UnitConstructionVerifier.Operations
{
    /// <summary>What <see cref="PlacementAppearanceWriter.ColorPlacements"/> did.</summary>
    public sealed class PlacementColorResult
    {
        public int PlacementsColored { get; set; }

        /// <summary>Assemblies now holding unsaved placement colors.</summary>
        public List<string> AssembliesChanged { get; } = new List<string>();

        /// <summary>One line per assembly or part that could not be colored.</summary>
        public List<string> Problems { get; } = new List<string>();

        /// <summary>Text to add to the write notice; empty when nothing was colored.</summary>
        public string Summary(string appearance) =>
            PlacementsColored == 0 ? string.Empty
                : $" Colored {UI.WriteReview.Plural(PlacementsColored, "placement")} {appearance} in "
                  + $"{UI.WriteReview.Plural(AssembliesChanged.Count, "assembly file")}; save the unit to keep them.";
    }

    /// <summary>
    /// Gives the placements of hard-edited parts the hard-edit appearance in the assemblies that place them.
    /// YCShell colors each placement by part type in the surface's "Default" design view; that override hides the
    /// part's own color on screen and in the PDW that Upload sends (ConvertToPDWFile reads faces in "Default").
    /// The assemblies are left unsaved: Inventor marks a surface unsaved as soon as its parts are newer than it, so
    /// UCV cannot tell a detailer's unsaved work apart, and YCShell's Upload saves every unsaved surface it sends.
    /// </summary>
    public sealed class PlacementAppearanceWriter
    {
        private readonly Inventor.Application _app;

        public PlacementAppearanceWriter(Inventor.Application app)
        {
            _app = app ?? throw new ArgumentNullException(nameof(app));
        }

        public PlacementColorResult ColorPlacements(AssemblyDocument unit, IEnumerable<string> partFiles, string appearance)
        {
            var result = new PlacementColorResult();
            if (string.IsNullOrWhiteSpace(appearance)) return result;

            // Placements grouped by the assembly that holds them (usually the surface IAM).
            var byAssembly = new Dictionary<string, (AssemblyDocument Doc, List<ComponentOccurrence> Occurrences)>(StringComparer.OrdinalIgnoreCase);

            foreach (string partFile in partFiles.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    if (!(_app.Documents.get_ItemByName(partFile) is Document part)) continue;

                    foreach (ComponentOccurrence occ in unit.ComponentDefinition.Occurrences.get_AllReferencedOccurrences(part))
                    {
                        ComponentOccurrence native = occ is ComponentOccurrenceProxy proxy ? proxy.NativeObject : occ;
                        if (!(((ComponentDefinition)native.Parent).Document is AssemblyDocument holder)) continue;

                        if (!byAssembly.TryGetValue(holder.FullFileName, out var entry))
                        {
                            entry = (holder, new List<ComponentOccurrence>());
                            byAssembly[holder.FullFileName] = entry;
                        }

                        entry.Occurrences.Add(native);
                    }
                }
                catch (Exception ex)
                {
                    result.Problems.Add($"{System.IO.Path.GetFileName(partFile)}: placements not found ({ex.Message})");
                }
            }

            foreach (var (doc, occurrences) in byAssembly.Values)
            {
                string name = System.IO.Path.GetFileName(doc.FullFileName);
                try
                {
                    // It could not be saved afterwards, so the color would only be lost.
                    if (new FileInfo(doc.FullFileName).IsReadOnly)
                    {
                        result.Problems.Add($"{name}: read-only (checked in to Vault?), so its placements were not colored.");
                        continue;
                    }

                    var failed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    int colored = ColorInEachView(doc, occurrences, appearance, failed);
                    if (colored < 0)
                    {
                        result.Problems.Add($"{name}: appearance '{appearance}' not found in Inventor's libraries.");
                        continue;
                    }

                    foreach (var failure in failed)
                    {
                        result.Problems.Add($"{name}: placement {failure.Key} was not colored ({failure.Value})");
                    }

                    if (colored > 0)
                    {
                        result.PlacementsColored += occurrences.Count - failed.Count;
                        result.AssembliesChanged.Add(doc.FullFileName);
                    }
                }
                catch (Exception ex)
                {
                    result.Problems.Add($"{name}: {ex.Message}");
                }
            }

            return result;
        }

        /// <summary>
        /// Sets the appearance in every unlocked design view, then returns to the view that was active.
        /// Returns how many (view, placement) pairs changed, or -1 when the appearance does not exist.
        /// A placement Inventor refuses goes into <paramref name="failed"/> (name → reason) and the rest carry on.
        /// </summary>
        private int ColorInEachView(AssemblyDocument doc, List<ComponentOccurrence> occurrences, string appearance,
            Dictionary<string, string> failed)
        {
            Asset? asset = AppearanceLookup.FindOrCopy(_app, doc.AppearanceAssets, doc, appearance);
            if (asset == null) return -1;

            RepresentationsManager reps = doc.ComponentDefinition.RepresentationsManager;
            // The name, not the object: once another view is active the old view object fails with E_FAIL.
            string original = reps.ActiveDesignViewRepresentation.Name;
            int changed = 0;

            // Names first: activating a view while enumerating the collection fails (E_FAIL).
            // Master is locked and shows the part's own color, which the part write already set.
            var views = new List<string>();
            foreach (DesignViewRepresentation view in reps.DesignViewRepresentations)
            {
                if (!view.Locked) views.Add(view.Name);
            }

            try
            {
                foreach (string viewName in views)
                {
                    if (reps.ActiveDesignViewRepresentation.Name != viewName) reps.DesignViewRepresentations[viewName].Activate();

                    foreach (ComponentOccurrence occ in occurrences)
                    {
                        string occName = string.Empty;
                        try
                        {
                            occName = occ.Name;
                            if (occ.AppearanceSourceType == AppearanceSourceTypeEnum.kOverrideAppearance &&
                                string.Equals(occ.Appearance?.DisplayName, asset.DisplayName, StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            occ.Appearance = asset;
                            changed++;
                        }
                        catch (Exception ex)
                        {
                            failed[occName.Length > 0 ? occName : "(unnamed)"] = ex.Message;
                        }
                    }
                }
            }
            finally
            {
                try
                {
                    if (reps.ActiveDesignViewRepresentation.Name != original) reps.DesignViewRepresentations[original].Activate();
                }
                catch (Exception ex)
                {
                    DebugLogger.Log(ex, $"PlacementAppearanceWriter: restoring design view '{original}'");
                }
            }

            return changed;
        }
    }
}
