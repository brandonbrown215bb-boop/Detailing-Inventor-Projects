namespace UnitProgressTracker.Core.Models;

public record JobContextModel(
    string SalesOrderNumber = "",
    string JobName = "",
    string ComNumber = "",
    string UnitTag = "",
    int UnitNumber = 0,
    string MfgLocation = "",
    string ProductType = "",
    string UnitType = "",
    string HousingStyle = "",
    string SkidSegmentSequence = ""
);
