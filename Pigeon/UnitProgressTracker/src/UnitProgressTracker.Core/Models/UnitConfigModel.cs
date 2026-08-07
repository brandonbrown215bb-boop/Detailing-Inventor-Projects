using System;
using System.Collections.Generic;

namespace UnitProgressTracker.Core.Models;

public class SegmentConfigModel
{
    public int Order { get; set; } = 1;
    public string TagName { get; set; } = string.Empty;
    public string TypeCode { get; set; } = string.Empty;
    public string SegmentType { get; set; } = string.Empty;
    public string Normalized { get; set; } = string.Empty;
    public string FolderPrefix { get; set; } = string.Empty;
}

public class SkidConfigModel
{
    public int Id { get; set; }
    public string Bracket { get; set; } = string.Empty;
    public List<SegmentConfigModel> Segments { get; set; } = new();
}

public class UnitConfigModel
{
    public string? SourceFile { get; set; }
    public string? ImportedAt { get; set; }
    public string? ProjectId { get; set; }
    public List<string> Warnings { get; set; } = new();
    public List<SkidConfigModel> Skids { get; set; } = new();
}
