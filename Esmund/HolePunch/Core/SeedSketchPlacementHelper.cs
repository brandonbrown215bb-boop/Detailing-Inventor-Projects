using System;
using Inventor;

namespace SkinChannelPunch.Core
{
    internal sealed class SketchPlaneFrame
    {
        public Point OriginPart { get; set; }
        public Vector AxisXPart { get; set; }
        public Vector AxisYPart { get; set; }
    }

    internal static class SeedSketchPlacementHelper
    {
        public static bool TryBuildFrame(Application app, PlanarSketch seedSketch, out SketchPlaneFrame frame, out string error)
        {
            frame = null;
            error = string.Empty;

            try
            {
                TransientGeometry tg = app.TransientGeometry;
                Point2d origin2d = tg.CreatePoint2d(0, 0);
                Point2d xUnit2d = tg.CreatePoint2d(1, 0);
                Point2d yUnit2d = tg.CreatePoint2d(0, 1);

                Point originPart = seedSketch.SketchToModelSpace(origin2d);
                Point xTipPart = seedSketch.SketchToModelSpace(xUnit2d);
                Point yTipPart = seedSketch.SketchToModelSpace(yUnit2d);

                Vector axisX = Subtract(app, xTipPart, originPart);
                Vector axisY = Subtract(app, yTipPart, originPart);
                if (Length(axisX) < 1e-9 || Length(axisY) < 1e-9)
                {
                    error = "Seed sketch plane has no measurable orientation.";
                    return false;
                }

                frame = new SketchPlaneFrame
                {
                    OriginPart = originPart,
                    AxisXPart = axisX,
                    AxisYPart = axisY,
                };
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static Point2d ModelPointToSketchCoords(SketchPlaneFrame frame, Application app, Point modelPoint)
        {
            Vector delta = Subtract(app, modelPoint, frame.OriginPart);
            double sketchX = Dot(delta, frame.AxisXPart) / Dot(frame.AxisXPart, frame.AxisXPart);
            double sketchY = Dot(delta, frame.AxisYPart) / Dot(frame.AxisYPart, frame.AxisYPart);
            return app.TransientGeometry.CreatePoint2d(sketchX, sketchY);
        }

        public static Point BuildModelPointOnSkinPlane(
            Application app,
            PartComponentDefinition partDefinition,
            double widthCoordCm,
            double heightCoordCm)
        {
            SkinPartExtentsHelper.GetAxisIndices(partDefinition, out int heightIndex, out int widthIndex);
            return SkinPartExtentsHelper.BuildModelPoint(
                app,
                heightIndex,
                widthIndex,
                widthCoordCm,
                heightCoordCm);
        }

        private static Vector Subtract(Application app, Point a, Point b)
        {
            return app.TransientGeometry.CreateVector(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        }

        private static double Dot(Vector a, Vector b)
        {
            return (a.X * b.X) + (a.Y * b.Y) + (a.Z * b.Z);
        }

        private static double Length(Vector vector)
        {
            return Math.Sqrt((vector.X * vector.X) + (vector.Y * vector.Y) + (vector.Z * vector.Z));
        }
    }
}
