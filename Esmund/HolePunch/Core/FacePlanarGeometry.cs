using System;
using System.Collections.Generic;
using Inventor;

namespace SkinChannelPunch.Core
{
    /// <summary>
    /// Rectangle-like planar face metrics in part space (cm).
    /// Up prefers the skin IPT part +Z axis (direction only); falls back to longest edge.
    /// Origin remains the bottom vertex along Up — not the part center.
    /// </summary>
    internal sealed class FacePlanarGeometry
    {
        public Edge LongestEdge { get; set; }
        public Vertex BottomVertex { get; set; }
        public Point OriginPart { get; set; }
        public UnitVector UpPart { get; set; }
        public UnitVector AcrossPart { get; set; }
        public UnitVector NormalPart { get; set; }
        public double SpanIn { get; set; }
        public bool NaturalEdgeDirection { get; set; }
    }

    internal static class FacePlanarGeometryBuilder
    {
        public static bool TryAnalyze(Application app, Face partFace, out FacePlanarGeometry geometry, out string error)
        {
            geometry = null;
            error = string.Empty;

            Plane plane = partFace.Geometry as Plane;
            if (plane == null)
            {
                error = "Skin face is not planar.";
                return false;
            }

            if (!TryGetLongestLinearEdge(partFace, out Edge longestEdge, out double longestLengthCm))
            {
                error = "Could not find a linear edge on the picked skin face.";
                return false;
            }

            bool naturalEdgeDirection;
            UnitVector upPart = EdgeDirectionUnit(app, longestEdge, out naturalEdgeDirection);
            UnitVector normalPart = plane.Normal;
            UnitVector acrossPart = BuildAcrossPart(app, partFace, longestEdge, upPart, normalPart);
            if (acrossPart == null)
            {
                error = "Could not determine the face width direction.";
                return false;
            }

            List<Point> samplePoints = CollectFaceSamplePoints(partFace);
            if (samplePoints.Count == 0)
            {
                error = "Could not sample points from the picked skin face.";
                return false;
            }

            Point reference = samplePoints[0];
            double minUp = double.MaxValue;
            double maxUp = double.MinValue;
            Point bottomPoint = reference;
            Point topPoint = reference;

            foreach (Point point in samplePoints)
            {
                double upCoord = CoordinateAlong(point, reference, upPart);
                if (upCoord < minUp)
                {
                    minUp = upCoord;
                    bottomPoint = point;
                }

                if (upCoord > maxUp)
                {
                    maxUp = upCoord;
                    topPoint = point;
                }
            }

            if (maxUp <= minUp)
            {
                error = "Picked skin face has no measurable height.";
                return false;
            }

            if (Dot(Sub(app, topPoint, bottomPoint), upPart) < 0)
            {
                upPart = Negate(app, upPart);
            }

            Vertex bottomVertex = FindExtremeVertex(partFace, reference, upPart, true) ?? longestEdge.StartVertex;
            Point edgeStart = longestEdge.StartVertex.Point;
            Point edgeStop = longestEdge.StopVertex.Point;
            naturalEdgeDirection = Dot(Sub(app, edgeStop, edgeStart), upPart) > 0;

            double spanCm = maxUp - minUp;
            if (longestLengthCm > spanCm + 0.05)
            {
                spanCm = longestLengthCm;
            }

            geometry = new FacePlanarGeometry
            {
                LongestEdge = longestEdge,
                BottomVertex = bottomVertex,
                OriginPart = bottomVertex.Point,
                UpPart = upPart,
                AcrossPart = acrossPart,
                NormalPart = normalPart,
                SpanIn = spanCm / PatternConstants.InchesToCm,
                NaturalEdgeDirection = naturalEdgeDirection,
            };
            return true;
        }

        /// <summary>
        /// Rebuilds Up/Across/origin/span so Up follows <paramref name="preferredUpPart"/>
        /// projected onto the face. Origin is the extreme vertex along Up (bottom), not part center.
        /// </summary>
        public static bool TryReorientUpFromPreferredDirection(
            Application app,
            Face partFace,
            FacePlanarGeometry geometry,
            UnitVector preferredUpPart,
            out string error)
        {
            error = string.Empty;
            if (geometry == null || preferredUpPart == null || partFace == null)
            {
                error = "Missing face geometry for preferred Up reorientation.";
                return false;
            }

            UnitVector upPart = ProjectPerpendicularTo(app, preferredUpPart, geometry.NormalPart);
            if (upPart == null)
            {
                error = "Preferred Up is parallel to the skin face normal.";
                return false;
            }

            UnitVector acrossPart = Normalize(app, CrossRaw(app, geometry.NormalPart, upPart));
            if (acrossPart == null)
            {
                acrossPart = Normalize(app, CrossRaw(app, upPart, geometry.NormalPart));
            }

            if (acrossPart == null)
            {
                error = "Could not build Across from preferred Up.";
                return false;
            }

            List<Point> samplePoints = CollectFaceSamplePoints(partFace);
            if (samplePoints.Count == 0)
            {
                error = "Could not sample points from the picked skin face.";
                return false;
            }

            Point reference = samplePoints[0];
            double minUp = double.MaxValue;
            double maxUp = double.MinValue;
            Point bottomPoint = reference;
            Point topPoint = reference;

            foreach (Point point in samplePoints)
            {
                double upCoord = CoordinateAlong(point, reference, upPart);
                if (upCoord < minUp)
                {
                    minUp = upCoord;
                    bottomPoint = point;
                }

                if (upCoord > maxUp)
                {
                    maxUp = upCoord;
                    topPoint = point;
                }
            }

            if (maxUp <= minUp)
            {
                error = "Picked skin face has no measurable height along preferred Up.";
                return false;
            }

            if (Dot(Sub(app, topPoint, bottomPoint), upPart) < 0)
            {
                upPart = Negate(app, upPart);
            }

            Vertex bottomVertex = FindExtremeVertex(partFace, reference, upPart, true);
            if (bottomVertex == null)
            {
                error = "Could not locate the bottom vertex along preferred Up.";
                return false;
            }

            geometry.UpPart = upPart;
            geometry.AcrossPart = acrossPart;
            geometry.OriginPart = bottomVertex.Point;
            geometry.BottomVertex = bottomVertex;
            geometry.SpanIn = (maxUp - minUp) / PatternConstants.InchesToCm;
            geometry.LongestEdge = null;
            geometry.NaturalEdgeDirection = true;
            return true;
        }

        /// <summary>
        /// Part +Z can map to assembly -Y on walls. Flip Up (and rebuild the bottom origin)
        /// only when Up is mainly vertical in assembly and pointing downhill (|Y| &gt;= |Z| and Y &lt; 0).
        /// Sloped/flat roofs keep part +Z (run along asm Z); verified on 1023 Cut5 (Ycomp≈0.02).
        /// </summary>
        public static void OrientUpTowardAssemblyPlusY(
            Application app,
            IReadOnlyList<ComponentOccurrence> skinChain,
            Face partFace,
            FacePlanarGeometry geometry)
        {
            if (app == null || geometry?.UpPart == null || partFace == null || skinChain == null || skinChain.Count == 0)
            {
                return;
            }

            UnitVector upRoot = OccurrenceTransformHelper.PartVectorToRoot(app, skinChain, geometry.UpPart);
            // Walls: |Y|≈1, |Z|≈0. Roofs: |Z|≈1, |Y| small — do not flip roofs.
            if (upRoot == null || upRoot.Y >= 0 || Math.Abs(upRoot.Y) < Math.Abs(upRoot.Z))
            {
                return;
            }

            UnitVector upPart = Negate(app, geometry.UpPart);
            List<Point> samplePoints = CollectFaceSamplePoints(partFace);
            if (samplePoints.Count == 0)
            {
                return;
            }

            Point reference = samplePoints[0];
            Vertex bottomVertex = FindExtremeVertex(partFace, reference, upPart, true);
            if (bottomVertex == null)
            {
                return;
            }

            geometry.UpPart = upPart;
            geometry.AcrossPart = Negate(app, geometry.AcrossPart);
            geometry.BottomVertex = bottomVertex;
            geometry.OriginPart = bottomVertex.Point;
        }

        public static void OrientAcrossTowardPoint(Application app, FacePlanarGeometry geometry, Point towardPointPart)
        {
            if (geometry == null || towardPointPart == null)
            {
                return;
            }

            Point onFace = ProjectToPlane(app, towardPointPart, geometry.OriginPart, geometry.NormalPart);
            Vector toPoint = Sub(app, onFace, geometry.OriginPart);
            UnitVector toward = ProjectPerpendicularTo(app, Normalize(app, toPoint), geometry.UpPart);
            if (toward != null && Dot(toward, geometry.AcrossPart) < 0)
            {
                geometry.AcrossPart = Negate(app, geometry.AcrossPart);
            }
        }

        public static Point BuildHolePartPoint(
            Application app,
            FacePlanarGeometry geometry,
            Point rowAnchorPart,
            double holeUpIn)
        {
            double acrossCm = GetAcrossCoordCm(app, geometry, rowAnchorPart);
            double upCm = holeUpIn * PatternConstants.InchesToCm;
            return BuildPointFromFaceCoords(app, geometry, upCm, acrossCm);
        }

        public static Point BuildPointFromFaceCoords(
            Application app,
            FacePlanarGeometry geometry,
            double upCm,
            double acrossCm)
        {
            return app.TransientGeometry.CreatePoint(
                geometry.OriginPart.X + (geometry.UpPart.X * upCm) + (geometry.AcrossPart.X * acrossCm),
                geometry.OriginPart.Y + (geometry.UpPart.Y * upCm) + (geometry.AcrossPart.Y * acrossCm),
                geometry.OriginPart.Z + (geometry.UpPart.Z * upCm) + (geometry.AcrossPart.Z * acrossCm));
        }

        public static double GetAcrossCoordCm(Application app, FacePlanarGeometry geometry, Point partPoint)
        {
            Point onFace = ProjectToPlane(app, partPoint, geometry.OriginPart, geometry.NormalPart);
            return Dot(Sub(app, onFace, geometry.OriginPart), geometry.AcrossPart);
        }

        public static double GetUpCoordCm(Application app, FacePlanarGeometry geometry, Point partPoint)
        {
            Point onFace = ProjectToPlane(app, partPoint, geometry.OriginPart, geometry.NormalPart);
            return Dot(Sub(app, onFace, geometry.OriginPart), geometry.UpPart);
        }

        public static bool TryMeasureAcrossSpanCm(
            Application app,
            Face partFace,
            FacePlanarGeometry geometry,
            out double minAcrossCm,
            out double maxAcrossCm,
            out string error)
        {
            minAcrossCm = 0;
            maxAcrossCm = 0;
            error = string.Empty;

            if (partFace == null || geometry == null)
            {
                error = "Face geometry is missing.";
                return false;
            }

            minAcrossCm = double.MaxValue;
            maxAcrossCm = double.MinValue;
            bool found = false;

            foreach (Vertex vertex in partFace.Vertices)
            {
                try
                {
                    double across = GetAcrossCoordCm(app, geometry, vertex.Point);
                    if (across < minAcrossCm)
                    {
                        minAcrossCm = across;
                    }

                    if (across > maxAcrossCm)
                    {
                        maxAcrossCm = across;
                    }

                    found = true;
                }
                catch
                {
                }
            }

            if (!found || maxAcrossCm <= minAcrossCm)
            {
                error = "Could not measure face width along the channel direction.";
                return false;
            }

            return true;
        }

        public static Point2d HoleToSketchCoords(
            Application app,
            FacePlanarGeometry geometry,
            Point rowAnchorPart,
            double holeUpIn)
        {
            Point partPoint = BuildHolePartPoint(app, geometry, rowAnchorPart, holeUpIn);
            return PartPointToSketchCoords(app, geometry, partPoint);
        }

        public static Point2d PartPointToSketchCoords(Application app, FacePlanarGeometry geometry, Point partPoint)
        {
            Vector delta = Sub(app, partPoint, geometry.OriginPart);
            double acrossCm = Dot(delta, geometry.AcrossPart);
            double upCm = Dot(delta, geometry.UpPart);
            return app.TransientGeometry.CreatePoint2d(acrossCm, upCm);
        }

        private static Point PointFromFaceCoords(
            Application app,
            FacePlanarGeometry geometry,
            double upCm,
            double acrossCm)
        {
            return BuildPointFromFaceCoords(app, geometry, upCm, acrossCm);
        }

        private static Point ProjectToPlane(
            Application app,
            Point point,
            Point planeOrigin,
            UnitVector planeNormal)
        {
            double dx = point.X - planeOrigin.X;
            double dy = point.Y - planeOrigin.Y;
            double dz = point.Z - planeOrigin.Z;
            double distance = (dx * planeNormal.X) + (dy * planeNormal.Y) + (dz * planeNormal.Z);
            return app.TransientGeometry.CreatePoint(
                point.X - (distance * planeNormal.X),
                point.Y - (distance * planeNormal.Y),
                point.Z - (distance * planeNormal.Z));
        }

        private static Vertex FindExtremeVertex(
            Face partFace,
            Point reference,
            UnitVector upPart,
            bool findMinimum)
        {
            Vertex bestVertex = null;
            double bestUp = findMinimum ? double.MaxValue : double.MinValue;

            foreach (Vertex vertex in partFace.Vertices)
            {
                try
                {
                    double upCoord = CoordinateAlong(vertex.Point, reference, upPart);
                    if (findMinimum ? upCoord < bestUp : upCoord > bestUp)
                    {
                        bestUp = upCoord;
                        bestVertex = vertex;
                    }
                }
                catch
                {
                }
            }

            return bestVertex;
        }

        private static bool TryGetLongestLinearEdge(Face partFace, out Edge bestEdge, out double bestLengthCm)
        {
            bestEdge = null;
            bestLengthCm = 0;

            foreach (Edge edge in partFace.Edges)
            {
                try
                {
                    if (edge.GeometryType != CurveTypeEnum.kLineSegmentCurve)
                    {
                        continue;
                    }

                    double lengthCm = EdgeLengthCm(edge);
                    if (lengthCm > bestLengthCm)
                    {
                        bestLengthCm = lengthCm;
                        bestEdge = edge;
                    }
                }
                catch
                {
                }
            }

            return bestEdge != null;
        }

        private static UnitVector BuildAcrossPart(
            Application app,
            Face partFace,
            Edge longestEdge,
            UnitVector upPart,
            UnitVector normalPart)
        {
            Edge bestEdge = null;
            double bestLengthCm = 0;

            foreach (Edge edge in partFace.Edges)
            {
                try
                {
                    if (edge.GeometryType != CurveTypeEnum.kLineSegmentCurve)
                    {
                        continue;
                    }

                    if (ReferenceEquals(edge, longestEdge))
                    {
                        continue;
                    }

                    UnitVector edgeDir = EdgeDirectionUnit(app, edge, out _);
                    if (Math.Abs(Dot(edgeDir, upPart)) > 0.95)
                    {
                        continue;
                    }

                    double lengthCm = EdgeLengthCm(edge);
                    if (lengthCm > bestLengthCm)
                    {
                        bestLengthCm = lengthCm;
                        bestEdge = edge;
                    }
                }
                catch
                {
                }
            }

            if (bestEdge != null)
            {
                UnitVector across = EdgeDirectionUnit(app, bestEdge, out _);
                across = ProjectPerpendicularTo(app, across, upPart);
                if (across != null)
                {
                    return across;
                }
            }

            return Normalize(app, CrossRaw(app, upPart, normalPart));
        }

        private static UnitVector EdgeDirectionUnit(Application app, Edge edge, out bool naturalDirection)
        {
            Point start = edge.StartVertex.Point;
            Point stop = edge.StopVertex.Point;
            Vector direction = app.TransientGeometry.CreateVector(
                stop.X - start.X,
                stop.Y - start.Y,
                stop.Z - start.Z);
            naturalDirection = true;
            UnitVector unit = Normalize(app, direction);
            if (unit == null)
            {
                unit = app.TransientGeometry.CreateUnitVector(0, 1, 0);
            }

            return unit;
        }

        private static double EdgeLengthCm(Edge edge)
        {
            Point start = edge.StartVertex.Point;
            Point stop = edge.StopVertex.Point;
            double dx = stop.X - start.X;
            double dy = stop.Y - start.Y;
            double dz = stop.Z - start.Z;
            return Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
        }

        private static List<Point> CollectFaceSamplePoints(Face partFace)
        {
            var points = new List<Point>();
            var seen = new HashSet<string>();

            foreach (Vertex vertex in partFace.Vertices)
            {
                TryAddPoint(vertex.Point, seen, points);
            }

            foreach (Edge edge in partFace.Edges)
            {
                TryAddPoint(edge.StartVertex.Point, seen, points);
                TryAddPoint(edge.StopVertex.Point, seen, points);
            }

            return points;
        }

        private static void TryAddPoint(Point point, HashSet<string> seen, List<Point> points)
        {
            if (point == null)
            {
                return;
            }

            try
            {
                string key = $"{point.X:F4},{point.Y:F4},{point.Z:F4}";
                if (seen.Add(key))
                {
                    points.Add(point);
                }
            }
            catch
            {
            }
        }

        private static double CoordinateAlong(Point point, Point origin, UnitVector axis)
        {
            return ((point.X - origin.X) * axis.X)
                + ((point.Y - origin.Y) * axis.Y)
                + ((point.Z - origin.Z) * axis.Z);
        }

        private static UnitVector ProjectPerpendicularTo(Application app, UnitVector vector, UnitVector axis)
        {
            double dot = Dot(vector, axis);
            Vector projected = app.TransientGeometry.CreateVector(
                vector.X - (dot * axis.X),
                vector.Y - (dot * axis.Y),
                vector.Z - (dot * axis.Z));
            return Normalize(app, projected);
        }

        private static Vector CrossRaw(Application app, UnitVector a, UnitVector b)
        {
            return app.TransientGeometry.CreateVector(
                (a.Y * b.Z) - (a.Z * b.Y),
                (a.Z * b.X) - (a.X * b.Z),
                (a.X * b.Y) - (a.Y * b.X));
        }

        private static UnitVector Normalize(Application app, Vector vector)
        {
            if (vector == null)
            {
                return null;
            }

            double length = Math.Sqrt((vector.X * vector.X) + (vector.Y * vector.Y) + (vector.Z * vector.Z));
            if (length < 1e-9)
            {
                return null;
            }

            return app.TransientGeometry.CreateUnitVector(
                vector.X / length,
                vector.Y / length,
                vector.Z / length);
        }

        private static UnitVector Negate(Application app, UnitVector vector)
        {
            return app.TransientGeometry.CreateUnitVector(-vector.X, -vector.Y, -vector.Z);
        }

        private static double Dot(UnitVector a, UnitVector b)
        {
            return (a.X * b.X) + (a.Y * b.Y) + (a.Z * b.Z);
        }

        private static double Dot(Vector a, UnitVector b)
        {
            return (a.X * b.X) + (a.Y * b.Y) + (a.Z * b.Z);
        }

        private static Vector Sub(Application app, Point a, Point b)
        {
            return app.TransientGeometry.CreateVector(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        }
    }
}
