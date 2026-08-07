namespace UnitProgressTracker.Core.Models;

public record CasingSpecModel(
    double? WallThicknessTop = null,
    double? WallThicknessBottom = null,
    double? WallThicknessLeft = null,
    double? WallThicknessRight = null,
    double? WallThicknessFront = null,
    double? WallThicknessRear = null,
    string SkinTop = "",
    string SkinBottom = "",
    string SkinLeft = "",
    string SkinRight = "",
    string SkinFront = "",
    string SkinRear = "",
    string LinerTop = "",
    string LinerBottom = "",
    string LinerLeft = "",
    string LinerRight = "",
    string LinerFront = "",
    string LinerRear = "",
    string FloorMaterialType = "",
    int FloorMaterialGauge = 0,
    string FloorPaintType = ""
);
