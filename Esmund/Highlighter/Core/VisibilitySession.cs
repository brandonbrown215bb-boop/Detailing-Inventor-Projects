using System;
using System.Collections.Generic;
using Inventor;

namespace Highlighter.Core
{
    /// <summary>
    /// Snapshot / restore occurrence Visible for selective surface focus.
    /// Nested occurrences often use short Names — ancestry is tracked with a walk flag,
    /// not string prefix matching (that hid skins and left only work points).
    /// </summary>
    internal sealed class VisibilitySession
    {
        private readonly Dictionary<string, bool> _original =
            new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        public bool HasSnapshot => _original.Count > 0;

        public void Begin(AssemblyDocument asm)
        {
            _original.Clear();
            if (asm?.ComponentDefinition?.Occurrences == null)
            {
                return;
            }

            Walk(asm.ComponentDefinition.Occurrences, capture: true, selected: null, underSelected: false, parentPath: null);
        }

        public void Restore(AssemblyDocument asm)
        {
            if (asm?.ComponentDefinition?.Occurrences == null || _original.Count == 0)
            {
                _original.Clear();
                return;
            }

            foreach (KeyValuePair<string, bool> kv in _original)
            {
                ComponentOccurrence occ = HighlightEngine.ResolvePath(asm.ComponentDefinition.Occurrences, kv.Key);
                if (occ == null)
                {
                    continue;
                }

                try { occ.Visible = kv.Value; } catch { }
            }

            _original.Clear();
        }

        /// <summary>
        /// Hide everything that is not under one of the selected surface roots.
        /// </summary>
        public void ApplySelectedOnly(AssemblyDocument asm, IEnumerable<string> selectedRootPaths)
        {
            if (asm?.ComponentDefinition?.Occurrences == null)
            {
                return;
            }

            var selected = new List<ComponentOccurrence>();
            var selectedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (selectedRootPaths != null)
            {
                foreach (string p in selectedRootPaths)
                {
                    if (string.IsNullOrWhiteSpace(p))
                    {
                        continue;
                    }

                    selectedNames.Add(p);
                    ComponentOccurrence occ = HighlightEngine.ResolvePath(asm.ComponentDefinition.Occurrences, p);
                    if (occ != null)
                    {
                        selected.Add(occ);
                    }
                }
            }

            Walk(asm.ComponentDefinition.Occurrences, capture: false, selected: selected, underSelected: false, parentPath: null);

            // VisTog/ICG fix: solids stay up; kill work-feature extents on host + selected trees.
            WorkFeatureHide.SuppressDocument((Document)(object)asm);
            foreach (ComponentOccurrence root in selected)
            {
                WorkFeatureHide.HideUnderOccurrence(root);
                try
                {
                    Document doc = null;
                    try { doc = root.ReferencedDocumentDescriptor.ReferencedDocument as Document; } catch { }
                    if (doc == null)
                    {
                        try { doc = root.Definition?.Document as Document; } catch { }
                    }

                    WorkFeatureHide.SuppressDocument(doc);
                }
                catch
                {
                }
            }
        }

        private void Walk(
            ComponentOccurrences occs,
            bool capture,
            List<ComponentOccurrence> selected,
            bool underSelected,
            string parentPath)
        {
            if (occs == null)
            {
                return;
            }

            for (int i = 1; i <= occs.Count; i++)
            {
                ComponentOccurrence occ;
                try { occ = occs[i]; }
                catch { continue; }
                WalkOcc(occ, capture, selected, underSelected, parentPath);
            }
        }

        private void WalkEnum(
            ComponentOccurrencesEnumerator occs,
            bool capture,
            List<ComponentOccurrence> selected,
            bool underSelected,
            string parentPath)
        {
            if (occs == null)
            {
                return;
            }

            for (int i = 1; i <= occs.Count; i++)
            {
                ComponentOccurrence occ;
                try { occ = occs[i]; }
                catch { continue; }
                WalkOcc(occ, capture, selected, underSelected, parentPath);
            }
        }

        private void WalkOcc(
            ComponentOccurrence occ,
            bool capture,
            List<ComponentOccurrence> selected,
            bool underSelected,
            string parentPath)
        {
            if (occ == null)
            {
                return;
            }

            string local = SafeName(occ);
            if (string.IsNullOrEmpty(local))
            {
                return;
            }

            string full = BuildFullPath(parentPath, local);
            bool isSelectedRoot = IsSelectedRoot(occ, selected, local, full);
            bool nowUnder = underSelected || isSelectedRoot;

            if (capture)
            {
                if (!_original.ContainsKey(full))
                {
                    try { _original[full] = occ.Visible; } catch { }
                }

                // Also index by local Name so ResolvePath/legacy lookups can restore.
                if (!string.Equals(full, local, StringComparison.OrdinalIgnoreCase)
                    && !_original.ContainsKey(local))
                {
                    try { _original[local] = occ.Visible; } catch { }
                }
            }
            else if (selected != null)
            {
                try { occ.Visible = nowUnder; } catch { }
            }

            try
            {
                if (occ.DefinitionDocumentType == DocumentTypeEnum.kAssemblyDocumentObject
                    && occ.SubOccurrences != null
                    && occ.SubOccurrences.Count > 0)
                {
                    WalkEnum(occ.SubOccurrences, capture, selected, nowUnder, full);
                }
            }
            catch
            {
            }
        }

        private static bool IsSelectedRoot(
            ComponentOccurrence occ,
            List<ComponentOccurrence> selected,
            string local,
            string full)
        {
            if (selected == null || selected.Count == 0)
            {
                return false;
            }

            foreach (ComponentOccurrence sel in selected)
            {
                if (ReferenceEquals(sel, occ))
                {
                    return true;
                }

                try
                {
                    if (string.Equals(sel.Name, local, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(sel.Name, full, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                catch
                {
                }
            }

            return false;
        }

        private static string BuildFullPath(string parentPath, string local)
        {
            if (string.IsNullOrEmpty(parentPath))
            {
                return local;
            }

            if (local.StartsWith(parentPath + ":", StringComparison.OrdinalIgnoreCase)
                || local.Equals(parentPath, StringComparison.OrdinalIgnoreCase))
            {
                return local;
            }

            return parentPath + ":" + local;
        }

        internal static bool IsUnderAnyRoot(string path, HashSet<string> roots)
        {
            if (string.IsNullOrEmpty(path) || roots == null || roots.Count == 0)
            {
                return false;
            }

            foreach (string root in roots)
            {
                if (path.Equals(root, StringComparison.OrdinalIgnoreCase)
                    || path.StartsWith(root + ":", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string SafeName(ComponentOccurrence occ)
        {
            try { return occ.Name; }
            catch { return null; }
        }
    }
}
