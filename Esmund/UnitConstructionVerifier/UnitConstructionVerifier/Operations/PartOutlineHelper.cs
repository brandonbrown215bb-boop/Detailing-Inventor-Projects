using System;
using System.Collections.Generic;
using Inventor;

namespace UnitConstructionVerifier.Operations
{
    /// <summary>
    /// Collects outer-boundary edges from part occurrences for HighlightSet outlining.
    /// Skips inner face loops (holes/cuts) to reduce work.
    /// </summary>
    internal static class PartOutlineHelper
    {
        private const double SecondaryFaceAreaRatio = 0.02;

        public static void CollectOuterOutlineEdges(
            ComponentOccurrence occurrence,
            string occurrenceKey,
            HashSet<string> seenEdges,
            List<object> outlineItems)
        {
            if (occurrence == null || seenEdges == null || outlineItems == null)
            {
                return;
            }

            SurfaceBodies bodies;
            try
            {
                bodies = occurrence.SurfaceBodies;
            }
            catch
            {
                return;
            }

            if (bodies == null || bodies.Count == 0)
            {
                return;
            }

            Face? primaryFace = FindLargestPlanarFace(bodies, out double primaryArea);
            double minSecondaryArea = primaryArea > 0
                ? primaryArea * SecondaryFaceAreaRatio
                : 0;

            try
            {
                for (int b = 1; b <= bodies.Count; b++)
                {
                    Faces faces = bodies[b].Faces;
                    for (int f = 1; f <= faces.Count; f++)
                    {
                        Face face = faces[f];
                        if (!ShouldOutlineFace(face, primaryFace, minSecondaryArea))
                        {
                            continue;
                        }

                        AddOuterLoopEdges(face, occurrenceKey, seenEdges, outlineItems);
                    }
                }
            }
            catch
            {
                // Best-effort outline collection.
            }
        }

        public static HighlightSet? CreateHighlightSet(Document document)
        {
            if (document == null)
            {
                return null;
            }

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

        public static void TrySetHighlightColor(Application app, HighlightSet set, byte r, byte g, byte b)
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

        public static void TryAddToHighlightSet(HighlightSet set, object item)
        {
            try
            {
                set.AddItem(item);
            }
            catch
            {
            }
        }

        private static Face? FindLargestPlanarFace(SurfaceBodies bodies, out double bestArea)
        {
            bestArea = 0;
            Face? primary = null;

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
            }

            return primary;
        }

        private static bool ShouldOutlineFace(Face face, Face? primaryFace, double minSecondaryArea)
        {
            if (face == null)
            {
                return false;
            }

            if (primaryFace != null && ReferenceEquals(face, primaryFace))
            {
                return true;
            }

            try
            {
                if (face.SurfaceType != SurfaceTypeEnum.kPlaneSurface)
                {
                    return false;
                }

                if (minSecondaryArea <= 0)
                {
                    return false;
                }

                return face.Evaluator.Area >= minSecondaryArea;
            }
            catch
            {
                return false;
            }
        }

        private static void AddOuterLoopEdges(
            Face face,
            string occurrenceKey,
            HashSet<string> seenEdges,
            List<object> outlineItems)
        {
            EdgeLoop? outerLoop = FindOuterEdgeLoop(face);
            if (outerLoop == null)
            {
                return;
            }

            try
            {
                Edges edges = outerLoop.Edges;
                for (int e = 1; e <= edges.Count; e++)
                {
                    Edge edge = edges[e];
                    string key = occurrenceKey + ":E:" + edge.TransientKey;
                    if (!seenEdges.Add(key))
                    {
                        continue;
                    }

                    outlineItems.Add(edge);
                }
            }
            catch
            {
            }
        }

        private static EdgeLoop? FindOuterEdgeLoop(Face face)
        {
            try
            {
                EdgeLoops loops = face.EdgeLoops;
                for (int l = 1; l <= loops.Count; l++)
                {
                    EdgeLoop loop = loops[l];
                    try
                    {
                        if (loop.IsOuterEdgeLoop)
                        {
                            return loop;
                        }
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }

            return null;
        }
    }
}
