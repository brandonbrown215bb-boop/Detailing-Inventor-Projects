using System;
using System.Collections.Generic;
using Inventor;

namespace SkinChannelPunch.Core
{
    internal sealed class SeedCircleRef
    {
        public PlanarSketch Sketch { get; set; }
        public SketchCircle Circle { get; set; }
        public SketchPoint CenterPoint { get; set; }
        public Point CenterPart { get; set; }
        public double DiameterCm { get; set; }
    }

    /// <summary>
    /// Reuses existing sheet-metal cut circles as sketch-plane seeds (INV2D pattern).
    /// </summary>
    internal static class SeedSketchHelper
    {
        public static bool TryFindSeedForFace(
            SheetMetalComponentDefinition smcd,
            Face partFace,
            out SeedCircleRef seed,
            out string error)
        {
            seed = null;
            error = string.Empty;

            List<SeedCircleRef> circles = CollectEditableCutCircles(smcd);
            if (circles.Count == 0)
            {
                error = "No existing sheet-metal cut circles found on the skin. Add at least one clearance hole first (seed sketch), then run punch again.";
                return false;
            }

            Plane facePlane = partFace?.Geometry as Plane;
            List<SeedCircleRef> onFace = new List<SeedCircleRef>();
            if (facePlane != null)
            {
                foreach (SeedCircleRef circle in circles)
                {
                    if (SketchMatchesFacePlane(smcd, circle.Sketch, facePlane))
                    {
                        onFace.Add(circle);
                    }
                }
            }

            List<SeedCircleRef> candidates = onFace.Count > 0 ? onFace : circles;
            candidates.Sort(CompareBottomFirst);
            seed = candidates[0];
            return true;
        }

        private static Application GetApplication(SheetMetalComponentDefinition smcd)
        {
            Document document = smcd.Document as Document;
            return document?.Parent as Application;
        }

        public static PlanarSketch CreateSketchFromSeed(
            SheetMetalComponentDefinition smcd,
            PlanarSketch seedSketch)
        {
            object plane = seedSketch.PlanarEntity;
            return smcd.Sketches.Add(plane, false);
        }

        private static List<SeedCircleRef> CollectEditableCutCircles(SheetMetalComponentDefinition smcd)
        {
            var list = new List<SeedCircleRef>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            SheetMetalFeatures smf = (SheetMetalFeatures)(object)smcd.Features;
            CutFeatures cfs = smf.CutFeatures;
            for (int i = 1; i <= cfs.Count; i++)
            {
                CutFeature cut;
                try
                {
                    cut = cfs[i];
                }
                catch
                {
                    continue;
                }

                if (!(cut.Definition is CutDefinition cutDefinition))
                {
                    continue;
                }

                Profile profile = cutDefinition.Profile;
                if (profile == null || !(profile.Parent is PlanarSketch sketch))
                {
                    continue;
                }

                SketchCircles circles = sketch.SketchCircles;
                for (int ci = 1; ci <= circles.Count; ci++)
                {
                    if (!(circles[ci] is SketchCircle circle))
                    {
                        continue;
                    }

                    if (circle.Construction || circle.Reference)
                    {
                        continue;
                    }

                    SketchPoint centerPoint = circle.CenterSketchPoint;
                    Point centerPart = sketch.SketchToModelSpace(centerPoint.Geometry);
                    double diameterCm = Math.Max(1e-6, circle.Radius * 2.0);
                    string key = $"{centerPart.X:F4}:{centerPart.Y:F4}:{centerPart.Z:F4}:{diameterCm:F4}";
                    if (!seen.Add(key))
                    {
                        continue;
                    }

                    list.Add(new SeedCircleRef
                    {
                        Sketch = sketch,
                        Circle = circle,
                        CenterPoint = centerPoint,
                        CenterPart = centerPart,
                        DiameterCm = diameterCm,
                    });
                }
            }

            return list;
        }

        private static bool SketchMatchesFacePlane(
            SheetMetalComponentDefinition smcd,
            PlanarSketch sketch,
            Plane facePlane)
        {
            try
            {
                Point sketchPoint = GetRepresentativeSketchPoint(smcd, sketch);
                UnitVector sketchNormal = TryGetSketchPlaneNormal(sketch);
                if (sketchNormal == null)
                {
                    return DistancePointToPlane(sketchPoint, facePlane) < 0.15;
                }

                double normalDot = Math.Abs(
                    (sketchNormal.X * facePlane.Normal.X)
                    + (sketchNormal.Y * facePlane.Normal.Y)
                    + (sketchNormal.Z * facePlane.Normal.Z));
                if (normalDot < 0.98)
                {
                    return false;
                }

                return DistancePointToPlane(sketchPoint, facePlane) < 0.15;
            }
            catch
            {
                return false;
            }
        }

        private static Point GetRepresentativeSketchPoint(SheetMetalComponentDefinition smcd, PlanarSketch sketch)
        {
            if (sketch.SketchCircles.Count > 0)
            {
                return sketch.SketchToModelSpace(sketch.SketchCircles[1].CenterSketchPoint.Geometry);
            }

            Application app = GetApplication(smcd);
            if (app == null)
            {
                throw new InvalidOperationException("Could not resolve Inventor application for sketch plane test.");
            }

            Point2d origin = app.TransientGeometry.CreatePoint2d(0, 0);
            return sketch.SketchToModelSpace(origin);
        }

        private static UnitVector TryGetSketchPlaneNormal(PlanarSketch sketch)
        {
            try
            {
                object planarEntity = sketch.PlanarEntity;
                if (planarEntity is Face face)
                {
                    Plane plane = face.Geometry as Plane;
                    return plane?.Normal;
                }

                if (planarEntity is WorkPlane workPlane)
                {
                    Plane plane = workPlane.Plane;
                    return plane?.Normal;
                }
            }
            catch
            {
            }

            return null;
        }

        private static double DistancePointToPlane(Point point, Plane plane)
        {
            Point root = plane.RootPoint;
            UnitVector normal = plane.Normal;
            double dx = point.X - root.X;
            double dy = point.Y - root.Y;
            double dz = point.Z - root.Z;
            return Math.Abs((dx * normal.X) + (dy * normal.Y) + (dz * normal.Z));
        }

        private static int CompareBottomFirst(SeedCircleRef a, SeedCircleRef b)
        {
            int zCompare = a.CenterPart.Z.CompareTo(b.CenterPart.Z);
            if (zCompare != 0)
            {
                return zCompare;
            }

            int xCompare = a.CenterPart.X.CompareTo(b.CenterPart.X);
            if (xCompare != 0)
            {
                return xCompare;
            }

            return a.CenterPart.Y.CompareTo(b.CenterPart.Y);
        }
    }
}
