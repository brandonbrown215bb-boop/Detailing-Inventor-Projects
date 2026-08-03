using System;
using System.Collections.Generic;
using Inventor;

namespace UnitConstructionVerifier.Operations
{
    /// <summary>
    /// Highlights surfaces and parts in the active assembly.
    /// Default mode draws oriented locator boxes; optional X-Ray mode outlines matching geometry
    /// within the active surface using Design Tracking Part Number.
    /// </summary>
    internal static class InventorSelectionHelper
    {
        private const string DesignTrackingProperties = "Design Tracking Properties";
        private const string PartNumberProperty = "Part Number";

        private static HighlightSet? _outlineSet;
        private static readonly List<TransparencyRestore> _transparencyRestores = new List<TransparencyRestore>();
        private static bool _prehighlightCaptured;
        private static bool _savedEnablePrehighlight = true;
        private static byte _outlineR = XRayHighlightColors.Default.R;
        private static byte _outlineG = XRayHighlightColors.Default.G;
        private static byte _outlineB = XRayHighlightColors.Default.B;
        private static AssemblyDocument? _locatorBoxHost;
        private static List<ComponentOccurrence>? _locatorBoxOccurrences;
        private static bool _locatorBoxFullAssembly;

        public static void SetXRayOutlineColor(byte r, byte g, byte b)
        {
            _outlineR = r;
            _outlineG = g;
            _outlineB = b;
        }

        public static (byte R, byte G, byte B) GetXRayOutlineColor() => (_outlineR, _outlineG, _outlineB);

        public static void ApplyXRayOutlineColorToActiveSet(Application app)
        {
            if (_outlineSet != null)
            {
                PartOutlineHelper.TrySetHighlightColor(app, _outlineSet, _outlineR, _outlineG, _outlineB);
            }

            if (_locatorBoxHost != null)
            {
                if (_locatorBoxFullAssembly)
                {
                    LocatorBoxGraphicsHelper.DrawAssemblyRangeBox(app, _locatorBoxHost, _outlineR, _outlineG, _outlineB);
                }
                else if (_locatorBoxOccurrences != null && _locatorBoxOccurrences.Count > 0)
                {
                    LocatorBoxGraphicsHelper.DrawOccurrenceBoxes(
                        app,
                        _locatorBoxHost,
                        _locatorBoxOccurrences,
                        _outlineR,
                        _outlineG,
                        _outlineB);
                }
            }

            try { app.ActiveView?.Update(); } catch { }
        }

        private sealed class TransparencyRestore
        {
            public ComponentOccurrence Occurrence { get; set; } = null!;
            public bool WasTransparent { get; set; }
            public double WasOverrideOpacity { get; set; }
        }

        public static void HighlightByFilePath(Application app, string rootIamPath, string targetFilePath)
        {
            if (app == null || string.IsNullOrWhiteSpace(targetFilePath))
            {
                return;
            }

            try
            {
                AssemblyDocument? asmDoc = ResolveHighlightAssembly(app, rootIamPath, targetFilePath);
                if (asmDoc == null)
                {
                    return;
                }

                if (IsSamePath(asmDoc.FullFileName, targetFilePath))
                {
                    BoxOutlineAssembly(app, asmDoc);
                    return;
                }

                var matches = new List<ComponentOccurrence>();
                foreach (ComponentOccurrence top in asmDoc.ComponentDefinition.Occurrences)
                {
                    CollectOccurrencesByFilePath(top, targetFilePath, matches);
                }

                if (matches.Count > 0)
                {
                    BoxOutlineOccurrences(app, asmDoc, matches);
                    return;
                }

                ComponentOccurrence? occurrence =
                    FindFirstOccurrenceByFilePath(asmDoc.ComponentDefinition.Occurrences, targetFilePath);
                if (occurrence == null)
                {
                    return;
                }

                BoxOutlineOccurrences(app, asmDoc, new[] { occurrence });
            }
            catch
            {
                // Highlighting is best-effort; do not interrupt the verifier UI.
            }
        }

        /// <summary>
        /// Highlights every matching part occurrence within one surface subassembly.
        /// </summary>
        public static void HighlightPartInSurface(
            Application app,
            string rootIamPath,
            string surfaceIamPath,
            string partNumber,
            string? partFilePath,
            bool useXRay)
        {
            if (app == null ||
                string.IsNullOrWhiteSpace(surfaceIamPath) ||
                (string.IsNullOrWhiteSpace(partNumber) && string.IsNullOrWhiteSpace(partFilePath)))
            {
                return;
            }

            try
            {
                AssemblyDocument? asmDoc = ResolveHighlightAssembly(app, rootIamPath, surfaceIamPath);
                if (asmDoc == null)
                {
                    return;
                }

                ComponentOccurrence? scopeRoot = null;
                if (!IsSurfaceAssemblyActive(asmDoc, surfaceIamPath))
                {
                    scopeRoot = FindFirstOccurrenceByFilePath(asmDoc.ComponentDefinition.Occurrences, surfaceIamPath);
                    if (scopeRoot == null)
                    {
                        return;
                    }
                }

                var matches = new List<ComponentOccurrence>();
                CollectMatchingPartsInScope(scopeRoot, asmDoc, partNumber, partFilePath, matches);

                if (matches.Count == 0)
                {
                    return;
                }

                if (useXRay)
                {
                    OutlineOccurrences(app, asmDoc, matches);
                }
                else
                {
                    BoxOutlineOccurrences(app, asmDoc, matches);
                }
            }
            catch
            {
                // Highlighting is best-effort; do not interrupt the verifier UI.
            }
        }

        public static ComponentOccurrence? FindFirstPartOccurrenceInSurface(
            Application app,
            string rootIamPath,
            string surfaceIamPath,
            string partNumber,
            string? partFilePath)
        {
            if (app == null ||
                string.IsNullOrWhiteSpace(surfaceIamPath) ||
                (string.IsNullOrWhiteSpace(partNumber) && string.IsNullOrWhiteSpace(partFilePath)))
            {
                return null;
            }

            try
            {
                AssemblyDocument? asmDoc = ResolveHighlightAssembly(app, rootIamPath, surfaceIamPath);
                if (asmDoc == null)
                {
                    return null;
                }

                ComponentOccurrence? scopeRoot = null;
                if (!IsSurfaceAssemblyActive(asmDoc, surfaceIamPath))
                {
                    scopeRoot = FindFirstOccurrenceByFilePath(asmDoc.ComponentDefinition.Occurrences, surfaceIamPath);
                    if (scopeRoot == null)
                    {
                        return null;
                    }
                }

                var matches = new List<ComponentOccurrence>();
                CollectMatchingPartsInScope(scopeRoot, asmDoc, partNumber, partFilePath, matches);

                return matches.Count > 0 ? matches[0] : null;
            }
            catch
            {
                return null;
            }
        }

        public static void ClearHighlight(Application? app = null, AssemblyDocument? assemblyDocument = null)
        {
            ClearOutlineHighlight();
            ClearLocatorBoxHighlight(assemblyDocument);
            RestorePartTransparency();
            RestorePrehighlight(app);

            AssemblyDocument? asm = assemblyDocument;
            if (asm == null && app?.ActiveDocument is AssemblyDocument activeAsm)
            {
                asm = activeAsm;
            }

            if (asm != null)
            {
                try { asm.SelectSet.Clear(); } catch { }
            }

            if (app != null)
            {
                try { app.ActiveView?.Update(); } catch { }
            }
        }

        /// <summary>
        /// Clears verifier highlights and restores Inventor native prehighlight (green hover selection).
        /// Same as Highlighter's Normal button.
        /// </summary>
        public static void RestoreNormal(Application? app)
        {
            ClearHighlight(app);
        }

        public static bool IsPrehighlightSuppressed => _prehighlightCaptured;

        private static void BoxOutlineAssembly(Application app, AssemblyDocument assemblyDocument)
        {
            PrepareForNewHighlight(app, assemblyDocument);

            LocatorBoxGraphicsHelper.DrawAssemblyRangeBox(app, assemblyDocument, _outlineR, _outlineG, _outlineB);
            _locatorBoxHost = assemblyDocument;
            _locatorBoxOccurrences = null;
            _locatorBoxFullAssembly = true;

            try { app.ActiveView?.Update(); } catch { }
        }

        private static void BoxOutlineOccurrences(
            Application app,
            AssemblyDocument assemblyDocument,
            IList<ComponentOccurrence> occurrences)
        {
            if (occurrences.Count == 0)
            {
                return;
            }

            PrepareForNewHighlight(app, assemblyDocument);

            LocatorBoxGraphicsHelper.DrawOccurrenceBoxes(
                app,
                assemblyDocument,
                occurrences,
                _outlineR,
                _outlineG,
                _outlineB);

            _locatorBoxHost = assemblyDocument;
            _locatorBoxOccurrences = new List<ComponentOccurrence>(occurrences);
            _locatorBoxFullAssembly = false;

            try { app.ActiveView?.Update(); } catch { }
        }

        private static void PrepareForNewHighlight(Application app, AssemblyDocument assemblyDocument)
        {
            ClearOutlineHighlight();
            ClearLocatorBoxHighlight(assemblyDocument);
            RestorePartTransparency();
            RestorePrehighlight(app);

            try { assemblyDocument.SelectSet.Clear(); } catch { }
        }

        private static void OutlineOccurrences(
            Application app,
            AssemblyDocument assemblyDocument,
            IList<ComponentOccurrence> occurrences)
        {
            if (occurrences.Count == 0)
            {
                return;
            }

            PrepareForNewHighlight(app, assemblyDocument);
            _locatorBoxHost = null;
            _locatorBoxOccurrences = null;
            _locatorBoxFullAssembly = false;

            bool savedScreenUpdating = true;
            try { savedScreenUpdating = app.ScreenUpdating; } catch { }
            try { app.ScreenUpdating = false; } catch { }

            try
            {
                var outlineItems = new List<object>();
                var seenEdges = new HashSet<string>(StringComparer.Ordinal);

                foreach (ComponentOccurrence occurrence in occurrences)
                {
                    ApplyPartXRayTransparency(occurrence);

                    string occurrenceKey;
                    try { occurrenceKey = occurrence.Name; }
                    catch { occurrenceKey = occurrence.GetHashCode().ToString(); }

                    PartOutlineHelper.CollectOuterOutlineEdges(
                        occurrence,
                        occurrenceKey,
                        seenEdges,
                        outlineItems);
                }

                if (outlineItems.Count == 0)
                {
                    RestorePartTransparency();
                    return;
                }

                DisablePrehighlight(app);

                Document host = (Document)(object)assemblyDocument;
                _outlineSet = PartOutlineHelper.CreateHighlightSet(host);
                if (_outlineSet == null)
                {
                    return;
                }

                PartOutlineHelper.TrySetHighlightColor(app, _outlineSet, _outlineR, _outlineG, _outlineB);
                foreach (object item in outlineItems)
                {
                    PartOutlineHelper.TryAddToHighlightSet(_outlineSet, item);
                }
            }
            finally
            {
                try { app.ScreenUpdating = savedScreenUpdating; } catch { }
            }

            try { app.ActiveView?.Update(); } catch { }
        }

        private static void ClearOutlineHighlight()
        {
            if (_outlineSet == null)
            {
                return;
            }

            try { _outlineSet.Clear(); } catch { }
            try { _outlineSet.Delete(); } catch { }
            _outlineSet = null;
        }

        private static void ClearLocatorBoxHighlight(AssemblyDocument? assemblyDocument)
        {
            if (_locatorBoxHost != null &&
                (assemblyDocument == null || !ReferenceEquals(_locatorBoxHost, assemblyDocument)))
            {
                LocatorBoxGraphicsHelper.Clear(_locatorBoxHost);
            }

            if (assemblyDocument != null)
            {
                LocatorBoxGraphicsHelper.Clear(assemblyDocument);
            }

            _locatorBoxHost = null;
            _locatorBoxOccurrences = null;
            _locatorBoxFullAssembly = false;
        }

        private static void ApplyPartXRayTransparency(ComponentOccurrence occurrence)
        {
            try
            {
                bool wasTransparent = false;
                try { wasTransparent = occurrence.Transparent; } catch { }

                double wasOpacity = 1.0;
                try { wasOpacity = occurrence.OverrideOpacity; } catch { }

                _transparencyRestores.Add(new TransparencyRestore
                {
                    Occurrence = occurrence,
                    WasTransparent = wasTransparent,
                    WasOverrideOpacity = wasOpacity
                });

                occurrence.Transparent = true;
                occurrence.OverrideOpacity = 0.0;
            }
            catch
            {
                // Skip occurrences that cannot be made transparent.
            }
        }

        private static void RestorePartTransparency()
        {
            foreach (TransparencyRestore entry in _transparencyRestores)
            {
                try { entry.Occurrence.OverrideOpacity = entry.WasOverrideOpacity; } catch { }
                try { entry.Occurrence.Transparent = entry.WasTransparent; } catch { }
            }

            _transparencyRestores.Clear();
        }

        private static void DisablePrehighlight(Application app)
        {
            try
            {
                if (!_prehighlightCaptured)
                {
                    _savedEnablePrehighlight = app.ColorSchemes.EnablePrehighlight;
                    _prehighlightCaptured = true;
                }

                app.ColorSchemes.EnablePrehighlight = false;
            }
            catch
            {
            }
        }

        private static void RestorePrehighlight(Application? app)
        {
            if (!_prehighlightCaptured || app == null)
            {
                return;
            }

            try
            {
                app.ColorSchemes.EnablePrehighlight = _savedEnablePrehighlight;
            }
            catch
            {
            }

            _prehighlightCaptured = false;
        }

        private static AssemblyDocument? ResolveHighlightAssembly(
            Application app,
            string rootIamPath,
            string? contextIamPath = null)
        {
            AssemblyDocument? activeAsm = app.ActiveDocument as AssemblyDocument;

            if (activeAsm != null &&
                !string.IsNullOrWhiteSpace(contextIamPath) &&
                IsSamePath(activeAsm.FullFileName, contextIamPath))
            {
                return activeAsm;
            }

            if (activeAsm != null &&
                (string.IsNullOrWhiteSpace(rootIamPath) ||
                 IsSamePath(activeAsm.FullFileName, rootIamPath)))
            {
                return activeAsm;
            }

            if (string.IsNullOrWhiteSpace(rootIamPath))
            {
                return activeAsm;
            }

            foreach (Document document in app.Documents)
            {
                if (IsSamePath(document.FullFileName, rootIamPath) &&
                    document is AssemblyDocument rootAsm)
                {
                    if (activeAsm == null ||
                        string.IsNullOrWhiteSpace(contextIamPath) ||
                        !IsSamePath(activeAsm.FullFileName, contextIamPath))
                    {
                        try { document.Activate(); } catch { }
                    }

                    return rootAsm;
                }
            }

            return activeAsm;
        }

        private static bool IsSurfaceAssemblyActive(AssemblyDocument asmDoc, string surfaceIamPath)
        {
            return IsSamePath(asmDoc.FullFileName, surfaceIamPath);
        }

        private static void CollectMatchingPartsInScope(
            ComponentOccurrence? scopeRoot,
            AssemblyDocument asmDoc,
            string partNumber,
            string? partFilePath,
            List<ComponentOccurrence> matches)
        {
            if (scopeRoot != null)
            {
                if (!string.IsNullOrWhiteSpace(partNumber))
                {
                    CollectOccurrencesByPartNumber(scopeRoot, partNumber.Trim(), matches);
                }

                if (matches.Count == 0 && !string.IsNullOrWhiteSpace(partFilePath))
                {
                    CollectOccurrencesByFilePath(scopeRoot, partFilePath, matches);
                }

                return;
            }

            foreach (ComponentOccurrence top in asmDoc.ComponentDefinition.Occurrences)
            {
                if (!string.IsNullOrWhiteSpace(partNumber))
                {
                    CollectOccurrencesByPartNumber(top, partNumber.Trim(), matches);
                }
            }

            if (matches.Count == 0 && !string.IsNullOrWhiteSpace(partFilePath))
            {
                foreach (ComponentOccurrence top in asmDoc.ComponentDefinition.Occurrences)
                {
                    CollectOccurrencesByFilePath(top, partFilePath, matches);
                }
            }
        }

        private static bool IsSamePath(string? left, string? right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            {
                return false;
            }

            return string.Equals(NormalizePath(left), NormalizePath(right), StringComparison.OrdinalIgnoreCase);
        }

        private static ComponentOccurrence? FindFirstOccurrenceByFilePath(
            ComponentOccurrences occurrences,
            string targetFilePath)
        {
            string normalizedTarget = NormalizePath(targetFilePath);

            foreach (ComponentOccurrence occurrence in occurrences)
            {
                ComponentOccurrence? found = FindFirstOccurrenceRecursive(occurrence, normalizedTarget);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static ComponentOccurrence? FindFirstOccurrenceRecursive(
            ComponentOccurrence occurrence,
            string normalizedTarget)
        {
            try
            {
                if (OccurrenceMatchesPath(occurrence, normalizedTarget))
                {
                    return occurrence;
                }

                foreach (ComponentOccurrence child in occurrence.SubOccurrences)
                {
                    ComponentOccurrence? found = FindFirstOccurrenceRecursive(child, normalizedTarget);
                    if (found != null)
                    {
                        return found;
                    }
                }
            }
            catch
            {
                // Skip inaccessible references.
            }

            return null;
        }

        private static void CollectOccurrencesByPartNumber(
            ComponentOccurrence occurrence,
            string targetPartNumber,
            List<ComponentOccurrence> results)
        {
            try
            {
                if (TryReadPartNumber(occurrence, out string partNumber) &&
                    string.Equals(partNumber, targetPartNumber, StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(occurrence);
                }

                foreach (ComponentOccurrence child in occurrence.SubOccurrences)
                {
                    CollectOccurrencesByPartNumber(child, targetPartNumber, results);
                }
            }
            catch
            {
                // Skip inaccessible references.
            }
        }

        private static void CollectOccurrencesByFilePath(
            ComponentOccurrence occurrence,
            string targetFilePath,
            List<ComponentOccurrence> results)
        {
            string normalizedTarget = NormalizePath(targetFilePath);

            try
            {
                if (OccurrenceMatchesPath(occurrence, normalizedTarget))
                {
                    results.Add(occurrence);
                }

                foreach (ComponentOccurrence child in occurrence.SubOccurrences)
                {
                    CollectOccurrencesByFilePath(child, targetFilePath, results);
                }
            }
            catch
            {
                // Skip inaccessible references.
            }
        }

        private static bool TryReadPartNumber(ComponentOccurrence occurrence, out string partNumber)
        {
            partNumber = string.Empty;

            try
            {
                if (occurrence.DefinitionDocumentType != DocumentTypeEnum.kPartDocumentObject)
                {
                    return false;
                }

                if (occurrence.Definition?.Document is not PartDocument partDoc)
                {
                    return false;
                }

                PropertySet tracking = partDoc.PropertySets[DesignTrackingProperties];
                object? value = tracking[PartNumberProperty].Value;
                partNumber = value?.ToString()?.Trim() ?? string.Empty;
                return !string.IsNullOrWhiteSpace(partNumber);
            }
            catch
            {
                return false;
            }
        }

        private static bool OccurrenceMatchesPath(ComponentOccurrence occurrence, string normalizedTarget)
        {
            try
            {
                Document? referencedDocument = occurrence.Definition?.Document as Document;
                if (referencedDocument != null &&
                    string.Equals(NormalizePath(referencedDocument.FullFileName), normalizedTarget, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            catch
            {
                // Fall through to descriptor-based match.
            }

            try
            {
                string descriptorPath = occurrence.ReferencedDocumentDescriptor.FullDocumentName;
                if (!string.IsNullOrWhiteSpace(descriptorPath) &&
                    string.Equals(NormalizePath(descriptorPath), normalizedTarget, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            catch
            {
                // No match for this occurrence.
            }

            return false;
        }

        private static string NormalizePath(string path)
        {
            try
            {
                return System.IO.Path.GetFullPath(path.Trim());
            }
            catch
            {
                return path.Trim();
            }
        }
    }
}
