using System.Collections.Generic;

namespace UnitProgressTracker.Core.Models;

public class BomFilterConfig
{
    public string Version { get; set; } = "1.0";
    public List<string> KeptPrefixes { get; set; } = new() { "391-", "291-", "386-", "486-", "251-", "091Z" };
    public List<string> DroppedPrefixes { get; set; } = new() { "007-", "024-", "025-", "026-", "028-", "035-", "091-", "290-", "491-" };
    public List<string> AlwaysKeepDescriptionKeywords { get; set; } = new()
    {
        "DOOR", "SPLIT COVER", "DRAIN PAN", "SAFETY GRATE", "HEAT WHEEL",
        "WING COIL", "INLET HOOD", "DDFAN", "WALL", "RECONNECT", "LEAK TEST",
        "HUMIDIFIER SUPPORT"
    };
    public List<string> AlwaysDropDescriptionKeywords { get; set; } = new()
    {
        "FACTOR", "SHIP SKID", "SHRINK WRAP", "CONDUIT", "CLAMP"
    };
}
