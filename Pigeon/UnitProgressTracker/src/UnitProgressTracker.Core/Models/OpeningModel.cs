namespace UnitProgressTracker.Core.Models;

public record OpeningModel(
    string SegmentType = "",
    string OpeningType = "",
    string OpeningShape = "",
    string UnitSide = "",
    string DoorPartNumber = "",
    GeometryBox? Geometry = null
);
