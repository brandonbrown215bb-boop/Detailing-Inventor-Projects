namespace UnitProgressTracker.Core.Models;

public class StatusState
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ColorHex { get; set; } = "#94a3b8";
    public string FillType { get; set; } = "solid";

    public StatusState() { }

    public StatusState(string id, string name, string colorHex, string fillType = "solid")
    {
        Id = id;
        Name = name;
        ColorHex = colorHex;
        FillType = fillType;
    }

    public static List<StatusState> DefaultStates => new()
    {
        new StatusState("current", "Current", "#94a3b8"),
        new StatusState("corrected", "Corrected", "#f59e0b"),
        new StatusState("built", "Built", "#3b82f6"),
        new StatusState("associated", "Associated", "#8b5cf6"),
        new StatusState("paperwork-corrected", "Paperwork Corrected", "#06b6d4"),
        new StatusState("paperwork-uploaded", "Paperwork Uploaded", "#10b981"),
        new StatusState("done", "Done", "#22c55e")
    };
}
