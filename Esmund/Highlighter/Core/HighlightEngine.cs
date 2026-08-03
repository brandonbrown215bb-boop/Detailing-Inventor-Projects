using System;
using System.Collections.Generic;
using Inventor;

namespace Highlighter.Core
{
    internal sealed class TransparencyRestore
    {
        public ComponentOccurrence Occurrence { get; set; }
        public bool WasVisible { get; set; } = true;
        public bool WasTransparent { get; set; }
        public double WasOverrideOpacity { get; set; } = 1.0;
        public bool GhostApplied { get; set; }
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

            string[] segments = path.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
            {
                return null;
            }

            return ResolveSegments(topOccs, segments, 0);
        }

        private static ComponentOccurrence ResolveSegments(ComponentOccurrences occurrences, string[] segments, int index)
        {
            if (occurrences == null || index >= segments.Length)
            {
                return null;
            }

            string target = segments[index];
            string prefix = string.Join(":", segments, 0, index + 1);

            for (int i = 1; i <= occurrences.Count; i++)
            {
                ComponentOccurrence occ;
                try { occ = occurrences[i]; }
                catch { continue; }

                if (!NameMatchesSegment(occ, target, prefix))
                {
                    continue;
                }

                if (index == segments.Length - 1)
                {
                    return occ;
                }

                try
                {
                    if (occ.DefinitionDocumentType == DocumentTypeEnum.kAssemblyDocumentObject
                        && occ.SubOccurrences != null
                        && occ.SubOccurrences.Count > 0)
                    {
                        ComponentOccurrence nested = ResolveSegmentsEnum(occ.SubOccurrences, segments, index + 1);
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

        private static ComponentOccurrence ResolveSegmentsEnum(
            ComponentOccurrencesEnumerator occurrences,
            string[] segments,
            int index)
        {
            if (occurrences == null || index >= segments.Length)
            {
                return null;
            }

            string target = segments[index];
            string prefix = string.Join(":", segments, 0, index + 1);

            for (int i = 1; i <= occurrences.Count; i++)
            {
                ComponentOccurrence occ;
                try { occ = occurrences[i]; }
                catch { continue; }

                if (!NameMatchesSegment(occ, target, prefix))
                {
                    continue;
                }

                if (index == segments.Length - 1)
                {
                    return occ;
                }

                try
                {
                    if (occ.DefinitionDocumentType == DocumentTypeEnum.kAssemblyDocumentObject
                        && occ.SubOccurrences != null
                        && occ.SubOccurrences.Count > 0)
                    {
                        ComponentOccurrence nested = ResolveSegmentsEnum(occ.SubOccurrences, segments, index + 1);
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

        private static bool NameMatchesSegment(ComponentOccurrence occ, string segment, string fullPrefix)
        {
            string name;
            try { name = occ.Name; }
            catch { return false; }

            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            if (name.Equals(segment, StringComparison.OrdinalIgnoreCase)
                || name.Equals(fullPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string local = LocalName(name);
            return local.Equals(segment, StringComparison.OrdinalIgnoreCase);
        }

        private static string LocalName(string name)
        {
            int i = name.LastIndexOf(':');
            return i >= 0 ? name.Substring(i + 1) : name;
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

            SurfaceBodies bodies = TryGetSurfaceBodies(occ);
            if (bodies == null)
            {
                return;
            }

            try
            {
                CollectPrimaryFaceOutline(bodies, path, seen, items);
            }
            catch
            {
            }
        }

        private static SurfaceBodies TryGetSurfaceBodies(ComponentOccurrence occ)
        {
            try
            {
                SurfaceBodies bodies = occ.SurfaceBodies;
                if (bodies != null && bodies.Count > 0)
                {
                    return bodies;
                }
            }
            catch
            {
            }

            try
            {
                if (occ.Definition is PartComponentDefinition partDef)
                {
                    SurfaceBodies bodies = partDef.SurfaceBodies;
                    if (bodies != null && bodies.Count > 0)
                    {
                        return bodies;
                    }
                }
            }
            catch
            {
            }

            return null;
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

            Face primaryPlane = null;
            Face primaryAny = null;
            double bestPlaneArea = 0;
            double bestAnyArea = 0;
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
                            double area = face.Evaluator.Area;
                            if (area > bestAnyArea)
                            {
                                bestAnyArea = area;
                                primaryAny = face;
                            }

                            if (face.SurfaceType != SurfaceTypeEnum.kPlaneSurface)
                            {
                                continue;
                            }

                            if (area > bestPlaneArea)
                            {
                                bestPlaneArea = area;
                                primaryPlane = face;
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

            Face primary = primaryPlane ?? primaryAny;
            if (primary != null)
            {
                int before = items.Count;
                CollectFaceLoopEdges(primary, occKey, seen, items);
                if (items.Count > before)
                {
                    return;
                }
            }

            // Last resort: outer edges on every body (bent/odd skins that lack one dominant face).
            try
            {
                for (int b = 1; b <= bodies.Count; b++)
                {
                    CollectBodyEdges(bodies[b], occKey, seen, items);
                }
            }
            catch
            {
            }
        }

        private static void CollectFaceLoopEdges(
            Face primary,
            string occKey,
            HashSet<string> seen,
            List<object> items)
        {
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
                        AddEdge(edges[e], occKey, seen, items);
                    }
                }
            }
            catch
            {
            }
        }

        private static void CollectBodyEdges(
            SurfaceBody body,
            string occKey,
            HashSet<string> seen,
            List<object> items)
        {
            if (body == null)
            {
                return;
            }

            try
            {
                Edges edges = body.Edges;
                for (int e = 1; e <= edges.Count; e++)
                {
                    AddEdge(edges[e], occKey, seen, items);
                }
            }
            catch
            {
            }
        }

        private static void AddEdge(Edge edge, string occKey, HashSet<string> seen, List<object> items)
        {
            if (edge == null)
            {
                return;
            }

            try
            {
                string key = occKey + ":E:" + edge.TransientKey;
                if (!seen.Add(key))
                {
                    return;
                }

                items.Add(edge);
            }
            catch
            {
            }
        }
    }
}
