using System.Collections.Generic;

namespace UnitConstructionVerifier.Operations
{
    public sealed class WireframeData
    {
        public WireframeData(
            IReadOnlyList<(double X1, double Y1, double Z1, double X2, double Y2, double Z2)> segments,
            double centerX,
            double centerY,
            double centerZ,
            double radius)
        {
            Segments = segments;
            CenterX = centerX;
            CenterY = centerY;
            CenterZ = centerZ;
            Radius = radius;
        }

        public IReadOnlyList<(double X1, double Y1, double Z1, double X2, double Y2, double Z2)> Segments { get; }
        public double CenterX { get; }
        public double CenterY { get; }
        public double CenterZ { get; }
        public double Radius { get; }
    }
}
