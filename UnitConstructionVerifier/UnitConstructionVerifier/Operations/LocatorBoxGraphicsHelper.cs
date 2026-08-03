using System;
using System.Collections.Generic;
using Inventor;

namespace UnitConstructionVerifier.Operations
{
    /// <summary>
    /// Draws part/surface locator boxes via ClientGraphics using each definition's local RangeBox
    /// (oriented to internal XYZ) transformed into the host assembly space.
    /// </summary>
    internal static class LocatorBoxGraphicsHelper
    {
        private const string ClientGraphicsName = "UCV_LocatorBox";
        private const string DataSetName = "UCV_LocatorBoxData";

        private static readonly (int A, int B)[] BoxEdges =
        {
            (0, 1), (2, 3), (4, 5), (6, 7),
            (0, 2), (1, 3), (4, 6), (5, 7),
            (0, 4), (1, 5), (2, 6), (3, 7),
        };

        public static void Clear(AssemblyDocument? assemblyDocument)
        {
            if (assemblyDocument?.ComponentDefinition == null)
            {
                return;
            }

            try
            {
                ClientGraphicsCollection graphicsCollection = assemblyDocument.ComponentDefinition.ClientGraphicsCollection;
                try { graphicsCollection[ClientGraphicsName].Delete(); } catch { }
            }
            catch
            {
            }

            try
            {
                Document document = (Document)(object)assemblyDocument;
                GraphicsDataSetsCollection dataSets = document.GraphicsDataSetsCollection;
                try { dataSets[DataSetName].Delete(); } catch { }
            }
            catch
            {
            }
        }

        public static void DrawAssemblyRangeBox(Application app, AssemblyDocument hostAssembly, byte r, byte g, byte b)
        {
            if (app == null || hostAssembly?.ComponentDefinition == null)
            {
                return;
            }

            try
            {
                Box rangeBox = hostAssembly.ComponentDefinition.RangeBox;
                Matrix identity = app.TransientGeometry.CreateMatrix();
                DrawBoxes(app, hostAssembly, new[] { (rangeBox, identity) }, r, g, b);
            }
            catch
            {
            }
        }

        public static void DrawOccurrenceBoxes(
            Application app,
            AssemblyDocument hostAssembly,
            IList<ComponentOccurrence> occurrences,
            byte r,
            byte g,
            byte b)
        {
            if (app == null || hostAssembly?.ComponentDefinition == null || occurrences == null || occurrences.Count == 0)
            {
                return;
            }

            var boxes = new List<(Box RangeBox, Matrix Transform)>();
            foreach (ComponentOccurrence occurrence in occurrences)
            {
                if (!TryGetLocalRangeBox(occurrence, out Box rangeBox))
                {
                    continue;
                }

                if (!TryGetTransformToHost(app, occurrence, hostAssembly, out Matrix transform))
                {
                    continue;
                }

                boxes.Add((rangeBox, transform));
            }

            if (boxes.Count == 0)
            {
                return;
            }

            DrawBoxes(app, hostAssembly, boxes, r, g, b);
        }

        private static void DrawBoxes(
            Application app,
            AssemblyDocument hostAssembly,
            IList<(Box RangeBox, Matrix Transform)> boxes,
            byte r,
            byte g,
            byte b)
        {
            Clear(hostAssembly);

            try
            {
                TransientGeometry tg = app.TransientGeometry;
                Document document = (Document)(object)hostAssembly;
                GraphicsDataSets dataSets = document.GraphicsDataSetsCollection.Add(DataSetName);
                ClientGraphics graphics = hostAssembly.ComponentDefinition.ClientGraphicsCollection.Add(ClientGraphicsName);

                GraphicsColorSet colorSet = dataSets.CreateColorSet(1);
                colorSet.Add(1, r, g, b);

                int nodeId = 1;
                int coordinateSetId = 1;

                foreach ((Box rangeBox, Matrix transform) in boxes)
                {
                    Point[] corners = CreateLocalCorners(tg, rangeBox);
                    var assemblyCorners = new Point[8];
                    for (int i = 0; i < corners.Length; i++)
                    {
                        assemblyCorners[i] = TransformPointCopy(tg, corners[i], transform);
                    }

                    GraphicsNode node = graphics.AddNode(nodeId++);

                    foreach ((int a, int bIndex) in BoxEdges)
                    {
                        LineStripGraphics strip = node.AddLineStripGraphics();
                        strip.ColorSet = colorSet;
                        strip.ColorBinding = ColorBindingEnum.kOverallColor;
                        try { strip.LineDefinitionSpace = LineDefinitionSpaceEnum.kModelSpace; } catch { }
                        try { strip.LineWeight = 3; } catch { }
                        try { strip.BurnThrough = true; } catch { }

                        double[] edgeCoords =
                        {
                            assemblyCorners[a].X, assemblyCorners[a].Y, assemblyCorners[a].Z,
                            assemblyCorners[bIndex].X, assemblyCorners[bIndex].Y, assemblyCorners[bIndex].Z,
                        };
                        GraphicsCoordinateSet edgeCoordinateSet = dataSets.CreateCoordinateSet(coordinateSetId++);
                        edgeCoordinateSet.PutCoordinates(ref edgeCoords);
                        strip.CoordinateSet = edgeCoordinateSet;
                    }
                }
            }
            catch
            {
                Clear(hostAssembly);
            }
        }

        private static Point[] CreateLocalCorners(TransientGeometry tg, Box rangeBox)
        {
            Point min = rangeBox.MinPoint;
            Point max = rangeBox.MaxPoint;

            return new[]
            {
                tg.CreatePoint(min.X, min.Y, min.Z),
                tg.CreatePoint(min.X, min.Y, max.Z),
                tg.CreatePoint(min.X, max.Y, min.Z),
                tg.CreatePoint(min.X, max.Y, max.Z),
                tg.CreatePoint(max.X, min.Y, min.Z),
                tg.CreatePoint(max.X, min.Y, max.Z),
                tg.CreatePoint(max.X, max.Y, min.Z),
                tg.CreatePoint(max.X, max.Y, max.Z),
            };
        }

        private static bool TryGetLocalRangeBox(ComponentOccurrence occurrence, out Box rangeBox)
        {
            rangeBox = null!;

            try
            {
                ComponentDefinition definition = occurrence.Definition;
                if (definition is PartComponentDefinition partDefinition)
                {
                    rangeBox = partDefinition.RangeBox;
                    return true;
                }

                if (definition is AssemblyComponentDefinition assemblyDefinition)
                {
                    rangeBox = assemblyDefinition.RangeBox;
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private static bool TryGetTransformToHost(
            Application app,
            ComponentOccurrence occurrence,
            AssemblyDocument hostAssembly,
            out Matrix transform)
        {
            transform = app.TransientGeometry.CreateMatrix();

            var chain = new List<ComponentOccurrence>();
            ComponentOccurrence? current = occurrence;
            int guard = 0;
            while (current != null && guard++ < 64)
            {
                chain.Insert(0, current);
                try
                {
                    current = current.ParentOccurrence;
                }
                catch
                {
                    break;
                }
            }

            if (chain.Count == 0)
            {
                return false;
            }

            bool topLevel = false;
            foreach (ComponentOccurrence top in hostAssembly.ComponentDefinition.Occurrences)
            {
                if (ReferenceEquals(top, chain[0]))
                {
                    topLevel = true;
                    break;
                }
            }

            if (!topLevel)
            {
                return false;
            }

            Matrix result = app.TransientGeometry.CreateMatrix();
            foreach (ComponentOccurrence chainOccurrence in chain)
            {
                result.PostMultiplyBy(chainOccurrence.Transformation);
            }

            transform = result;
            return true;
        }

        private static Point TransformPointCopy(TransientGeometry tg, Point point, Matrix transform)
        {
            Point copy = tg.CreatePoint(point.X, point.Y, point.Z);
            copy.TransformBy(transform);
            return copy;
        }
    }
}
