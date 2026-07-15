using System;
using System.Collections.Generic;
using Inventor;

namespace SkinChannelPunch.Core
{
    /// <summary>
    /// Right-handed frame on the picked skin face in root-assembly space (cm).
    /// Up = longest face edge (wall height); Across = face width toward channel.
    /// </summary>
    internal sealed class FaceLocalFrame
    {
        public Point OriginRoot { get; set; }
        public UnitVector NormalRoot { get; set; }
        public UnitVector UpRoot { get; set; }
        public UnitVector AcrossRoot { get; set; }

        public double CoordinateUpCm(Point rootPoint)
        {
            double dx = rootPoint.X - OriginRoot.X;
            double dy = rootPoint.Y - OriginRoot.Y;
            double dz = rootPoint.Z - OriginRoot.Z;
            return (dx * UpRoot.X) + (dy * UpRoot.Y) + (dz * UpRoot.Z);
        }

        public Point PointOnFaceRoot(Application app, double upCoordCm, double acrossCoordCm)
        {
            return app.TransientGeometry.CreatePoint(
                OriginRoot.X + (UpRoot.X * upCoordCm) + (AcrossRoot.X * acrossCoordCm),
                OriginRoot.Y + (UpRoot.Y * upCoordCm) + (AcrossRoot.Y * acrossCoordCm),
                OriginRoot.Z + (UpRoot.Z * upCoordCm) + (AcrossRoot.Z * acrossCoordCm));
        }
    }

    internal static class FaceLocalFrameBuilder
    {
        public static bool TryBuild(
            Application app,
            ComponentOccurrence channelOcc,
            IReadOnlyList<ComponentOccurrence> channelChainFromRoot,
            ComponentOccurrence skinOcc,
            IReadOnlyList<ComponentOccurrence> skinChainFromRoot,
            Face partFace,
            out FaceLocalFrame frame,
            out double skinBottomIn,
            out double skinTopIn,
            out FacePlanarGeometry faceGeometry,
            out string error)
        {
            frame = null;
            faceGeometry = null;
            skinBottomIn = 0;
            skinTopIn = 0;
            error = string.Empty;

            try
            {
                if (!FacePlanarGeometryBuilder.TryAnalyze(app, partFace, out faceGeometry, out error))
                {
                    return false;
                }

                Point channelCenterRoot = OccurrenceTransformHelper.ParentSpacePointToRoot(
                    app,
                    channelChainFromRoot,
                    RangeBoxCenter(channelOcc.RangeBox, app));
                Point channelCenterPart = OccurrenceTransformHelper.RootPointToPartSpace(
                    app,
                    skinChainFromRoot,
                    channelCenterRoot);
                FacePlanarGeometryBuilder.OrientAcrossTowardPoint(app, faceGeometry, channelCenterPart);

                Point originRoot = OccurrenceTransformHelper.PartPointToRoot(
                    app,
                    skinChainFromRoot,
                    faceGeometry.OriginPart);
                UnitVector upRoot = OccurrenceTransformHelper.PartVectorToRoot(
                    app,
                    skinChainFromRoot,
                    faceGeometry.UpPart);
                UnitVector acrossRoot = OccurrenceTransformHelper.PartVectorToRoot(
                    app,
                    skinChainFromRoot,
                    faceGeometry.AcrossPart);
                UnitVector normalRoot = OccurrenceTransformHelper.PartVectorToRoot(
                    app,
                    skinChainFromRoot,
                    faceGeometry.NormalPart);

                Point facePointRoot = OccurrenceTransformHelper.PartPointToRoot(
                    app,
                    skinChainFromRoot,
                    partFace.PointOnFace);
                EnsureNormalPointsOutward(app, ref normalRoot, channelCenterRoot, facePointRoot);

                frame = new FaceLocalFrame
                {
                    OriginRoot = originRoot,
                    NormalRoot = normalRoot,
                    UpRoot = upRoot,
                    AcrossRoot = acrossRoot,
                };

                skinBottomIn = 0;
                skinTopIn = faceGeometry.SpanIn;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static Point BuildHoleRootPoint(
            Application app,
            FaceLocalFrame frame,
            Point channelCenterRoot,
            double holeUpCoordIn,
            double flangeOffsetIn)
        {
            Point channelOnPlane = ProjectToPlane(app, channelCenterRoot, frame.OriginRoot, frame.NormalRoot);
            double channelAcrossCm = Dot(
                Sub(app, channelOnPlane, frame.OriginRoot),
                frame.AcrossRoot);
            double upCm = holeUpCoordIn * PatternConstants.InchesToCm;
            double acrossCm = channelAcrossCm + (flangeOffsetIn * PatternConstants.InchesToCm);
            return frame.PointOnFaceRoot(app, upCm, acrossCm);
        }

        private static void EnsureNormalPointsOutward(
            Application app,
            ref UnitVector normalRoot,
            Point channelCenterRoot,
            Point facePointRoot)
        {
            TransientGeometry tg = app.TransientGeometry;
            Vector toFace = tg.CreateVector(
                facePointRoot.X - channelCenterRoot.X,
                facePointRoot.Y - channelCenterRoot.Y,
                facePointRoot.Z - channelCenterRoot.Z);
            if (Dot(toFace, normalRoot) < 0)
            {
                normalRoot = Negate(app, normalRoot);
            }
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

        private static UnitVector ProjectVectorOntoPlane(Application app, Vector vector, UnitVector planeNormal)
        {
            double dot = Dot(vector, planeNormal);
            TransientGeometry tg = app.TransientGeometry;
            Vector projected = tg.CreateVector(
                vector.X - (dot * planeNormal.X),
                vector.Y - (dot * planeNormal.Y),
                vector.Z - (dot * planeNormal.Z));
            return Normalize(app, projected);
        }

        private static UnitVector CrossUnit(Application app, UnitVector a, UnitVector b)
        {
            TransientGeometry tg = app.TransientGeometry;
            Vector cross = tg.CreateVector(
                (a.Y * b.Z) - (a.Z * b.Y),
                (a.Z * b.X) - (a.X * b.Z),
                (a.X * b.Y) - (a.Y * b.X));
            return Normalize(app, cross);
        }

        private static UnitVector Normalize(Application app, Vector vector)
        {
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

        private static Point RangeBoxCenter(Box box, Application app)
        {
            Point min = box.MinPoint;
            Point max = box.MaxPoint;
            return app.TransientGeometry.CreatePoint(
                (min.X + max.X) * 0.5,
                (min.Y + max.Y) * 0.5,
                (min.Z + max.Z) * 0.5);
        }
    }
}
