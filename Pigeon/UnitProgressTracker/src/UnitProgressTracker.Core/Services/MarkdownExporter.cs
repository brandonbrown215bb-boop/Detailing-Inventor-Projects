using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnitProgressTracker.Core.Models;

namespace UnitProgressTracker.Core.Services;

public class MarkdownExportOptions
{
    public bool IncludeProjectMetadata { get; set; } = true;
    public bool IncludeStatusBreakdown { get; set; } = true;
    public bool IncludeChecklists { get; set; } = true;
    public bool IncludeNotes { get; set; } = true;
    public bool IncludeRetiredLineage { get; set; } = true;
    public bool UseDetailedPerSurfaceSections { get; set; } = true;
}

public static class MarkdownExporter
{
    public static string ExportToMarkdown(
        ProjectStateModel project,
        IEnumerable<StatusState>? statusStates = null,
        MarkdownExportOptions? options = null)
    {
        if (project == null) throw new ArgumentNullException(nameof(project));
        
        var activeSurfaces = project.Surfaces?.Select(kvp => new SurfaceModel
        {
            SurfaceNumber = kvp.Value.DisplayNumber ?? kvp.Key,
            DisplayNumber = kvp.Value.DisplayNumber ?? kvp.Key,
            StateId = kvp.Value.StateId ?? "current",
            Notes = kvp.Value.Notes ?? "",
            IsHidden = kvp.Value.Hidden,
            Checklist = kvp.Value.Checklist ?? new Dictionary<string, bool>(),
            PreviousNumbers = kvp.Value.PreviousNumbers ?? new List<string>(),
            GeometryFingerprint = kvp.Value.GeometryFingerprint ?? ""
        }).ToList() ?? new List<SurfaceModel>();

        return GenerateAuditReport(project, activeSurfaces, statusStates, options);
    }

    public static string GenerateAuditReport(IEnumerable<SurfaceModel> surfaces, IEnumerable<StatusState> states)
    {
        return GenerateAuditReport(null, surfaces, states, new MarkdownExportOptions
        {
            IncludeProjectMetadata = false,
            UseDetailedPerSurfaceSections = true
        });
    }

    public static string GenerateAuditReport(
        ProjectStateModel? project,
        IEnumerable<SurfaceModel>? activeSurfaces,
        IEnumerable<StatusState>? statusStates,
        MarkdownExportOptions? options = null)
    {
        options ??= new MarkdownExportOptions();
        var surfacesList = (activeSurfaces ?? Enumerable.Empty<SurfaceModel>()).ToList();
        var effectiveStates = statusStates ?? (project?.StatusDefinitions != null && project.StatusDefinitions.Count > 0 ? project.StatusDefinitions : StatusState.DefaultStates);
        var statesList = effectiveStates.ToList();
        var stateMap = statesList.ToDictionary(s => s.Id, s => s.Name, StringComparer.OrdinalIgnoreCase);

        var sb = new StringBuilder();

        // 1. Title Header
        sb.AppendLine("# Unit Progress Tracker — Surface Audit Report");
        sb.AppendLine($"*Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC*");
        sb.AppendLine();

        // Job & Order Summary (if available)
        var firstJobCtx = surfacesList.FirstOrDefault(s => s.JobContext != null && !string.IsNullOrEmpty(s.JobContext.SalesOrderNumber))?.JobContext;
        if (firstJobCtx != null)
        {
            sb.AppendLine("## Job & Order Summary");
            sb.AppendLine($"- **Sales Order Number:** `{firstJobCtx.SalesOrderNumber}`");
            sb.AppendLine($"- **Job Name:** `{firstJobCtx.JobName}`");
            sb.AppendLine($"- **COM # / Unit Tag:** `{firstJobCtx.ComNumber}` / `{firstJobCtx.UnitTag}` (Unit #{firstJobCtx.UnitNumber})");
            sb.AppendLine($"- **Product & Housing:** `{firstJobCtx.ProductType}` ({firstJobCtx.UnitType}) — `{firstJobCtx.HousingStyle}`");
            sb.AppendLine($"- **Mfg Location:** `{firstJobCtx.MfgLocation}`");
            sb.AppendLine($"- **Skid Segment Sequence:** `{firstJobCtx.SkidSegmentSequence}`");
            sb.AppendLine();
        }

        // 2. Project Metadata Header / Stats
        if (options.IncludeProjectMetadata && project != null)
        {
            sb.AppendLine("## Project Metadata");
            sb.AppendLine($"- **Project Source Folder:** `{project.SourceFolder ?? "N/A"}`");
            sb.AppendLine($"- **Project Last Updated:** `{project.UpdatedAt?.ToString("yyyy-MM-dd HH:mm:ss UTC") ?? "N/A"}`");
            sb.AppendLine($"- **Schema Version:** `{project.Version}`");
            sb.AppendLine($"- **Total Active Surfaces:** `{surfacesList.Count}` (Visible: `{surfacesList.Count(s => !s.IsHidden)}`, Hidden: `{surfacesList.Count(s => s.IsHidden)}`)");
            sb.AppendLine($"- **Retired Surfaces Tracked:** `{project.Retired?.Count ?? 0}`");
            if (project.Bom != null)
            {
                sb.AppendLine($"- **BOM Import Status:** `{project.Bom.TotalRowCount}` total rows (`{project.Bom.KeptCount}` kept, `{project.Bom.DroppedCount}` dropped)");
            }
            sb.AppendLine();
        }

        sb.AppendLine($"**Total Surfaces:** {surfacesList.Count}  ");
        sb.AppendLine($"**Active (Visible):** {surfacesList.Count(s => !s.IsHidden)}  ");
        sb.AppendLine($"**Hidden:** {surfacesList.Count(s => s.IsHidden)}");
        sb.AppendLine();

        // 3. Status Breakdown Table
        if (options.IncludeStatusBreakdown)
        {
            sb.AppendLine("## Status Breakdown");
            sb.AppendLine("| Status | Count | Percentage | Fill Type | Color |");
            sb.AppendLine("| --- | --- | --- | --- | --- |");

            var statusCounts = surfacesList
                .GroupBy(s => s.StateId ?? "current", StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

            foreach (var state in statesList)
            {
                int count = statusCounts.GetValueOrDefault(state.Id, 0);
                double pct = surfacesList.Count > 0 ? (double)count / surfacesList.Count * 100 : 0.0;
                sb.AppendLine($"| {state.Name} | {count} | {pct:F1}% | {state.FillType} | `{state.ColorHex}` |");
            }

            var knownStateIds = new HashSet<string>(statesList.Select(s => s.Id), StringComparer.OrdinalIgnoreCase);
            var unmappedGroups = surfacesList
                .Where(s => !knownStateIds.Contains(s.StateId ?? "current"))
                .GroupBy(s => s.StateId ?? "unknown", StringComparer.OrdinalIgnoreCase);

            foreach (var g in unmappedGroups)
            {
                int count = g.Count();
                double pct = surfacesList.Count > 0 ? (double)count / surfacesList.Count * 100 : 0.0;
                sb.AppendLine($"| Unknown State ({g.Key}) | {count} | {pct:F1}% | solid | `#94A3B8` |");
            }
            sb.AppendLine();
        }

        // 4. Surface Details Section
        sb.AppendLine("## Surface Details");

        if (options.UseDetailedPerSurfaceSections)
        {
            // Summary table format with Checklist Progress ratio (e.g. 2/2, 1/2, N/A)
            sb.AppendLine("| Surface # | Part # | Type | Side | Status | Hidden | Checklist Progress | Notes |");
            sb.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- |");

            foreach (var surf in surfacesList)
            {
                string statusName = stateMap.TryGetValue(surf.StateId ?? "current", out var resolvedName)
                    ? resolvedName
                    : $"Unknown State ({surf.StateId})";

                int totalCheck = surf.Checklist?.Count ?? 0;
                int doneCheck = surf.Checklist?.Values.Count(v => v) ?? 0;
                string progress = totalCheck > 0 ? $"{doneCheck}/{totalCheck}" : "N/A";
                
                string rawNotes = surf.Notes ?? "";
                string sanitizedNotes = string.IsNullOrWhiteSpace(rawNotes) 
                    ? "—" 
                    : rawNotes.Replace("\r\n", " ").Replace("\n", " ").Replace("|", "\\|");

                sb.AppendLine($"| {surf.SurfaceNumber} | {surf.PartNumber} | {surf.SurfaceType} | {surf.SurfaceUnitSide} | {statusName} | {surf.IsHidden} | {progress} | {sanitizedNotes} |");
            }
            sb.AppendLine();

            // Interactive per-surface checklist blocks with - [x] and - [ ] checkboxes
            if (options.IncludeChecklists || options.IncludeNotes)
            {
                sb.AppendLine("### Interactive Surface Checklists & Notes");
                foreach (var surf in surfacesList)
                {
                    string statusName = stateMap.TryGetValue(surf.StateId ?? "current", out var resolvedName)
                        ? resolvedName
                        : $"Unknown State ({surf.StateId})";
                    string displayNum = !string.IsNullOrWhiteSpace(surf.DisplayNumber) ? surf.DisplayNumber : surf.SurfaceNumber;

                    sb.AppendLine($"#### Surface: {displayNum} (Part #: {surf.PartNumber})");
                    sb.AppendLine($"- **Surface #:** {surf.SurfaceNumber}");
                    sb.AppendLine($"- **Status:** {statusName}");
                    sb.AppendLine($"- **Visibility:** {(surf.IsHidden ? "Hidden" : "Visible")}");

                    if (options.IncludeChecklists)
                    {
                        sb.AppendLine("- **Checklist:**");
                        if (surf.Checklist != null && surf.Checklist.Count > 0)
                        {
                            foreach (var (itemKey, isChecked) in surf.Checklist)
                            {
                                string mark = isChecked ? "x" : " ";
                                sb.AppendLine($"  - [{mark}] {itemKey}");
                            }
                        }
                        else
                        {
                            sb.AppendLine("  - *(No checklist items)*");
                        }
                    }

                    if (options.IncludeNotes)
                    {
                        sb.AppendLine("- **Notes:**");
                        if (!string.IsNullOrWhiteSpace(surf.Notes))
                        {
                            var lines = surf.Notes.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                            foreach (var line in lines)
                            {
                                sb.AppendLine($"  > {line}");
                            }
                        }
                        else
                        {
                            sb.AppendLine("  > *(None)*");
                        }
                    }

                    sb.AppendLine();
                }
            }
        }

        // 5. Retired Surface Lineage Audit Section
        if (options.IncludeRetiredLineage && project?.Retired != null && project.Retired.Count > 0)
        {
            sb.AppendLine("## Retired Surface Lineage Audit");
            sb.AppendLine("| Surface Number | Superseded By | Transfer Type | Retired At | File Key | Fingerprint |");
            sb.AppendLine("| --- | --- | --- | --- | --- | --- |");

            foreach (var (retiredKey, record) in project.Retired)
            {
                string superseded = !string.IsNullOrWhiteSpace(record.SupersededBy) ? record.SupersededBy : "—";
                string transferType = record.TransferType ?? "renumber";
                string retiredDate = record.RetiredAt.ToString("yyyy-MM-dd HH:mm:ss");
                string fileKey = !string.IsNullOrWhiteSpace(record.FileKey) ? record.FileKey : "—";
                string fingerprint = !string.IsNullOrWhiteSpace(record.GeometryFingerprint) ? record.GeometryFingerprint : "—";

                sb.AppendLine($"| {retiredKey} | {superseded} | {transferType} | {retiredDate} | {fileKey} | `{fingerprint}` |");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    public static void SaveAuditReport(
        string filePath,
        IEnumerable<SurfaceModel> surfaces,
        IEnumerable<StatusState> states)
    {
        SaveAuditReport(filePath, null, surfaces, states);
    }

    public static void SaveAuditReport(
        string filePath,
        ProjectStateModel? project,
        IEnumerable<SurfaceModel>? surfaces,
        IEnumerable<StatusState>? states,
        MarkdownExportOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

        string reportContent = GenerateAuditReport(project, surfaces, states, options);
        string? dir = Path.GetDirectoryName(Path.GetFullPath(filePath));
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(filePath, reportContent, Encoding.UTF8);
    }
}
