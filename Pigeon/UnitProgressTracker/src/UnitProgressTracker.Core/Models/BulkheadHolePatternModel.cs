namespace UnitProgressTracker.Core.Models;

public record BulkheadHolePatternModel(
    string SegmentType = "",
    string BulkheadPartNumber = "",
    string BulkheadDescription = "",
    string UnitSide = "",
    int Index = 0,
    double DoaOffset = 0.0,
    double WidthOffset = 0.0,
    double WidthQty = 0.0,
    double WidthSpacing = 0.0,
    double HoleDiameter = 0.0
);
