using System;
using System.Collections.Generic;
using Inventor;

namespace Highlighter.Core
{
    internal sealed class TransparencyRestore
    {
        public ComponentOccurrence Occurrence { get; set; }
        public bool WasTransparent { get; set; }
        public double WasOverrideOpacity { get; set; }
    }

    /// <summary>
    /// One-side primary-face outer + cut loops + occurrence translucency.
    /// </summary>
    internal static class HighlightEngine
    {
        public static ComponentOccurrence ResolvePath(ComponentOccurrences topOccs, string path)
        {
            if (topOccs == null || string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            try
            {
                return topOccs.ItemByName[path];
            }
            catch
            {
            }

            return FindByPath(topOccs, path);
        }

        public static void CollectOutlineItems(
            ComponentOccurrence occ,
            string path,
            HashSet<string> seen,
            List<object> items)
        {
            if (occ == null || items == null || seen == null)
            {
                return;
            }

            try
            {
                CollectPrimaryFaceOutline(occ.SurfaceBodies, path, seen, items);
            }
            catch
            {
            }
        }

        public static HighlightSet CreateHighlightSet(Document document)
        {
            try
            {
                return document.CreateHighlightSet();
            }
            catch
            {
                try
                {
                    return ((_Document)(object)document).CreateHighlightSet();
                }
                catch
                {
                    return null;
                }
            }
        }

        public static void Clear(HighlightSet set)
        {
            if (set == null)
            {
                return;
            }

            try
            {
                set.Clear();
            }
            catch
            {
            }
        }

        public static void TrySetColor(Application app, HighlightSet set, byte r, byte g, byte b)
        {
            if (app == null || set == null)
            {
                return;
            }

            try
            {
                set.Color = app.TransientObjects.CreateColor(r, g, b);
            }
            catch
            {
            }
        }

        public static bool TryAddToSet(HighlightSet set, object item)
        {
            try
            {
                set.AddItem(item);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static ComponentOccurrence FindByPath(ComponentOccurrences occurrences, string path)
        {
            if (occurrences == null)
            {
                return null;
            }

            for (int i = 1; i <= occurrences.Count; i++)
            {
                ComponentOccurrence occ;
                try
                {
                    occ = occurrences[i];
                }
                catch
                {
                    continue;
                }

                try
                {
                    if (string.Equals(occ.Name, path, StringComparison.OrdinalIgnoreCase))
                    {
                        return occ;
                    }
                }
                catch
                {
                }

                try
                {
                    if (occ.DefinitionDocumentType == DocumentTypeEnum.kAssemblyDocumentObject
                        && occ.SubOccurrences != null
                        && occ.SubOccurrences.Count > 0)
                    {
                        ComponentOccurrence nested = FindByPathEnum(occ.SubOccurrences, path);
                        if (nested != null)
                        {
                            return nested;
                        }
                    }
                }
                catch
                {
                }
            }

            return null;
        }

        private static ComponentOccurrence FindByPathEnum(ComponentOccurrencesEnumerator occurrences, string path)
        {
            if (occurrences == null)
            {
                return null;
            }

            for (int i = 1; i <= occurrences.Count; i++)
            {
                ComponentOccurrence occ;
                try
                {
                    occ = occurrences[i];
                }
                catch
                {
                    continue;
                }

                try
                {
                    if (string.Equals(occ.Name, path, StringComparison.OrdinalIgnoreCase))
                    {
                        return occ;
                    }
                }
                catch
                {
                }

                try
                {
                    if (occ.DefinitionDocumentType == DocumentTypeEnum.kAssemblyDocumentObject
                        && occ.SubOccurrences != null
                        && occ.SubOccurrences.Count > 0)
                    {
                        ComponentOccurrence nested = FindByPathEnum(occ.SubOccurrences, path);
                        if (nested != null)
                        {
                            return nested;
                        }
                    }
                }
                catch
                {
                }
            }

            return null;
        }

        private static void CollectPrimaryFaceOutline(
            SurfaceBodies bodies,
            string occKey,
            HashSet<string> seen,
            List<object> items)
        {
            if (bodies == null)
            {
                return;
            }

            Face primary = null;
            double bestArea = 0;
            try
            {
                for (int b = 1; b <= bodies.Count; b++)
                {
                    Faces faces = bodies[b].Faces;
                    for (int f = 1; f <= faces.Count; f++)
                    {
                        Face face = faces[f];
                        try
                        {
                            if (face.SurfaceType != SurfaceTypeEnum.kPlaneSurface)
                            {
                                continue;
                            }

                            double area = face.Evaluator.Area;
                            if (area > bestArea)
                            {
                                bestArea = area;
                                primary = face;
                            }
                        }
                        catch
                        {
                        }
                    }
                }
            }
            catch
            {
                return;
            }

            if (primary == null)
            {
                return;
            }

            try
            {
                EdgeLoops loops = primary.EdgeLoops;
                for (int l = 1; l <= loops.Count; l++)
                {
                    EdgeLoop loop = loops[l];
                    Edges edges = loop.Edges;
                    for (int e = 1; e <= edges.Count; e++)
                    {
                        Edge edge = edges[e];
                        string key = occKey + ":E:" + edge.TransientKey;
                        if (!seen.Add(key))
                        {
                            continue;
                        }

                        items.Add(edge);
                    }
                }
            }
            catch
            {
            }
        }
    }
}
