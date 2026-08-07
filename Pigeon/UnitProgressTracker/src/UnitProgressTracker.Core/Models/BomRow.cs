namespace UnitProgressTracker.Core.Models;

public class BomRow
{
    public string PartNumber { get; set; } = string.Empty;
    public string Quantity { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string Skid { get; set; } = string.Empty;
    public string Segment { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ExtDescription { get; set; } = string.Empty;

    public string CombinedDescription => string.IsNullOrWhiteSpace(ExtDescription)
        ? Description.Trim()
        : $"{Description.Trim()} {ExtDescription.Trim()}";
}

public class ShellFolderEntry
{
    public string EntryKey { get; set; } = string.Empty;
    public string PartNumber { get; set; } = string.Empty;
    public string Quantity { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string Skid { get; set; } = string.Empty;
    public string Segment { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ExtDescription { get; set; } = string.Empty;
    public string SegmentFolder { get; set; } = string.Empty;
    public string AssemblyFolder { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string? AbsolutePath { get; set; }
    public bool IsCustomSq { get; set; }
}

public class ShellFolderPlanStats
{
    public int Total391Rows { get; set; }
    public int FolderCount { get; set; }
    public int ExcludedCount { get; set; }
    public int MisplacedCount { get; set; }
    public int SkippedCount { get; set; }
    public int CustomSqCount { get; set; }
    public bool ConfigLoaded { get; set; }
    public List<string> ConfigWarnings { get; set; } = new();
}

public class ShellFolderPlan
{
    public List<ShellFolderEntry> Entries { get; set; } = new();
    public List<BomRow> Excluded { get; set; } = new();
    public List<BomRow> Misplaced { get; set; } = new();
    public List<(BomRow Row, string Reason)> Skipped { get; set; } = new();
    public ShellFolderPlanStats Stats { get; set; } = new();
}
