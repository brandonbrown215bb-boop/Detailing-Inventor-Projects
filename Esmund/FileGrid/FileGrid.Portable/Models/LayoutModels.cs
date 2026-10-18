using System.Text.Json.Serialization;

namespace FileGrid.Portable.Models;

public sealed class GridBox
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = "";
    public string Color { get; set; } = "#3d5a80";
    public string FolderPath { get; set; } = "";
    public int Gx { get; set; }
    public int Gy { get; set; }
    public int Gw { get; set; } = 2;
    public int Gh { get; set; } = 2;
}

public sealed class BoxLink
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FromBoxId { get; set; } = "";
    public string ToBoxId { get; set; } = "";
    public string Color { get; set; } = "#b8b8b8";
    public double Thickness { get; set; } = 2;
}

public sealed class ViewTransform
{
    public double PanX { get; set; } = 80;
    public double PanY { get; set; } = 80;
    public double Zoom { get; set; } = 1;
}

public sealed class LayoutState
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "Default";
    public string UpdatedAt { get; set; } = DateTime.UtcNow.ToString("o");
    public int CellSize { get; set; } = 48;
    public ViewTransform View { get; set; } = new();
    public List<GridBox> Boxes { get; set; } = new();
    public List<BoxLink> Links { get; set; } = new();

    public static LayoutState CreateDefault(string name = "Default") => new()
    {
        Id = Guid.NewGuid().ToString(),
        Name = name,
        UpdatedAt = DateTime.UtcNow.ToString("o")
    };
}

public sealed class AppSettings
{
    public double TextScale { get; set; } = 1;
    public bool ShowFilePath { get; set; } = true;
    public bool ShowGrid { get; set; } = true;
    public string BackgroundColor { get; set; } = "#0a0a0a";
    public string GridColor { get; set; } = "#ffffff";
    public double GridOpacity { get; set; } = 0.26;
    public string FontFamily { get; set; } = "Segoe UI";
    public string FontColor { get; set; } = "#f2f2f2";
}

public sealed class SessionMeta
{
    [JsonPropertyName("activeLayoutId")]
    public string? ActiveLayoutId { get; set; }
}
