namespace UnitProgressTracker.Core.Models;

public class CameraStateModel
{
    public double PositionX { get; set; }
    public double PositionY { get; set; }
    public double PositionZ { get; set; } = 1.0;
    public double TargetX { get; set; }
    public double TargetY { get; set; }
    public double TargetZ { get; set; }
    public double UpX { get; set; }
    public double UpY { get; set; } = 1.0;
    public double UpZ { get; set; }
}
