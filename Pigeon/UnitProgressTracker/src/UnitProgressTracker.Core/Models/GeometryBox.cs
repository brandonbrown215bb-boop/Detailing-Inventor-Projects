namespace UnitProgressTracker.Core.Models;

public class GeometryBox
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    public double XLength { get; set; }
    public double YLength { get; set; }
    public double ZLength { get; set; }

    public GeometryBox() { }

    public GeometryBox(double x, double y, double z, double xLength, double yLength, double zLength)
    {
        X = x;
        Y = y;
        Z = z;
        XLength = xLength;
        YLength = yLength;
        ZLength = zLength;
    }
}
